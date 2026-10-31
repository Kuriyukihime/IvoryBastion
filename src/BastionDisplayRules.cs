using UnityEngine;
namespace IvoryBastion
{
    internal static class BastionDisplayRules
    {
        internal const float Hold=.35f,Deploy=1.65f,Yaw=0,StartScale=.75f,FinalScale=.38f,LeftShift=.4f;
        internal static float Unfold(float age)=>Mathf.Clamp01((age-Hold)/Deploy);
        internal static float Scale(float unfold)=>Mathf.Lerp(StartScale,FinalScale,Mathf.SmoothStep(0,1,Mathf.Clamp01(unfold/.7f)));
        internal static float FortressScale(float unfold)=>FinalScale*Mathf.SmoothStep(0,1,Mathf.Clamp01(unfold/.16f));
    }
}
