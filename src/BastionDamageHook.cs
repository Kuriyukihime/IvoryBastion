using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    internal static class BastionDamageHook
    {
        internal static void Apply(On.RoR2.HealthComponent.orig_TakeDamage orig,HealthComponent hp,DamageInfo info)
        {
            var c=hp.body?hp.body.GetComponent<BastionController>():null;
            float before=hp.combinedHealth,barrierBefore=hp.barrier,
                shieldBefore=c?c.AbilityShield.Charge:0,aegisBefore=c?c.AegisBarrier.Charge:0;
            // Aegis applies globally. Charged Seraph fortifies our two defense
            // pools after native armor/blocks/barrier, never overflow or items.
            if(NetworkServer.active&&c){info.damage*=c.Bank.DamageMultiplier(c.Sieged);if(c.Sieged)info.force=Vector3.zero;}
            orig(hp,info);
            float shieldSpent=c?Mathf.Max(0,shieldBefore-c.AbilityShield.Charge):0;
            float aegisSpent=c?Mathf.Max(0,aegisBefore-c.AegisBarrier.Charge):0;
            if(NetworkServer.active&&c&&(hp.combinedHealth<before||hp.barrier<barrierBefore||shieldSpent>0||aegisSpent>0))
            {
                // Native barrier pauses passive hull repair, but does not
                // interrupt Seraph's separate recharge clock.
                c.LastHurt=Time.time;c.LastCombat=Time.time;
                if(aegisSpent>0&&Time.time-c.LastAegisImpact>=.06f)
                {
                    c.LastAegisImpact=Time.time;
                    Vector3 direction=(info.attacker?info.attacker.transform.position:info.position)-c.Body.corePosition;
                    c.Emit(BastionFxKind.AegisHit,direction.normalized,Vector3.zero);
                }
                if(shieldSpent>0&&Time.time-c.LastShieldImpact>=.06f)
                {
                    c.LastShieldImpact=Time.time;
                    Vector3 direction=(info.attacker?info.attacker.transform.position:info.position)-c.Body.corePosition;
                    c.Emit(BastionFxKind.ShieldHit,direction.normalized,Vector3.zero,value:
                        Mathf.Clamp01(.45f+shieldSpent/Mathf.Max(1,c.AbilityShield.Capacity)));
                }
            }
        }
    }
}
