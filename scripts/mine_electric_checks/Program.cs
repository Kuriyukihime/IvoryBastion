using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;
using IvoryBastion;
using UnityEngine;
static class Program
{
 static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic;
 static int[] Active=>(int[])typeof(BastionMineChainVisual).GetField("active",Hidden).GetValue(null);
 static BastionMineChainVisual[] Visuals=>GameObject.All.SelectMany(g=>g.Components).OfType<BastionMineChainVisual>().ToArray();
 static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
 static void Main()
 {
  int[] counts={BastionMineChainGeometry.ArcCount,BastionMineChainGeometry.ForkCount,BastionMineChainGeometry.CoreCount};int[] points={BastionMineChainGeometry.ArcPoints,BastionMineChainGeometry.ForkPoints,BastionMineChainGeometry.CorePoints};
  for(int layer=0;layer<3;layer++)foreach(float r in new[]{.001f,.01f,.1f,1,8,25,1000})for(int n=0;n<=100;n++)
  {
   float age=n/100f,width=BastionMineChainGeometry.BurstWidth(layer,age,r);Check(float.IsFinite(width)&&width>=0&&width<=r*2,"finite radius-bounded width");
   for(int index=0;index<counts[layer];index++)
   {
    float alpha=BastionMineChainGeometry.Opacity(layer,index,age);Check(float.IsFinite(alpha)&&alpha>=0&&alpha<=1,"finite bounded opacity");
    if(n==100)Check(alpha==0,"all electrical filaments finish invisible");
    for(int p=0;p<points[layer];p++)
    {
     var v=Point(layer,index,p,age,r);Check(Finite(v),"finite procedural point");Check(v.magnitude+width*.5f<=r+.001,"full visible half-width stays inside real radius");
    }
   }
  }
  foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1,0})for(int layer=0;layer<3;layer++)
  {var v=Point(layer,-777,int.MaxValue,invalid,invalid);Check(Finite(v)&&v.magnitude==0,"invalid inputs collapse safely");Check(float.IsFinite(BastionMineChainGeometry.BurstWidth(layer,invalid,invalid)),"invalid width is finite");}
  for(int i=0;i<BastionMineChainGeometry.CoreCount;i++)for(int a=0;a<=10;a++)Check((Point(2,i,0,a/10f,8)-Point(2,i,BastionMineChainGeometry.CorePoints-1,a/10f,8)).magnitude<.00001f,"plasma knots close without seams");
  for(int i=0;i<BastionMineChainGeometry.ForkCount;i++)for(int a=0;a<=10;a++)Check((Point(1,i,0,a/10f,8)-Point(0,(i*3+1)%BastionMineChainGeometry.ArcCount,4,a/10f,8)).magnitude<.00001f,"electrical forks start on real trunk points");
  foreach(var at in new[]{new Vector3(float.NaN,0,0),new Vector3(float.PositiveInfinity,0,0)})BastionMineChainVisual.ChargedDetonation(at,8);
  foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-1,0,1001})BastionMineChainVisual.ChargedDetonation(new(),invalid);
  Check(Active.All(x=>x==0)&&Visuals.Length==0,"invalid visual events allocate nothing");
  Application.isBatchMode=true;BastionMineChainVisual.ChargedDetonation(new(),8);Check(Visuals.Length==0,"headless server allocates no visual objects");Application.isBatchMode=false;
  for(int i=0;i<30;i++)BastionMineChainVisual.ChargedDetonation(new(),8);for(int i=0;i<30;i++)BastionMineChainVisual.Link(new(),new(1,2,3));
  Check(Active[1]==16&&Active[0]==24&&Visuals.Length==40,"existing16 detonation/24 link caps are preserved");
  foreach(var fx in Visuals)
  {
   int style=(int)typeof(BastionMineChainVisual).GetField("style",Hidden).GetValue(fx);
   Check(((LineRenderer[])typeof(BastionMineChainVisual).GetField("lines",Hidden).GetValue(fx)).Count(l=>l!=null)==(style==0?4:counts.Sum()),"pool builds correct fixed renderer budget");
  }
  Time.deltaTime=.03f;
  for(int frame=0;frame<20;frame++)foreach(var fx in Visuals.Where(v=>v.gameObject.Active))
  {
   typeof(BastionMineChainVisual).GetMethod("Update",Hidden).Invoke(fx,null);
   foreach(var line in (LineRenderer[])typeof(BastionMineChainVisual).GetField("lines",Hidden).GetValue(fx))if(line!=null)
   {Check(float.IsFinite(line.widthMultiplier)&&line.widthMultiplier>=0,"actual renderer uses finite widths");for(int p=0;p<line.positionCount;p++)Check(Finite(line.Points[p]),"actual renderer receives finite positions");}
  }
  Check(Active.All(x=>x==0)&&Visuals.All(v=>!v.gameObject.Active),"all effects clean up at finite lifetimes");
  int built=GameObject.All.Count;BastionMineChainVisual.Burst(new(3,5,7),8);BastionMineChainVisual.Link(new(),new(4,3,2));Check(GameObject.All.Count==built&&Active[1]==1&&Active[0]==1,"completed effects reuse existing pools including compatibility Burst alias");
  Time.deltaTime=1;foreach(var fx in Visuals.Where(v=>v.gameObject.Active))typeof(BastionMineChainVisual).GetMethod("Update",Hidden).Invoke(fx,null);Check(Active.All(x=>x==0),"reused effects release active accounting exactly once");
  var frames=new List<object>();foreach(float age in new[]{0f,.08f,.16f,.26f,.42f})
  {var lines=new List<object>();for(int layer=0;layer<3;layer++)for(int i=0;i<counts[layer];i++)lines.Add(new{Layer=layer,Index=i,Width=BastionMineChainGeometry.BurstWidth(layer,age/.5f,8),Alpha=BastionMineChainGeometry.Opacity(layer,i,age/.5f),Points=Enumerable.Range(0,points[layer]).Select(p=>{var v=Point(layer,i,p,age/.5f,8);return new[]{v.x,v.y,v.z};}).ToArray()});frames.Add(new{Age=age,Lines=lines});}
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));var output=Path.Combine(root,".build/mine-electric-proof/geometry.json");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonSerializer.Serialize(new{Radius=8,Frames=frames},new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine($"PASS: {checks:N0} production electric geometry, full-width physical bounds, invalid input, native renderer population, cap, cleanup and pooled reuse checks.");
 }
 static Vector3 Point(int layer,int i,int p,float t,float r)=>layer==0?BastionMineChainGeometry.Arc(i,p,t,r):layer==1?BastionMineChainGeometry.Fork(i,p,t,r):BastionMineChainGeometry.Core(i,p,t,r);
}
