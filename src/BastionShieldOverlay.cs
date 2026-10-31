using System;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

namespace IvoryBastion
{
    internal static class BastionShieldOverlay
    {
        internal static void Install()=>IL.RoR2.CharacterModel.UpdateOverlays+=UpdateOverlays;
        internal static void Remove()=>IL.RoR2.CharacterModel.UpdateOverlays-=UpdateOverlays;

        private static void UpdateOverlays(ILContext context)
        {
            var sites=context.Body.Instructions.Where(i=>
                i.MatchLdfld<HealthComponent>(nameof(HealthComponent.shield))).ToArray();
            // Both native branches (ordinary shield and Shield Booster) coat the
            // model. Fail open if the game changes, before modifying any IL.
            if(sites.Length!=2||sites.Any(i=>i.Next==null||i.Next.Next==null||
                i.Next.OpCode!=OpCodes.Ldc_R4||(float)i.Next.Operand!=0f||
                i.Next.Next.OpCode!=OpCodes.Cgt))
            {
                BastionPlugin.Log.LogWarning("Ivory Bastion: native shield overlay layout changed; leaving its presentation intact.");
                return;
            }
            var cursor=new ILCursor(context);
            foreach(var site in sites)
            {
                cursor.Goto(site,MoveType.After);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float,CharacterModel,float>>(VisibleNativeShield);
            }
        }

        private static float VisibleNativeShield(float shield,CharacterModel model)
        {
            // Presentation only: never write shield health or suppress other
            // status overlays. Our independent sphere supplies the shield cue.
            return model&&model.body&&model.body.GetComponent<BastionController>()?0f:shield;
        }
    }
}
