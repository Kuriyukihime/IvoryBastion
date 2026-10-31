using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System;

namespace IvoryBastion
{
    // Integrate healing continuously, but call the game's Heal method at 4 Hz.
    // This remains ordinary healing so Rack, Aegis and other item hooks still work.
    internal sealed class BastionRepair
    {
        internal const float Interval=.25f;
        private double pending,elapsed;

        internal float Tick(float dt,bool siege,float maxHealth,float sinceDamage,
            bool repairDuringCombat,float protocolSeconds,float protocolFraction)
        {
            if(!siege)return Flush();
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0)return 0;
            float passiveSeconds=repairDuringCombat?dt:Math.Min(dt,Math.Max(0,sinceDamage-B.Get(S.SiegeRepairDelay)));
            float activeSeconds=Math.Min(dt,Math.Max(0,protocolSeconds));
            float fraction=passiveSeconds*B.Fraction(S.SiegeRepair)+activeSeconds*protocolFraction/B.Get(S.AegisDuration);
            if(maxHealth>0&&!float.IsNaN(maxHealth)&&!float.IsInfinity(maxHealth))pending+=maxHealth*fraction;
            elapsed+=dt;
            if(fraction<=0)return Flush();
            if(elapsed+1e-6>=Interval)
            {
                elapsed=Math.Max(0,elapsed-Math.Floor((elapsed+1e-6)/Interval)*Interval);
                float healing=(float)pending;pending=0;return healing;
            }
            return 0;
        }

        internal float Flush()
        {
            float result=(float)pending;pending=elapsed=0;return result;
        }
    }
}
