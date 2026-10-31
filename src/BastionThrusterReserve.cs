using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System;
namespace IvoryBastion
{
    internal sealed class BastionThrusterReserve
    {
        internal int Capacity=B.Int(S.BoostFuel);
        internal float Fuel=B.Int(S.BoostFuel),Recovery;
        internal bool Active,Exhausted;
        internal bool CanStart=>Fuel>=.25f&&!Exhausted;
        internal float Fraction=>Math.Max(0,Math.Min(1,Fuel/Capacity));
        internal void Resize(int capacity){Capacity=Math.Max(1,capacity);Fuel=Math.Min(Fuel,Capacity);}
        internal void Restore(float seconds){if(float.IsNaN(seconds)||seconds<=0)return;Fuel=Math.Min(Capacity,Fuel+seconds);}
        internal void Tick(float dt,bool held,bool available,float refillSeconds,float overdrive)
        {
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            if(!held)Exhausted=false;
            Active=held&&available&&!Exhausted&&Fuel>0;
            if(Active)
            {
                Fuel=Math.Max(0,Fuel-dt);Recovery=B.Get(S.BoostRecovery);
                if(Fuel<=0){Active=false;Exhausted=true;}
                return;
            }
            float delay=Math.Min(dt,Math.Max(0,Recovery));Recovery=Math.Max(0,Recovery-dt);
            float recharge=dt-delay;
            // Overdrive accelerates recovery only during the portion of its remaining window.
            recharge+=(B.Get(S.ShiftRefillMultiplier)-1)*Math.Min(recharge,Math.Max(0,overdrive-delay));
            Restore(recharge*Capacity/Math.Max(.05f,refillSeconds));
        }
    }
}
