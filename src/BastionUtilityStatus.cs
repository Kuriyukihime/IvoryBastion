using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    public sealed class BastionUtilityStatus:INetMessage
    {
        internal NetworkInstanceId Owner,Target;
        internal uint Sequence,CastId;
        internal int Stock,Capacity,ActiveMines;
        internal int MineStock,MineCapacity;
        internal float Remaining,Interval,Age;
        internal float MineRemaining,MineInterval;
        internal int Shots=32;
        internal float Windup=.12f,Span=.6f,Distance=15,FieldRadius=15;
        internal BastionUtilityKind Kind,CastKind;
        internal Vector3 Direction,End;
        public BastionUtilityStatus(){}
        internal BastionUtilityStatus(BastionUtility utility,uint sequence)
        {Shots=utility.CastShots;Windup=utility.CastWindup;Span=utility.CastSpan;Distance=utility.CastDistance;FieldRadius=utility.CastFieldRadius;ActiveMines=utility.ActiveMines;Owner=utility.Controller.Body.netId;Sequence=sequence;CastId=utility.CastId;Kind=utility.Kind;CastKind=utility.CastKind;Age=utility.Age;Direction=utility.Direction;End=utility.BeamEnd;Target=utility.Target?utility.Target.netId:NetworkInstanceId.Invalid;var reserve=utility.WingburstReserve;Stock=reserve.Stock;Capacity=reserve.Capacity;Remaining=reserve.Remaining;Interval=reserve.Interval;reserve=utility.MinesReserve;MineStock=reserve.Stock;MineCapacity=reserve.Capacity;MineRemaining=reserve.Remaining;MineInterval=reserve.Interval;}
        public void Serialize(NetworkWriter w)
        {w.Write(Owner);w.Write(Sequence);w.Write(CastId);w.Write((byte)Kind);w.Write((byte)CastKind);w.Write(Age);w.Write(Direction);w.Write(End);w.Write(Target);w.Write(Stock);w.Write(Capacity);w.Write(Remaining);w.Write(Interval);w.Write(MineStock);w.Write(MineCapacity);w.Write(MineRemaining);w.Write(MineInterval);w.Write(ActiveMines);w.Write(Shots);w.Write(Windup);w.Write(Span);w.Write(Distance);w.Write(FieldRadius);}
        public void Deserialize(NetworkReader r)
        {Owner=r.ReadNetworkId();Sequence=r.ReadUInt32();CastId=r.ReadUInt32();Kind=(BastionUtilityKind)r.ReadByte();CastKind=(BastionUtilityKind)r.ReadByte();Age=r.ReadSingle();Direction=r.ReadVector3();End=r.ReadVector3();Target=r.ReadNetworkId();Stock=r.ReadInt32();Capacity=r.ReadInt32();Remaining=r.ReadSingle();Interval=r.ReadSingle();MineStock=r.ReadInt32();MineCapacity=r.ReadInt32();MineRemaining=r.ReadSingle();MineInterval=r.ReadSingle();ActiveMines=r.ReadInt32();Shots=r.ReadInt32();Windup=r.ReadSingle();Span=r.ReadSingle();Distance=r.ReadSingle();FieldRadius=r.ReadSingle();}
        private static bool InRange(float value,float min,float max)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=min&&value<=max;
        public void OnReceived()
        {
            if(NetworkServer.active||Kind>BastionUtilityKind.Mines||CastKind>BastionUtilityKind.Mines||float.IsNaN(Age)||float.IsInfinity(Age)||Age<0||!BastionCombat.Finite(Direction)||!BastionUtilityRules.FinitePoint(End))return;
            if(CastId!=0&&(Shots<1||Shots>64||!InRange(Windup,0,1)||!InRange(Span,.01f,32)||!InRange(Distance,0,80)||!InRange(FieldRadius,0,30)))return;
            if(Kind!=BastionUtilityKind.None&&CastKind!=Kind)return;
            var obj=Util.FindNetworkObject(Owner);if(obj)obj.GetComponent<BastionUtility>()?.Receive(this);
        }
    }
}
