using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace IvoryBastion
{
    public sealed class BastionWarheadVisual:MonoBehaviour
    {
        private static readonly Dictionary<string,BastionWarheadVisual> live=new Dictionary<string,BastionWarheadVisual>();
        private string key;private Vector3 from,to;private float duration,age;
        private TrailRenderer[] trails;private LineRenderer exhaust,core,marker;private GameObject markerRoot;
        private Mesh markerMesh;private readonly Color[] markerColours=new Color[16];
        internal static void Create(BastionEffect message)
        {
            string key=message.Owner.Value+":"+message.Id;
            if(live.TryGetValue(key,out var old)&&old)return;
            var go=new GameObject("Skyfall celestial siege warhead");go.transform.SetPositionAndRotation(message.A,Quaternion.LookRotation(Vector3.up,Vector3.forward));
            var visual=go.AddComponent<BastionWarheadVisual>();visual.key=key;visual.from=message.A;visual.to=message.B;
            visual.duration=Mathf.Max(.1f,message.Value);live[key]=visual;
            // Original length and compact fins fit the slim enclosed housings;
            // battery missiles use the same design with a broader radial scale.
            BastionModel.Rig(go,"skyfall",BastionWarheadGeometry.VisualScale,new List<CharacterModel.RendererInfo>(),out _);
            visual.Build();BastionMissileFx.Launch(message.A,true,true);
        }
        private void Build()
        {
            float size=BastionWarheadGeometry.VisualScale/2.4f;
            float width=size*BastionFlightGeometry.SkyfallVisualWidthScale;
            trails=new TrailRenderer[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Siege warhead blue contrail":"Siege warhead white core");go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.back*(2.35f*size);
                var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=BastionMissileFx.Glow;
                trail.time=.55f;trail.minVertexDistance=.2f;trail.widthMultiplier=(i==0?.65f:.20f)*width;
                trail.numCapVertices=2;trail.numCornerVertices=2;
                trail.widthCurve=AnimationCurve.Linear(0,1,1,0);trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;
                trail.lightProbeUsage=LightProbeUsage.Off;trail.reflectionProbeUsage=ReflectionProbeUsage.Off;
                var color=i==0?BastionModel.Blue:Color.white;
                trail.colorGradient=new Gradient{colorKeys=new[]{new GradientColorKey(color,0),new GradientColorKey(BastionModel.Blue,1)},alphaKeys=new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(0,1)}};
                trails[i]=trail;
            }
            exhaust=BastionFx.Line(transform,BastionMissileFx.ExhaustPoints,.65f*width,BastionModel.Blue,false);exhaust.sharedMaterial=BastionMissileFx.Glow;
            core=BastionFx.Line(transform,BastionMissileFx.ExhaustPoints,.19f*width,Color.white,false);core.sharedMaterial=BastionMissileFx.Glow;
            exhaust.widthCurve=BastionMissileFx.ExhaustShape();core.widthCurve=AnimationCurve.Linear(0,1,1,0);
            exhaust.endColor=new Color(.06f,.20f,1,0);core.endColor=new Color(.5f,.82f,1,0);
            markerRoot=new GameObject("Skyfall impact footprint");markerRoot.transform.position=to+Vector3.up*.04f;
            marker=BastionFx.Line(markerRoot.transform,97,.065f,BastionModel.Blue,false);
            for(int i=0;i<97;i++){float a=i/96f*Mathf.PI*2;marker.SetPosition(i,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*BastionWarheadGeometry.Radius);}
            var vertices=new Vector3[16];var triangles=new int[24];
            for(int i=0;i<4;i++)
            {
                for(int j=0;j<4;j++)vertices[i*4+j]=BastionEffectGeometry.SkyfallArrow(i,j,BastionWarheadGeometry.Radius);
                int t=i*6,v=i*4;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            markerMesh=new Mesh{name="Skyfall quadrant approach chevrons",vertices=vertices,triangles=triangles,colors=markerColours};markerMesh.MarkDynamic();markerMesh.RecalculateBounds();
            markerRoot.AddComponent<MeshFilter>().sharedMesh=markerMesh;
            var renderer=markerRoot.AddComponent<MeshRenderer>();renderer.sharedMaterial=BastionFx.LineMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        internal static void End(uint owner,uint id)
        {if(live.TryGetValue(owner+":"+id,out var visual)&&visual)Object.Destroy(visual.gameObject);}
        private void Update()
        {
            age+=Time.deltaTime;
            if(age>duration+1.5f){Destroy(gameObject);return;}
            float t=Mathf.Clamp01(age/duration);
            transform.position=BastionWarheadGeometry.Position(from,to,t);
            var tangent=BastionWarheadGeometry.Position(from,to,Mathf.Min(1,t+.003f))-BastionWarheadGeometry.Position(from,to,Mathf.Max(0,t-.003f));
            if(tangent.sqrMagnitude>.0001f)
            {
                var side=Vector3.Cross(to-from,Vector3.up);if(side.sqrMagnitude<.001f)side=Vector3.right;
                transform.rotation=Quaternion.LookRotation(tangent,Vector3.Cross(side,tangent));
            }
            float size=BastionWarheadGeometry.VisualScale/2.4f;
            float length=(2.8f+Mathf.Sin(age*39)*.12f)*size;
            BastionMissileFx.Exhaust(exhaust,core,Vector3.back*(2.35f*size),Vector3.back,length);
            // Keep one readable footprint when fallback targeting stacks several
            // warheads at the same point, instead of ten opaque overlapping rings.
            bool show=true;
            foreach(var other in live.Values)
                if(other&&other!=this&&(other.to-to).sqrMagnitude<10.24f&&
                    (other.age>age||other.age==age&&string.CompareOrdinal(other.key,key)<0)){show=false;break;}
            markerRoot.SetActive(show);
            float arrival=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.60f)/.40f));
            var color=BastionModel.Blue;color.a=.075f+.14f*arrival;marker.startColor=marker.endColor=color;
            for(int i=0;i<markerColours.Length;i++)markerColours[i]=new Color(.20f,.58f,1,(i%4==0?.30f:.12f)*(.4f+.6f*arrival));
            markerMesh.colors=markerColours;
            if(t>=1){foreach(var trail in trails)if(trail)trail.emitting=false;exhaust.enabled=core.enabled=false;}
        }
        private void OnDestroy()
        {
            if(key!=null)live.Remove(key);if(markerRoot)Destroy(markerRoot);if(markerMesh)Destroy(markerMesh);
            if(trails!=null)foreach(var trail in trails)if(trail){trail.emitting=false;trail.transform.SetParent(null,true);Destroy(trail.gameObject,trail.time+.05f);}
        }
    }
}
