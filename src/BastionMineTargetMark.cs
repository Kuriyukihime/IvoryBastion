using System.Collections.Generic;
using RoR2;
using UnityEngine;
namespace IvoryBastion
{
    // One mark per carrier, even if several mines latch onto the same boss.
    // Presentation only: no hurtbox, damage or timer-triggered detonation.
    public sealed class BastionMineTargetMark:MonoBehaviour
    {
        private static readonly Dictionary<CharacterBody,BastionMineTargetMark> live=new Dictionary<CharacterBody,BastionMineTargetMark>();
        private readonly List<BastionSpiderVisual> mines=new List<BastionSpiderVisual>();
        private CharacterBody target;private bool retired;private LineRenderer rim,core,diamond,link;private readonly LineRenderer[] ticks=new LineRenderer[BastionMineRules.MaximumPerTarget];
        internal static BastionMineTargetMark Add(CharacterBody target,BastionSpiderVisual mine)
        {
            if(!target||!mine)return null;
            if(!live.TryGetValue(target,out var mark)||!mark)
            {
                mark=new GameObject("Widow attached target mark").AddComponent<BastionMineTargetMark>();mark.target=target;mark.Build();live[target]=mark;
            }
            if(!mark.mines.Contains(mine))mark.mines.Add(mine);return mark;
        }
        internal void Remove(BastionSpiderVisual mine)
        {mines.Remove(mine);if(mines.Count==0)Retire();}
        private void Retire()
        {
            if(retired)return;retired=true;
            if(!ReferenceEquals(target,null)&&live.TryGetValue(target,out var current)&&current==this)live.Remove(target);
            Destroy(gameObject);
        }
        private void Build()
        {
            rim=BastionUtilityFx.Line(transform,33,.075f,BastionModel.Blue);
            core=BastionUtilityFx.Line(transform,33,.022f,Color.white);
            diamond=BastionUtilityFx.Line(transform,5,.036f,Color.white);
            link=BastionUtilityFx.Line(transform,9,.022f,BastionModel.Blue);
            for(int i=0;i<ticks.Length;i++)ticks[i]=BastionUtilityFx.Line(transform,2,.034f,BastionModel.Blue);
        }
        private void LateUpdate()
        {
            for(int i=mines.Count-1;i>=0;i--)if(!mines[i]||!mines[i].Attached||mines[i].AttachedTarget!=target)mines.RemoveAt(i);
            if(!target||mines.Count==0){Retire();return;}
            bool charged=false;foreach(var mine in mines)charged|=mine.Overcharged;
            int capacity=BastionMineRules.TargetLimit(target.isBoss||target.isChampion,target.isElite);
            if(target.healthComponent&&!target.healthComponent.alive&&!charged){Retire();return;}
            var view=SceneCamera._camera?SceneCamera._camera:Camera.main;
            Vector3 right=view?view.transform.right:Vector3.right,up=view?view.transform.up:Vector3.up;
            float radius=Mathf.Clamp(target.radius*.3f,.34f,.72f),height=target.corePosition.y+Mathf.Max(.7f,target.radius);
            var collider=target.mainHurtBox?target.mainHurtBox.GetComponent<Collider>():null;
            if(collider)height=Mathf.Max(height,collider.bounds.max.y);
            var center=new Vector3(target.corePosition.x,height+.45f,target.corePosition.z);
            float pulse=charged?1:.78f+Mathf.Sin(Time.time*5)*.12f;
            for(int i=0;i<33;i++)
            {
                float a=i*Mathf.PI*2/32;var p=center+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius;
                rim.SetPosition(i,p);core.SetPosition(i,p);
            }
            for(int i=0;i<5;i++)
            {float a=(i%4)*Mathf.PI*.5f;diamond.SetPosition(i,center+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius*.55f);}
            for(int i=0;i<ticks.Length;i++)
            {
                float a=(i+.5f)*Mathf.PI*2/capacity;var radial=right*Mathf.Cos(a)+up*Mathf.Sin(a);
                ticks[i].SetPosition(0,center+radial*radius*1.16f);ticks[i].SetPosition(1,center+radial*radius*1.42f);
                BastionUtilityFx.Color(ticks[i],charged?Color.white:BastionModel.Blue,i<capacity?(i<mines.Count?pulse:.14f):0);
            }
            var source=mines[0].transform.position;var perpendicular=Vector3.Cross(center-source,view?view.transform.forward:Vector3.forward).normalized;
            for(int i=0;i<9;i++)
            {float t=i/8f;link.SetPosition(i,Vector3.Lerp(source,center,t)+perpendicular*Mathf.Sin(t*Mathf.PI)*Mathf.Sin(i*2.7f+Time.time*24)*.045f);}
            BastionUtilityFx.Color(rim,charged?Color.white:BastionModel.Blue,pulse);
            BastionUtilityFx.Color(core,Color.white,pulse);BastionUtilityFx.Color(diamond,Color.white,pulse);
            BastionUtilityFx.Color(link,charged?Color.white:BastionModel.Blue,charged?.85f:.48f);
        }
        private void OnDestroy(){if(!ReferenceEquals(target,null)&&live.TryGetValue(target,out var current)&&current==this)live.Remove(target);}
    }
}
