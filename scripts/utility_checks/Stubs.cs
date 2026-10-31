// Engine stand-ins for executing the real host utility and mine lifecycle.
// Targeted fixtures include support planes, slopes, steps and a solid wall.
// Actual Unity rendering/physics still need live review.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
namespace UnityEngine
{
    public class Object
    {
        internal bool Dead;
        public static implicit operator bool(Object o)=>o is not null&&!o.Dead;
        public static void Destroy(Object o)
        {
            if(o is null||o.Dead)return;o.Dead=true;
            if(o is GameObject g)foreach(var c in g.Components.OfType<MonoBehaviour>()){c.Dead=true;c.GetType().GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(c,null);}
        }
    }
    public class GameObject:Object
    {
        public bool activeSelf=true;public void SetActive(bool value)=>activeSelf=value;public GameObject(string name=""){}public Transform transform=new();public readonly List<object> Components=new();
        public T GetComponent<T>()=>Components.OfType<T>().FirstOrDefault();
        public T AddComponent<T>() where T:new(){var t=new T();Components.Add(t);if(t is MonoBehaviour m)m.gameObject=this;return t;}
    }
    public class MonoBehaviour:Object
    {public GameObject gameObject=new();public Transform transform=>gameObject.transform;public T GetComponent<T>()=>gameObject.GetComponent<T>();}
    public class Transform:Object
    {
        public string name="";public Vector3 position,forward=Vector3.forward,right=Vector3.right;public float Yaw;
        private Vector3 Rotate(Vector3 p,float degrees){float r=degrees*MathF.PI/180;return new(MathF.Cos(r)*p.x+MathF.Sin(r)*p.z,p.y,-MathF.Sin(r)*p.x+MathF.Cos(r)*p.z);}
        public Vector3 TransformPoint(Vector3 p)=>position+Rotate(p,Yaw);
        public Vector3 InverseTransformPoint(Vector3 p)=>Rotate(p-position,-Yaw);
    }
    public class Collider:MonoBehaviour
    {
        public bool enabled=true;public float Radius;
        public Vector3 ClosestPoint(Vector3 from){var delta=from-transform.position;return delta.magnitude<=Radius?from:transform.position+delta.normalized*Radius;}
        public bool Raycast(Ray ray,out RaycastHit hit,float range)
        {
            hit=default;if(!enabled||!gameObject.activeSelf)return false;
            var delta=ray.origin-transform.position;float along=Vector3.Dot(delta,ray.direction);
            float discriminant=along*along-delta.sqrMagnitude+Radius*Radius;if(discriminant<0)return false;
            float distance=-along-MathF.Sqrt(discriminant);if(distance<0||distance>range)return false;
            hit.distance=distance;hit.point=ray.origin+ray.direction*distance;return true;
        }
    }
    public struct Ray{public Vector3 origin,direction;public Ray(Vector3 origin,Vector3 direction){this.origin=origin;this.direction=direction;}}
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>default;public static Vector3 up=>new(0,1,0);public static Vector3 down=>new(0,-1,0);public static Vector3 forward=>new(0,0,1);public static Vector3 right=>new(1,0,0);
        public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>MathF.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude>.000001f?this/magnitude:default;public void Normalize(){this=normalized;}
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);public static Vector3 operator /(Vector3 a,float b)=>new(a.x/b,a.y/b,a.z/b);
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Math.Clamp(t,0,1);public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n)=>v-n*Dot(v,n);
    }
    public struct RaycastHit{public Vector3 point,normal;public float distance;}
    public enum QueryTriggerInteraction{Ignore}
    public static class Physics
    {
        public static bool Floor=true,RadiusAwareFlight,Wall;
        public static float WallMinX=1,WallMaxX=1.4f,WallHalfZ=3,WallHeight=5;
        public static float FloorSlope,FloorHeight,StepX=float.PositiveInfinity,StepHeight;
        private static float Height(Vector3 p)=>FloorHeight+FloorSlope*p.x+(p.x>=StepX?StepHeight:0);
        private static float SurfaceDistance(Vector3 p)=>(p.y-Height(p))/MathF.Sqrt(1+FloorSlope*FloorSlope);
        private static Vector3 Normal=>new Vector3(-FloorSlope,1,0).normalized;
        private static bool WallSphere(Vector3 center,float radius)
        {
            if(!Wall)return false;var nearest=new Vector3(Math.Clamp(center.x,WallMinX,WallMaxX),Math.Clamp(center.y,0,WallHeight),Math.Clamp(center.z,-WallHalfZ,WallHalfZ));
            return (center-nearest).sqrMagnitude<=radius*radius;
        }
        public static bool Raycast(Vector3 origin,Vector3 direction,out RaycastHit hit,float distance,int mask,QueryTriggerInteraction q)
        {
            hit=default;if(!Floor||direction.y>=0)return false;float down=(origin.y-Height(origin))/-direction.y;
            if(down<0||down>distance)return false;hit.point=origin+direction*down;hit.normal=Normal;return true;
        }
        public static bool CheckSphere(Vector3 center,float radius,int mask,QueryTriggerInteraction q)=>RadiusAwareFlight&&(Floor&&SurfaceDistance(center)<radius-.000001f||WallSphere(center,radius));
        public static bool SphereCast(Vector3 origin,float radius,Vector3 direction,out RaycastHit hit,float distance,int mask,QueryTriggerInteraction q)
        {
            if(!RadiusAwareFlight)return Linecast(origin,origin+direction*distance,out hit,mask,q);
            hit=default;if(!Floor&&!Wall)return false;int steps=Math.Max(1,(int)MathF.Ceiling(distance/.02f));
            for(int i=1;i<=steps;i++){var point=origin+direction*(distance*i/steps);if(Floor&&SurfaceDistance(point)<radius-.000001f||WallSphere(point,radius)){hit.point=point-Normal*radius;hit.normal=Normal;return true;}}
            return false;
        }
        public static bool Linecast(Vector3 a,Vector3 b,int mask,QueryTriggerInteraction q)=>Linecast(a,b,out _,mask,q);
        public static bool Linecast(Vector3 a,Vector3 b,out RaycastHit hit,int mask,QueryTriggerInteraction q)
        {
            hit=default;if(!Floor&&!Wall)return false;bool startsInFloor=Floor&&SurfaceDistance(a)<=0;int steps=Math.Max(1,(int)MathF.Ceiling((b-a).magnitude/.02f));
            for(int i=1;i<=steps;i++){var point=Vector3.Lerp(a,b,(float)i/steps);if(!startsInFloor&&Floor&&SurfaceDistance(point)<0||WallSphere(point,0)){hit.point=point;hit.normal=Normal;return true;}}
            return false;
        }
    }
    public static class Mathf
    {
        public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Abs(float a)=>Math.Abs(a);
        public static float Clamp01(float a)=>Math.Clamp(a,0,1);public static int Clamp(int a,int lo,int hi)=>Math.Clamp(a,lo,hi);public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
        public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
    }
    public static class Application{public static bool isBatchMode;}
    public static class Time{public static float fixedDeltaTime=.02f,time,unscaledTime,timeScale=1;}
}
namespace UnityEngine.Networking
{
    public static class NetworkServer{public static bool active,dontListen;}
    public struct NetworkInstanceId
    {
        public uint Value;public static NetworkInstanceId Invalid=>default;public static bool operator ==(NetworkInstanceId a,NetworkInstanceId b)=>a.Value==b.Value;public static bool operator !=(NetworkInstanceId a,NetworkInstanceId b)=>a.Value!=b.Value;
        public override bool Equals(object o)=>o is NetworkInstanceId b&&this==b;public override int GetHashCode()=>(int)Value;
    }
    public class NetworkWriter
    {
        readonly MemoryStream stream=new();readonly BinaryWriter writer;public NetworkWriter(){writer=new(stream);}
        public void Write(uint v)=>writer.Write(v);public void Write(byte v)=>writer.Write(v);public void Write(int v)=>writer.Write(v);public void Write(float v)=>writer.Write(v);public void Write(NetworkInstanceId v)=>Write(v.Value);public void Write(UnityEngine.Vector3 v){Write(v.x);Write(v.y);Write(v.z);}public byte[] Bytes()=>stream.ToArray();
    }
    public class NetworkReader
    {
        readonly BinaryReader reader;public NetworkReader(byte[] data){reader=new(new MemoryStream(data));}
        public uint ReadUInt32()=>reader.ReadUInt32();public byte ReadByte()=>reader.ReadByte();public int ReadInt32()=>reader.ReadInt32();public float ReadSingle()=>reader.ReadSingle();public NetworkInstanceId ReadNetworkId()=>new(){Value=ReadUInt32()};public UnityEngine.Vector3 ReadVector3()=>new(ReadSingle(),ReadSingle(),ReadSingle());
    }
}
namespace R2API.Networking.Interfaces{public interface INetMessage{void Serialize(UnityEngine.Networking.NetworkWriter w);void Deserialize(UnityEngine.Networking.NetworkReader r);void OnReceived();}}
namespace R2API.Networking
{
    public enum NetworkDestination{Clients}
    public static class Transport{public static readonly List<R2API.Networking.Interfaces.INetMessage> Sent=new();public static void Send(this R2API.Networking.Interfaces.INetMessage m,NetworkDestination d)=>Sent.Add(m);}
}
namespace RoR2
{
    using UnityEngine;using UnityEngine.Networking;
    public class Run:MonoBehaviour{public static Run instance;}
    public static class RoR2Application{public static bool isInLocalMultiPlayer;public static bool isInSinglePlayer=>NetworkServer.active&&NetworkServer.dontListen&&!isInLocalMultiPlayer;public static Action onUpdate;}
    public static class PauseManager{public static bool ScreenOpen;public static bool PauseScreenIsOpen()=>ScreenOpen;}
    public class PauseStopController:MonoBehaviour{public static PauseStopController instance;public bool isPaused;}
    public class GenericSkill:Object{public int bonusStockFromBody;}
    public class SkillLocator:Object{public GenericSkill utility=new();}
    public class CharacterMaster:MonoBehaviour{}
    public class InputBank{public Vector3 aimDirection=Vector3.forward,aimOrigin;}
    public class TeamComponent{public int teamIndex;}
    public class CharacterBody:MonoBehaviour
    {
        public static readonly List<CharacterBody> readOnlyInstancesList=new();public bool hasEffectiveAuthority,isBoss,isChampion,isElite,isFlying,Blocked;
        public int Team{get=>teamComponent.teamIndex;set=>teamComponent.teamIndex=value;}public TeamComponent teamComponent=new();
        public HurtBoxGroup hurtBoxGroup;public CharacterMaster master=new();public SkillLocator skillLocator=new();public InputBank inputBank=new();public HurtBox mainHurtBox;
        public float damage=12,armor,maxHealth=100;public int StatRefreshes;public void MarkAllStatsDirty()=>StatRefreshes++;public Vector3 corePosition;public NetworkInstanceId netId;public HealthComponent healthComponent=new();public List<(object Buff,float Duration)> Buffs=new();
        public bool RollCrit()=>false;public void AddTimedBuff(object buff,float duration)=>Buffs.Add((buff,duration));
    }
    public class CharacterMotor{public Vector3 moveDirection,velocity;}
    public class HealthComponent:MonoBehaviour
    {public bool alive=true;public CharacterBody body;public float combinedHealth=1000,barrier,Damage;public int Hits;public void TakeDamage(DamageInfo info){Hits++;Damage+=info.damage;combinedHealth-=info.damage;}}
    public class HurtBox:MonoBehaviour{public HealthComponent healthComponent;}
    public class HurtBoxGroup:MonoBehaviour{public HurtBox[] hurtBoxes=Array.Empty<HurtBox>();}
    public class DamageInfo{public GameObject attacker,inflictor;public float damage,procCoefficient;public bool crit;public Vector3 position,force;public int procChainMask;}
    public static class RoR2Content{public static class Buffs{public static readonly object Slow50=new(),Nullified=new(),HiddenInvincibility=new();}}
    public enum AttackerFiltering{NeverHitSelf}
    public class BlastAttack
    {
        public enum FalloffModel{None}public class HitPoint{public HurtBox hurtBox;}public class Result{public HitPoint[] hitPoints;}
        public GameObject attacker,inflictor;public int teamIndex;public Vector3 position;public float radius,baseDamage,baseForce,procCoefficient;public bool crit;public FalloffModel falloffModel;public AttackerFiltering attackerFiltering;
        public static readonly List<BlastAttack> Fired=new();
        public Result Fire()
        {
            Fired.Add(this);var hits=new List<HitPoint>();foreach(var body in CharacterBody.readOnlyInstancesList)if(body.Team!=teamIndex&&((body.mainHurtBox?.GetComponent<Collider>()?.ClosestPoint(position)??body.corePosition)-position).magnitude<=radius){body.healthComponent.TakeDamage(new(){damage=baseDamage,procCoefficient=procCoefficient});hits.Add(new(){hurtBox=body.mainHurtBox});}
            return new(){hitPoints=hits.ToArray()};
        }
    }
    public static class LayerIndex{public static class CommonMasks{public const int bullet=1;}public static World world=new();public class World{public int mask=2;}}
    public static class Util{public static readonly Dictionary<uint,GameObject> Objects=new();public static GameObject FindNetworkObject(NetworkInstanceId id)=>Objects.GetValueOrDefault(id.Value);}
}
namespace RoR2
{
    public sealed class DamageReport{public CharacterBody victimBody,attackerBody;}
    public static class GlobalEventManager{public static event Action<DamageReport> onCharacterDeathServer;public static void Killed(DamageReport report)=>onCharacterDeathServer?.Invoke(report);}
}

