using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System;

namespace IvoryBastion
{
    internal static class BastionDefenseRules
    {
        internal static float MobileShieldFraction=>B.Fraction(S.MobileShield);
        internal static float MobileShieldReduction=>B.Fraction(S.MobileShieldDR);
        internal static float SiegeShieldFraction=>B.Fraction(S.SiegeShield);
        internal static float SiegeShieldReduction=>B.Fraction(S.SiegeShieldDR);
        internal static float RechargeDelay=>B.Get(S.MobileShieldDelay);
        internal static float SiegeRechargeDelay=>B.Get(S.SiegeShieldDelay);
        internal static float ModeSwitchCooldown=>B.Get(S.SwitchCooldown);
        internal static float DeploymentHeight=>B.Get(S.DeployHeight);
        internal static float ShieldCapacity(float health,bool siege,float multiplier=1)=>float.IsNaN(health)||float.IsInfinity(health)?0:Math.Max(0,health)*(siege?SiegeShieldFraction:MobileShieldFraction)*multiplier;
        internal static float HealthConversionRatio(float before,float after)=>
            float.IsNaN(before)||float.IsInfinity(before)||float.IsNaN(after)||float.IsInfinity(after)||before<=0||after<=0?1:before/after;
        internal static bool ShieldActive(float shield)=>shield>0;
        internal static float RechargeStep(float secondsSinceHurt,float delta,bool siege)=>Math.Max(0,Math.Min(delta,secondsSinceHurt-(siege?SiegeRechargeDelay:RechargeDelay)));
        internal static bool CanDeploy(float distance,bool hit,float normalY)=>hit&&!float.IsNaN(distance)&&!float.IsInfinity(distance)&&distance>=-.2f&&distance<=DeploymentHeight&&normalY>.15f;
    }
}
