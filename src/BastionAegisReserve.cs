using System;
namespace IvoryBastion
{
    // Aegis points are neither health, item shield nor vanilla barrier. Capacity
    // records the last grant's peak for its own gauge; the native-sized limit
    // and decay curve retain the previous ability's cap and lifetime.
    internal sealed class BastionAegisReserve
    {
        internal float Capacity { get; private set; }
        internal float Charge { get; private set; }
        internal void Grant(float amount,float limit)
        {
            limit=Positive(limit);amount=Positive(amount);
            Charge=Math.Min(limit,Charge+amount);
            Capacity=Math.Min(limit,Math.Max(Charge,amount));
        }
        internal float Absorb(float damage,float reduction=0)
        {
            if(float.IsNaN(damage)||float.IsInfinity(damage)||damage<=0||Charge<=0)return damage;
            // Charged Seraph also fortifies our outer Aegis layer. Account for
            // that protection in points spent, so overflow is not reduced twice
            // and native item barrier/shields never receive Seraph's reduction.
            reduction=float.IsNaN(reduction)||float.IsInfinity(reduction)?0:Math.Max(0,Math.Min(.95f,reduction));
            double cost=1d-reduction,absorbed=Math.Min(damage,Charge/cost);
            Charge=(float)Math.Max(0,Charge-absorbed*cost);
            return (float)Math.Max(0,damage-absorbed);
        }
        internal void Tick(float delta,float limit)
        {
            limit=Positive(limit);Charge=Math.Min(Charge,limit);Capacity=Math.Min(Capacity,limit);
            if(Charge<=0||limit<=0)return;
            float rate=limit/30f*(.5f+2.5f*Charge/limit);
            Charge=Math.Max(0,Charge-rate*Positive(delta));
        }
        internal void Receive(float capacity,float charge)
        {Capacity=Positive(capacity);Charge=Math.Min(Capacity,Positive(charge));}
        private static float Positive(float v)=>float.IsNaN(v)||float.IsInfinity(v)?0:Math.Max(0,v);
    }
}
