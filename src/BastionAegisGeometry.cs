using System;
using System.Collections.Generic;
using UnityEngine;

namespace IvoryBastion
{
    internal sealed class BastionAegisGeometry
    {
        internal Vector3[] Vertices,Normals;
        internal int[] Triangles;
        internal byte[] Kinds;
        internal float[] Heights,EdgeWeights;

        internal static BastionAegisGeometry Build()
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            var kinds=new List<byte>();var heights=new List<float>();var weights=new List<float>();
            var outline=new[]{new Vector2(-.72f,-1),new Vector2(.72f,-1),new Vector2(1,-.72f),new Vector2(1,.72f),
                new Vector2(.72f,1),new Vector2(-.72f,1),new Vector2(-1,.72f),new Vector2(-1,-.72f)};
            const int columns=12,rows=5,sides=32,rings=3;
            for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)
            {
                float yaw=(column+.5f)*Mathf.PI*2/columns;
                float latitude=-1.54f+(row+.5f)*3.08f/rows;
                Vector3 Point(Vector2 p,float lift=0)
                {
                    bool pole=row==0&&p.y<0||row==rows-1&&p.y>0;
                    float a=yaw+p.x*Mathf.PI/columns*.925f,b=latitude+p.y*1.54f/rows*(pole?1.095f:.915f);
                    float ring=(float)Math.Pow(Math.Max(0,Math.Cos(b)),.45);
                    return new Vector3((float)Math.Sin(a)*3.45f*ring,
                        (float)(Math.Sign(b)*Math.Pow(Math.Abs(Math.Sin(b)),.85))*2.6f,
                        (float)Math.Cos(a)*3.85f*ring)*(1+lift);
                }
                int Add(Vector2 p,byte kind,float edge,float lift=0)
                {
                    var v=Point(p,lift);int id=vertices.Count;vertices.Add(v);
                    normals.Add(new Vector3(v.x/(3.45f*3.45f),v.y/6.76f,v.z/(3.85f*3.85f)).normalized);
                    heights.Add(v.y/2.6f);kinds.Add(kind);weights.Add(edge);return id;
                }
                Vector2 Boundary(int i)
                {
                    int edge=(i%sides)/4;float t=(i%4)/4f;
                    return Vector2.Lerp(outline[edge],outline[(edge+1)%8],t);
                }
                int center=Add(Vector2.zero,0,.25f);int first=vertices.Count;
                for(int ring=1;ring<=rings;ring++)for(int i=0;i<sides;i++)Add(Boundary(i)*(ring/(float)rings),0,ring/(float)rings);
                for(int i=0;i<sides;i++)
                {
                    int next=(i+1)%sides;triangles.AddRange(new[]{center,first+i,first+next});
                    for(int ring=0;ring<rings-1;ring++)
                    {
                        int a=first+ring*sides+i,b=first+ring*sides+next,c=a+sides,d=b+sides;
                        triangles.AddRange(new[]{a,c,b,b,c,d});
                    }
                    byte kind=(byte)(i>=12&&i<24?2:1);
                    int e=Add(Boundary(i),kind,1,.0015f);
                    Add(Boundary(next),kind,1,.0015f);Add(Boundary(next)*.975f,kind,1,.0015f);Add(Boundary(i)*.975f,kind,1,.0015f);
                    triangles.AddRange(new[]{e,e+1,e+2,e,e+2,e+3});
                }
            }
            return new BastionAegisGeometry{Vertices=vertices.ToArray(),Normals=normals.ToArray(),Triangles=triangles.ToArray(),
                Kinds=kinds.ToArray(),Heights=heights.ToArray(),EdgeWeights=weights.ToArray()};
        }
    }
}
