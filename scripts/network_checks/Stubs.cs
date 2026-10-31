// Engine stand-ins let the actual request/status source run under .NET without
// booting Unity. Damage, effects, clock and native state delivery are observable.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace UnityEngine
{
    public class Object{public static implicit operator bool(Object o)=>o!=null;}
    public sealed class Components
    {
        public readonly List<object> Items=new();
        public void Add(MonoBehaviour item){Items.Add(item);item.Components=this;}
    }
    public class MonoBehaviour:Object
    {
        public Components Components=new();public Transform transform=new();
        public T GetComponent<T>()=>Components.Items.OfType<T>().FirstOrDefault();
        public T[] GetComponents<T>()=>Components.Items.OfType<T>().ToArray();
    }
    public class Transform{public Vector3 forward=Vector3.forward;}
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 up=>new(0,1,0);public static Vector3 forward=>new(0,0,1);
        public float sqrMagnitude=>x*x+y*y+z*z;
        public Vector3 normalized{get{float n=MathF.Sqrt(sqrMagnitude);return n>0?new(x/n,y/n,z/n):default;}}
    }
    public struct Ray{public Vector3 origin,direction;public Ray(Vector3 o,Vector3 d){origin=o;direction=d;}}
    public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float value,float min,float max)=>Math.Clamp(value,min,max);}
    public static class Time{public static float fixedDeltaTime=.02f,unscaledTime,time;}
    public static class Application{public static bool isBatchMode=true;}
}
namespace UnityEngine.Networking
{
    public static class NetworkServer{public static bool active;}
    public static class NetworkClient{public static bool active;}
    public struct NetworkInstanceId
    {
        public uint Value;public static NetworkInstanceId Invalid=>default;
        public static bool operator ==(NetworkInstanceId a,NetworkInstanceId b)=>a.Value==b.Value;
        public static bool operator !=(NetworkInstanceId a,NetworkInstanceId b)=>a.Value!=b.Value;
        public override bool Equals(object o)=>o is NetworkInstanceId id&&id==this;
        public override int GetHashCode()=>Value.GetHashCode();
    }
    public class NetworkWriter
    {
        private readonly MemoryStream stream=new();private readonly BinaryWriter writer;
        public NetworkWriter(){writer=new(stream);}
        public void Write(byte v)=>writer.Write(v);public void Write(bool v)=>writer.Write(v);public void Write(uint v)=>writer.Write(v);
        public void Write(int v)=>writer.Write(v);public void Write(float v)=>writer.Write(v);public void Write(string v)=>writer.Write(v);
        public void Write(ushort v)=>writer.Write(v);public void Write(ulong v)=>writer.Write(v);
        public void Write(NetworkInstanceId v)=>Write(v.Value);public void Write(UnityEngine.Vector3 v){Write(v.x);Write(v.y);Write(v.z);}
        public byte[] Bytes()=>stream.ToArray();
    }
    public class NetworkReader
    {
        private readonly BinaryReader reader;public NetworkReader(byte[] data){reader=new(new MemoryStream(data));}
        public byte ReadByte()=>reader.ReadByte();public bool ReadBoolean()=>reader.ReadBoolean();public uint ReadUInt32()=>reader.ReadUInt32();
        public int ReadInt32()=>reader.ReadInt32();public float ReadSingle()=>reader.ReadSingle();public string ReadString()=>reader.ReadString();
        public ushort ReadUInt16()=>reader.ReadUInt16();public ulong ReadUInt64()=>reader.ReadUInt64();
        public NetworkInstanceId ReadNetworkId()=>new(){Value=ReadUInt32()};public UnityEngine.Vector3 ReadVector3()=>new(ReadSingle(),ReadSingle(),ReadSingle());
    }
}
namespace EntityStates
{
    public class EntityState:UnityEngine.MonoBehaviour
    {
        public virtual void OnEnter(){}public virtual void OnSerialize(UnityEngine.Networking.NetworkWriter w){}public virtual void OnDeserialize(UnityEngine.Networking.NetworkReader r){}
    }
}
namespace RoR2
{
    public class EntityStateMachine:UnityEngine.MonoBehaviour
    {
        public string customName;public Action<EntityStates.EntityState> Deliver;
        public void SetNextState(EntityStates.EntityState state){Deliver?.Invoke(state);}
    }
    public class InputBank{public UnityEngine.Vector3 aimDirection=UnityEngine.Vector3.forward,aimOrigin;}
    public class CharacterBody:UnityEngine.Object
    {
        public bool hasEffectiveAuthority;public InputBank inputBank=new();public UnityEngine.Vector3 corePosition,footPosition;
        public UnityEngine.Networking.NetworkInstanceId netId;public int StatsDirty;
        public UnityEngine.Object master=new();public bool autoCalculateLevelStats=true;
        public float baseMaxHealth=100,levelMaxHealth=30,baseDamage=12,levelDamage=2.4f,baseRegen=1,levelRegen=.2f,baseArmor,levelArmor,baseMoveSpeed=14,sprintingSpeedMultiplier=20f/14,baseAttackSpeed=1,baseCrit=1,maxHealth=100;
        public TestHealth healthComponent=new();
        public void MarkAllStatsDirty(){StatsDirty++;}
        public void RecalculateStats(){maxHealth=baseMaxHealth;healthComponent.fullHealth=maxHealth;}
    }
    public class TestHealth:UnityEngine.Object{public bool alive=true;public float health=100,fullHealth=100;public float Networkhealth{get=>health;set=>health=value;}}
    public class Run:UnityEngine.Object
    {
        public static Run instance;public UnityEngine.Networking.NetworkInstanceId netId;public ulong seed;
        public static event Action<Run> onRunStartGlobal,onRunDestroyGlobal;
        public static void StartRun(Run run){instance=run;onRunStartGlobal?.Invoke(run);}
        public static void EndRun(Run run){onRunDestroyGlobal?.Invoke(run);if(instance==run)instance=null;}
    }
    public static class Util
    {
        public static readonly Dictionary<uint,UnityEngine.MonoBehaviour> Bodies=new();
        public static UnityEngine.MonoBehaviour FindNetworkObject(UnityEngine.Networking.NetworkInstanceId id)=>Bodies.TryGetValue(id.Value,out var o)?o:null;
    }
}
namespace R2API.Networking.Interfaces
{
    public interface INetMessage{void Serialize(UnityEngine.Networking.NetworkWriter w);void Deserialize(UnityEngine.Networking.NetworkReader r);void OnReceived();}
}
namespace R2API.Networking
{
    public enum NetworkDestination{Server,Clients}
    public static class NetworkingAPI{public static void RegisterMessageType<T>(){}}
    public static class NetworkTestTransport
    {
        public static Action<R2API.Networking.Interfaces.INetMessage,NetworkDestination> Deliver;
        public static void Send(this R2API.Networking.Interfaces.INetMessage message,NetworkDestination destination){Deliver?.Invoke(message,destination);}
    }
}
namespace IvoryBastion
{
    // Host ability implementation is exercised by utility_checks. This test
    // double records dispatch while the real request queue/retry source runs.
    public sealed class BastionUtility:UnityEngine.MonoBehaviour
    {
        internal BastionUtilityReserve Reserve=new(){Stock=2,Capacity=2};
        internal bool Active;internal int Cost=>1;internal int Casts;internal bool LastSiege;
        internal bool TryCast(bool siege,UnityEngine.Vector3 direction,out string reason)
        {reason="UTILITY UNAVAILABLE";if(!UnityEngine.Networking.NetworkServer.active||Active||!Reserve.Spend(siege?24:6))return false;Casts++;LastSiege=siege;return true;}
    }
    public enum BastionAction:byte{Pistol,Laser,Missile,Boost,Protocol,Transform,Redline,Saturation}
    public enum BastionFxKind:byte{Boost,Transform,Upgrade}
    public class Thruster{public int Capacity=3;public float Fuel=3,Recovery;public bool Active,Exhausted;}
    internal class BastionBank
    {
        public Thruster Thruster=new();public float Fabrication,Ammo=40,Laser,Protocol,Redline,Saturation,Toggle,DefenseTime,RedlineTime,Construction;
        public int MissileSequence,BarrageSequence,Reserved,ProtocolCapacity=1,SpecialCapacity=1,ProtocolCharges=1,RedlineCharges=1,SaturationCharges=1;
        public int[] Ranks=new int[4],Choices=new int[4],Loadout=new int[3];
        public int Buys;public float AmmoRate(bool s)=>1;
        public bool Buy(int b,int choice,bool s){if(!s||b<0||b>=4||Fabrication<60)return false;Fabrication-=60;Buys++;Ranks[b]++;return true;}
        public bool SetUnit(int slot,int kind,bool s){if(!s||slot<0||slot>=3||kind<0||kind>3)return false;Loadout[slot]=kind;return true;}
    }
    public class Model{public int Cancels;public void CancelTransform(){Cancels++;}}
    public sealed class BastionController:UnityEngine.MonoBehaviour
    {
        internal BastionBank Bank=new();public RoR2.CharacterBody Body=new();public bool Alive=true,Sieged,Deploying,BoostRequested,TransformTargetSiege,Initialized=true;
        public UnityEngine.Vector3 Anchor;public Model Model=new();internal BastionShieldReserve AbilityShield=new();internal BastionAegisReserve AegisBarrier=new();internal BastionTransformSync TransformSync=new();
        public string Notice="";public float NoticeUntil;public int Shots,Secondary,Special,ModeChanges,Transforms,BoostEffects,Upgrades,Statuses,Applies;
        public BastionRequests Requests=>GetComponent<BastionRequests>();public Action OnStatus;
        internal Queue<BastionTransformResult> GroundResults=new();
        public bool EnsureInitialized()=>Initialized;
        public float Cooldown(BastionAction a)=>a==BastionAction.Transform?Bank.Toggle:a==BastionAction.Protocol?Bank.Protocol:a==BastionAction.Redline?Bank.Redline:Bank.Saturation;
        internal BastionTransformResult SetSiege(bool desired)
        {
            if(!UnityEngine.Networking.NetworkServer.active||!Alive)return BastionTransformResult.Unavailable;
            if(Sieged==desired)return BastionTransformResult.Applied;
            if(Bank.Toggle>0)return BastionTransformResult.Cooldown;
            var result=GroundResults.Count>0?GroundResults.Dequeue():BastionTransformResult.Applied;
            if(result!=BastionTransformResult.Applied)return result;
            Sieged=desired;Deploying=false;Bank.Toggle=6;ModeChanges++;return result;
        }
        public void Shoot(BastionAction a,UnityEngine.Ray aim){if(!UnityEngine.Networking.NetworkServer.active||!Alive||Deploying)return;Shots++;if(a==BastionAction.Missile)Bank.Ammo--;}
        public void ActivateSecondary(){if(!UnityEngine.Networking.NetworkServer.active||!Alive||Bank.ProtocolCharges<=0)return;Secondary++;Bank.ProtocolCharges--;Bank.Protocol=18;}
        public void ActivateSpecial(){if(!UnityEngine.Networking.NetworkServer.active||!Alive)return;Special++;if(Sieged){Bank.SaturationCharges--;Bank.Saturation=60;}else{Bank.RedlineCharges--;Bank.Redline=40;}}
        public void Emit(BastionFxKind kind,UnityEngine.Vector3 a,UnityEngine.Vector3 b,RoR2.CharacterBody target=null,uint id=0,float value=0){if(kind==BastionFxKind.Transform)Transforms++;if(kind==BastionFxKind.Boost)BoostEffects++;if(kind==BastionFxKind.Upgrade)Upgrades++;}
        public void SendStatus(){Statuses++;OnStatus?.Invoke();}
        public void ApplyMode(bool relocate,UnityEngine.Vector3? anchor=null){Applies++;}
    }
    internal static class BastionCombat{internal static bool Finite(UnityEngine.Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);}
    internal static class BastionFx{internal static void Play(BastionEffect fx){}}
    internal static class BastionBalanceConfig
    {
        internal static float[] Saved=BastionBalance.Defaults();
        internal static float[] Capture()=>(float[])Saved.Clone();
    }
    internal static class BastionBalanceText{internal static void Refresh(){}}
    public enum BastionUnitKind:byte{Guardian,Gunner,Interceptor,Repair}
    public class BastionUnit:UnityEngine.MonoBehaviour{public BastionUnitKind Kind;}
    internal static class BastionUnitRules
    {internal static float Armor(BastionUnitKind kind)=>BastionBalance.Get(kind==BastionUnitKind.Guardian?BastionSetting.BulwarkArmor:kind==BastionUnitKind.Gunner?BastionSetting.GunnerArmor:kind==BastionUnitKind.Interceptor?BastionSetting.InterceptorArmor:BastionSetting.RepairArmor);}
}
