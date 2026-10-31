using System;
namespace IvoryBastion
{
    // Independent Seraph Shield points. Native health, barrier and item shields
    // never enter this pool and cannot refill it through healing/item hooks.
    internal sealed class BastionShieldReserve
    {
        internal float Capacity { get; private set; }
        internal float Charge { get; private set; }
        private bool recharging;
        internal void Resize(float capacity)
        {
            capacity=Positive(capacity);
            Charge=Math.Min(capacity,Charge+Math.Max(0,capacity-Capacity));
            Capacity=capacity;
            if(Charge>=Capacity)recharging=false;
        }
        internal float Restore(float amount)
        {
            float restored=Math.Min(Math.Max(0,Capacity-Charge),Positive(amount));
            Charge+=restored;if(Charge>=Capacity)recharging=false;return restored;
        }
        // Natural recharge reports one start per uninterrupted fill. Explicit
        // restores (packing/Renewal), full pools and zero-time ticks stay quiet.
        internal bool Recharge(float amount)
        {
            bool wasRecharging=recharging;
            if(Restore(amount)<=0)return false;
            recharging=Charge<Capacity;
            return !wasRecharging;
        }
        // Native armor/block/barrier have resolved. Only this pool receives its
        // damage reduction; remaining damage continues to item shields/health.
        internal float Absorb(float damage,float reduction)
        {
            if(float.IsNaN(damage)||float.IsInfinity(damage)||damage<=0||Charge<=0)return damage;
            recharging=false;
            reduction=float.IsNaN(reduction)||float.IsInfinity(reduction)?0:Math.Max(0,Math.Min(.95f,reduction));
            double cost=1d-reduction,absorbed=Math.Min(damage,Charge/cost);
            Charge=(float)Math.Max(0,Charge-absorbed*cost);
            return (float)Math.Max(0,damage-absorbed);
        }
        internal void Receive(float capacity,float charge)
        {Capacity=Positive(capacity);Charge=Math.Min(Capacity,Positive(charge));recharging=false;}
        private static float Positive(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(0,value);
    }
}
