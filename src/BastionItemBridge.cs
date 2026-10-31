using RoR2;
using UnityEngine.Networking;
namespace IvoryBastion
{
    // The forms keep independent charge pools. Forward external native restock
    // effects to the active pool; transforming itself never refills either pool.
    internal static class BastionItemBridge
    {
        internal static void Install(){On.RoR2.GenericSkill.Reset+=Reset;On.RoR2.GenericSkill.ApplyAmmoPack+=Ammo;On.RoR2.GenericSkill.AddOneStock+=One;On.RoR2.GenericSkill.RunRecharge_float_bool+=Recharge;}
        internal static void Remove(){On.RoR2.GenericSkill.Reset-=Reset;On.RoR2.GenericSkill.ApplyAmmoPack-=Ammo;On.RoR2.GenericSkill.AddOneStock-=One;On.RoR2.GenericSkill.RunRecharge_float_bool-=Recharge;}
        private static BastionController Get(GenericSkill skill,out BastionAction action)
        {var def=skill.skillDef as BastionSkillDef;action=def?def.Action:BastionAction.Pistol;return NetworkServer.active&&def&&skill.characterBody?skill.characterBody.GetComponent<BastionController>():null;}
        private static void Restock(GenericSkill s,bool all){var c=Get(s,out var action);if(c&&c.Ledger){c.Bank.Restock(action,all);c.SendStatus();}}
        private static void Reset(On.RoR2.GenericSkill.orig_Reset orig,GenericSkill s){orig(s);Restock(s,true);}
        private static void Ammo(On.RoR2.GenericSkill.orig_ApplyAmmoPack orig,GenericSkill s){bool can=s.stock<s.maxStock;orig(s);if(can)Restock(s,false);}
        private static void One(On.RoR2.GenericSkill.orig_AddOneStock orig,GenericSkill s){orig(s);Restock(s,false);}
        private static void Recharge(On.RoR2.GenericSkill.orig_RunRecharge_float_bool orig,GenericSkill s,float dt,bool continuous)
        {var c=Get(s,out var action);if(c&&c.Ledger){c.Bank.Advance(action,dt,c.Recharge(c.Interval(action),s));c.SendStatus();}else orig(s,dt,continuous);}
    }
}
