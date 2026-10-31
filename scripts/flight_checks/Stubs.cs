// Headless adapter for the production BastionMain, not a second flight state.
// Motor.PreMove follows the installed game's independently checked contract.
using System;
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object o)=>o is not null; }
    public class Transform { public Vector3 forward=Vector3.forward; }
    public static class Time { public static float fixedDeltaTime=.02f; }
    public static class Mathf
    {
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Exp(float x)=>MathF.Exp(x);
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new();public static Vector3 up=>new(0,1,0);public static Vector3 forward=>new(0,0,1);public static Vector3 right=>new(1,0,0);
        public float sqrMagnitude=>x*x+y*y+z*z;
        public float magnitude=>MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized=>magnitude>1e-6f?this/magnitude:zero;
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float s)=>new(a.x*s,a.y*s,a.z*s);
        public static Vector3 operator /(Vector3 a,float s)=>new(a.x/s,a.y/s,a.z/s);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n)=>v-n*(Dot(v,n)/Dot(n,n));
        public static Vector3 ClampMagnitude(Vector3 v,float limit)=>v.sqrMagnitude>limit*limit?v.normalized*limit:v;
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Math.Clamp(t,0,1);
        public static Vector3 MoveTowards(Vector3 a,Vector3 b,float max)=>a+ClampMagnitude(b-a,max);
    }
}
namespace RoR2
{
    using UnityEngine;
    public struct CharacterFlightParameters { public int channeledFlightGranterCount; }
    public struct CharacterGravityParameters { public int channeledAntiGravityGranterCount,environmentalAntiGravityGranterCount,antiGravityNeutralizerCount; }
    public struct Grounding { public bool IsStableOnGround; }
    public class InnerMotor
    {
        public Grounding GroundingStatus,LastGroundingStatus;
        public bool GroundSolving=true;
        public int Ungrounds;
        public void SetGroundSolvingActivation(bool value)=>GroundSolving=value;
        public void ForceUnground(){Ungrounds++;GroundingStatus.IsStableOnGround=false;}
    }
    public class CharacterMotor:Object
    {
        public float gravityScale=1,airControl=.25f;
        public bool useCustomGravity=true;
        public float CustomGravity=-35,gravityMultiplier=1;
        public CharacterFlightParameters flightParameters;
        public CharacterGravityParameters gravityParameters;
        public Vector3 moveDirection,velocity;
        public InnerMotor Motor=new();
        public int GroundTransitions;
        public bool useGravity=>gravityParameters.environmentalAntiGravityGranterCount<=0&&(gravityParameters.antiGravityNeutralizerCount>0||gravityParameters.channeledAntiGravityGranterCount<=0);
        public bool isFlying=>flightParameters.channeledFlightGranterCount>0;
        public void PostGroundingUpdate(float dt){GroundTransitions++;Motor.LastGroundingStatus=Motor.GroundingStatus;}
        public void PreMove(float speed,float dt)
        {
            Vector3 v=moveDirection;if(!isFlying)v.y=0;
            Vector3 target=v*speed;if(!isFlying)target.y=velocity.y;
            velocity=Vector3.MoveTowards(velocity,target,80*airControl*dt);
            if(useGravity)velocity.y+=(useCustomGravity?CustomGravity:-30*gravityScale)*gravityMultiplier*dt;
            if(Motor.GroundSolving&&Motor.GroundingStatus.IsStableOnGround)velocity.y=Math.Max(0,velocity.y);
        }
    }
    public class CharacterBody { public float moveSpeed=14; }
    public class CharacterDirection:Object { public Vector3 forward; }
    public class InputBank { public Button jump=new(); }
    public class Button { public bool down; }
}
namespace IvoryBastion
{
    public class BastionController:UnityEngine.Object { public bool Alive=true,Sieged,Deploying,Boosting; }
    // Utility motion itself is exercised from production source by utility_checks.
    public class BastionUtility:UnityEngine.Object { public bool Move(RoR2.CharacterMotor motor)=>false; }
}
namespace EntityStates
{
    using UnityEngine;using RoR2;
    public class GenericCharacterMain
    {
        public CharacterMotor characterMotor=new();public CharacterBody characterBody=new();
        public CharacterDirection characterDirection=new();public Transform transform=new();public InputBank inputBank=new();
        public Vector3 moveVector,aimDirection=Vector3.forward;public bool jumpInputReceived,isAuthority=true;
        public IvoryBastion.BastionController Controller=new();
        protected T GetComponent<T>() where T:class=>Controller as T;
        public virtual void OnEnter(){}
        public virtual void OnExit(){if(isAuthority)characterMotor.moveDirection=Vector3.zero;}
        public virtual void HandleMovements()
        {characterMotor.moveDirection=moveVector;if(isAuthority&&jumpInputReceived){characterMotor.velocity.y+=12;characterMotor.Motor.ForceUnground();}}
    }
}
