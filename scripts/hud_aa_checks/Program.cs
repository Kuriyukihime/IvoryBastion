using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Diagnostics;
using IvoryBastion;
using UnityEngine;

static class Checks
{
    static int count;
    static void Check(bool value,string message){count++;if(!value)throw new Exception("HUD coverage: "+message);}
    static void Mesh(IList<Vector2> points,IList<Color32> colors,IList<int> indices,float left,float bottom,float right,float top)
    {
        Check(points.Count==colors.Count&&indices.Count%3==0,"matching mesh channels");
        Check(points.Count<65000,"UGUI vertex count remains under its 16-bit mesh limit");
        Check(points.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&p.x>=left-.002f&&p.y>=bottom-.002f&&p.x<=right+.002f&&p.y<=top+.002f),"finite positions and bounded feather corners");
        Check(indices.All(i=>i>=0&&i<points.Count),"indices only reference existing vertices");
    }
    static float Coverage(BastionUiMesh mesh)
    {
        float result=0;
        for(int i=0;i<mesh.Triangles.Count;i+=3)
        {
            int a=mesh.Triangles[i],b=mesh.Triangles[i+1],c=mesh.Triangles[i+2];
            var ab=mesh.Vertices[b]-mesh.Vertices[a];var ac=mesh.Vertices[c]-mesh.Vertices[a];
            float area=Math.Abs(ab.x*ac.y-ab.y*ac.x)*.5f;
            result+=area*(mesh.Colors[a].a+mesh.Colors[b].a+mesh.Colors[c].a)/(3*255f);
        }
        return result;
    }
    static object Data(string name,IList<Vector2> v,IList<Color32> c,IList<int> t,float x=0,float y=0)=>new
    {name,x,y,vertices=v.Select(p=>new[]{p.x,p.y}).ToArray(),colors=c.Select(p=>new[]{p.r,p.g,p.b,p.a}).ToArray(),triangles=t.ToArray()};
    static void Main(string[] args)
    {
        var shape=new BastionUiMesh();
        foreach(float width in new[]{.15f,.35f,.65f,1f,2f,5f})foreach(float feather in new[]{.5f,1f,2f,4f})
        {
            shape.Clear();shape.Quad(0,0,90,width,0,new Color32(255,255,255,255),new Color32(255,255,255,255),feather);
            Mesh(shape.Vertices,shape.Colors,shape.Triangles,-feather,-feather,90+feather,width+feather);
            float ratio=Coverage(shape)/(90*width);
            Check(ratio>.97f&&ratio<1.03f,"subpixel strokes retain coverage without disappearing or blooming: "+ratio);
        }
        var gauge=new BastionShieldGaugeGeometry();
        foreach(float width in new[]{80f,512f,2048f,8192f})foreach(float height in new[]{2f,7f,14f,1024f})foreach(float fraction in new[]{.001f,.17f,1f})
        {
            gauge.Update(width,height,fraction,false,1);
            Mesh(gauge.Vertices,gauge.Colors,gauge.Triangles,-1,-1,width*fraction+1,height+1);
        }
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-1f,0f})
        {gauge.Update(invalid,7,1,false,1);Check(gauge.Vertices.Count==0,"invalid dimensions emit no geometry");}
        var chrome=new BastionChromeGeometry();var resource=new BastionResourceBarGeometry();
        foreach(float scale in new[]{.5f,.75f,1f,1.5f,2f})
        {
            float feather=1/scale;
            chrome.Update(380,150,new Color(.2f,.6f,1),true,feather);
            Mesh(chrome.Mesh.Vertices,chrome.Mesh.Colors,chrome.Mesh.Triangles,-feather,-feather,380+feather,150+feather);
            resource.Update(250,7,.83f,BastionResourceKind.Missiles,40,feather);
            Mesh(resource.Mesh.Vertices,resource.Mesh.Colors,resource.Mesh.Triangles,-feather,-feather,250*.83f+feather,7+feather);
        }
        if(args.Length>0)
        {
            var cases=new List<object>();
            foreach(float scale in new[]{.75f,1f,1.5f})foreach(bool aa in new[]{false,true})
            {
                float feather=aa?1/scale:0;var elements=new List<object>();
                chrome.Update(380,150,new Color(.2f,.6f,1),true,feather);
                elements.Add(Data("Panel",chrome.Mesh.Vertices,chrome.Mesh.Colors,chrome.Mesh.Triangles));
                resource.Update(250,7,.83f,BastionResourceKind.Missiles,40,feather);
                elements.Add(Data("Missiles",resource.Mesh.Vertices,resource.Mesh.Colors,resource.Mesh.Triangles,100,96));
                resource.Update(250,7,.71f,BastionResourceKind.Fuel,1,feather);
                elements.Add(Data("Fuel",resource.Mesh.Vertices,resource.Mesh.Colors,resource.Mesh.Triangles,100,73));
                gauge.Update(250,7,.9f,false,feather);elements.Add(Data("Seraph",gauge.Vertices,gauge.Colors,gauge.Triangles,100,50));
                gauge.Update(250,7,.6f,true,feather);elements.Add(Data("Aegis",gauge.Vertices,gauge.Colors,gauge.Triangles,100,27));
                cases.Add(new{scale,aa,elements});
            }
            File.WriteAllText(args[0],JsonSerializer.Serialize(cases));
        }
        for(int i=0;i<500;i++)gauge.Update(512,7,.83f,false,1);
        long allocationStart=GC.GetAllocatedBytesForCurrentThread();
        long started=Stopwatch.GetTimestamp();
        for(int i=0;i<1000;i++)gauge.Update(512,7,.83f,false,1);
        double milliseconds=(Stopwatch.GetTimestamp()-started)*1000d/Stopwatch.Frequency/1000;
        long allocations=GC.GetAllocatedBytesForCurrentThread()-allocationStart;
        Check(allocations==0,"warmed animated shield geometry creates no per-frame managed garbage: "+allocations);
        Console.WriteLine($"PASS: {count} shared HUD geometry checks (thin-stroke coverage, finite clipping, scale and mesh limits). Typical 512x7 AA gauge: {gauge.Vertices.Count} vertices, {milliseconds:F3} ms/update, {allocations} bytes across 1,000 warmed updates (managed geometry only).");
    }
}
