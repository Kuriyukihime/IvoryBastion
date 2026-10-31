using System;
using System.Collections.Generic;
using System.Reflection;
namespace UnityEngine
{
 public class Object {internal bool Dead;public static implicit operator bool(Object x)=>x!=null&&!x.Dead;}
 public class Component:Object{public GameObject gameObject;public Transform transform=>gameObject.transform;}
 public class MonoBehaviour:Component{}
 public class Transform:Component{public Vector3 position;}
 public class GameObject:Object
 {
  public static readonly List<GameObject> All=new();public readonly List<Component> Components=new();public Transform transform;public bool Active=true;
  public GameObject(string name){transform=new(){gameObject=this};All.Add(this);}
  public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
  public void SetActive(bool on){if(Active&&!on)foreach(var c in Components)c.GetType().GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(c,null);Active=on;}
 }
 public class LineRenderer:Component
 {
  public int positionCount,numCapVertices,numCornerVertices;public float widthMultiplier;public AnimationCurve widthCurve;public Color Color;public readonly Vector3[] Points=new Vector3[16];
  public void SetPosition(int i,Vector3 p){Points[i]=p;}
 }
 public struct Keyframe{public Keyframe(float t,float v){}}
 public class AnimationCurve{public AnimationCurve(params Keyframe[] keys){}public static AnimationCurve Linear(float a,float b,float c,float d)=>new();}
 public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1);}
 public static class Time{public static float deltaTime;}
 public static class Application{public static bool isBatchMode;}
 public static class Mathf{public const float PI=MathF.PI;public static float Clamp01(float x)=>Math.Clamp(x,0,1);public static float Sin(float x)=>MathF.Sin(x);public static int Max(int a,int b)=>Math.Max(a,b);}
 public struct Vector3
 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>MathF.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude>.000001f?this/magnitude:new();public void Normalize(){this=normalized;}
  public static Vector3 up=>new(0,1,0);public static Vector3 right=>new(1,0,0);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator*(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);public static Vector3 operator/(Vector3 a,float b)=>new(a.x/b,a.y/b,a.z/b);
  public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Math.Clamp(t,0,1);public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
 }
}
namespace IvoryBastion
{
 using UnityEngine;
 internal static class BastionModel{internal static readonly Color Blue=new(.16f,.58f,1);}
 internal static class BastionUtilityFx
 {
  internal static readonly List<LineRenderer> All=new();
  internal static LineRenderer Line(Transform parent,int count,float width,Color color,bool world=true){var l=new GameObject("line").AddComponent<LineRenderer>();l.positionCount=count;l.widthMultiplier=width;l.Color=color;All.Add(l);return l;}
  internal static void Color(LineRenderer line,Color color,float alpha){color.a*=alpha;line.Color=color;}
 }
}
