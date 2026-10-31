using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace IvoryBastion
{
    internal static class BastionUtilityFx
    {
        private static Material energy;
        internal static Material Energy
        {get{if(!energy){energy=new Material(BastionFx.LineMaterial){name="Seraph utility energy"};BastionEnergySurface.Apply(energy);}return energy;}}
        internal static LineRenderer Line(Transform parent,int count,float width,Color color,bool world=true)
        {var line=BastionFx.Line(parent,count,width,color,world);line.sharedMaterial=Energy;return line;}
        internal static void Color(LineRenderer line,Color color,float alpha){color.a*=alpha;line.startColor=line.endColor=color;}
    }

    // The body-owned driver reconstructs continuous effects from repeatable host
    // snapshots. Missing a one-shot VFX packet cannot remove an entire ability.
    [DefaultExecutionOrder(12100)]
    public sealed class BastionUtilityVisual:MonoBehaviour
    {
        private BastionUtility utility;private BastionAnimation model;
        private readonly Transform[] tips=new Transform[4];
        private readonly TrailRenderer[] trails=new TrailRenderer[8];
        private readonly LineRenderer[] wingRails=new LineRenderer[4],wake=new LineRenderer[12];
        private GameObject wingRoot;
        private uint seenCast;private BastionUtilityKind seenKind;private float wingAfterglow;
        private BastionPcmVoice sound;
        private void Start()
        {
            if(Application.isBatchMode){enabled=false;return;}
            utility=GetComponent<BastionUtility>();if(!utility){enabled=false;return;}
            wingRoot=new GameObject("Heavenfall Rush flight envelope");wingRoot.transform.SetParent(transform,false);
            for(int i=0;i<4;i++)
            {
                var anchor=new GameObject("Heavenfall wingtip "+i).transform;anchor.SetParent(wingRoot.transform,false);
                for(int layer=0;layer<2;layer++)
                {
                    var go=new GameObject(layer==0?"Ion vapor ribbon":"White hot filament");go.transform.SetParent(anchor,false);
                    var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=BastionUtilityFx.Energy;trail.time=layer==0?.42f:.29f;
                    trail.minVertexDistance=.045f;trail.widthMultiplier=layer==0?.34f:.075f;trail.numCapVertices=4;trail.numCornerVertices=3;
                    trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trail.autodestruct=false;trail.emitting=false;
                    trail.widthCurve=new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.16f,1),new Keyframe(1,0));
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(layer==0?new Color(.15f,.65f,1):Color.white,0),new GradientColorKey(new Color(.08f,.25f,.7f),1)},new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(0,1)});trail.colorGradient=gradient;trails[i*2+layer]=trail;
                }
                wingRails[i]=BastionUtilityFx.Line(wingRoot.transform,9,.075f,BastionModel.Blue);
            }
            for(int i=0;i<wake.Length;i++){wake[i]=BastionUtilityFx.Line(wingRoot.transform,12,i%2==0?.036f:.018f,new Color(.65f,.87f,1,.65f));wake[i].widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.25f,1),new Keyframe(1,0));}
            wingRoot.SetActive(false);
        }
        private void Cache()
        {
            var current=utility.Controller.Model;if(current==model)return;model=current;for(int i=0;i<tips.Length;i++)tips[i]=null;if(!model)return;
            foreach(var bone in model.MobileBones)
            {if(bone.name=="tip_wing_l")tips[0]=bone;else if(bone.name=="tip_wing_r")tips[1]=bone;else if(bone.name=="tip_vane_l")tips[2]=bone;else if(bone.name=="tip_vane_r")tips[3]=bone;}
        }
        private void LateUpdate()
        {
            if(!utility||!wingRoot||!utility.Controller)return;Cache();
            var kind=utility.Controller.Alive?utility.VisualKind:BastionUtilityKind.None;
            if(kind!=seenKind||utility.CastId!=seenCast)
            {
                if(seenKind==BastionUtilityKind.Wingburst&&kind!=BastionUtilityKind.Wingburst)wingAfterglow=.48f;
                sound?.Dispose();sound=null;seenCast=utility.CastId;seenKind=kind;
                foreach(var trail in trails){trail.emitting=false;if(kind==BastionUtilityKind.Wingburst)trail.Clear();}
                if(kind!=BastionUtilityKind.None&&utility.VisualAge<1)
                {
                    sound=BastionAudio.Cue(kind==BastionUtilityKind.Wingburst?"wingburst_start":"mine_deploy",transform.position,.65f);
                    BastionVoice.Request(utility.Controller.Body,kind==BastionUtilityKind.Wingburst?"voice_heavenfall":"voice_mines");
                }
            }
            bool flying=kind==BastionUtilityKind.Wingburst;
            wingAfterglow=Mathf.Max(0,wingAfterglow-Time.deltaTime);wingRoot.SetActive(flying||wingAfterglow>0);
            if(flying)Wingburst();else
            {foreach(var trail in trails)trail.emitting=false;foreach(var rail in wingRails)BastionUtilityFx.Color(rail,BastionModel.Blue,0);foreach(var line in wake)BastionUtilityFx.Color(line,Color.white,0);}
            if(sound!=null)BastionAudio.PositionVoice(sound,transform.position,.65f,60f);
        }
        private void Wingburst()
        {
            float age=utility.VisualAge,w=utility.CastWindup,duration=utility.CastSpan;
            float gather=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/Mathf.Max(.03f,w)));
            bool running=age>=w&&age<=w+duration;
            Vector3 direction=utility.Direction,across=Vector3.Cross(direction,Mathf.Abs(direction.y)>.95f?Vector3.forward:Vector3.up).normalized,up=Vector3.Cross(across,direction);
            for(int i=0;i<4;i++)
            {
                Vector3 tip=tips[i]?tips[i].position:transform.position+across*(i%2==0?-1:1);
                trails[i*2].transform.parent.position=tip;trails[i*2].emitting=trails[i*2+1].emitting=running;
                Vector3 source=model?model.Mobile.TransformPoint(new Vector3(i%2==0?-.3f:.3f,1.2f,0)):transform.position;
                for(int p=0;p<9;p++){float t=p/8f;wingRails[i].SetPosition(p,Vector3.Lerp(source,tip,t)+up*(Mathf.Sin(t*Mathf.PI)*.1f*gather));}
                BastionUtilityFx.Color(wingRails[i],i<2?BastionModel.Blue:BastionModel.White,gather*(running?.8f:.45f));
            }
            for(int k=0;k<wake.Length;k++)
            {
                float a=k*Mathf.PI*2/wake.Length;var radial=across*Mathf.Cos(a)+up*Mathf.Sin(a);
                Vector3 center=transform.position+direction*1.6f+radial*(1.0f+(k%3)*.38f);
                for(int i=0;i<12;i++){float t=i/11f;wake[k].SetPosition(i,center-direction*(t*(5.5f+k*.17f))+radial*(t*t*.35f));}
                BastionUtilityFx.Color(wake[k],new Color(.65f,.87f,1,.65f),running?1:0);
            }
        }
        private void OnDisable(){sound?.Dispose();sound=null;if(wingRoot)wingRoot.SetActive(false);}
        private void OnDestroy(){sound?.Dispose();}
    }
}
