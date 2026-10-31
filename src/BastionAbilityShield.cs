using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    internal static class BastionAbilityShield
    {
        internal static void Install()
        {
            IL.RoR2.CharacterBody.RecalculateStats+=ShieldStat;
            On.RoR2.CharacterBody.RecalculateStats+=Recalculate;
            IL.RoR2.HealthComponent.TakeDamageProcess+=DamageLayer;
        }
        internal static void Remove()
        {
            IL.RoR2.CharacterBody.RecalculateStats-=ShieldStat;
            On.RoR2.CharacterBody.RecalculateStats-=Recalculate;
            IL.RoR2.HealthComponent.TakeDamageProcess-=DamageLayer;
        }
        private static void Recalculate(On.RoR2.CharacterBody.orig_RecalculateStats orig,CharacterBody body)
        {
            var c=NetworkServer.active?body.GetComponent<BastionController>():null;
            if(!c||!body.healthComponent||c.ShieldStatDepth>0){orig(body);return;}
            c.ShieldStatDepth++;
            try
            {
                orig(body);
                RefreshCapacity(c,body);
                if(c.RestoreFlightShield)
                {c.AbilityShield.Restore(c.AbilityShield.Capacity);c.RestoreFlightShield=false;}
            }
            finally{c.ShieldStatDepth--;}
        }
        private static void ShieldStat(ILContext context)
        {
            var cursor=new ILCursor(context);
            // Retain only the ratio of health before/after native conversion
            // (Transcendence, blue elites). Capacity uses live final max health,
            // including later stat bonuses and curse, instead of an early snapshot.
            if(!cursor.TryGotoNext(MoveType.After,i=>i.MatchLdfld<CharacterBody>(nameof(CharacterBody.baseMaxShield)))||
               !cursor.TryGotoNext(MoveType.Before,i=>i.OpCode.Code==Code.Stloc||i.OpCode.Code==Code.Stloc_S||
                   i.OpCode.Code==Code.Stloc_0||i.OpCode.Code==Code.Stloc_1||i.OpCode.Code==Code.Stloc_2||i.OpCode.Code==Code.Stloc_3))
                throw new InvalidOperationException("Ivory Bastion: Seraph Shield capacity observation site was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float,CharacterBody,float>>((nativeShield,body)=>
            {
                var c=body.GetComponent<BastionController>();
                if(c)c.AbilityShieldBase=body.maxHealth;
                return nativeShield;
            });
            if(!cursor.TryGotoNext(MoveType.After,i=>i.MatchCallOrCallvirt<CharacterBody>("set_maxShield")))
                throw new InvalidOperationException("Ivory Bastion: native health conversion observation site was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<CharacterBody>>(body=>
            {
                var c=body.GetComponent<BastionController>();
                if(c)c.AbilityShieldBase=BastionDefenseRules.HealthConversionRatio(c.AbilityShieldBase,body.maxHealth);
            });
        }
        private static void RefreshCapacity(BastionController c,CharacterBody body)=>
            c.AbilityShield.Resize(BastionDefenseRules.ShieldCapacity(body.maxHealth*(c.AbilityShieldBase>0?c.AbilityShieldBase:1),c.Sieged,c.Bank.ShieldCapacityMultiplier));
        private static void DamageLayer(ILContext context)
        {
            BastionShieldDamageSite.Locate(context.Method,out var load,out var damageLocal);
            // Preserve the original load/branch target. All barrier branches
            // still enter this instruction, including fully absorbed hits.
            var cursor=new ILCursor(context);cursor.Goto(load,MoveType.After);
            cursor.Emit(OpCodes.Ldarg_0);cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Func<float,HealthComponent,DamageInfo,float>>(Absorb);
            cursor.Emit(OpCodes.Dup);cursor.Emit(OpCodes.Stloc,damageLocal);
        }
        private static float Absorb(float damage,HealthComponent hp,DamageInfo info)
        {
            if(!NetworkServer.active||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage)||info.rejected||!hp.body)return damage;
            var c=hp.body.GetComponent<BastionController>();if(!c||!c.Alive)return damage;
            float reduction=c.Bank.ShieldReduction(c.Sieged);
            damage=c.AegisBarrier.Absorb(damage,c.AbilityShield.Charge>0?reduction:0);
            if(c.AegisBarrier.Charge<=0)c.Body.SetBuffCount(BastionContent.AegisBarrier.buffIndex,0);
            // Aegis and native barrier protect the recharge clock. Only damage
            // that actually reaches the Seraph/item-shield/health layers resets it.
            if(damage<=0)return 0;
            c.LastShieldHit=Time.time;
            float before=c.AbilityShield.Charge;
            float remaining=c.AbilityShield.Absorb(damage,reduction);
            // Emit at the actual absorption transition, outside impact FX's
            // throttle and outer TakeDamage hooks (which can be reentrant).
            if(before>0&&c.AbilityShield.Charge<=0)
                c.Emit(BastionFxKind.ShieldBreak,c.Body.corePosition,Vector3.zero);
            if(c.AbilityShield.Charge<=0)c.Body.SetBuffCount(BastionContent.FlightShield.buffIndex,0);
            return remaining;
        }
        internal static void Tick(BastionController c,float delta)
        {
            if(!NetworkServer.active||!c.Alive)return;
            // Other mods may adjust max health after their RecalculateStats
            // wrapper returns. Observe that final value without recalculating
            // stats, writing native resources or resetting recharge progress.
            RefreshCapacity(c,c.Body);
            float step=BastionDefenseRules.RechargeStep(Time.time-c.LastShieldHit,delta,c.Sieged);
            if(step<=0)return;
            bool started=c.AbilityShield.Recharge(c.AbilityShield.Capacity/B.Get(c.Sieged?S.SiegeShieldRefill:S.MobileShieldRefill)*step);
            if(c.AbilityShield.Charge>0)c.Body.SetBuffCount(BastionContent.FlightShield.buffIndex,1);
            if(started)c.Emit(BastionFxKind.ShieldRecharge,c.Body.corePosition,Vector3.zero);
        }
        internal static void Restore(BastionController c,float amount)
        {
            if(!NetworkServer.active||!c.Alive)return;
            c.AbilityShield.Restore(amount);
            if(c.AbilityShield.Charge>0)c.Body.SetBuffCount(BastionContent.FlightShield.buffIndex,1);
        }
    }
}
