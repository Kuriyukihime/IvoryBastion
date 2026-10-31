using System;
using System.Collections.Generic;
using UnityEngine;

namespace IvoryBastion
{
    // Shared by the live status layer and offline mesh/preview checks.
    internal sealed class BastionShieldGaugeGeometry
    {
        private readonly BastionUiMesh mesh=new BastionUiMesh();
        private readonly BastionUiMesh pattern=new BastionUiMesh();
        private int[] patternMap=Array.Empty<int>();
        private float patternWidth=-1,patternHeight,patternFeather;private bool patternAegis,buildingPattern;
        internal List<Vector2> Vertices=>mesh.Vertices;
        internal List<Color32> Colors=>mesh.Colors;
        internal List<int> Triangles=>mesh.Triangles;
        private readonly Vector2[] polygon=new Vector2[4],clipA=new Vector2[16],clipB=new Vector2[16];
        private float width,height,filled,feather;
        internal void Update(float w,float h,float fraction,bool aegis=false,float edge=0)
        {
            Vertices.Clear();Colors.Clear();Triangles.Clear();
            feather=Finite(edge)?Math.Max(0,Math.Min(4,edge)):0;
            if(!Finite(w)||!Finite(h)||w<=0||h<=0)return;
            width=Math.Min(w,8192);height=Math.Min(h,1024);
            fraction=Finite(fraction)?Math.Max(0,Math.Min(1,fraction)):0;filled=width*fraction;
            if(filled<=0)return;
            Quad(0,0,filled,height,aegis?new Color32(255,205,55,255):new Color32(237,246,255,245));
            if(feather<=0)Cells(aegis);
            else
            {
                if(patternWidth!=width||patternHeight!=height||patternFeather!=feather||patternAegis!=aegis)
                {
                    patternWidth=width;patternHeight=height;patternFeather=feather;patternAegis=aegis;
                    float saved=filled;filled=width;buildingPattern=true;pattern.Clear();Cells(aegis);buildingPattern=false;filled=saved;
                    if(patternMap.Length<pattern.Vertices.Count)patternMap=new int[pattern.Vertices.Count];
                }
                // The changing fill clips cached coverage triangles. The solid
                // leading cap covers this cut, so its outer edge stays smooth.
                mesh.AppendClipped(pattern,Math.Max(0,filled-feather*.5f),patternMap);
            }
            Quad(0,0,filled,Math.Min(1,height*.1f),aegis?new Color32(30,25,16,255):new Color32(20,83,175,255));
            Quad(Math.Max(0,filled-1.5f),0,filled,height,aegis?new Color32(255,239,160,255):new Color32(34,130,255,255));
        }
        private void Cells(bool aegis)
        {
            // Fine blue cells read as a separate technological shield, rather
            // than vanilla health, item shields or yellow barrier.
            float radius=Math.Max(height/7,Math.Max(3,width/150));
            float half=(float)Math.Sqrt(3)*radius*.5f,stroke=Math.Max(.65f,radius*.16f);
            int rows=Math.Min(9,(int)Math.Ceiling(height/(half*2))+2);
            int columns=Math.Min(104,(int)Math.Ceiling(filled/(radius*1.5f))+2);
            for(int x=-1;x<columns;x++)for(int y=-1;y<rows;y++)
            {
                float cx=x*radius*1.5f,cy=y*half*2+((x&1)==0?0:half);
                for(int side=0;side<6;side++)
                {
                    double a=side*Math.PI/3,b=(side+1)*Math.PI/3;
                    polygon[0]=new Vector2(cx+(float)Math.Cos(a)*radius,cy+(float)Math.Sin(a)*radius);
                    polygon[1]=new Vector2(cx+(float)Math.Cos(b)*radius,cy+(float)Math.Sin(b)*radius);
                    polygon[2]=new Vector2(cx+(float)Math.Cos(b)*(radius-stroke),cy+(float)Math.Sin(b)*(radius-stroke));
                    polygon[3]=new Vector2(cx+(float)Math.Cos(a)*(radius-stroke),cy+(float)Math.Sin(a)*(radius-stroke));
                    Polygon(polygon,4,aegis?new Color32(21,23,29,245):new Color32(35,112,221,230));
                }
            }
        }
        private void Quad(float left,float bottom,float right,float top,Color32 color)
        {
            polygon[0]=new Vector2(left,bottom);polygon[1]=new Vector2(right,bottom);
            polygon[2]=new Vector2(right,top);polygon[3]=new Vector2(left,top);Polygon(polygon,4,color);
        }
        private void Polygon(Vector2[] points,int count,Color32 color)
        {
            Array.Copy(points,clipA,count);var source=clipA;var target=clipB;
            for(int edge=0;edge<4;edge++)
            {
                int next=0;if(count==0)return;
                var previous=source[count-1];float pd=Distance(previous,edge);
                for(int i=0;i<count;i++)
                {
                    var current=source[i];float d=Distance(current,edge);
                    if((d>=0)!=(pd>=0))target[next++]=previous+(current-previous)*(pd/(pd-d));
                    if(d>=0)target[next++]=current;
                    previous=current;pd=d;
                }
                count=next;var swap=source;source=target;target=swap;
            }
            if(count<3)return;
            if(feather>0){(buildingPattern?pattern:mesh).Polygon(source,count,color,feather);return;}
            int start=Vertices.Count;
            for(int i=0;i<count;i++){Vertices.Add(source[i]);Colors.Add(color);}
            for(int i=1;i<count-1;i++){Triangles.Add(start);Triangles.Add(start+i);Triangles.Add(start+i+1);}
        }
        private float Distance(Vector2 p,int edge)=>edge==0?p.x:edge==1?filled-p.x:edge==2?p.y:height-p.y;
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
