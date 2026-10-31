using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionLaserRules
    {
        internal static float Range=>B.Get(S.LaserRange);
        internal static float TargetScore(Vector3 origin,Vector3 aim,Vector3 point)
        {
            var delta=point-origin;float distance=delta.magnitude;
            if(!float.IsFinite(distance)||!float.IsFinite(aim.sqrMagnitude)||distance>Range||aim.sqrMagnitude<.001f)return float.NegativeInfinity;
            float alignment=distance>.001f?Vector3.Dot(aim.normalized,delta/distance):1;
            return alignment*1000-distance*.05f;
        }
        internal static bool NeedsFallback(bool aliveEnemy,bool registeredHit,bool unobstructed)=>aliveEnemy&&!registeredHit&&unobstructed;
    }
}
