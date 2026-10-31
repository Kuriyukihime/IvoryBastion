using System.Linq;
using EntityStates;
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    public sealed class BastionUtilitySkillDef:SkillDef
    {
        private static BastionUtility Get(GenericSkill skill)=>skill.characterBody?skill.characterBody.GetComponent<BastionUtility>():null;
        public override bool IsReady(GenericSkill skill)
        {var u=Get(skill);return u&&(u.Ready||u.Controller&&u.Controller.Alive&&!u.Controller.Deploying&&!u.Active&&u.Reserve.Stock<u.Cost&&BastionRequestRules.NearReady(u.Reserve.Wait(u.Cost)));}
        public override bool CanExecute(GenericSkill skill)=>IsReady(skill)&&base.CanExecute(skill);
        public override int GetMaxStock(GenericSkill skill)=>baseMaxStock;
        public override float GetRechargeInterval(GenericSkill skill)
        {var u=Get(skill);return u?u.Cooldown:baseRechargeInterval;}
        public override void OnFixedUpdate(GenericSkill skill,float dt)
        {var u=Get(skill);if(!u)return;skill.OverrideMaxStock(u.Reserve.Capacity/u.Cost);skill.stock=u.Reserve.Stock/u.Cost;if(skill.stock==0&&IsReady(skill))skill.stock=1;skill.rechargeStopwatch=Mathf.Max(0,skill.CalculateFinalRechargeInterval()-u.Reserve.Wait(u.Cost));}
    }
    public sealed class BastionUtilityState:BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();if(!isAuthority)return;
            var c=GetComponent<BastionController>();if(!c)return;
            var move=BastionPilotMotion.Direction(inputBank.moveVector,inputBank.aimDirection,transform.forward,inputBank.jump.down,false);
            if(!BastionCombat.Finite(move))move=inputBank.aimDirection;
            c.Requests?.Submit(c.Sieged?BastionRequestKind.Mines:BastionRequestKind.Wingburst,move.normalized);
        }
        public override void FixedUpdate(){base.FixedUpdate();if(isAuthority&&fixedAge>=.08f)outer.SetNextStateToMain();}
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.Skill;
    }
    // The configurable transform key uses the original receipt/retry state on its own channel.
    public sealed class BastionTransformInput:MonoBehaviour
    {
        private BastionController c;private EntityStateMachine machine;
        private void Awake(){c=GetComponent<BastionController>();}
        private void Update()
        {
            if(!c||!c.Body||!c.Body.hasEffectiveAuthority||!c.Alive||c.Deploying||Time.timeScale==0||!BastionHotkey.Pressed(BastionPlugin.TransformationKey.Value,BepInEx.UnityInput.Current))return;
            foreach(var system in MPEventSystem.readOnlyInstancesList)if(system.localUser!=null&&system.localUser.cachedBody==c.Body&&system.cursorOpenerCount>0)return;
            if(!c.Ready(BastionAction.Transform)&&!BastionRequestRules.NearReady(c.Bank.Toggle))return;
            if(!machine)machine=GetComponents<EntityStateMachine>().FirstOrDefault(m=>m.customName=="SeraphTransform");
            if(machine&&!(machine.state is BastionTransform))machine.SetNextState(new BastionTransform());
        }
    }
    internal static class BastionUtilityItemBridge
    {
        internal static void Install(){On.RoR2.GenericSkill.Reset+=Reset;On.RoR2.GenericSkill.ApplyAmmoPack+=Ammo;On.RoR2.GenericSkill.AddOneStock+=One;On.RoR2.GenericSkill.RunRecharge_float_bool+=Recharge;}
        internal static void Remove(){On.RoR2.GenericSkill.Reset-=Reset;On.RoR2.GenericSkill.ApplyAmmoPack-=Ammo;On.RoR2.GenericSkill.AddOneStock-=One;On.RoR2.GenericSkill.RunRecharge_float_bool-=Recharge;}
        private static BastionUtility Get(GenericSkill s)=>NetworkServer.active&&s.skillDef is BastionUtilitySkillDef&&s.characterBody?s.characterBody.GetComponent<BastionUtility>():null;
        private static void Reset(On.RoR2.GenericSkill.orig_Reset orig,GenericSkill s){orig(s);Get(s)?.Reserve.Restock(true);}
        private static void Ammo(On.RoR2.GenericSkill.orig_ApplyAmmoPack orig,GenericSkill s){bool needed=s.stock<s.maxStock;orig(s);var u=Get(s);if(needed&&u)u.Reserve.Restock(false,u.Cost);}
        private static void One(On.RoR2.GenericSkill.orig_AddOneStock orig,GenericSkill s){orig(s);var u=Get(s);if(u)u.Reserve.Restock(false,u.Cost);}
        private static void Recharge(On.RoR2.GenericSkill.orig_RunRecharge_float_bool orig,GenericSkill s,float elapsed,bool continuous){var u=Get(s);if(u)u.Reserve.Tick(elapsed);else orig(s,elapsed,continuous);}
    }
}
