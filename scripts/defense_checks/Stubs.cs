// Engine stand-ins only. Production damage, absorption, recharge, health
// observation, bank and healing sources are compiled directly into this harness.
using System;
using System.Collections.Generic;
using MonoMod.Cil;
namespace UnityEngine
{
    public class Object{public static implicit operator bool(Object value)=>value!=null;}
    public class GameObject:Object{public Transform transform=new();}
    public class Transform{public Vector3 position;}
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>default;
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public Vector3 normalized{get{float n=MathF.Sqrt(x*x+y*y+z*z);return n>0?new(x/n,y/n,z/n):default;}}
    }
    public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp01(float value)=>Math.Clamp(value,0,1);}
    public static class Time{public static float time;}
}
namespace UnityEngine.Networking{public static class NetworkServer{public static bool active=true;}}
namespace On.RoR2
{
    public static class HealthComponent{public delegate void orig_TakeDamage(global::RoR2.HealthComponent hp,global::RoR2.DamageInfo info);}
    public static class CharacterBody
    {
        public delegate void orig_RecalculateStats(global::RoR2.CharacterBody body);
        public delegate void hook_RecalculateStats(orig_RecalculateStats orig,global::RoR2.CharacterBody body);
        public static event hook_RecalculateStats RecalculateStats;
        public static void Run(orig_RecalculateStats orig,global::RoR2.CharacterBody body){if(RecalculateStats!=null)RecalculateStats(orig,body);else orig(body);}
    }
}
namespace IL.RoR2
{
    public static class HealthComponent{public static event Action<ILContext> TakeDamageProcess;public static void Patch(ILContext context)=>TakeDamageProcess?.Invoke(context);}
    public static class CharacterBody{public static event Action<ILContext> RecalculateStats;public static void Patch(ILContext context)=>RecalculateStats?.Invoke(context);}
}
namespace RoR2
{
    public sealed class DamageInfo
    {
        public float damage;public bool rejected,bypassArmor,blocked;
        public UnityEngine.GameObject attacker;public UnityEngine.Vector3 position,force;
    }
    public sealed class CharacterBody:UnityEngine.Object
    {
        public IvoryBastion.BastionController Controller;
        public HealthComponent healthComponent;
        public float baseMaxShield,levelMaxShield,level=1,armor,cursePenalty=1;
        public float requestedMaxHealth=100,lateMultiplier=1;public bool convertHealth;
        public float maxHealth{get;set;}=100;public float maxShield{get;set;}
        public UnityEngine.Vector3 corePosition;
        public readonly Dictionary<int,int> Buffs=new();
        public T GetComponent<T>() where T:class=>Controller as T;
        public void SetBuffCount(int index,int count)=>Buffs[index]=count;
        // A native-shaped stat routine permits executing the real IL modifier
        // and its observation callbacks without Unity's native object engine.
        public void NativeStats()
        {
            maxHealth=requestedMaxHealth;
            float shield=baseMaxShield+levelMaxShield*level;
            if(convertHealth){shield+=maxHealth*1.5f;maxHealth=1;}
            maxShield=shield;
            maxHealth*=lateMultiplier;maxShield*=lateMultiplier;
            maxHealth/=cursePenalty;maxShield/=cursePenalty;
        }
    }
    public sealed class HealthComponent:UnityEngine.Object
    {
        public CharacterBody body;public float health=100,shield,barrier,LastReportedDamage;
        public bool alive=>health>0;public float combinedHealth=>health+shield;
        public float fullBarrier=>body.maxHealth+body.maxShield;
    }
}
namespace IvoryBastion
{
    public enum BastionFxKind{ShieldHit=2,AegisHit=25,ShieldBreak=26,ShieldRecharge=27}
    public sealed class Buff{public int buffIndex;}
    internal static class BastionContent{internal static Buff FlightShield=new(){buffIndex=1},AegisBarrier=new(){buffIndex=2};}
    public sealed class BastionController:UnityEngine.Object
    {
        internal readonly BastionBank Bank=new();
        internal readonly BastionShieldReserve AbilityShield=new();
        internal readonly BastionAegisReserve AegisBarrier=new();
        public RoR2.CharacterBody Body;public bool Sieged,RestoreFlightShield;
        public bool Alive=>Body&&Body.healthComponent&&Body.healthComponent.alive;
        public float AbilityShieldBase=1,LastShieldHit=-99,LastHurt=-99,LastCombat=-99,LastAegisImpact=-99,LastShieldImpact=-99;
        public int ShieldStatDepth;
        public readonly List<BastionFxKind> Effects=new();
        public void Emit(BastionFxKind kind,UnityEngine.Vector3 a,UnityEngine.Vector3 b,RoR2.CharacterBody target=null,uint id=0,float value=0)=>Effects.Add(kind);
    }
}
