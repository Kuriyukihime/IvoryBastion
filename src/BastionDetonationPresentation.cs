using System;

namespace IvoryBastion
{
    // Shared by runtime, preview export and managed stress measurements.
    // Radius and the primary fireball/flash are never reduced by this budget.
    internal static class BastionDetonationPresentation
    {
        internal static int Count(BastionExplosionLayer layer,int tier,int original,int active)
        {
            if(layer!=BastionExplosionLayer.Smoke&&layer!=BastionExplosionLayer.Spark)return original;
            float density=Math.Max(tier==2?.65f:.4f,Math.Min(1,1-Math.Max(0,active-10)*.035f));
            return Math.Max(1,(int)Math.Ceiling(original*density));
        }
        internal static float Opacity(BastionExplosionLayer layer)
            =>layer==BastionExplosionLayer.Smoke?.55f:layer==BastionExplosionLayer.Fireball?.84f:1;
        // A camera-facing square may rotate its corner beyond the circular
        // texture's support. Cover that diagonal as well as the entire life.
        internal static float BoundsExtent(float radius)=>Math.Max(0,radius)*1.415f+.05f;
        // Delayed smoke must be populated even if the earlier fireball was
        // never visible. Keep occasional offscreen updates independent of
        // native visibility, then resume every-frame sampling on re-entry.
        internal static bool RequiresSample(float age,float lastSampleAge,bool visible)
            =>visible||age<.1f||age-lastSampleAge>=.15f;
    }
}
