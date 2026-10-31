using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using UnityEngine;
using IvoryBastion;

internal static class Program
{
    static int count;
    static void Check(bool value,string reason){count++;if(!value)throw new Exception(reason);}
    static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
    static void Main(string[] args)
    {
        var root=Path.GetFullPath(args.Length>0?args[0]:".");
        var vertices=new List<float[]>();var triangles=new List<int>();
        for(int layer=0;layer<2;layer++)for(int plane=0;plane<3;plane++)
        {
            int start=vertices.Count;
            for(int corner=0;corner<4;corner++)vertices.Add(V(BastionThrusterGeometry.Vertex(plane,corner)));
            var a=BastionThrusterGeometry.Vertex(plane,0);var b=BastionThrusterGeometry.Vertex(plane,1);var c=BastionThrusterGeometry.Vertex(plane,2);
            Check(Vector3.Cross(b-a,c-a).magnitude>.99f,"Gas slice must have area at every angle");
            triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        foreach(int seed in new[]{31,32,33,34,173,202,231,260,289,318})
        {
            for(int frame=0;frame<32;frame++)for(int corner=0;corner<4;corner++)
            {
                var uv=BastionThrusterGeometry.UV(corner,frame);
                Check(uv.x>frame%8/8f&&uv.x<(frame%8+1)/8f,"UV stays inside its atlas column");
                Check(uv.y>(3-frame/8)/4f&&uv.y<(4-frame/8)/4f,"UV stays inside its atlas row");
            }
            float phase=.3f;
            for(int i=0;i<240;i++)
            {
                float power=i<80?0:i<160?1:0;
                float next=BastionThrusterGeometry.Advance(phase,1f/60,power),delta=(next-phase+1)%1;
                Check(delta>=.0119f&&delta<=.0194f,"Boost transitions cannot jump the animation clock");
                phase=next;int frame=BastionThrusterGeometry.Frame(phase,seed,out float blend);
                Check(frame>=0&&frame<32&&blend>=0&&blend<1,"Animation frame and interpolation are valid");
            }
            Check(BastionThrusterGeometry.Advance(phase,0,1)==phase,"Paused game freezes plume flow");
            for(int i=0;i<14;i++)for(int step=0;step<80;step++)
            {
                var p=BastionThrusterGeometry.Fleck(i,seed,step/40f,out float alpha,out float size);
                Check(p.y>=.12f&&p.y<=.981f&&p.x*p.x+p.z*p.z<.068f,"Gas flecks remain inside bounded plume envelope");
                Check(alpha>=0&&alpha<=.271f&&size>=.045f&&size<=.106f,"Fleck density remains restrained");
            }
        }
        var scenarios=new List<object>();
        foreach(var scenario in new[]{("mobile",31,1.9f,.22f), ("siege",173,2.3f,.84f)})
        {
            var frames=new List<object>();
            for(int i=0;i<48;i++)
            {
                float time=i/24f;
                var jets=new List<object>();
                int total=scenario.Item1=="mobile"?4:6;
                for(int jet=0;jet<total;jet++)
                {
                    int seed=scenario.Item2+jet*(total==4?1:29);
                    int frame=BastionThrusterGeometry.Frame(time*1.16f,seed,out float blend);
                    var uv=new List<float[]>();var colors=new List<float[]>();
                    for(int layer=0;layer<2;layer++)for(int plane=0;plane<3;plane++)for(int corner=0;corner<4;corner++)
                    {
                        var p=BastionThrusterGeometry.UV(corner,(frame+layer)%32);uv.Add(new[]{p.x,p.y});colors.Add(new[]{1f,1f,1f,.48f*(layer==0?1-blend:blend)});
                    }
                    var flecks=Enumerable.Range(0,14).Select(k=>{var p=BastionThrusterGeometry.Fleck(k,seed,time,out float alpha,out float size);return new{position=V(p),alpha,size};}).ToArray();
                    jets.Add(new{seed,frame,blend,uv,colors,flecks});
                }
                frames.Add(new{time,jets});
            }
            scenarios.Add(new{name=scenario.Item1,length=scenario.Item3,width=scenario.Item4,frames});
        }
        Directory.CreateDirectory(Path.Combine(root,".build"));
        File.WriteAllText(Path.Combine(root,".build","thruster-geometry.json"),JsonSerializer.Serialize(new{vertices,triangles,scenarios}));
        Console.WriteLine($"PASS: {count:N0} thruster geometry, UV, flow-continuity, pause and particle-envelope checks.");
    }
}
