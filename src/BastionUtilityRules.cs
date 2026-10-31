using System;
namespace IvoryBastion
{
    internal enum BastionUtilityKind:byte {None,Wingburst,Mines}

    // Independent of native shields, ammunition and the transform handshake.
    internal sealed class BastionUtilityReserve
    {
        internal int Stock=1,Capacity=1;
        internal float Remaining,Interval=6;
        internal void Resize(int capacity)
        {Capacity=Math.Max(1,capacity);Stock=Math.Min(Stock,Capacity);if(Stock<Capacity&&Remaining<=0)Remaining=Interval;}
        internal float Wait(int cost)=>Stock>=cost?0:Remaining+Math.Max(0,cost-Stock-1)*Interval;
        internal void Reconfigure(float interval)
        {
            if(!Finite(interval))return;
            interval=Math.Max(.05f,interval);
            if(Stock<Capacity&&Interval>0)Remaining=Remaining/Interval*interval;
            Interval=interval;
        }
        internal bool Spend(float interval,int cost=1)
        {
            if(cost<1||Stock<cost)return false;
            float cell=Finite(interval)?Math.Max(.05f,interval/cost):6;
            if(Stock==Capacity){Interval=cell;Remaining=cell;}
            else if(cell>Interval){Interval=cell;Remaining=Math.Max(Remaining,cell);}
            Stock-=cost;return true;
        }
        internal void Tick(float elapsed)
        {
            if(!Finite(elapsed)||elapsed<=0)return;
            if(Stock>=Capacity){Stock=Capacity;Remaining=0;return;}
            Remaining-=elapsed;
            while(Remaining<=0&&Stock<Capacity){Stock++;if(Stock<Capacity)Remaining+=Math.Max(.05f,Interval);}
            if(Stock==Capacity)Remaining=0;
        }
        internal void Restock(bool all,int count=1)
        {Stock=all?Capacity:Math.Min(Capacity,Stock+Math.Max(0,count));if(Stock==Capacity)Remaining=0;}
        internal void Receive(int stock,int capacity,float remaining,float interval)
        {Capacity=Math.Max(1,Math.Min(100,capacity));Stock=Math.Max(0,Math.Min(Capacity,stock));Interval=Finite(interval)?Math.Max(.05f,interval):12;Remaining=Stock==Capacity?0:Finite(remaining)?Math.Max(0,remaining):Interval;}
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }

    internal static class BastionUtilityRules
    {
        internal static bool FinitePoint(UnityEngine.Vector3 p)=>
            !(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z));
        internal static int Threat(bool boss,bool elite,bool flying)=>boss?3:elite?2:flying?1:0;
        internal static bool Prefer(int tier,float distance,uint id,int bestTier,float bestDistance,uint bestId)
        {return tier>bestTier||tier==bestTier&&(distance<bestDistance-.001f||Math.Abs(distance-bestDistance)<=.001f&&id<bestId);}
        internal static int ShotsDue(float age,float windup,float duration,int count)
        {
            if(float.IsNaN(age)||age<windup||count<=0)return 0;
            if(age>=windup+duration)return count;
            return Math.Min(count,1+(int)Math.Floor((age-windup)/Math.Max(.01f,duration)*count));
        }
        internal static float Travel(float age,float windup,float duration,float distance)
        {float t=Math.Max(0,Math.Min(1,(age-windup)/Math.Max(.01f,duration)));return distance*t*t*(3-2*t);}
        internal static float TravelSpeed(float age,float dt,float windup,float duration,float distance)
        {return dt<=0?0:(Travel(age+dt,windup,duration,distance)-Travel(age,windup,duration,distance))/dt;}
    }
}
