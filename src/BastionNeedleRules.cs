using System;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionNeedleRules
    {
        internal const float ConeDegrees=6f;
        // Siege Missile Battery manual aim; homing missiles tolerate a wider cone.
        internal const float BatteryConeDegrees=15f;
        private static readonly float minAlignment=(float)Math.Cos(ConeDegrees*Math.PI/180);

        // Crosshair proximity ranks first; the caller breaks exact ties by distance.
        internal static float Score(Vector3 origin,Vector3 aim,Vector3 point,float range)
        {
            Vector3 delta=point-origin;
            float distanceSquared=delta.sqrMagnitude,aimSquared=aim.sqrMagnitude;
            if(!float.IsFinite(distanceSquared)||!float.IsFinite(aimSquared)||!float.IsFinite(range)||
                range<=0||distanceSquared<.0001f||distanceSquared>range*range||aimSquared<.001f)
                return float.NegativeInfinity;
            float alignment=Vector3.Dot(aim.normalized,delta.normalized);
            return alignment>=minAlignment?alignment:float.NegativeInfinity;
        }
    }
}
