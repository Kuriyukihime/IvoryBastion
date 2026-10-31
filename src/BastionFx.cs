using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;
namespace IvoryBastion
{
    public enum BastionFxKind:byte {Pistol,Laser,Missile,HeavyMissile,Impact,HeavyImpact,Protocol,Dash,Transform,Redline,Saturation,Upgrade,Drone,Guardian,Repair,Intercept,UnitShot,Boost,MissileEnd,DroneShot,SiegeWarhead,SiegeWarheadImpact,SiegeWarheadEnd,ShieldHit,LaserImpact,AegisHit,ShieldBreak,ShieldRecharge,WingburstMissile,HaloImpact,WingburstMicroImpact,WingburstMicroEnd,MineLaunch,MineState,MineExplosion,MineEnd,MineChain,MineChainExplosion,MineArming}
    internal static class BastionFx
    {
        private static Material lineMaterial;private static float nextMicroSound,nextChainPrime,nextChainBurst,nextMineArm,nextMineBlast;
        internal static Material LineMaterial
        {get{if(lineMaterial)return lineMaterial;lineMaterial=new Material(BastionContent.Load<Material>("RoR2/Base/Common/VFX/matTracerBrightTransparent.mat")){name="IvoryEnergy",renderQueue=3100};
            foreach(var p in new[]{"_MainTex","_RemapTex","_Cloud1Tex","_Cloud2Tex"})if(lineMaterial.HasProperty(p))lineMaterial.SetTexture(p,Texture2D.whiteTexture);
            foreach(var p in new[]{"_Color","_TintColor"})if(lineMaterial.HasProperty(p))lineMaterial.SetColor(p,Color.white);
            foreach(var p in new[]{"_AlphaBoost","_Boost","_VertexAlphaOn","_VertexColorOn","_DisableRemapOn"})if(lineMaterial.HasProperty(p))lineMaterial.SetFloat(p,1);
            lineMaterial.EnableKeyword("VERTEXALPHA");lineMaterial.EnableKeyword("VERTEXCOLOR");lineMaterial.EnableKeyword("DISABLEREMAP");
            if(lineMaterial.HasProperty("_SrcBlend"))lineMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);if(lineMaterial.HasProperty("_DstBlend"))lineMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);if(lineMaterial.HasProperty("_ZWrite"))lineMaterial.SetFloat("_ZWrite",0);if(lineMaterial.HasProperty("_Cull"))lineMaterial.SetFloat("_Cull",0);return lineMaterial;}}
        internal static LineRenderer Line(Transform parent,int count,float width,Color color,bool world=true)
        {var go=new GameObject("Ivory energy ribbon");go.transform.SetParent(parent,false);var l=go.AddComponent<LineRenderer>();l.sharedMaterial=LineMaterial;l.positionCount=count;l.widthMultiplier=width;l.useWorldSpace=world;l.startColor=l.endColor=color;l.numCapVertices=3;l.shadowCastingMode=ShadowCastingMode.Off;l.receiveShadows=false;return l;}
        internal static GameObject Part(Transform parent,PrimitiveType type,Vector3 position,Vector3 size,Material material)
        {var g=GameObject.CreatePrimitive(type);g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=size;var collider=g.GetComponent<Collider>();if(collider){collider.enabled=false;Object.Destroy(collider);}g.GetComponent<Renderer>().sharedMaterial=material;return g;}
        internal static void Pulse(Vector3 at,float radius,float duration,Color color,bool dome=false,Transform follow=null)
        {var g=new GameObject("Ivory pulse");g.transform.position=at;if(follow)g.transform.SetParent(follow,true);var p=g.AddComponent<BastionPulse>();p.Radius=radius;p.Duration=duration;p.Color=color;p.Dome=dome;}
        internal static void Beam(Vector3 a,Vector3 b,float width,float duration,Color color)
        {var g=new GameObject("Ivory beam");var l=Line(g.transform,2,width,color);l.SetPosition(0,a);l.SetPosition(1,b);var fade=g.AddComponent<BastionFade>();fade.Line=l;fade.Duration=duration;fade.Color=color;}
        internal static void Play(BastionEffect m)
        {
            var obj=Util.FindNetworkObject(m.Owner);var body=obj?obj.GetComponent<CharacterBody>():null;var targetObj=Util.FindNetworkObject(m.Target);var target=targetObj?targetObj.GetComponent<CharacterBody>():null;
            if(body)body.GetComponent<BastionController>()?.Model?.Cue(m.Kind,m.Value,m.A,m.Id);
            string sound=null;
            switch(m.Kind)
            {
                case BastionFxKind.ShieldHit:
                    if(body){body.GetComponent<BastionShieldVisual>()?.Impact(m.A,m.Value);if(body.hasEffectiveAuthority)BastionAudio.StopShieldRecharge();}
                    break;
                case BastionFxKind.AegisHit:if(body)body.GetComponent<BastionAegisVisual>()?.Impact(m.A);break;
                case BastionFxKind.ShieldBreak:
                    if(body&&body.hasEffectiveAuthority){BastionAudio.StopShieldRecharge();BastionAudio.Cue("shieldbreak",body.corePosition,.48f);}
                    break;
                case BastionFxKind.ShieldRecharge:
                    if(body&&body.hasEffectiveAuthority)BastionAudio.BeginShieldRecharge(body.corePosition);
                    break;
                case BastionFxKind.Pistol: Beam(m.A,m.B,.055f,.12f,BastionModel.Blue);Beam(m.A,m.B,.018f,.08f,BastionModel.White);BastionAccentFx.Sparks(m.A,m.B-m.A,false);BastionAccentFx.Sparks(m.B,m.A-m.B,true);sound="pistol";break;
                case BastionFxKind.UnitShot:BastionUnitVisual.Cue(m.A);BastionAccentFx.Sparks(m.A,m.B-m.A,false,.65f);Beam(m.A,m.B,.035f,.12f,BastionModel.Blue);sound="pistol";break;
                case BastionFxKind.Laser:BastionLanceVisual.Create(m.A,m.B);sound="laser";break;
                case BastionFxKind.WingburstMissile:BastionMicroMissileVisual.Create(m,target);break;
                case BastionFxKind.WingburstMicroEnd:BastionMicroMissileVisual.End(m.Owner.Value,m.Id);break;
                case BastionFxKind.WingburstMicroImpact:
                    BastionMicroMissileVisual.End(m.Owner.Value,m.Id);BastionFireExplosion.Spawn(m.A,0,m.Value);BastionAccentFx.Sparks(m.A,Vector3.up,true,.7f);
                    if(Time.unscaledTime>=nextMicroSound){nextMicroSound=Time.unscaledTime+.07f;BastionAudio.Cue("impact",m.A,.28f);}break;
                case BastionFxKind.MineState:BastionSpiderVisual.Receive(m,body,target);break;
                case BastionFxKind.MineEnd:BastionSpiderVisual.End(m.Owner.Value,m.Id);break;
                case BastionFxKind.MineLaunch:if(Time.unscaledTime>=nextMicroSound){nextMicroSound=Time.unscaledTime+.09f;BastionAudio.Cue("missile",m.A,.30f);}break;
                case BastionFxKind.MineArming:
                    if(Time.unscaledTime>=nextMineArm){nextMineArm=Time.unscaledTime+.1f;BastionAudio.Cue("mine_arm",m.A,.40f);}break;
                case BastionFxKind.MineExplosion:
                    if(!BastionSpiderVisual.End(m.Owner.Value,m.Id))break;
                    BastionFireExplosion.Spawn(m.A,0,m.Value);BastionAccentFx.Sparks(m.A,Vector3.up,true,.9f);
                    if(Time.unscaledTime>=nextMineBlast){nextMineBlast=Time.unscaledTime+.08f;BastionAudio.Cue("mine_blast",m.A,.80f);}break;
                case BastionFxKind.MineChain:
                    BastionMineChainVisual.Link(m.A,m.B);
                    if(Time.unscaledTime>=nextChainPrime){nextChainPrime=Time.unscaledTime+.12f;BastionAudio.Cue("mine_chain_prime",m.B,.32f);}break;
                case BastionFxKind.MineChainExplosion:
                    if(!BastionSpiderVisual.End(m.Owner.Value,m.Id))break;
                    BastionMineChainVisual.ChargedDetonation(m.A,m.Value);
                    if(body&&body.hasEffectiveAuthority)body.GetComponent<BastionUtility>()?.ChainFeedback();
                    if(Time.unscaledTime>=nextChainBurst){nextChainBurst=Time.unscaledTime+.09f;BastionAudio.Cue("mine_chain_burst",m.A,.60f);}break;
                case BastionFxKind.HaloImpact:BastionFireExplosion.Spawn(m.A,2,m.Value);BastionAudio.Cue("siege_impact",m.A,.68f);break;
                case BastionFxKind.LaserImpact:BastionFireExplosion.Spawn(m.A,1,BastionBank.LaserBlastRadius);BastionAudio.Cue("impact",m.A,.8f);break;
                case BastionFxKind.Missile:case BastionFxKind.HeavyMissile:
                    if(BastionMissileVisual.Create(m,target))sound=m.Kind==BastionFxKind.HeavyMissile?"heavy":"missile";break;
                case BastionFxKind.Impact:case BastionFxKind.HeavyImpact:
                    BastionMissileVisual.End(m.Owner.Value,m.Id);bool heavy=m.Kind==BastionFxKind.HeavyImpact;BastionMissileFx.Explosion(m.A,heavy);sound=heavy?"explosion":"impact";break;
                case BastionFxKind.Protocol:if(m.Value>.5f)BastionVoice.Request(body,"voice_aegis");if(body)body.GetComponent<BastionAegisVisual>()?.Trigger();sound="protocol";break;
                case BastionFxKind.Dash:if(body){var g=new GameObject("Slipstream wake");g.transform.position=m.A;var d=g.AddComponent<BastionWake>();d.Follow=body.transform;}sound="dash";break;
                case BastionFxKind.Boost:if(body)body.GetComponent<BastionThrusterVisual>()?.Ignite();sound="dash";break;
                case BastionFxKind.Transform:if(body)body.GetComponent<BastionShieldVisual>()?.Transforming(m.Value);BastionAccentFx.Create(m.Kind,m.A,body,m.Value);BastionAudio.Transformation(m.A,m.Value);break;
                case BastionFxKind.Redline:BastionVoice.Request(body,"voice_mobile");BastionBlueShiftVisual.Create(body);sound="blueshift";break;
                case BastionFxKind.Saturation:BastionVoice.Request(body,"voice_siege");BastionAccentFx.Create(m.Kind,m.A,body);sound="barrage";break;
                case BastionFxKind.Upgrade:BastionAccentFx.Create(m.Kind,m.A,body);sound="upgrade";break;
                case BastionFxKind.MissileEnd:BastionMissileVisual.End(m.Owner.Value,m.Id);break;
                case BastionFxKind.SiegeWarhead:BastionWarheadVisual.Create(m);BastionAudio.Cue("siege_launch",m.A,.7f);break;
                case BastionFxKind.SiegeWarheadImpact:BastionWarheadVisual.End(m.Owner.Value,m.Id);BastionMissileFx.SiegeExplosion(m.A);BastionAudio.Cue("siege_impact",m.A,.8f);break;
                case BastionFxKind.SiegeWarheadEnd:BastionWarheadVisual.End(m.Owner.Value,m.Id);break;
                case BastionFxKind.Guardian:BastionUnitVisual.Cue(m.A);Pulse(m.A,BastionBalance.Get(BastionSetting.BulwarkPullRadius),.65f,BastionModel.Blue);sound="guardian";break;
                case BastionFxKind.Repair:BastionUnitVisual.Cue(m.A);Beam(m.A,m.B,.055f,.4f,BastionModel.Blue);break;
                case BastionFxKind.Intercept:BastionUnitVisual.Cue(m.A);Beam(m.A,m.B,.045f,.15f,BastionModel.White);BastionAccentFx.Sparks(m.B,m.A-m.B,true,.7f);break;
            }
            if(sound!=null)BastionAudio.Cue(sound,m.A);
        }
    }
    public sealed class BastionFade:MonoBehaviour
    {internal LineRenderer Line;internal float Duration;internal Color Color;private float age;private void Update(){age+=Time.deltaTime;if(age>=Duration){Destroy(gameObject);return;}var c=Color;c.a*=1-age/Duration;Line.startColor=Line.endColor=c;}}
    public sealed class BastionPulse:MonoBehaviour
    {
        internal float Radius,Duration;internal Color Color;internal bool Dome;private float age;private LineRenderer[] lines;
        private void Start(){lines=new LineRenderer[Dome?7:1];for(int i=0;i<lines.Length;i++)lines[i]=BastionFx.Line(transform,65,Dome?.025f:.065f,Color,false);}
        private void Update(){age+=Time.deltaTime;if(age>=Duration){Destroy(gameObject);return;}float f=age/Duration,r=Radius*(Dome?Mathf.Min(1,age*5):Mathf.Sin(f*Mathf.PI*.5f));var c=Color;c.a*=Mathf.Min(1,(Duration-age)*4)*(Dome?.52f:1-f);
            for(int j=0;j<lines.Length;j++){lines[j].startColor=lines[j].endColor=c;for(int i=0;i<65;i++){float a=i/64f*Mathf.PI*2;Vector3 p;if(!Dome)p=new Vector3(Mathf.Cos(a),.03f,Mathf.Sin(a))*r;else if(j<3){float y=(j-1)*.48f;float rr=Mathf.Sqrt(1-y*y);p=new Vector3(Mathf.Cos(a)*rr,y,Mathf.Sin(a)*rr)*r;}else{float yaw=(j-3)*Mathf.PI/4;p=new Vector3(Mathf.Cos(a)*Mathf.Cos(yaw),Mathf.Sin(a),Mathf.Cos(a)*Mathf.Sin(yaw))*r;}lines[j].SetPosition(i,p);}}
        }
    }
    public sealed class BastionWake:MonoBehaviour
    {internal Transform Follow;private float age;private LineRenderer line;private readonly List<Vector3> points=new List<Vector3>();private void Start(){line=BastionFx.Line(transform,2,.22f,BastionModel.Blue);}private void Update(){age+=Time.deltaTime;if(age>.6f||!Follow){Destroy(gameObject);return;}if(age<.3f)points.Add(Follow.position);line.positionCount=points.Count;line.SetPositions(points.ToArray());var color=BastionModel.Blue;color.a=1-age/.6f;line.startColor=line.endColor=color;}}
}
