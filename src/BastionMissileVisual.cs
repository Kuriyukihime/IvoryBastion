using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;
namespace IvoryBastion
{
    public sealed class BastionMissileVisual:MonoBehaviour
    {
        private static readonly Dictionary<string,BastionMissileVisual> live=new Dictionary<string,BastionMissileVisual>();
        private string key;private CharacterBody target;private Vector3 destination,waypoint;
        private bool heavy,loft;private float age,scale;private TrailRenderer[] trails;private LineRenderer exhaust,core;
        internal static bool Create(BastionEffect message,CharacterBody target)
        {
            string key=message.Owner.Value+":"+message.Id;
            if(live.TryGetValue(key,out var existing)&&existing)
            {existing.target=target;existing.destination=message.B;existing.waypoint=message.B;existing.loft=message.Value>.5f;existing.transform.position=message.A;return false;}
            var go=new GameObject("Ivory armored seeking missile");go.transform.SetPositionAndRotation(message.A,Quaternion.LookRotation(Vector3.up));
            var visual=go.AddComponent<BastionMissileVisual>();visual.key=key;visual.target=target;visual.destination=message.B;visual.waypoint=message.B;visual.loft=message.Value>.5f;
            visual.heavy=message.Kind==BastionFxKind.HeavyMissile;visual.scale=visual.heavy?1.8f:1;live[key]=visual;
            BastionModel.Rig(go,visual.heavy?"warhead":"missile",1,new List<CharacterModel.RendererInfo>(),out _);
            visual.BuildExhaust();BastionMissileFx.Launch(message.A,visual.heavy);return true;
        }
        private void BuildExhaust()
        {
            float width=scale*BastionFlightGeometry.BatteryVisualWidthScale;
            trails=new TrailRenderer[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Blue missile wake":"White exhaust core");go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.back*.55f*scale;
                var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=BastionMissileFx.Glow;
                trail.time=heavy?.24f:.16f;trail.minVertexDistance=.13f;trail.widthMultiplier=(i==0?.16f:.045f)*width;
                trail.numCapVertices=2;trail.numCornerVertices=2;
                trail.widthCurve=AnimationCurve.Linear(0,1,1,0);trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;
                trail.lightProbeUsage=LightProbeUsage.Off;trail.reflectionProbeUsage=ReflectionProbeUsage.Off;
                Color color=i==0?BastionModel.Blue:Color.white;trail.colorGradient=new Gradient{colorKeys=new[]{new GradientColorKey(color,0),new GradientColorKey(BastionModel.Blue,1)},alphaKeys=new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(0,1)}};
                trails[i]=trail;
            }
            exhaust=BastionFx.Line(transform,BastionMissileFx.ExhaustPoints,.23f*width,BastionModel.Blue,false);exhaust.sharedMaterial=BastionMissileFx.Glow;
            core=BastionFx.Line(transform,BastionMissileFx.ExhaustPoints,.065f*width,Color.white,false);core.sharedMaterial=BastionMissileFx.Glow;
            exhaust.widthCurve=BastionMissileFx.ExhaustShape();core.widthCurve=AnimationCurve.Linear(0,1,1,0);
            exhaust.endColor=new Color(.06f,.20f,1,0);core.endColor=new Color(.5f,.82f,1,0);
        }
        internal static void End(uint owner,uint id){if(id!=0&&live.TryGetValue(owner+":"+id,out var visual)&&visual)Object.Destroy(visual.gameObject);}
        private void Update()
        {
            age+=Time.deltaTime;if(age>BastionFlightGeometry.MissileLifetime){Destroy(gameObject);return;}
            if(target)destination=BastionCombat.AimPoint(target);
            Vector3 before=transform.position,goal=loft?waypoint:destination;
            transform.position=BastionFlightGeometry.Step(before,goal,heavy,Time.deltaTime);
            Vector3 motion=transform.position-before;
            if(motion.sqrMagnitude>.0001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(motion),1-Mathf.Exp(-24*Time.deltaTime));
            if(loft&&(transform.position-waypoint).sqrMagnitude<.01f)loft=false;
            float length=(.62f+.045f*Mathf.Sin(age*53))*scale;
            BastionMissileFx.Exhaust(exhaust,core,Vector3.back*.55f*scale,Vector3.back,length);
        }
        private void OnDestroy()
        {
            if(key!=null)live.Remove(key);
            if(trails!=null)foreach(var trail in trails)if(trail){trail.emitting=false;trail.transform.SetParent(null,true);Destroy(trail.gameObject,trail.time+.05f);}
        }
    }
}
