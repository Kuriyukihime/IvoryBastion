using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;
using Mono.Cecil;
using UnityEngine;
internal static class PerformanceChecks
{
    private static int count;private static double sink;
    private static void Check(bool ok,string why){count++;if(!ok)throw new Exception("Performance regression: "+why);}
    private static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.00001f,why);
    // Freeze the prior visual equations, independently of the optimized implementation.
    private static Color OldTint(Vector3 normal,Vector3 view,float time,float ripple,float visibility)
    {
        float facing=Vector3.Dot(normal,view);
        float rim=(float)Math.Pow(1-Math.Abs(facing),4),edge=(float)Math.Pow(1-Math.Abs(facing),18);
        float shimmer=(float)Math.Pow(.5+.5*Math.Sin(normal.x*9+normal.y*13+normal.z*6-time*1.1),9);
        float glint=(float)Math.Pow(Math.Max(0,normal.y*.65f-normal.x*.35f+normal.z*.2f),16)*.16f,glow=Math.Min(1,ripple);
        return new Color(Math.Min(1,.12f+.80f*glow+.20f*rim+.12f*edge),Math.Min(1,.46f+.49f*glow+.18f*rim+.12f*edge),1,
            Math.Min(.8f,.009f+.12f*rim+.075f*edge+.008f*shimmer+glint+.64f*glow)*visibility);
    }
    private static float OldRipple(float angle,float age,float strength)
    {
        if(age<0||age>=BastionShieldGeometry.RippleDuration)return 0;
        float front=age*5.8f;double crest=Math.Exp(-Math.Pow((angle-front)/.12f,2));
        double wake=.32*Math.Exp(-Math.Pow((angle-front+.24f)/.18f,2));
        double contact=Math.Exp(-angle*angle/.025f)*Math.Exp(-age*16);
        return(float)((crest+wake+contact)*Math.Pow(1-age/BastionShieldGeometry.RippleDuration,1.3)*strength);
    }
    private static Color OldAegis(byte kind,Vector3 normal,Vector3 view,float age,float impact,float visibility)
    {
        float rim=(float)Math.Pow(1-Math.Abs(Vector3.Dot(normal,view)),2.5),scan=(float)Math.Sin(age*1.7f-.8f);
        float wave=Math.Max(0,1-Math.Abs(normal.y-scan)/.12f);
        float alpha=kind==0?.009f+.021f*rim+.065f*impact+.005f*wave:kind==1?.20f+.19f*rim+.36f*impact+.08f*wave:.31f+.18f*rim+.34f*impact+.15f*wave;
        var color=kind==0?new Color(1,.72f,.07f):kind==1?new Color(1,.82f+.12f*impact,.12f+.52f*impact):new Color(1,.91f+.08f*impact,.48f+.38f*impact);
        color.a=alpha*visibility;return color;
    }
    private static void ColorNear(Color a,Color b,string why){Near(a.r,b.r,why);Near(a.g,b.g,why);Near(a.b,b.b,why);Near(a.a,b.a,why);}
    private static double Timed(bool previous,Vector3[] normals)
    {
        double total=0;var timer=Stopwatch.StartNew();
        for(int pass=0;pass<8;pass++)
        {
            float age=.1f+pass*.05f;var wave=new BastionShieldGeometry.RippleFrame(age,1);
            foreach(var normal in normals)
            {
                float ripple=previous?OldRipple(normal.y+1,age,1):wave.At(normal.y+1);
                var color=previous?OldTint(normal,Vector3.forward,age,ripple,1):BastionShieldGeometry.Tint(normal,Vector3.forward,age,ripple,1);
                total+=color.a;
            }
        }
        timer.Stop();sink=total;return timer.Elapsed.TotalMilliseconds;
    }
    internal static void Run(string root)
    {
        var normals=BastionShieldGeometry.Build().Normals;
        foreach(var view in new[]{Vector3.forward,new Vector3(.3f,.5f,.8f).normalized,Vector3.up})
        for(int i=0;i<normals.Length;i+=7)
        {
            float age=(i%119)*.01f,angle=i%315*.01f,visibility=i%17/16f;
            Near(new BastionShieldGeometry.RippleFrame(age,.7f).At(angle),OldRipple(angle,age,.7f),"prepared ripple preserves the prior wave");
            ColorNear(BastionShieldGeometry.Tint(normals[i],view,age,.4f,visibility),OldTint(normals[i],view,age,.4f,visibility),"shield appearance unchanged");
            for(byte kind=0;kind<3;kind++)ColorNear(BastionAegisHexGeometry.TintWithScan(kind,normals[i],view,BastionAegisHexGeometry.Scan(age),.6f,visibility),OldAegis(kind,normals[i],view,age,.6f,visibility),"Aegis appearance unchanged");
        }
        var payloads=new List<object>();long mineArrays=0,minePayload=0;
        foreach(var path in Directory.GetFiles(Path.Combine(root,"assets"),"*.meshbin"))
        {
            using var stream=File.OpenRead(path);using var reader=new BinaryReader(stream);
            string format=new string(reader.ReadChars(5));bool four=format=="BAST3",textured=four||format=="BAST2";
            Check(textured||format=="BAST1","recognized binary layout");
            int bones=reader.ReadInt32();for(int i=0;i<bones;i++){reader.ReadString();for(int j=0;j<3;j++)reader.ReadSingle();reader.ReadInt32();}
            int parts=reader.ReadInt32();long payload=0,arrays=0;
            for(int part=0;part<parts;part++)
            {
                reader.ReadString();for(int i=0;i<5;i++)reader.ReadSingle();int vertices=reader.ReadInt32(),indices=reader.ReadInt32();long start=stream.Position;
                for(int vertex=0;vertex<vertices;vertex++)
                {for(int j=0;j<(textured?8:6);j++)reader.ReadSingle();reader.ReadInt32();reader.ReadInt32();if(four){reader.ReadInt32();reader.ReadInt32();for(int j=0;j<4;j++)reader.ReadSingle();}else reader.ReadSingle();}
                for(int i=0;i<indices;i++)reader.ReadInt32();long end=stream.Position;
                stream.Position=start;stream.Seek(BastionMeshPayload.Bytes(vertices,indices,textured,four),SeekOrigin.Current);
                Check(stream.Position==end,"cached payload lands on exactly the same next-part offset: "+Path.GetFileName(path));
                payload+=end-start;arrays+=(long)vertices*(12+12+32+(textured?8:0))+(long)indices*4;
            }
            Check(stream.Position==stream.Length,"cache path preserves complete asset alignment");
            if(Path.GetFileName(path)=="spider_mine.meshbin"){mineArrays=arrays;minePayload=payload;}
            payloads.Add(new{Asset=Path.GetFileName(path),Parts=parts,PayloadBytes=payload,AvoidedManagedBufferBytesPerCachedInstance=arrays});
        }
        using(var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll")))
        {
            var rig=assembly.MainModule.Types.Single(t=>t.Name=="BastionModel").Methods.Single(m=>m.Name=="Rig");
            Check(rig.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionMeshPayload"&&m.Name=="Bytes"),"production rig uses the verified cache skip");
            foreach(string type in new[]{"BastionShieldVisual","BastionAegisVisual"})
            {
                var frame=assembly.MainModule.Types.Single(t=>t.Name==type).Methods.Single(m=>m.Name=="LateUpdate");
                Check(frame.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="RippleFrame"&&m.Name==".ctor"),"production shield prepares waves outside vertex evaluation");
                Check(!frame.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Ripple"),"no per-vertex age-envelope recomputation");
            }
        }
        Timed(true,normals);Timed(false,normals);var old=new double[7];var next=new double[7];
        for(int i=0;i<old.Length;i++){old[i]=Timed(true,normals);next[i]=Timed(false,normals);}Array.Sort(old);Array.Sort(next);
        File.WriteAllText(Path.Combine(root,".build","performance-122.json"),JsonSerializer.Serialize(new{Scope="Offline .NET CPU math benchmark, not Unity/GPU frame time or an in-game FPS measurement. Same topology, colors and ripple equations, within 0.00001. No frame throttling or particle-quality reduction.",Checks=count,ShieldVertices=normals.Length,EightShieldFramesMedianMilliseconds=new{Before=old[3],After=next[3]},MeshCache=payloads,Cached24MineDeployment=new{AvoidedPayloadDecodingBytes=minePayload*24,AvoidedManagedVertexWeightIndexBytes=mineArrays*24},Sink=sink},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {count} shield visual-equivalence, actual binary cache offsets and compiled performance-path checks; wrote .build/performance-122.json.");
    }
}
