using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace IvoryBastion
{
    // One evolving gas volume, a short ignition flash, cooling smoke and ejecta.
    // Render-only: damage remains the original single server-side BlastAttack.
    public sealed class BastionFireExplosion:MonoBehaviour
    {
        private static readonly Material[] materials=new Material[4];
        private static int lightCount;
        private static int activeCount;
        private static readonly Queue<BastionFireExplosion>[] pools={new Queue<BastionFireExplosion>(),new Queue<BastionFireExplosion>(),new Queue<BastionFireExplosion>()};
        private Light flash;
        private bool countedLight,countedActive;
        private float age,radius,duration,lastSampleAge;
        private int tier,seed;
        private readonly ParticleSystem[] systems=new ParticleSystem[4];
        private readonly ParticleSystem.Particle[][] particles=new ParticleSystem.Particle[4][];
        private readonly ParticleSystemRenderer[] renderers=new ParticleSystemRenderer[4];

        internal static void Spawn(Vector3 at,int tier,float radius)
        {
            if(Application.isBatchMode||radius<=0)return;
            tier=Mathf.Clamp(tier,0,2);BastionFireExplosion effect=null;
            while(pools[tier].Count>0&&!effect)effect=pools[tier].Dequeue();
            if(!effect){var go=new GameObject("Seraph combustion detonation");effect=go.AddComponent<BastionFireExplosion>();}
            effect.transform.position=at;effect.tier=tier;effect.radius=radius;effect.age=0;
            effect.seed=Mathf.RoundToInt(at.x*31+at.y*17+at.z*7);
            effect.gameObject.SetActive(true);effect.countedActive=true;activeCount++;
            if(!effect.systems[0])effect.Build();else effect.Rearm();
        }

        private static Material MaterialFor(BastionExplosionLayer layer)
        {
            int index=(int)layer;if(materials[index])return materials[index];
            string name=layer==BastionExplosionLayer.Fireball?"missile_bluefire":layer==BastionExplosionLayer.Smoke?"missile_smoke":
                layer==BastionExplosionLayer.Flash?"missile_flash":"missile_ember";
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("IvoryBastion.assets."+name+".png"))
            using(var reader=new BinaryReader(stream??throw new FileNotFoundException(name)))
                ImageConversion.LoadImage(texture,reader.ReadBytes((int)stream.Length),true);
            bool additive=layer==BastionExplosionLayer.Flash||layer==BastionExplosionLayer.Spark;
            var material=new Material(BastionFx.LineMaterial){name="Seraph "+name,renderQueue=additive?3101:3100};
            material.SetTexture("_MainTex",texture);
            if(material.HasProperty("_SrcBlend"))material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            if(material.HasProperty("_DstBlend"))material.SetFloat("_DstBlend",(float)(additive?BlendMode.One:BlendMode.OneMinusSrcAlpha));
            if(material.HasProperty("_Boost"))material.SetFloat("_Boost",1);
            materials[index]=material;return material;
        }

        private void Build()
        {
            duration=BastionExplosionGeometry.Lifetime(tier);
            for(int i=0;i<systems.Length;i++)
            {
                var layer=(BastionExplosionLayer)i;
                var child=new GameObject("Detonation "+layer);child.transform.SetParent(transform,false);
                var system=child.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i]=system;particles[i]=new ParticleSystem.Particle[BastionExplosionGeometry.Count(layer,tier)];
                var main=system.main;main.playOnAwake=false;main.loop=false;main.duration=duration;
                main.maxParticles=particles[i].Length;main.simulationSpace=ParticleSystemSimulationSpace.Local;
                // Native simulation advances flipbook UVs; each frame we reset the
                // zero-velocity particles to the bounded analytic trajectory.
                main.simulationSpeed=1;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
                var renderer=system.GetComponent<ParticleSystemRenderer>();renderers[i]=renderer;renderer.sharedMaterial=MaterialFor(layer);
                renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.maxParticleSize=1;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.sortingFudge=layer==BastionExplosionLayer.Smoke?-.1f:0;
                if(layer==BastionExplosionLayer.Fireball)
                {
                    var sheet=system.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=8;sheet.numTilesY=4;
                    sheet.animation=ParticleSystemAnimationType.WholeSheet;sheet.cycleCount=1;
                    sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
                }
                system.Play();
            }
            Rearm();
        }

        private void Rearm()
        {
            var bounds=new Bounds(Vector3.zero,Vector3.one*(BastionDetonationPresentation.BoundsExtent(radius)*2));
            for(int i=0;i<systems.Length;i++)
            {
                systems[i].Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);systems[i].Play();
                // Native particle bounds otherwise reflect only the tiny
                // ignition. An offscreen blast must still become visible as
                // its full volume expands or the camera turns toward it.
                // Refresh on reuse because a pooled tier can serve any radius.
                renderers[i].localBounds=bounds;
            }
            if(tier>0&&lightCount<4)
            {
                if(!flash){flash=gameObject.AddComponent<Light>();flash.type=LightType.Point;flash.shadows=LightShadows.None;}
                flash.enabled=true;flash.color=new Color(.18f,.48f,1);flash.range=radius*1.25f;flash.intensity=4;
                lightCount++;countedLight=true;
            }
            else if(flash)flash.enabled=false;
            RenderParticles();
        }

        private void RenderParticles()
        {
            lastSampleAge=age;
            for(int layer=0;layer<systems.Length;layer++)
            {
                var kind=(BastionExplosionLayer)layer;var buffer=particles[layer];int count=0;
                // Keep the complete blast footprint and ignition. Reduce only
                // secondary smoke/sparks during overlapping barrages.
                int samples=BastionDetonationPresentation.Count(kind,tier,buffer.Length,activeCount);
                for(int i=0;i<samples;i++)
                {
                    var sample=BastionExplosionGeometry.At(kind,i*buffer.Length/samples,tier,radius,age,seed);
                    if(sample.Alpha<=0||sample.Remaining<=0)continue;
                    sample.Alpha*=BastionDetonationPresentation.Opacity(kind);
                    var colour=kind==BastionExplosionLayer.Flash?new Color(.72f,.90f,1,sample.Alpha):
                        kind==BastionExplosionLayer.Spark?new Color(.18f,.57f,1,sample.Alpha):new Color(1,1,1,sample.Alpha);
                    buffer[count++]=new ParticleSystem.Particle{position=sample.Position,startSize=sample.Size,
                        rotation=sample.Rotation,startColor=colour,startLifetime=sample.Life,remainingLifetime=sample.Remaining,
                        randomSeed=(uint)(i+1)};
                }
                systems[layer].SetParticles(buffer,count);
            }
        }

        private void Update()
        {
            age+=Time.deltaTime;
            if(flash)flash.intensity=4*Mathf.Clamp01(1-age/.18f);
            if(age>=duration){Retire();return;}
            bool visible=false;for(int i=0;i<renderers.Length&&!visible;i++)visible=renderers[i]&&renderers[i].isVisible;
            if(BastionDetonationPresentation.RequiresSample(age,lastSampleAge,visible))RenderParticles();
        }
        private void ReleaseCounters()
        {if(countedLight){lightCount=Mathf.Max(0,lightCount-1);countedLight=false;}if(countedActive){activeCount=Mathf.Max(0,activeCount-1);countedActive=false;}}
        private void Retire()
        {
            ReleaseCounters();if(flash)flash.enabled=false;
            for(int i=0;i<systems.Length;i++)if(systems[i])systems[i].Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(pools[tier].Count<(tier==0?8:4)){gameObject.SetActive(false);pools[tier].Enqueue(this);}
            else Destroy(gameObject);
        }
        private void OnDisable()=>ReleaseCounters();
        private void OnDestroy()=>ReleaseCounters();
    }
}
