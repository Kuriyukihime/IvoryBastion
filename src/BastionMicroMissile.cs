using System.Collections.Generic;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
namespace IvoryBastion
{
    public sealed class BastionServerMicroMissile:MonoBehaviour
    {
        private BastionController owner;private CharacterBody target;private uint id;private float age,routeCheck,damage;
        private Vector3 waypoint;private bool loft,impacted;
        private readonly BastionMicroSplashReservation reservation=new BastionMicroSplashReservation();
        private BastionRushTargetPlan plan;private BastionRushTargetPlan.Allocation allocation;
        internal static void Launch(BastionController owner,CharacterBody target,Vector3 from,Vector3 flight,uint id,int shot,BastionRushTargetPlan plan)
        {
            if(!NetworkServer.active||!target)return;
            var go=new GameObject("Wingburst server micro-missile");go.transform.position=from;
            var missile=go.AddComponent<BastionServerMicroMissile>();missile.owner=owner;missile.target=target;missile.id=id;
            missile.plan=plan;missile.allocation=plan?.Claim(owner,target,B.Get(S.WingburstRadius));
            missile.damage=owner.Body.damage*B.Fraction(S.WingburstDamage);missile.reservation.Assign(owner,target,missile.damage,B.Get(S.WingburstRadius));
            var side=Vector3.Cross(Vector3.up,flight).normalized;if(side.sqrMagnitude<.01f)side=owner.transform.right;
            var fan=from+flight*2.4f+side*(shot%2==0?-1.1f:1.1f)+Vector3.up*(1.0f+(shot%4)*.22f);
            missile.loft=BastionCombat.ClearFlight(from,fan)&&BastionCombat.ClearFlight(fan,BastionCombat.AimPoint(target));
            missile.waypoint=missile.loft?fan:BastionCombat.AimPoint(target);missile.Emit();
        }
        private void Emit()=>owner.Emit(BastionFxKind.WingburstMissile,transform.position,waypoint,target,id,loft?1:0);
        private bool Course()
        {if(!target||!BastionCombat.TryMissileRoute(transform.position,target,out waypoint,out loft))return false;Emit();return true;}
        private bool Retarget()
        {
            reservation.Release();allocation?.Release();target=BastionUtilityCombat.FindRush(owner,transform.position,B.Get(S.WingburstRange),plan);
            if(!target||!Course())return false;
            allocation=plan?.Claim(owner,target,B.Get(S.WingburstRadius));reservation.Assign(owner,target,damage,B.Get(S.WingburstRadius));return true;
        }
        private void FixedUpdate()
        {
            if(!NetworkServer.active){Destroy(gameObject);return;}
            float dt=Time.fixedDeltaTime;age+=dt;
            if(!owner||!owner.Alive||age>2.5f){Destroy(gameObject);return;}
            if(!BastionCombat.Enemy(owner.Body,target)&&!Retarget()){Destroy(gameObject);return;}
            var before=transform.position;var point=BastionCombat.AimPoint(target);routeCheck-=dt;
            if(routeCheck<=0&&!loft){routeCheck=.12f;if(!BastionCombat.ClearFlight(before,point)&&!Course()&&!Retarget()){Destroy(gameObject);return;}}
            var next=Vector3.MoveTowards(before,loft?waypoint:point,75*dt);
            bool hit=BastionFlightGeometry.CrossesTarget(before,next,point,.55f);
            if(hit)next=point;
            if(Physics.Linecast(before,next,out var wall,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))
            {if(Course()||Retarget())return;Impact(wall.point);return;}
            transform.position=next;if(hit){Impact(point);return;}
            if(loft&&(next-waypoint).sqrMagnitude<.01f)loft=false;
        }
        private void Impact(Vector3 at)
        {
            if(impacted)return;impacted=true;reservation.Release();allocation?.Commit();
            BastionCombat.Blast(owner.gameObject,owner.Body,at,damage,B.Get(S.WingburstRadius),B.Get(S.WingburstProc));
            owner.Emit(BastionFxKind.WingburstMicroImpact,at,Vector3.up,null,id,B.Get(S.WingburstRadius));Destroy(gameObject);
        }
        private void OnDestroy()
        {reservation.Release();allocation?.Release();if(!impacted&&owner&&NetworkServer.active)owner.Emit(BastionFxKind.WingburstMicroEnd,transform.position,Vector3.zero,null,id);}
    }

