using RoR2;
using UnityEngine;
using UnityEngine.Rendering;
namespace IvoryBastion
{
    // Short-lived visual cues only. No colliders, lights or damage components.
    public sealed class BastionAccentFx:MonoBehaviour
    {
        private BastionFxKind kind;private CharacterBody owner;private float age,duration;private bool packing;
        private LineRenderer[] lines;
        internal static void Create(BastionFxKind kind,Vector3 at,CharacterBody owner=null,float value=0)
        {
            if(Application.isBatchMode||kind!=BastionFxKind.Transform&&kind!=BastionFxKind.Saturation&&kind!=BastionFxKind.Upgrade)return;
            var go=new GameObject("Seraph "+kind+" articulation light");go.transform.position=at;
            var fx=go.AddComponent<BastionAccentFx>();fx.kind=kind;fx.owner=owner;fx.packing=value<0;
            fx.duration=kind==BastionFxKind.Transform?Mathf.Max(.3f,Mathf.Abs(value))+.25f:kind==BastionFxKind.Saturation?1.2f:.85f;
            int count=kind==BastionFxKind.Transform?12:kind==BastionFxKind.Saturation?10:6;
            fx.lines=new LineRenderer[count];
            for(int i=0;i<count;i++)
            {
                fx.lines[i]=BastionFx.Line(go.transform,5,.035f,BastionModel.Blue,false);
                fx.lines[i].widthCurve=new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.25f,1),new Keyframe(.75f,1),new Keyframe(1,.3f));
            }
            if(owner)go.transform.rotation=Quaternion.Euler(0,owner.transform.eulerAngles.y,0);
            fx.Render();
        }
        private void Update(){age+=Time.deltaTime;if(age>=duration){Destroy(gameObject);return;}if(owner&&kind!=BastionFxKind.Transform)transform.position=owner.corePosition;Render();}
        private void Render()
        {
            float t=Mathf.Clamp01(age/duration),fade=Mathf.Sin(t*Mathf.PI),open=Mathf.SmoothStep(0,1,Mathf.Clamp01(t*1.8f));
            if(packing)open=1-open;
            for(int i=0;i<lines.Length;i++)
            {
                var c=i%3==0?BastionModel.White:BastionModel.Blue;c.a=fade*(kind==BastionFxKind.Transform?.52f:.70f);
                lines[i].startColor=c;c.a*=kind==BastionFxKind.Transform?.80f:.22f;lines[i].endColor=c;
                float angle=i/(float)lines.Length*Mathf.PI*2;Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                for(int j=0;j<5;j++)
                {
                    float u=j/4f;Vector3 p;
                    if(kind==BastionFxKind.Transform)
                    {
                        p=BastionEffectGeometry.Deployment(i,j,open);
                    }
                    else if(kind==BastionFxKind.Saturation)
                    {
                        float sweep=Mathf.Clamp01((t-i*.024f)*1.6f);
                        // Ordered launch-channel streaks rise from the fortress racks.
                        float radius=2.10f+Mathf.Sin(angle*2)*.18f;
                        p=radial*(radius-u*.18f)+Vector3.up*(.45f+sweep*2.7f+u*.8f);
                        lines[i].widthMultiplier=.035f+.025f*Mathf.Sin(sweep*Mathf.PI);
                    }
                    else
                    {
                        float r=.6f+Mathf.Sin(u*Mathf.PI)*.5f;
                        p=radial*r+Vector3.up*(-.65f+t*1.7f+u*.5f);
                    }
                    lines[i].SetPosition(j,p);
                }
            }
        }
        private static Material sparkMaterial;
        internal static void Sparks(Vector3 at,Vector3 direction,bool impact,float scale=1)
        {
            if(Application.isBatchMode)return;
            if(direction.sqrMagnitude<.001f)direction=Vector3.up;direction.Normalize();
            if(!sparkMaterial)
            {
                sparkMaterial=new Material(BastionFx.LineMaterial){name="Seraph needle scintillation"};sparkMaterial.SetTexture("_MainTex",BastionContent.Icon("missile_flash").texture);
                if(sparkMaterial.HasProperty("_DstBlend"))sparkMaterial.SetFloat("_DstBlend",(float)BlendMode.One);
            }
            var go=new GameObject(impact?"Needle impact sparks":"Needle muzzle flash");go.transform.position=at;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=.35f;main.startLifetime=.24f;main.startSize=.1f*scale;main.gravityModifier=impact?.35f:0;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=8;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(BastionModel.Blue,.4f)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});fade.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sparkMaterial;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.045f;renderer.lengthScale=impact?2.4f:3.3f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            Vector3 right=Vector3.Cross(direction,Mathf.Abs(direction.y)>.95f?Vector3.forward:Vector3.up).normalized,up=Vector3.Cross(direction,right);
            ps.Play();int count=impact?7:4;
            for(int i=0;i<count;i++)
            {
                float a=i*2.399963f;Vector3 spread=right*Mathf.Cos(a)+up*Mathf.Sin(a);
                var p=new ParticleSystem.EmitParams{position=at,velocity=(direction*(impact?1.6f:4.2f)+spread*(impact?2.4f:.38f))*scale,startSize=(i==0?.22f:.07f)*scale,startLifetime=impact?.20f+i*.015f:.075f+i*.01f,startColor=Color.white};ps.Emit(p,1);
            }
            Object.Destroy(go,.5f);
        }
    }
}
