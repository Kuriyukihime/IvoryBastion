using System;
using System.Collections.Generic;
using UnityEngine;

namespace IvoryBastion
{
    // Coverage is built into the vertex alpha. UI/Default can render these
    // meshes without an offscreen texture or a different canvas material.
    internal sealed class BastionUiMesh
    {
        internal readonly List<Vector2> Vertices;
        internal readonly List<Color32> Colors;
        internal readonly List<int> Triangles;
        internal BastionUiMesh(int vertices=4096,int indices=8192)
        {Vertices=new List<Vector2>(vertices);Colors=new List<Color32>(vertices);Triangles=new List<int>(indices);}
        private readonly Vector2[] points=new Vector2[32],inner=new Vector2[32],outer=new Vector2[32],quad=new Vector2[4];
        private readonly Color32[] colors=new Color32[32],quadColors=new Color32[4];
        private readonly int[] clipped=new int[4];
        internal void Clear(){Vertices.Clear();Colors.Clear();Triangles.Clear();}
        internal void AppendClipped(BastionUiMesh source,float right,int[] map)
        {
            if(right<=0)return;
            for(int i=0;i<source.Vertices.Count;i++)
            {
                if(source.Vertices[i].x>right){map[i]=-1;continue;}
                map[i]=Vertices.Count;Vertices.Add(source.Vertices[i]);Colors.Add(source.Colors[i]);
            }
            for(int triangle=0;triangle<source.Triangles.Count;triangle+=3)
            {
                int a=source.Triangles[triangle],b=source.Triangles[triangle+1],c=source.Triangles[triangle+2];
                if(map[a]>=0&&map[b]>=0&&map[c]>=0){Tri(map[a],map[b],map[c]);continue;}
                if(map[a]<0&&map[b]<0&&map[c]<0)continue;
                int count=0,previous=c;
                for(int corner=0;corner<3;corner++)
                {
                    int current=corner==0?a:corner==1?b:c;
                    if((map[current]>=0)!=(map[previous]>=0))
                    {
                        var from=source.Vertices[previous];var to=source.Vertices[current];float t=(right-from.x)/(to.x-from.x);
                        var ca=source.Colors[previous];var cb=source.Colors[current];
                        clipped[count++]=Vertices.Count;Vertices.Add(new Vector2(right,from.y+(to.y-from.y)*t));
                        Colors.Add(new Color32(Blend(ca.r,cb.r,t),Blend(ca.g,cb.g,t),Blend(ca.b,cb.b,t),Blend(ca.a,cb.a,t)));
                    }
                    if(map[current]>=0)clipped[count++]=map[current];
                    previous=current;
                }
                for(int i=1;i<count-1;i++)Tri(clipped[0],clipped[i],clipped[i+1]);
            }
        }
        private static byte Blend(byte a,byte b,float t)=>(byte)Math.Round(a+(b-a)*t);
        internal void Quad(float x,float y,float width,float height,float cut,Color32 low,Color32 high,float feather,bool vertical=false)
        {
            if(width<=0||height<=0)return;
            cut=Math.Max(-width*.45f,Math.Min(width*.45f,cut));
            quad[0]=new Vector2(x,y);quad[1]=new Vector2(x+width-cut,y);
            quad[2]=new Vector2(x+width,y+height);quad[3]=new Vector2(x+cut,y+height);
            quadColors[0]=low;quadColors[1]=vertical?low:high;quadColors[2]=high;quadColors[3]=vertical?high:low;
            Polygon(quad,4,quadColors,feather);
        }
        internal void Polygon(Vector2[] source,int count,Color32 tint,float feather)
        {for(int i=0;i<Math.Min(count,colors.Length);i++)colors[i]=tint;Polygon(source,count,colors,feather);}
        internal void Polygon(Vector2[] source,int count,Color32[] tints,float feather)
        {
            if(count<3||count>points.Length)return;
            int n=0;
            for(int i=0;i<count;i++)
            {
                var p=source[i];if(!Finite(p.x)||!Finite(p.y))return;
                if(n>0&&Distance2(p,points[n-1])<1e-10f)continue;
                points[n]=p;colors[n]=tints[i];n++;
            }
            if(n>2&&Distance2(points[0],points[n-1])<1e-10f)n--;
            if(n<3)return;
            float area=Area(points,n);if(!Finite(area)||Math.Abs(area)<1e-6f)return;
            if(area<0){Array.Reverse(points,0,n);Array.Reverse(colors,0,n);area=-area;}
            feather=Finite(feather)?Math.Max(0,Math.Min(4,feather)):0;
            if(feather<1e-5f)
            {
                int start=Vertices.Count;for(int i=0;i<n;i++){Vertices.Add(points[i]);Colors.Add(colors[i]);}
                Fan(start,n);return;
            }
            var center=new Vector2();for(int i=0;i<n;i++)center+=points[i];center/=n;
            float depth=float.PositiveInfinity;
            for(int i=0;i<n;i++)
            {
                var p=points[i];var edge=points[(i+1)%n]-p;
                float length=(float)Math.Sqrt(edge.x*edge.x+edge.y*edge.y);
                depth=Math.Min(depth,Cross(edge,center-p)/Math.Max(1e-6f,length));
            }
            float inset=Math.Min(feather*.5f,Math.Max(0,depth*.85f)),outset=feather*.5f;
            Offset(points,n,-inset,inner);Offset(points,n,outset,outer);
            // A subpixel stroke cannot retain an opaque core without getting
            // fatter. Preserve its integrated coverage by reducing the peak.
            float coverage=Math.Max(0,Area(inner,n));
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                coverage+=TriangleArea(inner[i],inner[j],outer[j])*2f/3f;
                coverage+=TriangleArea(inner[i],outer[j],outer[i])/3f;
            }
            float peak=Math.Min(1,area/Math.Max(1e-7f,coverage));
            int first=Vertices.Count;
            for(int i=0;i<n;i++){var c=colors[i];c.a=(byte)Math.Round(c.a*peak);Vertices.Add(inner[i]);Colors.Add(c);}
            for(int i=0;i<n;i++){var c=colors[i];c.a=0;Vertices.Add(outer[i]);Colors.Add(c);}
            Fan(first,n);
            for(int i=0;i<n;i++)
            {int j=(i+1)%n;Tri(first+i,first+j,first+n+j);Tri(first+i,first+n+j,first+n+i);}
        }
        private static void Offset(Vector2[] source,int count,float amount,Vector2[] result)
        {
            for(int i=0;i<count;i++)
            {
                var p=source[i];var a=p-source[(i+count-1)%count];var b=source[(i+1)%count]-p;
                float al=(float)Math.Sqrt(a.x*a.x+a.y*a.y),bl=(float)Math.Sqrt(b.x*b.x+b.y*b.y);
                a=new Vector2(a.y/Math.Max(al,1e-6f),-a.x/Math.Max(al,1e-6f));
                b=new Vector2(b.y/Math.Max(bl,1e-6f),-b.x/Math.Max(bl,1e-6f));
                var sum=a+b;float denominator=Math.Max(.15f,sum.x*b.x+sum.y*b.y);
                var direction=sum/denominator;float length=(float)Math.Sqrt(direction.x*direction.x+direction.y*direction.y);
                if(length>2)direction*=2/length;
                result[i]=p+direction*amount;
            }
        }
        private void Fan(int start,int count){for(int i=1;i<count-1;i++)Tri(start,start+i,start+i+1);}
        private void Tri(int a,int b,int c){Triangles.Add(a);Triangles.Add(b);Triangles.Add(c);}
        private static float Area(Vector2[] p,int count)
        {float result=0;for(int i=0;i<count;i++)result+=Cross(p[i],p[(i+1)%count]);return result*.5f;}
        private static float TriangleArea(Vector2 a,Vector2 b,Vector2 c)=>Math.Abs(Cross(b-a,c-a))*.5f;
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static float Distance2(Vector2 a,Vector2 b){var d=a-b;return d.x*d.x+d.y*d.y;}
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
