using System;
namespace IvoryBastion
{
    // Shared by the live articulated rig and offline actual-mesh proofs.
    internal static class BastionMinePresentation
    {
        internal const float UnfoldSpeed=7,LauncherBoundsExpansion=.9f;
        private static float Stage(float value,float from,float to)
        {float t=Math.Max(0,Math.Min(1,(value-from)/(to-from)));return t*t*(3-2*t);}

        // Lift the hip before extending the lower leg, then place the foot.
        // A simultaneous negative-angle blend would pass through the ground.
        internal static float Hip(float opened)=>-55+73*Stage(opened,0,.45f)-18*Stage(opened,.8f,1);
        internal static float Knee(float opened)=>-100+100*Stage(opened,.45f,.8f);
        internal static float HatchAngle(int side,float opened)=>-side*76*Math.Max(0,Math.Min(1,opened));
        internal static float Contact(float age)=>Stage(age,0,.18f);
        internal static float Clamp(float age)=>Stage(age,0,.22f);
        internal static float LatchFlash(float age)=>Math.Max(0,1-age/.38f);
    }
}
