using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    internal static class BastionCombat
    {
        internal static bool Finite(Vector3 p)=>!(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z))&&p.sqrMagnitude>.01f;
        internal static bool OpposingTeams(CharacterBody a,CharacterBody b)=>a&&b&&a.teamComponent&&b.teamComponent&&a.teamComponent.teamIndex!=b.teamComponent.teamIndex&&b.teamComponent.teamIndex!=TeamIndex.Neutral;
        internal static bool Enemy(CharacterBody a,CharacterBody b)=>OpposingTeams(a,b)&&b.healthComponent&&b.healthComponent.alive;
        internal static IEnumerable<CharacterBody> Enemies(CharacterBody owner,Vector3 center,float radius)
        {foreach(var b in CharacterBody.readOnlyInstancesList)if(Enemy(owner,b)&&(b.corePosition-center).sqrMagnitude<=radius*radius)yield return b;}
        internal static Vector3 AimPoint(CharacterBody body)=>body.mainHurtBox?body.mainHurtBox.transform.position:body.corePosition;
        internal static bool Visible(Vector3 from,CharacterBody b)=>!Physics.Linecast(from,AimPoint(b),LayerIndex.world.mask,QueryTriggerInteraction.Ignore);
        internal static bool ClearFlight(Vector3 from,Vector3 to)
        {
            Vector3 delta=to-from;
            return !Physics.CheckSphere(from,.12f,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)&&
                (delta.sqrMagnitude<.001f||!Physics.SphereCast(from,.12f,delta.normalized,out _,delta.magnitude,LayerIndex.world.mask,QueryTriggerInteraction.Ignore));
        }
        internal static bool TryMissileRoute(Vector3 from,CharacterBody target,out Vector3 waypoint,out bool loft)
        {
            Vector3 destination=AimPoint(target);waypoint=destination;loft=false;
            if(ClearFlight(from,destination))return true;
            // Low rocks and ledges should not waste a whole barrage. Only choose
            // a lofted route when both legs actually clear the terrain.
            for(int rise=2;rise<=8;rise*=2)
            {
                waypoint=from+Vector3.up*rise;
                if(ClearFlight(from,waypoint)&&ClearFlight(waypoint,destination)){loft=true;return true;}
            }
            return false;
        }
        internal static CharacterBody Select(BastionController c,float radius,Vector3 aim,Vector3? source=null)
            =>BastionBatteryTargeting.Find(c,radius,aim,source);
        internal static Vector3 Bullet(BastionController c,Ray aim,float coefficient,float range,float proc,bool pierce,out bool damagedEnemy,CharacterBody lockedTarget=null)
        {
            if(!pierce){coefficient*=c.Bank.NeedleMultiplier;aim.direction=BastionNeedleTracker.Direction(c,aim,range);}
            if(pierce&&lockedTarget)aim.direction=(BastionLaserTracker.Point(lockedTarget)-aim.origin).normalized;
            Vector3 end=aim.origin+aim.direction*range;CharacterBody struck=null;bool dealt=false;
            var laserVictims=pierce?new HashSet<HealthComponent>():null;
            Vector3 impact=end;float nearestImpact=float.PositiveInfinity;bool hitSurface=false;
            var bullet=new BulletAttack{owner=c.gameObject,weapon=c.gameObject,origin=aim.origin,aimVector=aim.direction,bulletCount=1,damage=c.Body.damage*coefficient,force=pierce?600:30,
                maxDistance=range,minSpread=0,maxSpread=0,radius=pierce?.55f:.28f,procCoefficient=proc,isCrit=c.Body.RollCrit(),smartCollision=true,
                hitMask=LayerIndex.CommonMasks.bullet,stopperMask=pierce?LayerIndex.world.mask:LayerIndex.CommonMasks.bullet,falloffModel=BulletAttack.FalloffModel.None};
            bullet.hitCallback=(BulletAttack attack,ref BulletAttack.BulletHit hit)=>
            {
                var hp=hit.hitHurtBox?hit.hitHurtBox.healthComponent:null;
                bool enemy=hp&&Enemy(c.Body,hp.body);float before=hp?hp.combinedHealth+hp.barrier:0;
                if(pierce&&(enemy||!hp))
                {
                    float distance=(hit.point-aim.origin).sqrMagnitude;
                    if(distance<nearestImpact){nearestImpact=distance;impact=hit.point;hitSurface=true;}
                    if(enemy)laserVictims.Add(hp);
                }
                bool pass=BulletAttack.defaultHitCallback(attack,ref hit);
                if(!pass||!pierce)end=hit.point;
                if(enemy&&hp&&hp.combinedHealth+hp.barrier<before){dealt=true;if(!struck)struck=hp.body;}
                return pass;
            };
            bullet.Fire();
            if(pierce&&lockedTarget)
            {
                var hp=lockedTarget.healthComponent;var point=BastionLaserTracker.Point(lockedTarget);
                bool clear=!Physics.Linecast(aim.origin,point,LayerIndex.world.mask,QueryTriggerInteraction.Ignore);
                if(BastionLaserRules.NeedsFallback(Enemy(c.Body,lockedTarget),laserVictims.Contains(hp),clear))
                {
                    // Some modded hurtboxes do not intersect a center-aimed ray.
                    // Resolve that locked hit through native damage/item events,
                    // then exclude it from splash just like a normal beam victim.
                    float before=hp.combinedHealth+hp.barrier;
                    var info=new DamageInfo{attacker=c.gameObject,inflictor=c.gameObject,damage=c.Body.damage*coefficient,
                        crit=bullet.isCrit,position=point,force=aim.direction*600,procCoefficient=proc,procChainMask=default};
                    hp.TakeDamage(info);
                    if(GlobalEventManager.instance){GlobalEventManager.instance.OnHitEnemy(info,hp.gameObject);GlobalEventManager.instance.OnHitAll(info,hp.gameObject);}
                    laserVictims.Add(hp);dealt|=hp.combinedHealth+hp.barrier<before;
                    float distance=(point-aim.origin).sqrMagnitude;
                    if(distance<nearestImpact){nearestImpact=distance;impact=point;hitSurface=true;}
                }
            }
            damagedEnemy=dealt;
            if(pierce&&hitSurface&&NetworkServer.active)
            {
                // Preserve the piercing ray. Its victims have already received
                // this shot's damage/procs, so only nearby splash targets are new.
                var splash=new BlastAttack{attacker=c.gameObject,inflictor=c.gameObject,teamIndex=c.Body.teamComponent.teamIndex,
                    position=impact,radius=BastionBank.LaserBlastRadius,baseDamage=c.Body.damage*coefficient,
                    baseForce=600,procCoefficient=proc,crit=bullet.isCrit,falloffModel=BlastAttack.FalloffModel.None,
                    attackerFiltering=AttackerFiltering.NeverHitSelf,
                    filterCallback=hit=>!laserVictims.Contains(hit.healthComponent)}.Fire();
                foreach(var hit in splash.hitPoints)if(hit.hurtBox&&hit.hurtBox.healthComponent)laserVictims.Add(hit.hurtBox.healthComponent);
                c.Emit(BastionFxKind.LaserImpact,impact,Vector3.up);
                foreach(var victim in laserVictims)BastionServerSpiderMine.TriggerWeaponHit(c,victim);
                BastionUpgradeEffects.BeamHit(c,laserVictims.Count);
            }
            if(!pierce&&struck&&c.Bank.Ricochet&&c.Bank.RedlineTime>0)
            {
                var other=Enemies(c.Body,struck.corePosition,B.Get(S.RicochetRange)).FirstOrDefault(x=>x!=struck&&Visible(struck.corePosition,x));
                if(other){new BulletAttack{owner=c.gameObject,origin=struck.corePosition,aimVector=(other.corePosition-struck.corePosition).normalized,damage=c.Body.damage*B.Fraction(S.RicochetDamage),maxDistance=B.Get(S.RicochetRange),procCoefficient=B.Get(S.RicochetProc),hitMask=LayerIndex.CommonMasks.bullet,stopperMask=LayerIndex.CommonMasks.bullet,radius=.2f,smartCollision=true}.Fire();c.Emit(BastionFxKind.Pistol,struck.corePosition,other.corePosition);}
            }
            return end;
        }
        internal static void Launch(BastionController owner,CharacterBody target,Vector3 at,bool heavy,bool ultimate,uint id)
        {
            var obj=new GameObject("BastionServerMissile");obj.transform.position=at;var p=obj.AddComponent<BastionServerMissile>();
            p.Owner=owner;p.Target=target;p.Heavy=heavy;p.Id=id;p.Damage=owner.Body.damage*BastionBank.MissileDamage(heavy,ultimate);p.Begin();
        }
        internal static void Blast(GameObject attacker,CharacterBody owner,Vector3 at,float damage,float radius,float proc)
        {
            var result=new BlastAttack{attacker=attacker,inflictor=attacker,teamIndex=owner.teamComponent.teamIndex,position=at,radius=radius,baseDamage=damage,baseForce=250,procCoefficient=proc,crit=owner.RollCrit(),falloffModel=BlastAttack.FalloffModel.None,attackerFiltering=AttackerFiltering.NeverHitSelf}.Fire();
            if(NetworkServer.active&&owner)
            {
                var controller=owner.GetComponent<BastionController>();
                foreach(var hit in result.hitPoints)if(hit.hurtBox&&hit.hurtBox.healthComponent)BastionServerSpiderMine.TriggerWeaponHit(controller,hit.hurtBox.healthComponent);
            }
        }

    }
    internal sealed class BastionDamageReservation
    {
        private BastionController owner;private HealthComponent target;private float damage;
        internal void Assign(BastionController source,CharacterBody victim,float amount)
        {
            Release();if(!source||!victim||!victim.healthComponent)return;
            owner=source;target=victim.healthComponent;damage=BastionBank.PredictedDamage(amount,victim.armor);
            owner.Reservations.TryGetValue(target,out float old);owner.Reservations[target]=old+damage;
        }
        internal void Release()
        {
            if(owner&&!ReferenceEquals(target,null)&&owner.Reservations.TryGetValue(target,out float old))
            {float left=Mathf.Max(0,old-damage);if(left<.01f)owner.Reservations.Remove(target);else owner.Reservations[target]=left;}
            target=null;owner=null;damage=0;
        }
    }
    public sealed class BastionServerMissile:MonoBehaviour
    {
        internal BastionController Owner;internal CharacterBody Target;internal bool Heavy;internal uint Id;internal float Damage;
        private readonly BastionDamageReservation reservation=new BastionDamageReservation();
        private float age,routeCheck;private Vector3 waypoint;private bool loft,impacted;
        internal void Begin(){reservation.Assign(Owner,Target,Damage);Course(true);}
        private bool Course(bool launch)
        {
            if(!Target||!BastionCombat.TryMissileRoute(transform.position,Target,out waypoint,out loft))return false;
            if(launch&&!loft&&(BastionCombat.AimPoint(Target)-transform.position).sqrMagnitude>64)
            {
                var raised=transform.position+Vector3.up*1.5f;
                if(BastionCombat.ClearFlight(transform.position,raised)&&BastionCombat.ClearFlight(raised,BastionCombat.AimPoint(Target))){waypoint=raised;loft=true;}
            }
            Owner.Emit(Heavy?BastionFxKind.HeavyMissile:BastionFxKind.Missile,transform.position,loft?waypoint:BastionCombat.AimPoint(Target),Target,Id,loft?1:0);
            return true;
        }
        private bool Retarget()
        {
            reservation.Release();
            Target=BastionCombat.Select(Owner,B.Get(S.MissileRange)+10,Owner.Body.inputBank.aimDirection,transform.position);
            if(!Target)return false;
            reservation.Assign(Owner,Target,Damage);return Course(false);
        }
        private void FixedUpdate()
        {
            float dt=Time.fixedDeltaTime;age+=dt;
            if(!Owner||!Owner.Alive||age>BastionFlightGeometry.MissileLifetime){Destroy(gameObject);return;}
            if(!BastionCombat.Enemy(Owner.Body,Target)&&!Retarget()){Destroy(gameObject);return;}
            Vector3 before=transform.position,point=BastionCombat.AimPoint(Target);
            routeCheck-=dt;
            if(routeCheck<=0&&!loft)
            {
                routeCheck=.15f;
                if(!BastionCombat.ClearFlight(before,point)&&!Course(false)&&!Retarget()){Destroy(gameObject);return;}
                point=BastionCombat.AimPoint(Target);
            }
            Vector3 next=BastionFlightGeometry.Step(before,loft?waypoint:point,Heavy,dt);
            bool hit=BastionFlightGeometry.CrossesTarget(before,next,point,.9f);
            if(hit)next=point;
            if(Physics.Linecast(before,next,out var wall,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))
            {
                if(Course(false)||Retarget())return;
                Impact(wall.point);return;
            }
            transform.position=next;
            if(hit){Impact(point);return;}
            if(loft&&(next-waypoint).sqrMagnitude<.01f)loft=false;
        }
        private void Impact(Vector3 at)
        {
            impacted=true;
            BastionCombat.Blast(Owner.gameObject,Owner.Body,at,Damage,BastionBank.MissileRadius(Heavy),B.Get(Heavy?S.HeavyProc:S.MissileProc));
            Owner.Emit(Heavy?BastionFxKind.HeavyImpact:BastionFxKind.Impact,at,Vector3.up,null,Id);Destroy(gameObject);
        }
        private void OnDestroy()
        {
            reservation.Release();
            if(!impacted&&Owner&&NetworkServer.active)Owner.Emit(BastionFxKind.MissileEnd,transform.position,Vector3.zero,null,Id);
        }
    }
}
