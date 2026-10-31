using RoR2;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionBatteryTargeting
    {
        private static readonly float minAlignment=(float)System.Math.Cos(BastionNeedleRules.BatteryConeDegrees*System.Math.PI/180);

        // A hurtbox under the crosshair wins, then the visible enemy closest to
        // the crosshair inside the cone, then the nearest reachable enemy.
        internal static CharacterBody Find(BastionController owner,float range,Vector3 aim,Vector3? source=null)
        {
            if(!owner||!owner.Alive||!owner.Body||owner.Body.inputBank==null||!float.IsFinite(range)||range<=0||
                !BastionUtilityRules.FinitePoint(aim)||aim.sqrMagnitude<.001f)return null;
            var ray=new Ray(owner.Body.inputBank.aimOrigin,aim.normalized);
            var from=source??owner.MissileOrigin();
            var proximity=source??owner.Body.corePosition;
            if(!BastionUtilityRules.FinitePoint(ray.origin)||!BastionUtilityRules.FinitePoint(from))return null;
            CharacterBody aimed=null,cursor=null,nearest=null;
            float bestHit=float.PositiveInfinity,bestAlignment=minAlignment,bestDistance=float.PositiveInfinity;
            uint aimedId=uint.MaxValue,cursorId=uint.MaxValue,nearestId=uint.MaxValue;
            foreach(var enemy in BastionCombat.Enemies(owner.Body,owner.Body.corePosition,range))
            {
                Vector3 point=BastionCombat.AimPoint(enemy);
                float distance=(point-proximity).sqrMagnitude;
                bool nearer=float.IsFinite(distance)&&BastionUtilityRules.Prefer(0,distance,enemy.netId.Value,0,bestDistance,nearestId);
                bool onAim=AimHit(enemy,ray,range,out float hit)&&BastionUtilityRules.Prefer(0,hit,enemy.netId.Value,0,bestHit,aimedId);
                float alignment=Vector3.Dot(ray.direction,(point-ray.origin).normalized);
                bool nearAim=(alignment>bestAlignment||alignment==bestAlignment&&enemy.netId.Value<cursorId)&&
                    !Physics.Linecast(ray.origin,point,LayerIndex.world.mask,QueryTriggerInteraction.Ignore);
                if(!nearer&&!onAim&&!nearAim)continue;
                if(!BastionCombat.TryMissileRoute(from,enemy,out _,out _) &&
                    (source.HasValue||!BastionCombat.TryMissileRoute(owner.Body.corePosition,enemy,out _,out _)))continue;
                if(nearer)
                {nearest=enemy;bestDistance=distance;nearestId=enemy.netId.Value;}
                if(onAim)
                {aimed=enemy;bestHit=hit;aimedId=enemy.netId.Value;}
                if(nearAim)
                {cursor=enemy;bestAlignment=alignment;cursorId=enemy.netId.Value;}
            }
            // Incoming-damage reservations, enemy tier and splash value cannot
            // override the player's chosen target or the nearest fallback.
            return aimed?aimed:cursor?cursor:nearest;
        }
        private static bool AimHit(CharacterBody enemy,Ray ray,float range,out float distance)
        {
            float closest=float.PositiveInfinity;
            void Consider(HurtBox box)
            {
                if(!box||box.healthComponent!=enemy.healthComponent)return;
                var collider=box.GetComponent<Collider>();
                if(!collider||!collider.enabled||!collider.Raycast(ray,out var hit,range)||
                    !float.IsFinite(hit.distance)||hit.distance<0||
                    Physics.Linecast(ray.origin,hit.point,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))return;
                closest=Mathf.Min(closest,hit.distance);
            }
            Consider(enemy.mainHurtBox);
            var group=enemy.hurtBoxGroup;
            if(group&&group.hurtBoxes!=null)foreach(var box in group.hurtBoxes)Consider(box);
            distance=closest;return float.IsFinite(closest);
        }
    }
}
