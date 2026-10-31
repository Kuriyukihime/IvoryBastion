using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace IvoryBastion
{
    public sealed class BastionAegisVisual:MonoBehaviour
    {
        private BastionController owner;
        private BastionShieldVisual innerShield;
        private GameObject field;
        private Mesh mesh;
        private Material material;
        private BastionAegisHexGeometry geometry;
        private Color[] colors;
        private float visibility,activated,impact;
        private readonly float[] hitTimes={-99,-99,-99};
        private readonly float[][] hitAngles=new float[3][];
        private readonly BastionShieldGeometry.RippleFrame[] ripples=new BastionShieldGeometry.RippleFrame[3];
        private int nextHit;
        private bool wasActive;

        private void Start(){owner=GetComponent<BastionController>();innerShield=GetComponent<BastionShieldVisual>();}
        private void OnEnable(){GlobalEventManager.onClientDamageNotified+=OnDamage;}
        private void OnDisable(){GlobalEventManager.onClientDamageNotified-=OnDamage;}
        private void OnDamage(DamageDealtMessage message)
        {
            if(Application.isBatchMode||message.victim!=gameObject||message.damage<=0||
                !owner||!owner.Alive||owner.AegisBarrier.Charge>0||!owner.Sieged||owner.Bank.DefenseTime<=0)return;
            if(!field)Build();
            Vector3 origin=innerShield?innerShield.Center:owner.Body.corePosition;
            Vector3 direction=message.position-origin;
            if(direction.sqrMagnitude<.01f&&message.attacker)direction=message.attacker.transform.position-origin;
            Impact(direction);
        }
        internal void Impact(Vector3 direction)
        {
            if(Application.isBatchMode)return;
            if(!owner)owner=GetComponent<BastionController>();
            if(!owner||!owner.Alive)return;
            if(!field)Build();
            direction=direction.sqrMagnitude>.001f?direction.normalized:Vector3.forward;
            int slot=nextHit++%hitTimes.Length;hitTimes[slot]=Time.time;
            for(int i=0;i<colors.Length;i++)hitAngles[slot][i]=Mathf.Acos(Mathf.Clamp(Vector3.Dot(geometry.Normals[i],direction),-1,1));
        }
        internal void Trigger(){activated=Time.time;impact=.65f;}

        private void Build()
        {
            geometry=BastionAegisHexGeometry.Build();colors=new Color[geometry.Vertices.Length];
            for(int i=0;i<hitAngles.Length;i++)hitAngles[i]=new float[colors.Length];
            mesh=new Mesh{name="Seraph Aegis hexagonal outer shell",vertices=geometry.Vertices,normals=geometry.Normals,triangles=geometry.Triangles,colors=colors};
            mesh.MarkDynamic();mesh.RecalculateBounds();
            material=new Material(BastionFx.LineMaterial){name="Seraph yellow Aegis",renderQueue=3100};
            if(material.HasProperty("_Cull"))material.SetFloat("_Cull",(float)CullMode.Back);
            field=new GameObject("Seraph yellow hexagonal Aegis barrier");
            field.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=field.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }

        private void LateUpdate()
        {
            if(Application.isBatchMode||!owner)return;
            // Aegis owns this layer independently: shield depletion never removes it.
            bool active=owner.Alive&&(owner.AegisBarrier.Charge>0||owner.Sieged&&owner.Bank.DefenseTime>0);
            if(active&&!wasActive)Trigger();wasActive=active;
            float targetVisibility=active?1:0;
            visibility=Mathf.MoveTowards(visibility,targetVisibility,Time.deltaTime*(active?4.5f:7));
            if(visibility<=0||!owner.Alive){if(field)field.SetActive(false);return;}
            if(!field)Build();field.SetActive(true);
            impact=Mathf.MoveTowards(impact,0,Time.deltaTime*4);
            field.transform.position=innerShield?innerShield.Center:owner.Body.corePosition;
            field.transform.rotation=Quaternion.identity;
            float radius=(innerShield?innerShield.CurrentRadius:BastionShieldGeometry.SiegeRadius)+BastionShieldGeometry.FortificationGap;
            field.transform.localScale=Vector3.one*(radius*owner.Body.currentScale);
            var camera=Camera.main;
            var view=(camera?camera.transform.position-field.transform.position:-owner.Body.inputBank.aimDirection).normalized;
            float age=Time.time-activated;
            float scan=BastionAegisHexGeometry.Scan(age);
            for(int j=0;j<hitTimes.Length;j++)ripples[j]=new BastionShieldGeometry.RippleFrame(Time.time-hitTimes[j],1);
            for(int i=0;i<colors.Length;i++)
            {
                float assemble=Mathf.SmoothStep(0,1,(age-(geometry.Normals[i].y+1)*.06f)*6);
                float pressure=impact*.35f;
                for(int j=0;j<hitTimes.Length;j++)pressure+=ripples[j].At(hitAngles[j][i]);
                colors[i]=BastionAegisHexGeometry.TintWithScan(geometry.Kinds[i],geometry.Normals[i],view,scan,Mathf.Clamp01(pressure),visibility*assemble);
            }
            mesh.colors=colors;
        }

        private void OnDestroy(){if(mesh)Destroy(mesh);if(field)Destroy(field);if(material)Destroy(material);}
    }
}