    public sealed class BastionMicroMissileVisual:MonoBehaviour
    {
        private static readonly Dictionary<string,BastionMicroMissileVisual> live=new Dictionary<string,BastionMicroMissileVisual>();
        private string key;private CharacterBody target;private Vector3 destination,waypoint;private bool loft;private float age;
        private TrailRenderer[] trails;private LineRenderer exhaust,core;
        internal static void Create(BastionEffect message,CharacterBody target)
        {
            string key=message.Owner.Value+":"+message.Id;
            if(live.TryGetValue(key,out var old)&&old){old.target=target;old.waypoint=message.B;old.loft=message.Value>.5f;old.transform.position=message.A;return;}
            var go=new GameObject("Seraph seeking micro-missile");go.transform.position=message.A;
            var visual=go.AddComponent<BastionMicroMissileVisual>();visual.key=key;visual.target=target;visual.destination=target?BastionCombat.AimPoint(target):message.B;
            visual.waypoint=message.B;visual.loft=message.Value>.5f;live[key]=visual;
            var motion=(message.B-message.A).normalized;go.transform.rotation=Quaternion.LookRotation(motion.sqrMagnitude>.001f?motion:Vector3.up);
            BastionModel.Rig(go,"missile",.52f,new List<CharacterModel.RendererInfo>(),out _);
            visual.trails=new TrailRenderer[2];
            for(int i=0;i<2;i++)
            {
                var child=new GameObject(i==0?"Micro-missile vapor":"Micro-missile white core");child.transform.SetParent(go.transform,false);child.transform.localPosition=Vector3.back*.24f;
                var trail=child.AddComponent<TrailRenderer>();trail.sharedMaterial=BastionUtilityFx.Energy;trail.time=i==0?.23f:.15f;trail.minVertexDistance=.055f;
                trail.widthMultiplier=i==0?.12f:.032f;trail.widthCurve=AnimationCurve.Linear(0,1,1,0);trail.numCapVertices=3;trail.numCornerVertices=2;
                trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;
                var color=i==0?BastionModel.Blue:Color.white;trail.colorGradient=new Gradient{colorKeys=new[]{new GradientColorKey(color,0),new GradientColorKey(BastionModel.Blue,1)},alphaKeys=new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(0,1)}};visual.trails[i]=trail;
            }
            visual.exhaust=BastionUtilityFx.Line(go.transform,BastionMissileFx.ExhaustPoints,.14f,BastionModel.Blue,false);
            visual.core=BastionUtilityFx.Line(go.transform,BastionMissileFx.ExhaustPoints,.04f,Color.white,false);
            visual.exhaust.widthCurve=BastionMissileFx.ExhaustShape();visual.core.widthCurve=AnimationCurve.Linear(0,1,1,0);
        }
        internal static void End(uint owner,uint id){if(live.TryGetValue(owner+":"+id,out var missile)&&missile)Object.Destroy(missile.gameObject);}
        private void Update()
        {
            age+=Time.deltaTime;if(age>2.7f){Destroy(gameObject);return;}
            if(target)destination=BastionCombat.AimPoint(target);
            var before=transform.position;transform.position=Vector3.MoveTowards(before,loft?waypoint:destination,75*Time.deltaTime);
            var motion=transform.position-before;if(motion.sqrMagnitude>.00001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(motion),1-Mathf.Exp(-32*Time.deltaTime));
            if(loft&&(transform.position-waypoint).sqrMagnitude<.01f)loft=false;
            BastionMissileFx.Exhaust(exhaust,core,Vector3.back*.24f,Vector3.back,.42f+.025f*Mathf.Sin(age*45));
        }
        private void OnDestroy()
        {if(key!=null)live.Remove(key);if(trails!=null)foreach(var trail in trails)if(trail){trail.emitting=false;trail.transform.SetParent(null,true);Destroy(trail.gameObject,trail.time+.05f);}}
    }
}
