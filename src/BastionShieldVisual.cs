using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace IvoryBastion
{
    public sealed class BastionShieldVisual:MonoBehaviour
    {
        private BastionController owner;
        private BastionShieldGeometry geometry;
        private GameObject field;
        private Mesh mesh;
        private Material material;
        private Vector3[] vertices;
        private Color[] colors;
        private readonly float[] hitTimes={-99,-99,-99,-99},strengths=new float[4];
        private readonly float[][] distances=new float[4][];
        private readonly BastionShieldGeometry.RippleFrame[] ripples=new BastionShieldGeometry.RippleFrame[4];
        private int nextHit;
        private float visibility;
        private float transformStarted=-99,transformDuration,transformFrom,transformTo;

        private float DeploymentBlend
        {
            get
            {
                float age=Time.time-transformStarted;
                // The model holds its deployment pose until the server replies.
                // Its surrounding shield must hold the same radius on clients.
                if(transformDuration>0&&(age<transformDuration+.3f||
                    owner&&owner.Deploying&&owner.TransformTargetSiege==(transformTo>0)))
                    return Mathf.Lerp(transformFrom,transformTo,Mathf.SmoothStep(0,1,age/transformDuration));
                return owner&&owner.Sieged?1:0;
            }
        }
        internal float CurrentRadius=>BastionShieldGeometry.FormRadius(DeploymentBlend);
        internal Vector3 Center=>owner.Body.corePosition+Vector3.up*
            ((Mathf.Lerp(BastionVisualRules.Height(false),BastionVisualRules.Height(true),DeploymentBlend)-BastionVisualRules.Height(owner.Sieged))*.5f*owner.Body.currentScale);

        internal void Transforming(float duration)
        {
            if(!owner)owner=GetComponent<BastionController>();
            transformFrom=DeploymentBlend;transformTo=duration>=0?1:0;
            transformStarted=Time.time;transformDuration=Mathf.Abs(duration);
        }

        private void Start(){owner=GetComponent<BastionController>();}

        private void Build()
        {
            geometry=BastionShieldGeometry.Build();vertices=(Vector3[])geometry.Normals.Clone();colors=new Color[vertices.Length];
            for(int i=0;i<distances.Length;i++)distances[i]=new float[vertices.Length];
            mesh=new Mesh{name="Seraph continuous shield surface",vertices=vertices,normals=geometry.Normals,triangles=geometry.Triangles,colors=colors};
            mesh.MarkDynamic();mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2.15f);
            material=new Material(BastionFx.LineMaterial){name="Seraph continuous blue shield",renderQueue=3090};
            if(material.HasProperty("_Cull"))material.SetFloat("_Cull",(float)CullMode.Back);
            field=new GameObject("Seraph innate blue shield");
            field.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=field.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }

        internal void Impact(Vector3 direction,float strength)
        {
            if(Application.isBatchMode)return;
            if(!owner)owner=GetComponent<BastionController>();
            if(!owner||!owner.Alive)return;
            if(!field)Build();
            direction=direction.sqrMagnitude>.001f?direction.normalized:Vector3.forward;
            int slot=nextHit++%hitTimes.Length;hitTimes[slot]=Time.time;strengths[slot]=Mathf.Clamp(strength,.4f,1);
            for(int i=0;i<vertices.Length;i++)distances[slot][i]=Mathf.Acos(Mathf.Clamp(Vector3.Dot(geometry.Normals[i],direction),-1,1));
            visibility=Mathf.Max(visibility,.65f);
        }

        private void LateUpdate()
        {
            if(Application.isBatchMode||!owner)return;
            bool active=owner.AbilityShieldActive;
            // A shield-breaking blow leaves one short dissolving ripple, then vanishes.
            bool breaking=!active&&owner.Alive&&Time.time-hitTimes[(nextHit+3)%4]<.24f;
            float target=active?1:breaking?.35f:0;
            visibility=Mathf.MoveTowards(visibility,target,Time.deltaTime*(active?5:7));
            if(visibility<=0||!owner.Alive){if(field)field.SetActive(false);return;}
            if(!field)Build();field.SetActive(true);
            field.transform.position=Center;
            field.transform.rotation=Quaternion.identity;
            field.transform.localScale=Vector3.one*(CurrentRadius*owner.Body.currentScale*Mathf.Lerp(.96f,1,visibility));
            var camera=Camera.main;
            var view=(camera?camera.transform.position-field.transform.position:-owner.Body.inputBank.aimDirection).normalized;
            float now=Time.time;
            for(int j=0;j<hitTimes.Length;j++)ripples[j]=new BastionShieldGeometry.RippleFrame(now-hitTimes[j],strengths[j]);
            for(int i=0;i<vertices.Length;i++)
            {
                float ripple=0;
                for(int j=0;j<hitTimes.Length;j++)ripple+=ripples[j].At(distances[j][i]);
                ripple=Mathf.Min(1.25f,ripple);
                colors[i]=BastionShieldGeometry.Tint(geometry.Normals[i],view,now,ripple,visibility);
                vertices[i]=geometry.Normals[i]*(1+.018f*ripple);
            }
            mesh.vertices=vertices;mesh.colors=colors;
        }

        private void OnDestroy(){if(field)Destroy(field);if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}
