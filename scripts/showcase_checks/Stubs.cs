using System;
using System.Collections.Generic;
using NVector = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object x)=>x is not null; }
    public class Component:Object
    {
        internal GameObject gameObject;
        public Transform transform=>gameObject.transform;
        public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();
    }
    public class MonoBehaviour:Component { }
    public class GameObject:Object
    {
        private readonly Dictionary<Type,Component> parts=new();
        public readonly Transform transform;
        public GameObject(){transform=new Transform{gameObject=this};parts[typeof(Transform)]=transform;}
        public T AddComponent<T>() where T:Component,new(){var p=new T{gameObject=this};parts[typeof(T)]=p;return p;}
        public T GetComponent<T>() where T:Component=>parts.TryGetValue(typeof(T),out var p)?(T)p:null;
    }
    public class Transform:Component
    {
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation=Quaternion.identity;
        public Vector3 localScale=Vector3.one;
        public Vector3 position{get=>parent?parent.TransformPoint(localPosition):localPosition;set=>localPosition=parent?parent.InverseTransformPoint(value):value;}
        public Quaternion rotation{get=>parent?parent.rotation*localRotation:localRotation;set=>localRotation=parent?Quaternion.Inverse(parent.rotation)*value:value;}
        public Vector3 right=>rotation*Vector3.right;
        public Vector3 TransformPoint(Vector3 p)=>position+rotation*Vector3.Scale(p,localScale);
        public Vector3 InverseTransformPoint(Vector3 p){var q=Quaternion.Inverse(rotation)*(p-position);return new(q.x/localScale.x,q.y/localScale.y,q.z/localScale.z);}
    }
    public class Camera:Component{public static Camera main;}
    public static class Time{public static float time,unscaledTime;}
    public static class Application{public static bool isBatchMode;}
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new(0,0,0);public static Vector3 one=>new(1,1,1);public static Vector3 right=>new(1,0,0);
        public float magnitude=>MathF.Sqrt(x*x+y*y+z*z);
        public Vector3 normalized=>magnitude>0?this/magnitude:zero;
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float s)=>new(a.x*s,a.y*s,a.z*s);
        public static Vector3 operator /(Vector3 a,float s)=>new(a.x/s,a.y/s,a.z/s);
        public static Vector3 Scale(Vector3 a,Vector3 b)=>new(a.x*b.x,a.y*b.y,a.z*b.z);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        public static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;
        public override string ToString()=>$"({x},{y},{z})";
    }
    public struct Quaternion
    {
        private NQuaternion q;
        private Quaternion(NQuaternion value){q=value;}
        public static Quaternion identity=>new(NQuaternion.Identity);
        public static Quaternion Euler(float x,float y,float z)=>new(NQuaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180));
        public static Quaternion Inverse(Quaternion a)=>new(NQuaternion.Inverse(a.q));
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new(a.q*b.q);
        public static Vector3 operator *(Quaternion a,Vector3 v){var r=NVector.Transform(new(v.x,v.y,v.z),a.q);return new(r.X,r.Y,r.Z);}
    }
    public static class Mathf
    {
        public static float Abs(float v)=>MathF.Abs(v);
        public static float Max(float a,float b)=>MathF.Max(a,b);
        public static float Clamp(float v,float min,float max)=>Math.Clamp(v,min,max);
        public static float Clamp01(float v)=>Clamp(v,0,1);
        public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
        public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
        public static float Pow(float x,float y)=>MathF.Pow(x,y);
    }
}
namespace RoR2
{
    using UnityEngine;
    public class Run:Object{public static Run instance;}
    public class CharacterBody:Component{public Vector3 corePosition;}
    public class LocalUser{public CharacterBody cachedBody;}
    public static class LocalUserManager{public static LocalUser User;public static LocalUser GetFirstLocalUser()=>User;}
    public class SceneCamera:Component
    {
        public static event Action<SceneCamera> onSceneCameraPreCull;
        public static Camera _camera;
        public Camera camera;
        public static int Subscribers=>onSceneCameraPreCull?.GetInvocationList().Length??0;
        public void Cull()=>onSceneCameraPreCull?.Invoke(this);
    }
}
namespace IvoryBastion
{
    using UnityEngine;
    public sealed class BastionAnimation:Component{public RoR2.CharacterBody Owner;public bool Showcase;public float ShowcaseUnfold;}
    internal sealed class Setting<T>{public T Value;public Setting(T value){Value=value;}}
    internal static class BastionPlugin
    {
        internal static readonly Setting<float> AudioVolume=new(1);
        internal static readonly FakeLog Log=new();
    }
    internal sealed class FakeLog{internal readonly List<string> Messages=new();internal void LogWarning(object x)=>Messages.Add(x.ToString());}
    internal static class BastionPresentationSettings
    {
        internal static readonly Setting<float> EffectsVolume=new(1),DialogueVolume=new(1),AlertVolume=new(1),DialogueSpacing=new(8),DialogueRepeatCooldown=new(30);
    }
    internal static class BastionGameAudio{internal static float Gain=1;}
    internal interface IBastionAudioOutput:IDisposable{bool IsRunning{get;}}
    internal sealed class FakeOutput:IBastionAudioOutput
    {
        internal bool Running=true,Disposed;
        internal Action<string> Failure;
        public bool IsRunning=>Running&&!Disposed;
        public void Dispose(){Disposed=true;Running=false;}
        internal void Fail(){Running=false;Failure?.Invoke("simulated output loss");}
    }
    internal static class BastionAudioOutput
    {
        internal static readonly List<FakeOutput> Opened=new();
        internal static IBastionAudioOutput Open(BastionPcmMixer mixer,Action<string> error)
        {var p=new FakeOutput{Failure=error};Opened.Add(p);return p;}
    }
}
