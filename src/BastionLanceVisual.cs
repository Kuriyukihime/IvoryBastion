using UnityEngine;
using UnityEngine.Rendering;

namespace IvoryBastion
{
    // Presentation only: the piercing hit, range and damage remain server-authoritative.
    public sealed class BastionLanceVisual:MonoBehaviour
    {
        private static Material material;
        private readonly LineRenderer[] layers=new LineRenderer[3];
        private readonly float[] widths={1.8f,.9f,.30f};
        private readonly Color[] colors={new Color(.10f,.36f,1f,.32f),new Color(.15f,.70f,1f,.95f),Color.white};
        private LineRenderer muzzleRing,impactRing;
        private readonly LineRenderer[] filaments=new LineRenderer[2];
        private readonly LineRenderer[] corona=new LineRenderer[6];
        private Light flash;
        private Vector3 start,end,across,up;
        private float age;
        private const float Duration=.72f;

        internal static void Create(Vector3 start,Vector3 end)
        {
            if(Application.isBatchMode)return;
            var go=new GameObject("Ivory ion lance discharge");
            var effect=go.AddComponent<BastionLanceVisual>();effect.start=start;effect.end=end;
            go.transform.position=start;effect.Build();
        }
        private void Build()
        {
            if(!material)
            {
                material=new Material(BastionFx.LineMaterial){name="Ivory luminous lance",renderQueue=3101};
                if(material.HasProperty("_SrcBlend"))material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                if(material.HasProperty("_DstBlend"))material.SetFloat("_DstBlend",(float)BlendMode.One);
                if(material.HasProperty("_Boost"))material.SetFloat("_Boost",2);
                BastionEnergySurface.Apply(material);
            }
            var direction=(end-start).normalized;
            if(direction.sqrMagnitude<.01f)direction=Vector3.forward;
            across=Vector3.Cross(direction,Mathf.Abs(direction.y)>.95f?Vector3.forward:Vector3.up).normalized;
            up=Vector3.Cross(across,direction).normalized;
            for(int i=0;i<layers.Length;i++)
            {
                var line=BastionFx.Line(transform,2,widths[i],colors[i]);line.sharedMaterial=material;
                line.widthCurve=new AnimationCurve(new Keyframe(0,.72f),new Keyframe(.025f,1),new Keyframe(.96f,1),new Keyframe(1,.65f));
                line.SetPosition(0,start);line.SetPosition(1,end);layers[i]=line;
            }
            muzzleRing=BastionFx.Line(transform,49,.11f,BastionModel.Blue);muzzleRing.sharedMaterial=material;
            impactRing=BastionFx.Line(transform,49,.08f,BastionModel.Blue);impactRing.sharedMaterial=material;
            for(int i=0;i<2;i++){filaments[i]=BastionFx.Line(transform,33,.045f,BastionModel.White);filaments[i].sharedMaterial=material;}
            for(int i=0;i<corona.Length;i++)
            {
                corona[i]=BastionFx.Line(transform,4,.065f,BastionModel.White);corona[i].sharedMaterial=material;
                corona[i].widthCurve=AnimationCurve.Linear(0,1,1,0);
            }
            BastionAccentFx.Sparks(end,-direction,true,2);
            flash=gameObject.AddComponent<Light>();flash.color=new Color(.35f,.75f,1f);flash.type=LightType.Point;
            flash.range=8;flash.intensity=4;flash.shadows=LightShadows.None;
            Render();
        }
        private void Update(){age+=Time.deltaTime;if(age>=Duration){Destroy(gameObject);return;}Render();}
        private void Render()
        {
            // Hold the full discharge briefly, then shrink and fade the whole afterglow.
            float fade=BastionEffectGeometry.LanceFade(age);
            for(int i=0;i<layers.Length;i++)
            {
                var color=colors[i];color.a*=fade;layers[i].startColor=layers[i].endColor=color;
                layers[i].widthMultiplier=widths[i]*(.55f+.45f*fade);
            }
            float ringFade=Mathf.Clamp01(1-age/.5f);var ringColor=BastionModel.Blue;ringColor.a=ringFade;
            muzzleRing.startColor=muzzleRing.endColor=impactRing.startColor=impactRing.endColor=ringColor;
            for(int i=0;i<49;i++)
            {
                float angle=i/48f*Mathf.PI*2;var radial=across*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                muzzleRing.SetPosition(i,start+radial*(.30f+age*2.2f));
                impactRing.SetPosition(i,end+radial*(.18f+age*3));
            }
            for(int k=0;k<2;k++)
            {
                var c=BastionModel.White;c.a=fade*.70f;filaments[k].startColor=filaments[k].endColor=c;
                // A pair of shallow traveling current traces gives the rigid
                // lance movement without turning it into a corkscrew projectile.
                for(int i=0;i<33;i++)
                {
                    float t=i/32f,a=t*10-age*18+k*Mathf.PI;
                    float pulse=Mathf.Exp(-Mathf.Pow((t-Mathf.Clamp01(age/.15f))/.13f,2));
                    float r=Mathf.Sin(t*Mathf.PI)*(.075f+.13f*pulse)*fade;
                    filaments[k].SetPosition(i,Vector3.Lerp(start,end,t)+(across*Mathf.Cos(a)+up*Mathf.Sin(a))*r);
                }
            }
            var direction=(end-start).normalized;
            var tipColour=BastionModel.White;tipColour.a=Mathf.Clamp01(1-age/.36f)*.88f;
            for(int k=0;k<corona.Length;k++)
            {
                corona[k].startColor=tipColour;var blue=BastionModel.Blue;blue.a=tipColour.a*.20f;corona[k].endColor=blue;
                for(int i=0;i<4;i++)
                {
                    var p=BastionEffectGeometry.LanceCrown(k,i,age);
                    corona[k].SetPosition(i,end+across*p.x+up*p.y+direction*p.z);
                }
            }
            flash.intensity=4*Mathf.Clamp01(1-age/.3f);
        }
    }
}
