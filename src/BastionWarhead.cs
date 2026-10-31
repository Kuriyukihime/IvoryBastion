using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace IvoryBastion
{
    internal static class BastionWarheads
    {
        internal static bool Launch(BastionController owner,ref uint sequence)
        {
            float damage=owner.Body.damage*BastionWarheadGeometry.Damage;
            if(!TryTarget(owner,damage,out var initialTarget,out _))return false;
            for(int shot=0;shot<BastionWarheadGeometry.Count;shot++)
            {
                var go=new GameObject("Skyfall server siege warhead");
                var missile=go.AddComponent<BastionServerWarhead>();
                missile.Owner=owner;missile.Id=++sequence;
                missile.Launcher=shot%2;
                missile.From=owner.Model?owner.Model.SkyfallPort(missile.Launcher):owner.Body.corePosition+Vector3.up*3;
                missile.To=initialTarget;missile.Damage=damage;
                missile.Delay=shot*BastionWarheadGeometry.LaunchSpacing;missile.Begin();
            }
            return true;
        }

        internal static bool TryTarget(BastionController owner,float damage,out Vector3 target,out CharacterBody[] enemies)
        {
            enemies=BastionCombat.Enemies(owner.Body,owner.Body.corePosition,BastionWarheadGeometry.Range).ToArray();
            var points=enemies.Select(BastionCombat.AimPoint).ToArray();
            var remaining=enemies.Select(b=>
            {
                owner.Reservations.TryGetValue(b.healthComponent,out float reserved);
                float hit=Mathf.Max(.001f,BastionBank.PredictedDamage(damage,b.armor));
                return Mathf.Max(0,b.healthComponent.combinedHealth+b.healthComponent.barrier-reserved)/hit;
            }).ToArray();
            var kinds=enemies.Select(b=>b.isBoss||b.isChampion?BastionWarheadTargetKind.Boss:
                b.isElite?BastionWarheadTargetKind.Elite:BastionWarheadTargetKind.Ordinary).ToArray();
            int selected=BastionWarheadGeometry.Select(owner.Body.corePosition,points,remaining,kinds);
            target=selected<0?Vector3.zero:points[selected];return selected>=0;
        }
    }

    public sealed class BastionServerWarhead:MonoBehaviour
    {
        internal BastionController Owner;internal uint Id;internal Vector3 From,To;
        internal int Launcher;
        internal float Damage,Delay;
        private float age,duration;private bool launched,impacted;
        private readonly List<BastionDamageReservation> reservations=new List<BastionDamageReservation>();
        internal void Begin()=>transform.position=From;
        private void FixedUpdate()
        {
            if(!Owner||!Owner.Alive){Destroy(gameObject);return;}
            age+=Time.fixedDeltaTime;if(age<Delay)return;
            if(!launched)
            {
                // Re-evaluate all directions at launch. Only missiles actually in
                // flight reserve damage, so unfired shots cannot lock out a group.
                if(!BastionWarheads.TryTarget(Owner,Damage,out To,out var enemies)){Destroy(gameObject);return;}
                if(Owner.Sieged&&Owner.Model){From=Owner.Model.SkyfallPort(Launcher);transform.position=From;}
                duration=BastionWarheadGeometry.Duration(From,To);
                foreach(var enemy in enemies)
                {
                    if((BastionCombat.AimPoint(enemy)-To).sqrMagnitude>BastionWarheadGeometry.Radius*BastionWarheadGeometry.Radius)continue;
                    var reservation=new BastionDamageReservation();reservation.Assign(Owner,enemy,Damage);reservations.Add(reservation);
                }
                launched=true;
                Owner.Emit(BastionFxKind.SiegeWarhead,From,To,null,Id,duration);
            }
            float elapsed=age-Delay;
            Vector3 next=BastionWarheadGeometry.Position(From,To,elapsed/duration),before=transform.position;
            if(Physics.Linecast(before,next,out var wall,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))
            {Impact(wall.point+wall.normal*.2f);return;}
            transform.position=next;
            if(elapsed>=duration)Impact(To);
        }
        private void Impact(Vector3 position)
        {
            impacted=true;
            // Native BlastAttack preserves armor, crits, kill credit and item procs.
            BastionCombat.Blast(Owner.gameObject,Owner.Body,position,Damage,BastionWarheadGeometry.Radius,B.Get(S.SkyfallProc));
            Owner.Emit(BastionFxKind.SiegeWarheadImpact,position,Vector3.up,null,Id);
            Destroy(gameObject);
        }
        private void OnDestroy()
        {
            foreach(var reservation in reservations)reservation.Release();
            if(launched&&!impacted&&Owner&&NetworkServer.active)Owner.Emit(BastionFxKind.SiegeWarheadEnd,transform.position,Vector3.zero,null,Id);
        }
    }
}
