using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace IvoryBastion
{
    [DefaultExecutionOrder(12100)]
    public sealed class BastionThrusterVisual:MonoBehaviour
    {
        private BastionController controller;private BastionAnimation boundModel;private Transform[] ports;
        private BastionThrusterPlume[] plumes;private TrailRenderer[] airflow;
        private float strength,ignited=-99,airStrength;private Vector3 lastPosition;
        private readonly Gradient gradient=new Gradient();
        private readonly GradientColorKey[] colours={new GradientColorKey(new Color(.83f,.95f,1),0),new GradientColorKey(new Color(.14f,.53f,1),1)};
        private readonly GradientAlphaKey[] alpha=new GradientAlphaKey[4];private float lastAlpha=-1;
        private void Start(){controller=GetComponent<BastionController>();lastPosition=transform.position;}
        internal void Ignite(){ignited=Time.time;}
        internal static Vector3 PetalTip(Transform port)
        {var tip=port.Find("tip_"+port.name);if(tip)return tip.position;float s=port.name.EndsWith("l")?-1:1;bool upper=port.name.StartsWith("wing_");return port.TransformPoint(new Vector3(s*.32f,upper?.56f:-.275f,-.12f));}
        private void Bind()
        {
            if(plumes!=null)foreach(var plume in plumes)plume?.Dispose();
            if(airflow!=null)foreach(var r in airflow)if(r)Destroy(r.gameObject);
            var model=controller.Model;boundModel=model;if(!model){ports=null;return;}
            ports=model.MobileBones.Where(b=>b.name.StartsWith("wing_")||b.name.StartsWith("vane_")).ToArray();
            plumes=new BastionThrusterPlume[ports.Length];airflow=new TrailRenderer[ports.Length];lastAlpha=-1;
            for(int i=0;i<ports.Length;i++)
            {
                plumes[i]=BastionThrusterPlume.Create(transform,"Seraph wing plasma plume "+i,i+31);
                var go=new GameObject("Seraph wingtip airflow");go.transform.SetParent(transform,false);
                var trail=go.AddComponent<TrailRenderer>();airflow[i]=trail;trail.sharedMaterial=BastionFx.LineMaterial;trail.time=.18f;trail.minVertexDistance=.06f;trail.emitting=false;trail.autodestruct=false;
                trail.numCapVertices=2;trail.numCornerVertices=2;trail.widthCurve=new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.15f,1),new Keyframe(1,0));
                trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trail.lightProbeUsage=LightProbeUsage.Off;trail.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
        }
        private void LateUpdate()
        {
            if(Application.isBatchMode||!controller||!controller.Body)return;
            if(ports==null||boundModel!=controller.Model||ports.Any(p=>!p)){Bind();if(ports==null)return;}
            var body=controller.Body;var velocity=body.characterMotor?body.characterMotor.velocity:Vector3.zero;
            float speed=velocity.magnitude,dt=Mathf.Min(.05f,Time.deltaTime);
            bool active=!controller.Sieged&&controller.Alive&&controller.Model&&controller.Model.Mobile.gameObject.activeSelf;
            float ignition=BastionEffectGeometry.Ignition(Time.time-ignited);
            strength=Mathf.Lerp(strength,controller.Boosting?1:.16f+Mathf.Min(speed,20)*.012f,1-Mathf.Exp(-12*dt));
            // Every nozzle points along the same actual travel axis. There is no
            // interpolation from independently rotating petals and no curved fan.
            Vector3 exhaust=speed>2?-velocity/speed:controller.Boosting&&body.inputBank?-body.inputBank.aimDirection:Vector3.down;
            if(exhaust.sqrMagnitude<.001f)exhaust=Vector3.down;exhaust.Normalize();
            float length=(.55f+Mathf.Min(speed,45)*.032f)*strength*(1+ignition*.15f);
            // Wingtip airflow belongs to held boost, not ordinary flight or Blue Shift.
            bool boosting=active&&controller.Boosting;
            float desiredAir=boosting?Mathf.InverseLerp(5,28,speed):0;airStrength=boosting?Mathf.MoveTowards(airStrength,desiredAir,dt*5):0;
            bool discontinuity=(transform.position-lastPosition).sqrMagnitude>Mathf.Pow(Mathf.Max(6,speed*dt*3),2);lastPosition=transform.position;
            if(Mathf.Abs(airStrength-lastAlpha)>.015f)
            {
                lastAlpha=airStrength;alpha[0]=new GradientAlphaKey(0,0);alpha[1]=new GradientAlphaKey(.48f*airStrength,.14f);alpha[2]=new GradientAlphaKey(.16f*airStrength,.70f);alpha[3]=new GradientAlphaKey(0,1);gradient.SetKeys(colours,alpha);
                foreach(var trail in airflow)trail.colorGradient=gradient;
            }
            for(int i=0;i<ports.Length;i++)
            {
                var origin=ports[i].TransformPoint(new Vector3(0,0,-.045f));
                plumes[i].Update(origin,exhaust,length,(.09f+.13f*strength)*(1+ignition*.20f),strength,ignition,active);
                var air=airflow[i];air.transform.position=PetalTip(ports[i]);air.widthMultiplier=.015f+.025f*airStrength;
                air.time=Mathf.Clamp((1.4f+Mathf.Min(speed,40)*.055f)/Mathf.Max(speed,1),.05f,.25f);
                bool emit=boosting&&speed>5&&!discontinuity;
                air.enabled=boosting&&!discontinuity;
                if(!boosting||discontinuity)air.Clear();else if(emit&&!air.emitting)air.Clear();air.emitting=emit;
            }
        }
        private void OnDisable()
        {
            if(plumes!=null)foreach(var plume in plumes)plume?.Update(Vector3.zero,Vector3.down,0,0,0,0,false);
            if(airflow!=null)foreach(var air in airflow)if(air){air.emitting=false;air.Clear();air.enabled=false;}
        }
        private void OnDestroy()
        {
            if(plumes!=null)foreach(var plume in plumes)plume?.Dispose();
            if(airflow!=null)foreach(var r in airflow)if(r)Destroy(r.gameObject);
        }
    }
}
