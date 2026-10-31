using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace IvoryBastion
{
    [DefaultExecutionOrder(12110)]
    public sealed class BastionSiegeThrusters:MonoBehaviour
    {
        private BastionController owner;
        private BastionAnimation model;
        private CharacterModel characterModel;
        private Transform[] ports;
        private Renderer[] housings;
        private LineRenderer[] glow;
        private BastionThrusterPlume[] plumes;
        private ParticleSystem[] wash;
        private readonly bool[] washing=new bool[6];
        private readonly float[] washAt=new float[6];
        private readonly int[] washSequence=new int[6];
        private readonly RaycastHit[] contacts=new RaycastHit[6];
        private readonly bool[] ground=new bool[6];
        private Mesh nozzleMesh;
        private float probeAt,targetStrength;
        internal float Strength { get; private set; }

        private void Start(){owner=GetComponent<BastionController>();}
        private void Build()
        {
            nozzleMesh=Nozzle();ports=new Transform[6];housings=new Renderer[6];glow=new LineRenderer[6];
            plumes=new BastionThrusterPlume[6];wash=new ParticleSystem[6];characterModel=model.GetComponent<CharacterModel>();
            var rendererInfos=new CharacterModel.RendererInfo[6];
            for(int i=0;i<6;i++)
            {
                float angle=(i+.5f)*Mathf.PI/3;
                var port=new GameObject("Seraph lift thruster "+i);ports[i]=port.transform;
                port.transform.SetParent(model.Siege,false);port.transform.localPosition=new Vector3(Mathf.Sin(angle)*.64f,.16f,Mathf.Cos(angle)*.64f);
                port.AddComponent<MeshFilter>().sharedMesh=nozzleMesh;
                var housing=port.AddComponent<MeshRenderer>();housings[i]=housing;housing.sharedMaterial=BastionModel.Material("LiftHousing",BastionModel.Black,0,.55f);
                housing.shadowCastingMode=ShadowCastingMode.On;
                rendererInfos[i]=new CharacterModel.RendererInfo{renderer=housing,defaultMaterial=housing.sharedMaterial,defaultShadowCastingMode=ShadowCastingMode.On,ignoreOverlays=false};
                glow[i]=BastionFx.Line(transform,33,.018f,BastionModel.Blue);
                plumes[i]=BastionThrusterPlume.Create(transform,"Seraph siege lift plume "+i,173+i*29);
                wash[i]=BuildWash(i);
            }
            model.SiegeRenderers=(model.SiegeRenderers??new CharacterModel.RendererInfo[0]).Concat(rendererInfos).ToArray();
            if(characterModel&&owner&&owner.Sieged)BastionVisualSizing.SetRenderers(characterModel,model.SiegeRenderers);
        }
        private ParticleSystem BuildWash(int index)
        {
            var go=new GameObject("Seraph soft groundwash "+index);go.transform.SetParent(transform,false);
            var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.playOnAwake=false;main.loop=true;main.duration=1;main.maxParticles=10;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=.48f;main.startSpeed=0;
            main.gravityModifier=0;main.scalingMode=ParticleSystemScalingMode.Shape;
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            var fade=system.colorOverLifetime;fade.enabled=true;
            fade.color=new Gradient{colorKeys=new[]{new GradientColorKey(new Color(.50f,.65f,.80f),0),new GradientColorKey(new Color(.26f,.36f,.48f),1)},
                alphaKeys=new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.16f,.2f),new GradientAlphaKey(.09f,.55f),new GradientAlphaKey(0,1)}};
            var size=system.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.55f),new Keyframe(.45f,1),new Keyframe(1,1.5f)));
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=BastionThrusterPlume.VaporMaterial;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            system.useAutoRandomSeed=false;system.randomSeed=(uint)(501+index*37);
            return system;
        }
        private void GroundWash(int index,bool visible)
        {
            var system=wash[index];if(!system)return;
            if(!visible)
            {
                if(washing[index])system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                washing[index]=false;washAt[index]=0;return;
            }
            if(!washing[index]){system.Play();washing[index]=true;}
            if(Time.time<washAt[index])return;
            washAt[index]=Time.time+Mathf.Lerp(.24f,.14f,Strength);
            var hit=contacts[index];var normal=hit.normal;
            var tangent=Vector3.Cross(normal,Mathf.Abs(normal.z)>.8f?Vector3.right:Vector3.forward).normalized;
            var across=Vector3.Cross(normal,tangent);
            int sequence=washSequence[index]++;float phase=sequence*2.399963f+index*1.7f;
            var radial=tangent*Mathf.Cos(phase)+across*Mathf.Sin(phase);
            float spread=.09f+Mathf.Min(hit.distance,3.2f)*.11f;
            float closeness=1-Mathf.Clamp01(hit.distance/3.2f);
            var particle=new ParticleSystem.EmitParams{
                position=hit.point+normal*.075f+radial*spread,
                velocity=radial*(.45f+Strength*.60f)+normal*.22f,
                startSize=.26f+spread*.65f,startLifetime=.38f+(sequence%4)*.045f,
                rotation=phase*Mathf.Rad2Deg,startColor=new Color(1,1,1,Strength*closeness)};
            system.Emit(particle,1);
        }
        private bool NeedsBind(BastionAnimation current)
        {
            if(current!=model||ports==null)return true;
            for(int i=0;i<ports.Length;i++)if(!ports[i])return true;
            return false;
        }
        private void LateUpdate()
        {
            if(Application.isBatchMode||!owner)return;
            var current=owner.Model;
            if(NeedsBind(current))
            {
                Release();model=current;if(!model||!model.Siege)return;Build();
            }
            if(!model||ports==null)return;
            bool active=owner.Alive&&owner.Sieged&&model.Siege.gameObject.activeInHierarchy;
            if(active&&Time.time>=probeAt)
            {
                probeAt=Time.time+.12f;
                var foot=model.Siege.TransformPoint(new Vector3(0,.04f,0));
                bool supported=Physics.Raycast(foot+Vector3.up*.25f,Vector3.down,out var center,100,LayerIndex.world.mask,QueryTriggerInteraction.Ignore);
                targetStrength=BastionVisualRules.HoverStrength(supported?center.distance-.25f:0,supported);
                for(int i=0;i<6;i++)ground[i]=Physics.Raycast(ports[i].position+Vector3.up*.03f,Vector3.down,out contacts[i],12,LayerIndex.world.mask,QueryTriggerInteraction.Ignore);
            }
            if(!active){targetStrength=0;probeAt=0;}
            Strength=Mathf.MoveTowards(Strength,targetStrength,Time.deltaTime*3.5f);
            for(int i=0;i<6;i++)
            {
                bool show=active&&Strength>.015f;glow[i].enabled=show;
                GroundWash(i,show&&ground[i]&&contacts[i].distance<3.2f);
                var origin=ports[i].position;
                float pulse=1+.055f*Mathf.Sin(Time.time*19+i*1.7f);
                float length=(1.3f+Strength*1.3f)*pulse;
                if(ground[i])length=Mathf.Min(length,Mathf.Max(0,contacts[i].distance-.07f));
                plumes[i].Update(origin,Vector3.down,length,.62f+Strength*.22f,Strength,0,show);
                if(show)Ring(glow[i],origin,Vector3.up,.165f,.42f*Strength);
            }
        }
        private static void Ring(LineRenderer line,Vector3 at,Vector3 normal,float radius,float alpha)
        {
            Vector3 u=Vector3.Cross(normal,Mathf.Abs(normal.z)>.8f?Vector3.right:Vector3.forward).normalized,v=Vector3.Cross(normal,u);
            var color=BastionModel.Blue;color.a=alpha;line.startColor=line.endColor=color;
            for(int k=0;k<=32;k++){float a=k*Mathf.PI/16;line.SetPosition(k,at+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius);}
        }
        private static Mesh Nozzle()
        {
            const int sides=24;var vertices=new Vector3[sides*4];var triangles=new int[sides*4*6];
            float[] radii={.18f,.17f,.11f,.09f},heights={.12f,0,0,.11f};
            int index=0;
            for(int ring=0;ring<4;ring++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides;int k=ring*sides+i;
                vertices[k]=new Vector3(Mathf.Cos(a)*radii[ring],heights[ring],Mathf.Sin(a)*radii[ring]);
                int b=ring*sides+(i+1)%sides,c=((ring+1)%4)*sides+i,d=((ring+1)%4)*sides+(i+1)%sides;
                triangles[index++]=k;triangles[index++]=b;triangles[index++]=c;triangles[index++]=b;triangles[index++]=d;triangles[index++]=c;
            }
            var mesh=new Mesh{name="Seraph hollow lift nozzles",vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private void Release()
        {
            // Remove only this component's six housings. Do not retain dead
            // renderer references or append another set when the rig respawns.
            if(housings!=null)
            {
                var owned=new HashSet<Renderer>(housings.Where(renderer=>!ReferenceEquals(renderer,null)));
                if(model&&model.SiegeRenderers!=null)
                    model.SiegeRenderers=model.SiegeRenderers.Where(info=>!owned.Contains(info.renderer)).ToArray();
                if(characterModel&&characterModel.baseRendererInfos!=null)
                {
                    var keep=characterModel.baseRendererInfos.Where(info=>!owned.Contains(info.renderer)).ToArray();
                    if(keep.Length!=characterModel.baseRendererInfos.Length)
                        BastionVisualSizing.SetRenderers(characterModel,keep);
                }
            }
            if(plumes!=null)foreach(var plume in plumes)plume?.Dispose();
            if(glow!=null)foreach(var line in glow)if(line)Destroy(line.gameObject);
            if(wash!=null)foreach(var system in wash)if(system){system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Destroy(system.gameObject);}
            if(ports!=null)foreach(var port in ports)if(port)Destroy(port.gameObject);
            if(nozzleMesh)Destroy(nozzleMesh);
            ports=null;housings=null;glow=null;plumes=null;wash=null;model=null;characterModel=null;nozzleMesh=null;
            Strength=targetStrength=0;probeAt=0;
            for(int i=0;i<ground.Length;i++){ground[i]=washing[i]=false;washAt[i]=0;washSequence[i]=0;}
        }
        private void OnDisable(){Release();}
        private void OnDestroy(){Release();}
    }
}
