using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System;
using UnityEngine;
namespace IvoryBastion
{
    // Shared by authoritative missiles and their client visuals. Direct pursuit
    // with a clamped step cannot orbit or overshoot a nearby target.
    internal static class BastionFlightGeometry
    {
        internal static float MissileLifetime=>B.Get(S.MissileLifetime);
        internal const float BatteryVisualWidthScale=.8f,SkyfallVisualWidthScale=.583333333f;
        internal static float Speed(bool heavy)=>B.Get(heavy?S.HeavySpeed:S.MissileSpeed);
        internal static Vector3 Step(Vector3 from,Vector3 destination,bool heavy,float dt)
            =>Vector3.MoveTowards(from,destination,Speed(heavy)*Mathf.Max(0,dt));
        internal static bool CrossesTarget(Vector3 from,Vector3 to,Vector3 target,float radius)
        {
            Vector3 step=to-from;float length=step.sqrMagnitude;
            float t=length>0?Mathf.Clamp01(Vector3.Dot(target-from,step)/length):0;
            return (from+step*t-target).sqrMagnitude<=radius*radius;
        }
        // Prefer the current footprint, then lift above clutter. A small lateral
        // adjustment allows deployment beside rocks without crossing walls.
        internal static bool FindAnchor(Vector3 foot,Vector3 facing,Func<Vector3,bool> fits,out Vector3 anchor,Func<Vector3,bool> nearGround=null)
        {
            if(nearGround!=null&&!nearGround(foot)){anchor=Vector3.zero;return false;}
            Vector3 center=foot+Vector3.up*2.08f;
            for(int lift=0;lift<=8;lift++)
            {
                anchor=center+Vector3.up*(lift*.75f);
                if((nearGround==null||nearGround(anchor-Vector3.up*2))&&fits(anchor))return true;
            }
            var back=-Vector3.ProjectOnPlane(facing,Vector3.up).normalized;
            if(back.sqrMagnitude<.01f)back=Vector3.back;
            for(int lift=0;lift<=4;lift++)for(int direction=0;direction<8;direction++)
            {
                float angle=direction*Mathf.PI*.25f;
                var side=Vector3.Cross(Vector3.up,back);
                anchor=center+Vector3.up*(lift*.75f)+(back*Mathf.Cos(angle)+side*Mathf.Sin(angle))*2;
                if((nearGround==null||nearGround(anchor-Vector3.up*2))&&fits(anchor))return true;
            }
            anchor=Vector3.zero;return false;
        }
    }
}
