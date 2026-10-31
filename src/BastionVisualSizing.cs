using System.Reflection;
using RoR2;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionVisualSizing
    {
        private const BindingFlags HiddenInstance=BindingFlags.NonPublic|BindingFlags.Instance;
        private static readonly PropertyInfo baseRadius=typeof(CharacterBody).GetProperty("baseRadius",HiddenInstance);
        private static readonly PropertyInfo baseHeight=typeof(CharacterBody).GetProperty("baseHeight",HiddenInstance);
        private static readonly MethodInfo updateSize=typeof(CharacterBody).GetMethod("UpdateSizeProperties",HiddenInstance);
        private static readonly MethodInfo skinApplied=typeof(CharacterModel).GetMethod("OnSkinApplied",HiddenInstance);

        internal static void Install()=>On.RoR2.CharacterBody.UpdateSingleTemporaryVisualEffect_refTemporaryVisualEffect_GameObject_float_bool_string+=Effect;
        internal static void Remove()=>On.RoR2.CharacterBody.UpdateSingleTemporaryVisualEffect_refTemporaryVisualEffect_GameObject_float_bool_string-=Effect;

        internal static void SetBodySize(CharacterBody body,bool siege)
        {
            // CharacterBody otherwise retains the Huntress dimensions cached in
            // Awake even after the motor and hurtbox switch to our other form.
            baseRadius.SetValue(body,BastionVisualRules.Radius(siege));
            baseHeight.SetValue(body,BastionVisualRules.Height(siege));
            updateSize.Invoke(body,null);
            if(body.healthComponent&&body.inventory)body.UpdateAllTemporaryVisualEffects();
        }

        internal static void SetRenderers(CharacterModel model,CharacterModel.RendererInfo[] renderers)
        {
            if(!model||renderers==null||ReferenceEquals(model.baseRendererInfos,renderers))return;
            model.baseRendererInfos=renderers;
            // Also updates the main skinned renderer used by native item VFX.
            skinApplied.Invoke(model,new object[]{-1});
        }

        private static void Effect(On.RoR2.CharacterBody.orig_UpdateSingleTemporaryVisualEffect_refTemporaryVisualEffect_GameObject_float_bool_string orig,
            CharacterBody body,ref TemporaryVisualEffect effect,GameObject prefab,float radius,bool active,string child)
        {
            var controller=body.GetComponent<BastionController>();
            if(controller)radius=BastionVisualRules.SelfEffectRadius(radius,controller.Sieged,body.currentScale);
            orig(body,ref effect,prefab,radius,active,child);
            // Vanilla initializes this only once. Assign an absolute radius on
            // every refresh so an existing aura also fits after changing forms.
            if(controller&&effect)effect.radius=radius;
        }
    }
}
