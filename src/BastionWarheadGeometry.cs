using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IvoryBastion
{
    internal enum BastionWarheadTargetKind { Ordinary,Elite,Boss }

    internal static class BastionWarheadGeometry
    {
        internal static int Count=>B.Int(S.SkyfallCount);
        internal static int ClusterMinimum=>B.Int(S.SkyfallCluster);
        internal const float VisualScale=2.4f;
        internal static float Damage=>B.Fraction(S.SkyfallDamage);
        internal static float Radius=>B.Get(S.SkyfallRadius);
        internal static float Range=>B.Get(S.SkyfallRange);
        internal static float LaunchSpacing=>B.Get(S.SkyfallSpacing);

        // Compare actual blast coverage across the entire search area. Damage
        // already assigned to a target is expressed in remaining warhead hits.
        internal static int Select(Vector3 origin,IReadOnlyList<Vector3> points,IReadOnlyList<float> remainingHits,
            IReadOnlyList<BastionWarheadTargetKind> kinds=null)
        {
            var eligible=new bool[points.Count];
            for(int i=0;i<points.Count;i++)eligible[i]=(points[i]-origin).sqrMagnitude<=Range*Range;
            int selected=Pick(false);
            return selected>=0?selected:Pick(true);

            int Pick(bool fallback)
            {
                int best=-1,bestPriority=-1,bestCount=-1;
                float bestUseful=-1,bestCenter=float.PositiveInfinity,bestDistance=float.PositiveInfinity;
                for(int i=0;i<points.Count;i++)
                {
                    if(!eligible[i])continue;
                    int count=0,priority=0;float useful=0,center=0;
                    for(int j=0;j<points.Count;j++)
                    {
                        float distance=(points[j]-points[i]).sqrMagnitude,need=Need(j);
                        if(!eligible[j]||distance>Radius*Radius||!fallback&&need<=0)continue;
                        count++;useful+=Mathf.Clamp01(need);center+=distance;
                        if(kinds!=null&&j<kinds.Count)priority=Math.Max(priority,(int)kinds[j]);
                    }
                    if(count==0)continue;
                    if(count>=ClusterMinimum)priority=3;
                    float from=(points[i]-origin).sqrMagnitude;
                    if(priority<bestPriority||priority==bestPriority&&(count<bestCount||count==bestCount&&
                        (useful<bestUseful||useful==bestUseful&&(center>bestCenter||center==bestCenter&&from>=bestDistance))))continue;
                    best=i;bestPriority=priority;bestCount=count;bestUseful=useful;bestCenter=center;bestDistance=from;
                }
                return best;
            }

            float Need(int index)
            {
                float value=remainingHits!=null&&index<remainingHits.Count?remainingHits[index]:1;
                return float.IsNaN(value)?0:Mathf.Clamp(value,0,Count+1);
            }
        }

        // Offline salvo simulation uses the same selector as each live launch.
        internal static int[] Plan(Vector3 origin,IReadOnlyList<Vector3> points,IReadOnlyList<float> remainingHits,
            IReadOnlyList<BastionWarheadTargetKind> kinds=null)
        {
            var result=new int[Count];var remaining=new float[points.Count];
            for(int i=0;i<remaining.Length;i++)remaining[i]=remainingHits!=null&&i<remainingHits.Count?remainingHits[i]:1;
            for(int shot=0;shot<Count;shot++)
            {
                int target=result[shot]=Select(origin,points,remaining,kinds);
                if(target<0)continue;
                for(int i=0;i<remaining.Length;i++)
                    if((points[i]-points[target]).sqrMagnitude<=Radius*Radius)remaining[i]=Math.Max(0,remaining[i]-1);
            }
            return result;
        }

        internal static float Duration(Vector3 from,Vector3 to)=>2.1f+Mathf.Clamp01(Vector3.Distance(from,to)/Range)*.8f;
        internal static Vector3 Position(Vector3 from,Vector3 to,float time)
        {
            float t=Mathf.Clamp01(time),u=1-t;
            float roof=Mathf.Max(from.y,to.y)+Mathf.Max(26,Vector3.Distance(from,to)*.6f);
            var a=new Vector3(from.x,roof,from.z);var b=new Vector3(to.x,roof,to.z);
            // Vertical launch and vertical terminal dive, shared by the server
            // collision path and every client's mesh/trail animation.
            return from*(u*u*u)+a*(3*u*u*t)+b*(3*u*t*t)+to*(t*t*t);
        }
    }
}
