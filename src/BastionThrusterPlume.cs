using UnityEngine;
using UnityEngine.Rendering;

namespace IvoryBastion
{
    // A fixed-size, animated volume approximation. Three intersecting soft
    // slices give the plume depth; adjacent atlas frames blend continuously.
    // The nozzle and sparse gas flecks are separate, bounded particle layers.
    internal sealed class BastionThrusterPlume
    {
        private static Material gasMaterial,vaporMaterial,glowMaterial;
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Vector2[] uv=new Vector2[24];
        private readonly Color[] colors=new Color[24];
        private readonly ParticleSystem glow,flecks;
        private readonly ParticleSystem.Particle[] glowParticles=new ParticleSystem.Particle[2];
        private readonly ParticleSystem.Particle[] fleckParticles=new ParticleSystem.Particle[BastionThrusterGeometry.Flecks];
        private readonly int seed;
        private int lastFrame=-1;
        private float phase;

        private static Material Material(string name,string asset,bool additive)
        {
            var material=new Material(BastionFx.LineMaterial){name=name,renderQueue=3098};
            material.SetTexture("_MainTex",BastionContent.Icon(asset).texture);
            if(material.HasProperty("_DstBlend"))material.SetFloat("_DstBlend",(float)(additive?BlendMode.One:BlendMode.OneMinusSrcAlpha));
            if(material.HasProperty("_Boost"))material.SetFloat("_Boost",1);
            return material;
        }
        internal static Material VaporMaterial=>vaporMaterial?vaporMaterial:(vaporMaterial=Material("Seraph soft thruster vapor","thruster_vapor",false));
        private static Material GasMaterial=>gasMaterial?gasMaterial:(gasMaterial=Material("Seraph flowing blue plasma","thruster_plume",true));
        private static Material GlowMaterial=>glowMaterial?glowMaterial:(glowMaterial=Material("Seraph nozzle radiance","thruster_glow",true));

        internal static BastionThrusterPlume Create(Transform parent,string name,int seed)=>new BastionThrusterPlume(parent,name,seed);
        private BastionThrusterPlume(Transform parent,string name,int seed)
        {
            this.seed=seed;root=new GameObject(name);root.transform.SetParent(parent,false);
            var vertices=new Vector3[24];var triangles=new int[36];
            for(int layer=0;layer<2;layer++)for(int plane=0;plane<3;plane++)
            {
                int start=(layer*3+plane)*4,t=(layer*3+plane)*6;
                for(int corner=0;corner<4;corner++)vertices[start+corner]=BastionThrusterGeometry.Vertex(plane,corner);
                triangles[t]=start;triangles[t+1]=start+1;triangles[t+2]=start+2;
                triangles[t+3]=start;triangles[t+4]=start+2;triangles[t+5]=start+3;
            }
            mesh=new Mesh{name="Seraph layered exhaust volume",vertices=vertices,triangles=triangles,uv=uv,colors=colors};
            mesh.RecalculateNormals();mesh.MarkDynamic();mesh.bounds=new Bounds(new Vector3(0,.5f,0),new Vector3(1.1f,1.1f,1.1f));
            root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=GasMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            glow=Particles(root.transform,"Nozzle glow",GlowMaterial,2);
            flecks=Particles(root.transform,"Flowing plasma flecks",GlowMaterial,fleckParticles.Length);
            root.SetActive(false);
        }
        private static ParticleSystem Particles(Transform parent,string name,Material material,int count)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.playOnAwake=false;main.loop=true;main.maxParticles=count;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;main.startSpeed=0;
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return system;
        }
        internal void Update(Vector3 origin,Vector3 axis,float length,float width,float strength,float ignition,bool visible)
        {
            if(!root)return;
            bool show=visible&&length>.025f&&width>.005f&&strength>.01f;
            if(root.activeSelf!=show)
            {
                root.SetActive(show);
                if(show){glow.Play();flecks.Play();}
                else{glow.Clear();flecks.Clear();}
            }
            if(!show)return;
            axis=axis.sqrMagnitude>.001f?axis.normalized:Vector3.down;
            var rotation=Quaternion.FromToRotation(Vector3.up,axis);root.transform.SetPositionAndRotation(origin,rotation);
            Vector3 scale=root.transform.parent?root.transform.parent.lossyScale:Vector3.one;
            root.transform.localScale=new Vector3(width/Mathf.Max(.001f,Mathf.Abs(scale.x)),length/Mathf.Max(.001f,Mathf.Abs(scale.y)),width/Mathf.Max(.001f,Mathf.Abs(scale.z)));
            float power=Mathf.Clamp01(strength),flash=Mathf.Clamp01(ignition);
            phase=BastionThrusterGeometry.Advance(phase,Time.deltaTime,power);
            int frame=BastionThrusterGeometry.Frame(phase,seed,out float blend);
            if(frame!=lastFrame)
            {
                lastFrame=frame;
                for(int layer=0;layer<2;layer++)for(int plane=0;plane<3;plane++)for(int corner=0;corner<4;corner++)
                    uv[(layer*3+plane)*4+corner]=BastionThrusterGeometry.UV(corner,(frame+layer)%BastionThrusterGeometry.Frames);
                mesh.uv=uv;
            }
            float opacity=(.32f+power*.16f)*(1+flash*.16f);
            for(int i=0;i<colors.Length;i++)colors[i]=new Color(1,1,1,opacity*(i<12?1-blend:blend));
            mesh.colors=colors;
            for(int i=0;i<glowParticles.Length;i++)
                glowParticles[i]=new ParticleSystem.Particle{position=origin+axis*(i==0?.012f:length*.035f),startSize=width*(i==0?.68f:.35f)*(1+flash*.25f),startColor=new Color(.60f,.88f,1,(i==0?.65f:.45f)*(.4f+power*.6f)),startLifetime=1,remainingLifetime=1};
            glow.SetParticles(glowParticles,glowParticles.Length);
            for(int i=0;i<fleckParticles.Length;i++)
            {
                var p=BastionThrusterGeometry.Fleck(i,seed,Time.time,out float alpha,out float size);
                fleckParticles[i]=new ParticleSystem.Particle{position=origin+rotation*new Vector3(p.x*width,p.y*length,p.z*width),startSize=width*size,
                    startColor=new Color(.30f,.65f,1,alpha*power),startLifetime=1,remainingLifetime=1};
            }
            flecks.SetParticles(fleckParticles,fleckParticles.Length);
        }
        internal void Dispose(){if(root)Object.Destroy(root);if(mesh)Object.Destroy(mesh);}
    }
}
