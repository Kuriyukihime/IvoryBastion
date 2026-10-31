using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NV=System.Numerics.Vector3;
using NQ=System.Numerics.Quaternion;
namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;public string name;
        public static implicit operator bool(Object x)=>x is not null&&!x.Destroyed;
        public static void Destroy(Object obj)
        {
            if(!obj)return;
            if(obj is GameObject go)
            {
                go.Destroyed=true;
                foreach(var p in go.parts.Values.ToArray()){p.GetType().GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(p,null);p.Destroyed=true;}
                foreach(var child in go.transform.children.ToArray())Destroy(child.gameObject);
            }
            else obj.Destroyed=true;
        }
    }
    public class Component:Object
    {
        public GameObject gameObject;public Transform transform=>gameObject.transform;
        public new string name=>gameObject.name;
        public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>() where T:Component=>gameObject.GetComponentsInChildren<T>();
    }
    public class MonoBehaviour:Component{}
    public class GameObject:Object
    {
        internal readonly Dictionary<Type,Component> parts=new();public readonly Transform transform;
        public GameObject(string name="Object"){this.name=name;transform=new Transform{gameObject=this};parts[typeof(Transform)]=transform;}
        public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};parts[typeof(T)]=c;return c;}
        public T GetComponent<T>() where T:Component=>parts.TryGetValue(typeof(T),out var c)?(T)c:null;
        public T[] GetComponentsInChildren<T>() where T:Component=>parts.Values.OfType<T>().Concat(transform.children.SelectMany(t=>t.gameObject.GetComponentsInChildren<T>())).ToArray();
    }
    public class Transform:Component
    {
        public Transform parent;internal readonly List<Transform> children=new();public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;
        public Vector3 position{get=>parent?parent.TransformPoint(localPosition):localPosition;set=>localPosition=parent?parent.InverseTransformPoint(value):value;}
        public Quaternion rotation{get=>parent?parent.rotation*localRotation:localRotation;set=>localRotation=parent?Quaternion.Inverse(parent.rotation)*value:value;}
        public Vector3 right=>rotation*Vector3.right;public Vector3 up=>rotation*Vector3.up;public Vector3 forward=>rotation*Vector3.forward;
        public void SetParent(Transform p,bool world=false){var previous=position;parent?.children.Remove(this);parent=p;p?.children.Add(this);if(world)position=previous;}
        public Vector3 TransformPoint(Vector3 p){p=localPosition+localRotation*Vector3.Scale(p,localScale);return parent?parent.TransformPoint(p):p;}
        public Vector3 InverseTransformPoint(Vector3 p){if(parent)p=parent.InverseTransformPoint(p);p=Quaternion.Inverse(localRotation)*(p-localPosition);return new(p.x/localScale.x,p.y/localScale.y,p.z/localScale.z);}
        public Vector3 TransformDirection(Vector3 p)=>rotation*p;public Vector3 InverseTransformDirection(Vector3 p)=>Quaternion.Inverse(rotation)*p;
    }
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new(0,0,0);public static Vector3 one=>new(1,1,1);public static Vector3 up=>new(0,1,0);public static Vector3 down=>new(0,-1,0);public static Vector3 right=>new(1,0,0);public static Vector3 forward=>new(0,0,1);
        public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>MathF.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude>.000001f?this/magnitude:zero;
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator -(Vector3 a)=>a*-1;public static Vector3 operator *(Vector3 a,float s)=>new(a.x*s,a.y*s,a.z*s);public static Vector3 operator /(Vector3 a,float s)=>new(a.x/s,a.y/s,a.z/s);
        public static Vector3 Scale(Vector3 a,Vector3 b)=>new(a.x*b.x,a.y*b.y,a.z*b.z);public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Math.Clamp(t,0,1);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static Vector3 ProjectOnPlane(Vector3 v,Vector3 normal)=>v-normal*Dot(v,normal);
        public override string ToString()=>$"({x},{y},{z})";
    }
    public struct Quaternion
    {
        internal NQ q;private Quaternion(NQ value){q=value;}
        public static Quaternion identity=>new(NQ.Identity);public static Quaternion Inverse(Quaternion a)=>new(NQ.Inverse(a.q));
        public static Quaternion Euler(float x,float y,float z)=>new(NQ.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180));
        public static Quaternion AngleAxis(float angle,Vector3 axis)=>new(NQ.CreateFromAxisAngle(new NV(axis.x,axis.y,axis.z),angle*MathF.PI/180));
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>new(NQ.Slerp(a.q,b.q,Math.Clamp(t,0,1)));
        public static Quaternion FromToRotation(Vector3 a,Vector3 b)
        {a=a.normalized;b=b.normalized;float dot=Vector3.Dot(a,b);if(dot>.99999)return identity;if(dot<-.99999)return AngleAxis(180,MathF.Abs(a.y)<.9f?Vector3.up:Vector3.right);var c=Vector3.Cross(a,b);return new(NQ.Normalize(new NQ(c.x,c.y,c.z,1+dot)));}
        public static Quaternion LookRotation(Vector3 direction)=>FromToRotation(Vector3.forward,direction);
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new(a.q*b.q);
        public static Vector3 operator *(Quaternion a,Vector3 v){var r=NV.Transform(new NV(v.x,v.y,v.z),a.q);return new(r.X,r.Y,r.Z);}
    }
    public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1);public static Color operator *(Color c,float f)=>new(c.r*f,c.g*f,c.b*f,c.a*f);}
    public class Material:Object{public Material(string name){this.name=name;}}
    public class MaterialPropertyBlock{internal readonly Dictionary<string,float> floats=new();internal readonly Dictionary<string,Color> colors=new();public void SetFloat(string n,float v)=>floats[n]=v;public void SetColor(string n,Color v)=>colors[n]=v;}
    public class Renderer:Component{public bool enabled=true;public Material sharedMaterial=new("MineBlue");internal Dictionary<string,float> Floats=new();internal Dictionary<string,Color> Colors=new();public void SetPropertyBlock(MaterialPropertyBlock p){Floats=new(p.floats);Colors=new(p.colors);}}
    public struct BoneWeight{public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;public float weight0,weight1,weight2,weight3;}
    public class Mesh:Object
    {
        private Vector3[] data=Array.Empty<Vector3>();public Vector3[] vertices{get=>(Vector3[])data.Clone();set=>data=(Vector3[])value.Clone();}
        public int[] triangles=Array.Empty<int>();public BoneWeight[] boneWeights=Array.Empty<BoneWeight>();public bool isReadable=true;
    }
    public class MeshFilter:Component{public Mesh sharedMesh;}
    public class MeshRenderer:Renderer{}
    public class SkinnedMeshRenderer:Renderer
    {
        public Mesh sharedMesh;public Transform rootBone;public Transform[] bones=Array.Empty<Transform>();public static int Bakes;public bool FailBake;
        public void BakeMesh(Mesh destination){Bakes++;if(FailBake)throw new InvalidOperationException("unavailable skin");destination.vertices=sharedMesh.vertices;destination.triangles=sharedMesh.triangles;}
    }
    public class LineRenderer:Renderer{public Vector3[] points;public float width;public Color color;public void SetPosition(int i,Vector3 p)=>points[i]=p;}
    public class Collider:Component{public Bounds bounds=>new(transform.position+Vector3.up,new Vector3(1,2,1));}
    public struct Bounds{public Vector3 center,size;public Bounds(Vector3 center,Vector3 size){this.center=center;this.size=size;}public Vector3 max=>center+size*.5f;}
    public struct RaycastHit{public Vector3 point,normal;}
    public enum QueryTriggerInteraction{Ignore}
    public static class Physics{public static bool Linecast(Vector3 a,Vector3 b,out RaycastHit hit,int mask,QueryTriggerInteraction q){hit=default;return false;}}
    public class Camera:Component{public static Camera main;}
    public static class Time{public static float time,deltaTime=.02f;}
    public static class Application{public static bool isBatchMode;}
    public static class Mathf
    {
        public const float PI=MathF.PI;public static float Sin(float v)=>MathF.Sin(v);public static float Cos(float v)=>MathF.Cos(v);public static float Exp(float v)=>MathF.Exp(v);public static float Max(float a,float b)=>MathF.Max(a,b);public static float Abs(float v)=>MathF.Abs(v);
        public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);public static int FloorToInt(float v)=>(int)MathF.Floor(v);public static float MoveTowards(float a,float b,float delta)=>a+MathF.Sign(b-a)*MathF.Min(MathF.Abs(b-a),delta);
    }
}
namespace UnityEngine.Networking{public struct NetworkInstanceId{public uint Value;public NetworkInstanceId(uint value){Value=value;}public static NetworkInstanceId Invalid=>new(0);}}
namespace RoR2
{
    using UnityEngine;
    public class HealthComponent:Component{public bool alive=true;}
    public class HurtBox:Component{}
    public class CharacterBody:Component{public Vector3 corePosition=>transform.position+Vector3.up;public float radius=1;public bool isBoss,isChampion,isElite;public HealthComponent healthComponent;public HurtBox mainHurtBox;}
    public class CharacterModel:Component{public RendererInfo[] baseRendererInfos;public class RendererInfo{public Renderer renderer;}}
    public class SceneCamera:Component{public static Camera _camera;}
    public static class Util{public static readonly Dictionary<uint,GameObject> objects=new();public static GameObject FindNetworkObject(UnityEngine.Networking.NetworkInstanceId id)=>objects.TryGetValue(id.Value,out var g)?g:null;}
    public struct LayerIndex{public int mask;public static LayerIndex world=>new(){mask=1};}
}
namespace IvoryBastion
{
    using UnityEngine;using UnityEngine.Networking;
    internal enum BastionSetting{MineLifetime,MineAirTime}
    internal static class BastionBalance{internal static float Get(BastionSetting key)=>key==BastionSetting.MineLifetime?60:1.1f;}
    public sealed class BastionEffect{public NetworkInstanceId Owner,Target;public uint Id;public Vector3 A,B;public float Value;}
    internal static class BastionServerSpiderMine{internal static bool Ground(Vector3 p,out Vector3 ground,float depth=24){ground=new(p.x,0,p.z);return true;}}
    internal static class BastionModel
    {
        internal static Color Blue=>new(.15f,.65f,1);
        internal static void Rig(GameObject obj,string asset,float scale,List<RoR2.CharacterModel.RendererInfo> infos,out Transform[] bones)
        {
            var joints=new List<Transform>();for(int i=0;i<8;i++)foreach(string prefix in new[]{"mine_hip_","mine_knee_"}){var b=new GameObject(prefix+i).transform;b.SetParent(obj.transform);joints.Add(b);}bones=joints.ToArray();obj.AddComponent<Renderer>();
        }
    }
    internal static class BastionUtilityFx
    {
        internal static LineRenderer Line(Transform parent,int count,float width,Color color){var go=new GameObject("mark-line");go.transform.SetParent(parent);var l=go.AddComponent<LineRenderer>();l.points=new Vector3[count];l.width=width;l.color=color;return l;}
        internal static void Color(LineRenderer line,Color color,float alpha){color.a*=alpha;line.color=color;}
    }
    internal enum BastionUtilityKind{None,Mines}
    public sealed class BastionUtility:Component{internal BastionUtilityKind Kind;}
    public sealed class BastionAnimation:Component{public RoR2.CharacterBody Owner;}
}
