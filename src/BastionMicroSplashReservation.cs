using System.Collections.Generic;
using RoR2;
namespace IvoryBastion
{
    // Rush-only projected splash. Other weapons keep their existing reservations.
    internal sealed class BastionMicroSplashReservation
    {
        private readonly List<BastionDamageReservation> claims=new List<BastionDamageReservation>();
        internal void Assign(BastionController owner,CharacterBody primary,float damage,float radius)
        {
            Release();
            if(!owner||!owner.Alive||!BastionUtilityCombat.ValidRushVictim(owner,primary)||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage)||radius<=0||float.IsNaN(radius)||float.IsInfinity(radius))return;
            var center=BastionCombat.AimPoint(primary);var seen=new HashSet<HealthComponent>();
            void Claim(CharacterBody body)
            {
                if(!BastionUtilityCombat.ValidRushVictim(owner,body)||!seen.Add(body.healthComponent)||!BastionUtilityCombat.InRushBlast(center,body,radius)||!BastionCombat.Visible(center,body))return;
                var reservation=new BastionDamageReservation();reservation.Assign(owner,body,damage);claims.Add(reservation);
            }
            Claim(primary);
            foreach(var body in CharacterBody.readOnlyInstancesList)Claim(body);
        }
        internal void Release()
        {foreach(var reservation in claims)reservation.Release();claims.Clear();}
    }
}
