using RoR2;
using UnityEngine;

namespace IvoryBastion
{
    public sealed class BastionLaserTracker:MonoBehaviour
    {
        private BastionController controller;private Indicator indicator;private GameObject tinted;private float nextSearch;
        internal CharacterBody Target {get;private set;}
        internal static Vector3 Point(CharacterBody body)
        {
            var box=body.mainHurtBox;
            return box&&box.collider?box.collider.bounds.center:BastionCombat.AimPoint(body);
        }
        internal static CharacterBody Find(BastionController owner,Vector3 aim)
        {
            Vector3 origin=owner.Body.inputBank.aimOrigin;CharacterBody best=null;float score=float.NegativeInfinity;
            foreach(var enemy in CharacterBody.readOnlyInstancesList)
            {
                if(!BastionCombat.Enemy(owner.Body,enemy))continue;
                Vector3 point=Point(enemy);float candidate=BastionLaserRules.TargetScore(origin,aim,point);
                if(candidate<=score||Physics.Linecast(origin,point,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))continue;
                best=enemy;score=candidate;
            }
            return best;
        }
        private void Awake()
        {
            controller=GetComponent<BastionController>();
            if(!Application.isBatchMode)indicator=new Indicator(gameObject,LegacyResourcesAPI.Load<GameObject>("Prefabs/HuntressTrackingIndicator"));
        }
        private void Update()
        {
            bool tracking=controller&&controller.Alive&&controller.Sieged&&!controller.Deploying&&controller.Body.hasEffectiveAuthority;
            if(!tracking){Target=null;if(indicator!=null)indicator.active=false;return;}
            if(Time.unscaledTime>=nextSearch){nextSearch=Time.unscaledTime+.05f;Target=Find(controller,controller.Body.inputBank.aimDirection);}
            if(indicator==null)return;
            indicator.targetTransform=Target?(Target.mainHurtBox?Target.mainHurtBox.transform:Target.transform):null;
            indicator.active=Target;
            var visual=indicator.visualizerInstance;
            if(!visual||visual==tinted)return;tinted=visual;
            foreach(var sprite in visual.GetComponentsInChildren<SpriteRenderer>(true))sprite.color=BastionModel.Blue;
            foreach(var image in visual.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))image.color=BastionModel.Blue;
            var block=new MaterialPropertyBlock();
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {renderer.GetPropertyBlock(block);block.SetColor("_Color",BastionModel.Blue);block.SetColor("_TintColor",BastionModel.Blue);renderer.SetPropertyBlock(block);}
        }
        private void OnDisable(){Target=null;if(indicator!=null)indicator.active=false;}
        private void OnDestroy(){if(indicator!=null)indicator.active=false;}
    }
}
