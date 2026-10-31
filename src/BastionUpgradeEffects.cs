using System;
using System.Runtime.CompilerServices;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
namespace IvoryBastion
{
    // All capstones run after an accepted action on the server. They neither
    // submit another input nor replace native damage/item processing.
    internal static class BastionUpgradeEffects
    {
        private sealed class ClampGate{internal float Next;}
        private static readonly ConditionalWeakTable<CharacterBody,ClampGate> clamps=new ConditionalWeakTable<CharacterBody,ClampGate>();
        private static bool Has(BastionController c,BastionUpgradeNode node)=>NetworkServer.active&&c&&c.Alive&&c.Bank.Upgrades.Active&&c.Bank.Upgrades.Maxed(node);
        internal static void AegisCast(BastionController c)
        {
            if(!Has(c,BastionUpgradeNode.AegisCircuit))return;
            float fraction=.25f+(Math.Max(.25f,B.Fraction(S.RenewalRestore))-.25f)*c.Bank.Upgrades.Progress(BastionUpgradeNode.Renewal);
            BastionAbilityShield.Restore(c,c.AbilityShield.Capacity*fraction);
        }
        internal static void BlueShiftCast(BastionController c)
        {
            if(!Has(c,BastionUpgradeNode.ThrusterTanks)||!c.Bank.Upgrades.Perk(BastionUpgradeNode.ThrusterTanks,Time.time,20))return;
            var utility=c.GetComponent<BastionUtility>();if(utility)utility.WingburstReserve.Restock(false);
        }
        internal static void RushCast(BastionController c)
        {if(Has(c,BastionUpgradeNode.HeavenfallRigs))BastionAbilityShield.Restore(c,c.AbilityShield.Capacity*.2f);}
        internal static void BeamHit(BastionController c,int victims)
        {
            if(victims<1)return;
            if(Has(c,BastionUpgradeNode.IonOptics))c.Bank.Ammo=Math.Min(c.Bank.Capacity-c.Bank.Reserved,c.Bank.Ammo+1);
            if(victims>=3&&Has(c,BastionUpgradeNode.IonCapacitors))c.Bank.Laser=Math.Max(0,c.Bank.Laser-.75f);
        }
        internal static void MineAttached(BastionController c,CharacterBody target)
        {
            if(!Has(c,BastionUpgradeNode.WidowFabricators)||!target||target.isBoss||target.isChampion||!target.healthComponent||!target.healthComponent.alive)return;
            var gate=clamps.GetValue(target,_=>new ClampGate());if(Time.time<gate.Next)return;
            gate.Next=Time.time+2;target.AddTimedBuff(RoR2Content.Buffs.Nullified,.6f);
        }
        internal static void MineBurst(BastionController c,int kills)
        {
            if(kills<1||!Has(c,BastionUpgradeNode.ChargedNetwork)||!c.Bank.Upgrades.Perk(BastionUpgradeNode.ChargedNetwork,Time.time,1))return;
            var utility=c.GetComponent<BastionUtility>();if(utility&&utility.MinesReserve.Stock<utility.MinesReserve.Capacity)utility.MinesReserve.Remaining=Math.Max(0,utility.MinesReserve.Remaining-Math.Min(3,kills));
        }
        internal static void RepairPulse(BastionController c,Vector3 from)
        {
            if(!Has(c,BastionUpgradeNode.Blueprints)||(c.Body.corePosition-from).sqrMagnitude>B.Get(S.RepairRange)*B.Get(S.RepairRange))return;
            float before=c.AbilityShield.Charge;
            BastionAbilityShield.Restore(c,c.Body.maxHealth*.01f*B.Get(S.RepairInterval));
            if(c.AbilityShield.Charge>before)c.Emit(BastionFxKind.Repair,from,c.Body.corePosition);
        }
        internal static void DroneLost(BastionController c)
        {
            // Apply after the native destruction callback starts construction.
            // Detecting a zero-health drone earlier can be overwritten when its
            // corpse is removed; docking and temporary Aegis units never call it.
            if(Has(c,BastionUpgradeNode.Hangar)&&c.Sieged&&c.Bank.Upgrades.Perk(BastionUpgradeNode.Hangar,Time.time,30))c.Bank.Construction=Math.Min(c.Bank.Construction,1);
        }
    }
}
