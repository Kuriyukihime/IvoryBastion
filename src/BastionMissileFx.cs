using UnityEngine;
using UnityEngine.Rendering;
namespace IvoryBastion
{
    internal static class BastionMissileFx
    {
        private static Material glow,puff,smoke;
        internal const int ExhaustPoints=13;
        internal static AnimationCurve ExhaustShape()
        {
            var keys=new Keyframe[ExhaustPoints];
            for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(i/(float)(keys.Length-1),BastionEffectGeometry.ExhaustWidths[i]);
            return new AnimationCurve(keys);
        }
        internal static void Exhaust(LineRenderer outer,LineRenderer core,Vector3 origin,Vector3 axis,float length)
        {
            for(int i=0;i<ExhaustPoints;i++)
            {
                float t=i/(float)(ExhaustPoints-1);
                outer.SetPosition(i,origin+axis*length*t);
                core.SetPosition(i,origin+axis*length*t*.72f);
            }
        }
        internal static Material Glow
        {
            get
            {
                if(glow)return glow;glow=new Material(BastionFx.LineMaterial){name="Ivory missile plasma",renderQueue=3101};
                if(glow.HasProperty("_DstBlend"))glow.SetFloat("_DstBlend",(float)BlendMode.One);
                if(glow.HasProperty("_Boost"))glow.SetFloat("_Boost",1.5f);return glow;
            }
        }
        private static Material ParticleMaterial(bool dark)
        {
            if(dark&&smoke)return smoke;if(!dark&&puff)return puff;
            var material=new Material(dark?BastionFx.LineMaterial:Glow){name=dark?"Ivory blast vapor":"Ivory blast flare"};
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var colors=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {float dx=(x-31.5f)/31.5f,dy=(y-31.5f)/31.5f;float r=Mathf.Sqrt(dx*dx+dy*dy);float soft=Mathf.Pow(Mathf.Clamp01(1-r),1.6f);colors[y*64+x]=new Color(1,1,1,soft);}
            texture.SetPixels(colors);texture.Apply(false,true);material.SetTexture("_MainTex",texture);
            if(dark)smoke=material;else puff=material;return material;
        }
        internal static void Burst(Transform parent,int count,float size,float speed,float lifetime,bool dark)
        {
            var go=new GameObject(dark?"Missile vapor":"Missile ion burst");go.transform.SetParent(parent,false);
            var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.playOnAwake=false;main.loop=false;main.duration=lifetime;main.maxParticles=count;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=ParticleMaterial(dark);renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var fading=system.colorOverLifetime;fading.enabled=true;
            var bright=dark?new Color(.55f,.65f,.78f):Color.white;var end=dark?new Color(.025f,.035f,.055f):BastionModel.Blue;
            fading.color=new Gradient{colorKeys=new[]{new GradientColorKey(bright,0),new GradientColorKey(end,.65f)},alphaKeys=new[]{new GradientAlphaKey(dark?.4f:1,0),new GradientAlphaKey(dark?.25f:.7f,.3f),new GradientAlphaKey(0,1)}};
            var sizing=system.sizeOverLifetime;sizing.enabled=true;sizing.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,dark?.4f:1,1,dark?2.8f:.08f));
            system.Play();
            // Fibonacci directions give even coverage without disturbing gameplay's random state.
            for(int i=0;i<count;i++)
            {
                float y=1-2*(i+.5f)/count,a=i*2.399963f;float flat=Mathf.Sqrt(1-y*y);
                var direction=new Vector3(Mathf.Cos(a)*flat,y,Mathf.Sin(a)*flat);
                system.Emit(new ParticleSystem.EmitParams{position=parent.position,velocity=direction*speed+Vector3.up*(dark?.9f:0),startLifetime=lifetime*(.75f+.25f*(i%3)/2),startSize=size*(.8f+.4f*(i%4)/3),startColor=Color.white},1);
            }
        }
        internal static void Launch(Vector3 at,bool heavy,bool skyfall=false)
        {
            if(Application.isBatchMode)return;
            var go=new GameObject("Ivory launcher discharge");go.transform.position=at;
            float width=skyfall?BastionFlightGeometry.SkyfallVisualWidthScale:BastionFlightGeometry.BatteryVisualWidthScale;
            Burst(go.transform,heavy?6:3,(heavy?.48f:.25f)*width,(heavy?2.5f:1.5f)*width,.18f,false);
            Burst(go.transform,heavy?4:2,(heavy?.40f:.22f)*width,(heavy?1.25f:.75f)*width,.48f,true);
            Object.Destroy(go,.55f);
        }
        internal static void Explosion(Vector3 at,bool heavy)
        {
            BastionFireExplosion.Spawn(at,heavy?1:0,BastionBank.MissileRadius(heavy));
        }
        internal static void SiegeExplosion(Vector3 at)
        {
            BastionFireExplosion.Spawn(at,2,BastionWarheadGeometry.Radius);
        }
    }
}
