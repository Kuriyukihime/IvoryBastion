using RoR2;
using UnityEngine;
using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;

namespace IvoryBastion
{
    public sealed class BastionNeedleTracker:MonoBehaviour
    {
        private BastionController controller;
        private Indicator indicator;
        private GameObject tinted;
        private float nextSearch;
        internal CharacterBody Target {get;private set;}

        internal static CharacterBody Find(BastionController owner,Ray aim,float range)
        {
            if(!owner||!owner.Alive||owner.Sieged||owner.Deploying)return null;
            CharacterBody best=null;float bestScore=float.NegativeInfinity,bestDistance=float.PositiveInfinity;
            foreach(var enemy in CharacterBody.readOnlyInstancesList)
            {
                if(!BastionCombat.Enemy(owner.Body,enemy))continue;
                Vector3 point=BastionLaserTracker.Point(enemy);
                float score=BastionNeedleRules.Score(aim.origin,aim.direction,point,range);
                float distance=(point-aim.origin).sqrMagnitude;
                if(float.IsNegativeInfinity(score)||score<bestScore||(score==bestScore&&distance>=bestDistance))continue;
                if(Physics.Linecast(aim.origin,point,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))continue;
                best=enemy;bestScore=score;bestDistance=distance;
            }
            return best;
        }

        internal static Vector3 Direction(BastionController owner,Ray aim,float range)
        {
            var target=Find(owner,aim,range);
            return target?(BastionLaserTracker.Point(target)-aim.origin).normalized:aim.direction;
        }

        private void Awake()
        {
            controller=GetComponent<BastionController>();
            if(!Application.isBatchMode)indicator=new Indicator(gameObject,LegacyResourcesAPI.Load<GameObject>("Prefabs/HuntressTrackingIndicator"));
        }

        private void Update()
        {
            bool tracking=controller&&controller.Alive&&!controller.Sieged&&!controller.Deploying&&controller.Body.hasEffectiveAuthority;
            if(!tracking){Clear();return;}
            if(Time.unscaledTime>=nextSearch)
            {
                nextSearch=Time.unscaledTime+.05f;
                Target=Find(controller,new Ray(controller.Body.inputBank.aimOrigin,controller.Body.inputBank.aimDirection),B.Get(S.NeedleRange));
            }
            if(indicator==null)return;
            indicator.targetTransform=Target?(Target.mainHurtBox?Target.mainHurtBox.transform:Target.transform):null;
            indicator.active=Target;
            var visual=indicator.visualizerInstance;
            if(!visual||visual==tinted)return;
            tinted=visual;
            foreach(var sprite in visual.GetComponentsInChildren<SpriteRenderer>(true))sprite.color=BastionModel.Blue;
            foreach(var image in visual.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))image.color=BastionModel.Blue;
            var block=new MaterialPropertyBlock();
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);block.SetColor("_Color",BastionModel.Blue);block.SetColor("_TintColor",BastionModel.Blue);renderer.SetPropertyBlock(block);
            }
        }

        private void Clear(){Target=null;nextSearch=0;if(indicator!=null){indicator.active=false;indicator.targetTransform=null;}}
        private void OnDisable()=>Clear();
        private void OnDestroy()=>Clear();
    }
}
