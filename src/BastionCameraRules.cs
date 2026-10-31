using System;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionCameraRules
    {
        internal static float Opacity(bool ownSiege,bool obstructing,float hitDistance,float upwardAim,float coverage=1)
        {
            if(!ownSiege||!obstructing||!float.IsFinite(hitDistance))return 1;
            if(!float.IsFinite(upwardAim))upwardAim=0;
            if(!float.IsFinite(coverage))coverage=0;
            // Protect the ordinary horizontal/slightly downward third-person
            // view. An actual obstruction while aiming up, or steeply down,
            // receives a strong fade even at the normal 11m siege camera.
            float upward=Smooth((upwardAim-.14f)/.36f);
            float downward=Smooth((-upwardAim-.45f)/.30f);
            float aiming=Math.Max(upward,downward)*Clamp(coverage);
            float contact=Smooth((1.25f-hitDistance)/1.10f);
            return 1-.92f*Math.Max(contact,aiming);
        }
        private static float Clamp(float value)=>Math.Max(0,Math.Min(1,value));
        private static float Smooth(float value){value=Clamp(value);return value*value*(3-2*value);}

        internal static float Coverage(int hitMask)
        {
            if((hitMask&1)!=0)return 1;
            int edges=0;for(int bit=1;bit<=4;bit++)if((hitMask&(1<<bit))!=0)edges++;
            return edges*.2f;
        }

        internal static float Approach(float current,float target,float deltaTime)
        {
            if(!float.IsFinite(current))current=1;
            if(!float.IsFinite(target))target=1;
            current=Clamp(current);target=Clamp(target);
            if(!float.IsFinite(deltaTime)||deltaTime<=0)return current;
            float step=Math.Min(deltaTime,.1f)*(target<current?12:6);
            return current<target?Math.Min(target,current+step):Math.Max(target,current-step);
        }

        // Keep the transformed direction unnormalized: the ray parameter then
        // remains a world-space distance even through scaled/rotated bones.
        internal static bool RayBox(Vector3 origin,Vector3 direction,Vector3 minimum,Vector3 maximum,out float distance)
        {
            distance=0;float far=float.PositiveInfinity;
            if(!float.IsFinite(direction.sqrMagnitude)||direction.sqrMagnitude<1e-12f)return false;
            for(int axis=0;axis<3;axis++)
            {
                float at=origin[axis],along=direction[axis];
                if(!float.IsFinite(at))return false;
                if(Math.Abs(along)<1e-7f){if(at<minimum[axis]||at>maximum[axis])return false;continue;}
                float a=(minimum[axis]-at)/along,b=(maximum[axis]-at)/along;
                distance=Math.Max(distance,Math.Min(a,b));far=Math.Min(far,Math.Max(a,b));
                if(distance>far)return false;
            }
            return far>=0;
        }
    }
}
