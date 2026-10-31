using System.Collections.Generic;
using UnityEngine;
namespace IvoryBastion
{
    // A standalone charged electrical detonation: tangled white-hot plasma
    // and irregular volumetric forks, not the ordinary blue fireball/ring.
    // Host damage and terminal-event deduplication remain outside this renderer.
    public sealed class BastionMineChainVisual:MonoBehaviour
    {
        private static readonly Queue<BastionMineChainVisual>[] pools={new Queue<BastionMineChainVisual>(),new Queue<BastionMineChainVisual>()};
        private static readonly int[] active=new int[2];private static readonly int[] limits={24,16};
        private readonly LineRenderer[] lines=new LineRenderer[BastionMineChainGeometry.ArcCount+BastionMineChainGeometry.ForkCount+BastionMineChainGeometry.CoreCount];
        private int style;private float age,radius;private Vector3 from,to;private bool counted;
        internal static void Link(Vector3 from,Vector3 to)=>Create(0,from,to,1);
        internal static void Burst(Vector3 at,float radius)=>ChargedDetonation(at,radius);
        internal static void ChargedDetonation(Vector3 at,float radius)=>Create(1,at,at,radius);
        private static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
        private static void Create(int style,Vector3 from,Vector3 to,float radius)
        {
            if(Application.isBatchMode||active[style]>=limits[style]||radius<=0||radius>1000||float.IsNaN(radius)||float.IsInfinity(radius)||!Finite(from)||!Finite(to))return;
            BastionMineChainVisual fx=null;while(pools[style].Count>0&&!fx)fx=pools[style].Dequeue();
            if(!fx){var go=new GameObject(style==0?"Widow external charge arc":"Widow white electrical detonation");fx=go.AddComponent<BastionMineChainVisual>();fx.style=style;fx.Build();}
            fx.from=from;fx.to=to;fx.radius=radius;fx.age=0;fx.transform.position=from;fx.counted=true;active[style]++;fx.gameObject.SetActive(true);fx.Draw();
        }
        private void Build()
        {
            int count=style==0?4:lines.Length;
            for(int i=0;i<count;i++)
            {
                int layer=i<BastionMineChainGeometry.ArcCount?0:i<BastionMineChainGeometry.ArcCount+BastionMineChainGeometry.ForkCount?1:2;
                int points=style==0?(i<2?11:3):layer==0?BastionMineChainGeometry.ArcPoints:layer==1?BastionMineChainGeometry.ForkPoints:BastionMineChainGeometry.CorePoints;
                var line=BastionUtilityFx.Line(transform,points,style==0?(i==0?.12f:.025f):.1f,Color.white);
                line.numCapVertices=3;line.numCornerVertices=2;
                line.widthCurve=style==0?AnimationCurve.Linear(0,1,1,.25f):layer==2?
                    new AnimationCurve(new Keyframe(0,.45f),new Keyframe(.5f,1),new Keyframe(1,.45f)):
                    new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.18f,1),new Keyframe(.75f,.65f),new Keyframe(1,0));
                lines[i]=line;
            }
        }
        private void Update()
        {
            age+=Time.deltaTime;float lifetime=style==0?BastionMineChainGeometry.LinkLifetime:BastionMineChainGeometry.BurstLifetime;
            if(age>=lifetime){Release();gameObject.SetActive(false);pools[style].Enqueue(this);return;}Draw();
        }
        private void Draw()
        {
            float t=Mathf.Clamp01(age/(style==0?BastionMineChainGeometry.LinkLifetime:BastionMineChainGeometry.BurstLifetime));float alpha=(1-t)*(1-t);
            if(style==0)
            {
                Vector3 delta=to-from;var cross=Vector3.Cross(delta.normalized,Vector3.up);if(cross.sqrMagnitude<.01f)cross=Vector3.right;cross.Normalize();
                for(int i=0;i<11;i++)
                {float u=i/10f;var p=Vector3.Lerp(from,to,u)+cross*(Mathf.Sin(i*2.3f+age*44)*.15f*Mathf.Sin(u*Mathf.PI))+Vector3.up*.12f;lines[0].SetPosition(i,p);lines[1].SetPosition(i,p);}
                for(int i=2;i<4;i++){Vector3 axis=i==2?Vector3.up:Vector3.right;lines[i].SetPosition(0,to-axis*(.5f+.3f*t));lines[i].SetPosition(1,to);lines[i].SetPosition(2,to+axis*(.5f+.3f*t));}
                BastionUtilityFx.Color(lines[0],BastionModel.Blue,alpha*.85f);for(int i=1;i<4;i++)BastionUtilityFx.Color(lines[i],Color.white,alpha);
            }
            else for(int i=0;i<lines.Length;i++)
            {
                int layer=i<BastionMineChainGeometry.ArcCount?0:i<BastionMineChainGeometry.ArcCount+BastionMineChainGeometry.ForkCount?1:2;
                int index=layer==0?i:layer==1?i-BastionMineChainGeometry.ArcCount:i-BastionMineChainGeometry.ArcCount-BastionMineChainGeometry.ForkCount;
                lines[i].widthMultiplier=BastionMineChainGeometry.BurstWidth(layer,t,radius);
                for(int p=0;p<lines[i].positionCount;p++)
                {
                    var offset=layer==0?BastionMineChainGeometry.Arc(index,p,t,radius):layer==1?BastionMineChainGeometry.Fork(index,p,t,radius):BastionMineChainGeometry.Core(index,p,t,radius);
                    lines[i].SetPosition(p,from+offset);
                }
                // White is the dominant silhouette; only a few thin peripheral
                // branches carry the restrained ice-blue Seraph accent.
                bool ice=layer==0&&index%4==3;
                var color=ice?new Color(.74f,.9f,1f):layer==2?new Color(.98f,.995f,1f):Color.white;
                BastionUtilityFx.Color(lines[i],color,BastionMineChainGeometry.Opacity(layer,index,t)*(ice?.64f:1));
            }
        }
        private void Release(){if(counted){active[style]=Mathf.Max(0,active[style]-1);counted=false;}}
        private void OnDisable()=>Release();private void OnDestroy()=>Release();
    }
}
