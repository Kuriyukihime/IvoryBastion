using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    internal static class BastionUtilityCombat
    {
        internal static bool ValidRushVictim(BastionController owner,CharacterBody body)
        {
            if(!owner||!owner.Body||!BastionCombat.Enemy(owner.Body,body))return false;
            float health=body.healthComponent.combinedHealth+body.healthComponent.barrier;
            return Finite(health)&&health>0&&Finite(body.armor);
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        internal static bool InRushBlast(Vector3 center,CharacterBody body,float radius)
        {
            bool colliderFound=false;float squared=radius*radius;
            bool Covers(HurtBox box)
            {
                if(!box||box.healthComponent!=body.healthComponent)return false;
                var collider=box.GetComponent<Collider>();if(!collider)return false;
                colliderFound=true;
                return collider.enabled&&(collider.ClosestPoint(center)-center).sqrMagnitude<=squared;
            }
            if(Covers(body.mainHurtBox))return true;
            var group=body.hurtBoxGroup;
            if(group&&group.hurtBoxes!=null)foreach(var box in group.hurtBoxes)if(Covers(box))return true;
            return !colliderFound&&(BastionCombat.AimPoint(body)-center).sqrMagnitude<=squared;
        }
        internal static CharacterBody FindRush(BastionController owner,Vector3 origin,float range,BastionRushTargetPlan plan=null)
        {
            if(!owner||!owner.Alive||!BastionUtilityRules.FinitePoint(origin)||!Finite(range)||range<=0)return null;
            CharacterBody best=null;int fewest=int.MaxValue;bool bestMarked=false;float nearest=float.PositiveInfinity;uint bestId=uint.MaxValue;
            foreach(var candidate in CharacterBody.readOnlyInstancesList)
            {
                if(!ValidRushVictim(owner,candidate))continue;
                var center=BastionCombat.AimPoint(candidate);float distance=(center-origin).sqrMagnitude;
                if(!Finite(distance)||distance>range*range)continue;
                int covered=plan?.Count(candidate.healthComponent)??0;
                bool marked=BastionServerSpiderMine.HasAttachedMarker(owner,candidate.healthComponent);
                if(covered>fewest)continue;
                if(covered==fewest)
                {
                    if(marked!=bestMarked){if(!marked)continue;}
                    else if(!BastionUtilityRules.Prefer(0,distance,candidate.netId.Value,0,nearest,bestId))continue;
                }
                if(!BastionCombat.Visible(origin,candidate))continue;
                best=candidate;fewest=covered;bestMarked=marked;nearest=distance;bestId=candidate.netId.Value;
            }
            return best;
        }
        internal static CharacterBody Find(BastionController owner,Vector3 origin,float range,CharacterBody retain,bool avoidOverkill=false)
        {
            CharacterBody best=null;int tier=-1;float distance=float.PositiveInfinity;uint id=uint.MaxValue;
            bool retained=BastionCombat.Enemy(owner.Body,retain)&&(retain.corePosition-owner.Body.corePosition).sqrMagnitude<=range*range&&BastionCombat.Visible(origin,retain);
            int retainedTier=retained?Rank(retain):-1;
            foreach(var body in CharacterBody.readOnlyInstancesList)
            {
                if(!BastionCombat.Enemy(owner.Body,body))continue;
                if(avoidOverkill&&owner.Reservations.TryGetValue(body.healthComponent,out var reserved)&&reserved>=body.healthComponent.combinedHealth+body.healthComponent.barrier)continue;
                float d=(body.corePosition-owner.Body.corePosition).sqrMagnitude;
                if(d>range*range)continue;
                int rank=Rank(body);if(retained&&rank<=retainedTier)continue;
                if(!BastionUtilityRules.Prefer(rank,d,body.netId.Value,tier,distance,id)||!BastionCombat.Visible(origin,body))continue;
                best=body;tier=rank;distance=d;id=body.netId.Value;
            }
            return best?best:retained?retain:null;
        }
        private static int Rank(CharacterBody body)=>BastionUtilityRules.Threat(body.isBoss||body.isChampion,body.isElite,body.isFlying);
    }
}
