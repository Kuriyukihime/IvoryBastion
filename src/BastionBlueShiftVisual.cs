using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;
namespace IvoryBastion
{
    // Blue Shift has its own reactor ignition and articulated energy feathers.
    // It follows the actual animated rig and never locks steering or attacks.
    [DefaultExecutionOrder(12200)]
    public sealed class BastionBlueShiftVisual:MonoBehaviour
    {
        private BastionController owner;private Transform[] petals;private GameObject root;
        private LineRenderer[] links,rings;private Mesh featherMesh;private float age;
        private const int FeatherVertices=(BastionEffectGeometry.FeatherSections+1)*3;
        private readonly Vector3[] vertices=new Vector3[FeatherVertices*8];private readonly Color[] colours=new Color[FeatherVertices*8];
        private static Material featherMaterial;
        internal static void Create(CharacterBody body)
        {
            if(Application.isBatchMode||!body)return;
            var effect=body.GetComponent<BastionBlueShiftVisual>();if(!effect)effect=body.gameObject.AddComponent<BastionBlueShiftVisual>();
            effect.owner=body.GetComponent<BastionController>();effect.age=0;
        }
        private bool Build()
        {
            if(!owner||!owner.Model)return false;
            petals=owner.Model.MobileBones.Where(b=>b.name.StartsWith("wing_")||b.name.StartsWith("vane_")).ToArray();if(petals.Length!=4)return false;
            root=new GameObject("Seraph Blue Shift reactor ignition");links=new LineRenderer[4];rings=new LineRenderer[5];
            for(int i=0;i<4;i++)
            {
                links[i]=BastionFx.Line(root.transform,3,.025f,BastionModel.Blue);
                links[i].widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.6f,1),new Keyframe(1,.25f));
            }
            for(int i=0;i<5;i++)rings[i]=BastionFx.Line(root.transform,i==0?7:17,.035f,i==0?BastionModel.White:BastionModel.Blue);
            featherMesh=new Mesh{name="Seraph power release feathers"};featherMesh.MarkDynamic();featherMesh.vertices=vertices;
            var triangles=new int[8*BastionEffectGeometry.FeatherSections*12];var uv=new Vector2[vertices.Length];int index=0;
            for(int feather=0;feather<8;feather++)for(int section=0;section<=BastionEffectGeometry.FeatherSections;section++)for(int side=0;side<3;side++)
            {
                int v=feather*FeatherVertices+section*3+side;uv[v]=new Vector2(section/(float)BastionEffectGeometry.FeatherSections,side*.5f);
                if(section==BastionEffectGeometry.FeatherSections||side==2)continue;
                triangles[index++]=v;triangles[index++]=v+3;triangles[index++]=v+1;
                triangles[index++]=v+1;triangles[index++]=v+3;triangles[index++]=v+4;
            }
            featherMesh.uv=uv;featherMesh.triangles=triangles;
            if(!featherMaterial){featherMaterial=new Material(BastionFx.LineMaterial){name="Seraph feathered power ribbons",renderQueue=3101};BastionEnergySurface.Apply(featherMaterial);}
            root.AddComponent<MeshFilter>().sharedMesh=featherMesh;var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=featherMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return true;
        }
        private void LateUpdate()
        {
            if(!owner||!owner.Alive||owner.Sieged||age>=1.3f){Destroy(this);return;}
            if(!root&&!Build()){Destroy(this);return;}
            var model=owner.Model;if(!model||petals.Any(p=>!p)){Destroy(this);return;}
            age+=Time.deltaTime;float charge=BastionChoreography.PowerCharge(age),release=BastionChoreography.PowerRelease(age);
            var mobile=model.Mobile;Vector3 core=mobile.TransformPoint(new Vector3(0,1.12f,-.23f));Vector3 right=mobile.right,up=mobile.up,normal=-mobile.forward;
            for(int i=0;i<4;i++)
            {
                Vector3 origin=petals[i].TransformPoint(new Vector3(0,0,-.04f)),tip=BastionThrusterVisual.PetalTip(petals[i])+normal*.025f;
                Vector3 axis=(tip-origin).normalized,side=Vector3.Cross(normal,axis);
                if(side.sqrMagnitude<.001f)side=Vector3.Cross(mobile.up,axis);
                side.Normalize();
                float flare=BastionChoreography.PowerRelease(age-i*.025f);
                var blue=BastionModel.Blue;blue.a=charge*.85f+flare*.55f;var white=BastionModel.White;white.a=charge*.35f+flare*.90f;
                links[i].startColor=blue;links[i].endColor=white;links[i].widthMultiplier=.022f+charge*.035f+flare*.026f;
                // Charge travels along each articulated feather. Avoid a bright
                // spiderweb connecting the limbs or coating the pilot's silhouette.
                float head=Mathf.Clamp01(charge+release);
                links[i].SetPosition(0,origin);links[i].SetPosition(1,Vector3.Lerp(origin,tip,head*.5f));
                links[i].SetPosition(2,Vector3.Lerp(origin,tip,head));
                for(int layer=0;layer<2;layer++)for(int section=0;section<=BastionEffectGeometry.FeatherSections;section++)for(int edge=0;edge<3;edge++)
                {
                    int v=(i*2+layer)*FeatherVertices+section*3+edge;float along=section/(float)BastionEffectGeometry.FeatherSections;
                    vertices[v]=BastionEffectGeometry.FeatherRibbon(origin,tip,side,flare,layer,along,edge-1)+normal*layer*.005f;
                    float opacity=(float)System.Math.Pow(System.Math.Max(0,System.Math.Sin(along*System.Math.PI)),.4)*(edge==1?.88f:.46f);
                    colours[v]=layer==0?new Color(.08f,.38f,1,flare*opacity):new Color(.68f,.94f,1,flare*opacity);
                }
            }
            featherMesh.vertices=vertices;featherMesh.colors=colours;featherMesh.RecalculateBounds();
            for(int k=0;k<rings.Length;k++)
            {
                float radius=k==0?.14f+charge*.12f+release*.22f:.24f+BastionChoreography.Stage(age,.26f,1.15f)*.92f;
                var colour=k==0?BastionModel.White:BastionModel.Blue;colour.a=k==0?(charge*.90f+release*.48f):release*.65f;
                rings[k].startColor=rings[k].endColor=colour;rings[k].widthMultiplier=k==0?.035f:.040f+release*.020f;
                int count=rings[k].positionCount;
                for(int j=0;j<count;j++)
                {
                    float a=k==0?j/6f*Mathf.PI*2+Mathf.PI/6:(k-1)*Mathf.PI*.5f+(j/16f-.5f)*1.12f;
                    rings[k].SetPosition(j,core+normal*(.08f+release*.05f)+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius);
                }
            }
        }
        private void OnDestroy(){if(root)Destroy(root);if(featherMesh)Destroy(featherMesh);}
    }
}