namespace IvoryBastion
{
    using UnityEngine;using RoR2;
    internal enum BastionFxKind{MineLaunch,MineState,MineExplosion,MineEnd,MineChain,MineChainExplosion,MineArming,Repair,Upgrade}
    public class BastionAnimation:MonoBehaviour{public Transform[] SiegeBones=Array.Empty<Transform>(),MobileBones=Array.Empty<Transform>();internal bool Showcase;internal float ShowcaseUnfold;internal BastionController Owner;public Transform Mobile=new(),Siege=new();}
    public class BastionController:MonoBehaviour
    {
        internal BastionBank Bank=new();internal CharacterBody Body;internal bool Sieged,Deploying;internal bool Alive=>Body&&Body.healthComponent.alive;internal float LastCombat,CooldownScale=1;internal BastionAnimation Model;
        internal int StatusSends;internal void SendStatus()=>StatusSends++;
        internal Vector3 MissileOrigin()=>Body.corePosition;
        internal readonly BastionShieldReserve AbilityShield=new();internal readonly BastionAegisReserve AegisBarrier=new();internal readonly List<BastionUnit> Units=new();
        internal readonly Dictionary<HealthComponent,float> Reservations=new();internal readonly List<(BastionFxKind Kind,uint Id,Vector3 A,Vector3 B,CharacterBody Target,float Value)> Effects=new();
        internal float Recharge(float interval,GenericSkill slot)=>interval*CooldownScale;internal void Emit(BastionFxKind kind,Vector3 a,Vector3 b,CharacterBody target=null,uint id=0,float value=0)=>Effects.Add((kind,id,a,b,target,value));
    }
    internal class BastionUnit:MonoBehaviour{internal BastionController Owner;internal int Slot;}
    internal static class BastionAbilityShield{internal static void Restore(BastionController c,float amount){if(UnityEngine.Networking.NetworkServer.active&&c.Alive)c.AbilityShield.Restore(amount);}}
    internal static class BastionRequestRules{internal static uint Next(ref uint n){n++;if(n==0)n++;return n;}internal static bool Newer(uint a,uint b)=>unchecked((int)(a-b))>0;}
    internal static class BastionCombat
    {
        internal static IEnumerable<CharacterBody> Enemies(CharacterBody owner,Vector3 center,float range)=>CharacterBody.readOnlyInstancesList.Where(b=>Enemy(owner,b)&&(b.corePosition-center).sqrMagnitude<=range*range);
        internal static bool TryMissileRoute(Vector3 from,CharacterBody target,out Vector3 waypoint,out bool loft)
        {
            waypoint=AimPoint(target);loft=false;if(target.Blocked)return false;
            if(ClearFlight(from,waypoint))return true;
            for(int rise=2;rise<=8;rise*=2){waypoint=from+Vector3.up*rise;if(ClearFlight(from,waypoint)&&ClearFlight(waypoint,AimPoint(target))){loft=true;return true;}}
            return false;
        }
        internal static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);internal static bool Enemy(CharacterBody a,CharacterBody b)=>a&&b&&b.healthComponent&&b.healthComponent.alive&&a.Team!=b.Team;
        internal static bool Visible(Vector3 from,CharacterBody b)=>!b.Blocked;internal static bool ClearFlight(Vector3 a,Vector3 b)
        {var delta=b-a;return !Physics.CheckSphere(a,.12f,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)&&(delta.sqrMagnitude<.001f||!Physics.SphereCast(a,.12f,delta.normalized,out _,delta.magnitude,LayerIndex.world.mask,QueryTriggerInteraction.Ignore));}internal static Vector3 AimPoint(CharacterBody body)=>body.corePosition;
    }

    internal sealed class BastionDamageReservation
    {
        BastionController owner;HealthComponent target;float damage;
        internal void Assign(BastionController source,CharacterBody victim,float amount){Release();owner=source;target=victim.healthComponent;damage=BastionBank.PredictedDamage(amount,victim.armor);owner.Reservations.TryGetValue(target,out float prior);owner.Reservations[target]=prior+damage;}
        internal void Release(){if(owner&&!ReferenceEquals(target,null)&&owner.Reservations.TryGetValue(target,out float prior)){float left=prior-damage;if(left<=.01f)owner.Reservations.Remove(target);else owner.Reservations[target]=left;}owner=null;target=null;}
    }

    internal static class BastionServerMicroMissile
    {
        internal static readonly List<(uint Id,CharacterBody Target)> Shots=new();
        internal static void Launch(BastionController owner,CharacterBody target,Vector3 origin,Vector3 direction,uint id,int shot,BastionRushTargetPlan plan)
        {Shots.Add((id,target));plan?.Claim(owner,target,BastionBalance.Get(BastionSetting.WingburstRadius)).Commit();}
    }
}

