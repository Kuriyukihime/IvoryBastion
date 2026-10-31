using System;
using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
namespace IvoryBastion
{
    internal static class BastionUnitRules
    {
        // Damage is a fraction of the owner's current damage stat. These are
        // separate roles, not four interchangeable versions of the same drone.
        internal static float HealthMultiplier(BastionUnitKind kind)=>B.Get(kind==BastionUnitKind.Guardian?S.BulwarkHealth:kind==BastionUnitKind.Gunner?S.GunnerHealth:kind==BastionUnitKind.Interceptor?S.InterceptorHealth:S.RepairHealth);
        internal static float Armor(BastionUnitKind kind)=>B.Get(kind==BastionUnitKind.Guardian?S.BulwarkArmor:kind==BastionUnitKind.Gunner?S.GunnerArmor:kind==BastionUnitKind.Interceptor?S.InterceptorArmor:S.RepairArmor);
        internal static float ShotInterval(BastionUnitKind kind)=>kind==BastionUnitKind.Gunner?B.Get(S.GunnerInterval):kind==BastionUnitKind.Guardian?B.Get(S.BulwarkInterval):kind==BastionUnitKind.Repair?B.Get(S.RepairInterval):1;
        internal static float ShotDamage(BastionUnitKind kind)=>kind==BastionUnitKind.Gunner?B.Fraction(S.GunnerDamage):kind==BastionUnitKind.Guardian?B.Fraction(S.BulwarkDamage):0;
        internal static float ShotRange(BastionUnitKind kind)=>kind==BastionUnitKind.Gunner?B.Get(S.GunnerRange):kind==BastionUnitKind.Guardian?B.Get(S.BulwarkRange):0;
        internal static float RepairPriority(float health,float maximum)
        {
            // Shields/barrier cannot be healed. A full-health ally with an empty
            // shield must not prevent a wounded teammate from receiving a pulse.
            if(float.IsNaN(health)||float.IsInfinity(health)||float.IsNaN(maximum)||float.IsInfinity(maximum)||maximum<=0||health<=0||health>=maximum)return float.PositiveInfinity;
            return health/maximum;
        }
        internal static string RoleSummary(BastionUnitKind kind)
        {
            switch(kind)
            {
                case BastionUnitKind.Guardian:return "DEFENSE / PULL + STUN";
                case BastionUnitKind.Gunner:return $"DAMAGE / {B.Get(S.GunnerDamage)/B.Get(S.GunnerInterval):0.#}% PER SEC / {B.Get(S.GunnerRange):0.#}m";
                case BastionUnitKind.Interceptor:return $"PROJECTILE DEFENSE / {B.Get(S.InterceptorRange):0.#}m";
                default:return $"HEALING / {B.Get(S.RepairHealing):0.#}% PER SEC / {B.Get(S.RepairRange):0.#}m";
            }
        }
    }
}
