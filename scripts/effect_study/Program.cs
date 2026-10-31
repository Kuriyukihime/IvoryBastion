using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;
using UnityEngine;

internal static class Program
{
    static float[] V(Vector3 p)=>new[]{p.x,p.y,p.z};
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
    static void Main(string[] args)
    {
        string root=Path.GetFullPath(args.Length>0?args[0]:".");
        int checks=0;
        for(int f=0;f<=100;f++)
        {
            float t=f/100f;
            for(int i=0;i<12;i++)for(int p=0;p<5;p++)
            {
                var a=BastionEffectGeometry.Deployment(i,p,t);
                Require(Finite(a)&&a.magnitude<2.7f,"Deployment lock indicator outside its mechanical base");checks++;
            }
            float glow=BastionEffectGeometry.Ignition(t*2);
            Require(float.IsFinite(glow)&&glow>=0&&glow<=1,"Ignition must remain bounded");checks++;
            var origin=Vector3.zero;var tip=new Vector3(.3f,.65f,0);var side=new Vector3(-.65f,.3f,0).normalized;
            for(int layer=0;layer<2;layer++)
            {
                var v=Enumerable.Range(0,4).Select(c=>BastionEffectGeometry.Feather(origin,tip,side,t,layer,c)).ToArray();
                Require(v.All(Finite)&&v.All(p=>p.magnitude<1.6f),"Reactor feather escapes its bounded extent");
                Require(Vector3.Cross(v[1]-v[0],v[2]-v[0]).sqrMagnitude>1e-7f,"Reactor feather degenerate first triangle");
                Require(Vector3.Cross(v[2]-v[0],v[3]-v[0]).sqrMagnitude>1e-7f,"Reactor feather degenerate second triangle");checks+=3;
            }
        }
        for(int i=0;i<4;i++)for(int p=0;p<4;p++)
        {
            var a=BastionEffectGeometry.SkyfallArrow(i,p,BastionWarheadGeometry.Radius);
            Require(Finite(a)&&a.magnitude<BastionWarheadGeometry.Radius,"Skyfall arrow must fit within actual blast radius");checks++;
        }
        Require(BastionEffectGeometry.ExhaustWidths.Length==13&&BastionEffectGeometry.ExhaustWidths.All(w=>w>=0&&w<=1),"Bounded exhaust profile");
        Require(BastionEffectGeometry.ExhaustWidths.Last()==0,"Exhaust terminates at zero width");
        Require(BastionEffectGeometry.Ignition(.4f)==0&&BastionEffectGeometry.Ignition(-1)==0,"No stale boost ignition");
        Require(BastionEffectGeometry.LanceFade(.8f)==0,"No permanent laser afterglow");checks+=4;
        var result=new {
            phases=Enumerable.Range(0,40).Select(frame=>frame/30f).Select(age=>new {
                age,charge=BastionChoreography.PowerCharge(age),release=BastionChoreography.PowerRelease(age),
                feathers=Enumerable.Range(0,4).Select(i=>{
                    float s=i%2==0?-1:1;bool upper=i<2;
                    var a=new Vector3(s*.38f,upper?.35f:-.18f,0);
                    var b=new Vector3(s*(upper?1.1f:.86f),upper?1.22f:-.85f,0);
                    var axis=(b-a).normalized;var side=Vector3.Cross(Vector3.back,axis);
                    return new {origin=V(a),tip=V(b),layers=Enumerable.Range(0,2).Select(layer=>Enumerable.Range(0,4).Select(c=>V(BastionEffectGeometry.Feather(a,b,side,BastionChoreography.PowerRelease(age-i*.025f),layer,c))))};
                })
            }),
            deployment=new[]{.10f,.5f,1}.Select(open=>new {open,lines=Enumerable.Range(0,12).Select(i=>Enumerable.Range(0,5).Select(p=>V(BastionEffectGeometry.Deployment(i,p,open))))}),
            corona=new[]{.04f,.14f,.28f}.Select(age=>new {age,lines=Enumerable.Range(0,6).Select(i=>Enumerable.Range(0,4).Select(p=>V(BastionEffectGeometry.LanceCrown(i,p,age))))}),
            exhaust=BastionEffectGeometry.ExhaustWidths,
            skyfallRadius=BastionWarheadGeometry.Radius,
            skyfall=Enumerable.Range(0,4).Select(i=>Enumerable.Range(0,4).Select(p=>V(BastionEffectGeometry.SkyfallArrow(i,p,BastionWarheadGeometry.Radius))))
        };
        Directory.CreateDirectory(Path.Combine(root,".build"));
        File.WriteAllText(Path.Combine(root,".build/effect-study.json"),JsonSerializer.Serialize(result));
        Console.WriteLine($"PASS: {checks} bounded VFX geometry, nondegenerate feather topology, exact footprint and fade checks.");
    }
}
