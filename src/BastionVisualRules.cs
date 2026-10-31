using System;

namespace IvoryBastion
{
    internal static class BastionVisualRules
    {
        internal static float Height(bool siege)=>siege?4f:1.35f;
        internal static float Radius(bool siege)=>siege?1.5f:.23f;
        internal static float SelfEffectRadius(float requested,bool siege,float scale)
        {
            // No dependence on the previous effect size or stage number.
            if(float.IsNaN(scale)||float.IsInfinity(scale)||scale<=0)scale=1;
            float limit=Height(siege)*scale;
            if(float.IsNaN(requested)||float.IsInfinity(requested)||requested<=0)return limit;
            return Math.Min(requested,limit);
        }
        internal static float HoverStrength(float groundClearance,bool hasGround)
        {
            if(!hasGround)return 1;
            if(float.IsNaN(groundClearance)||float.IsInfinity(groundClearance))return 0;
            return Math.Max(0,Math.Min(1,(groundClearance-.18f)/.65f));
        }
    }
}
