using System;
using System.Collections.Generic;
using UnityEngine;

namespace IvoryBastion
{
    internal sealed class BastionAegisHexGeometry
    {
        internal Vector3[] Vertices,Normals;
        internal int[] Triangles;
        internal byte[] Kinds;
        internal int Hexagons,Pentagons;

        // Dual of a subdivided icosphere: evenly sized hexagonal cells, with the
        // twelve small pentagonal joins required to close a geodesic sphere.
        internal static BastionAegisHexGeometry Build()
        {
            float t=(1+(float)Math.Sqrt(5))*.5f;
            var points=new List<Vector3>{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),
                new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),
                new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)};
            for(int i=0;i<points.Count;i++)points[i]=points[i].normalized;
            var faces=new List<int[]>{new[]{0,11,5},new[]{0,5,1},new[]{0,1,7},new[]{0,7,10},new[]{0,10,11},
                new[]{1,5,9},new[]{5,11,4},new[]{11,10,2},new[]{10,7,6},new[]{7,1,8},
                new[]{3,9,4},new[]{3,4,2},new[]{3,2,6},new[]{3,6,8},new[]{3,8,9},
                new[]{4,9,5},new[]{2,4,11},new[]{6,2,10},new[]{8,6,7},new[]{9,8,1}};
            for(int level=0;level<2;level++)
            {
                var midpoints=new Dictionary<long,int>();var next=new List<int[]>();
                int Midpoint(int a,int b)
                {
                    long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
                    if(midpoints.TryGetValue(key,out int index))return index;
                    index=points.Count;points.Add((points[a]+points[b]).normalized);midpoints[key]=index;return index;
                }
                foreach(var face in faces)
                {
                    int a=face[0],b=face[1],c=face[2],ab=Midpoint(a,b),bc=Midpoint(b,c),ca=Midpoint(c,a);
                    next.Add(new[]{a,ab,ca});next.Add(new[]{b,bc,ab});next.Add(new[]{c,ca,bc});next.Add(new[]{ab,bc,ca});
                }
                faces=next;
            }
            var neighbors=new List<Vector3>[points.Count];
            for(int i=0;i<neighbors.Length;i++)neighbors[i]=new List<Vector3>();
            foreach(var face in faces)
            {
                var center=(points[face[0]]+points[face[1]]+points[face[2]]).normalized;
                foreach(int id in face)neighbors[id].Add(center);
            }
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();var kinds=new List<byte>();
            int hexagons=0,pentagons=0;
            int Add(Vector3 point,byte kind){int id=vertices.Count;vertices.Add(point);normals.Add(point.normalized);kinds.Add(kind);return id;}
            for(int cell=0;cell<points.Count;cell++)
            {
                var center=points[cell];var tangent=Vector3.Cross(Math.Abs(center.y)>.9f?Vector3.right:Vector3.up,center).normalized;
                var bitangent=Vector3.Cross(center,tangent);
                var ring=neighbors[cell];
                ring.Sort((a,b)=>Math.Atan2(Vector3.Dot(a,bitangent),Vector3.Dot(a,tangent)).CompareTo(Math.Atan2(Vector3.Dot(b,bitangent),Vector3.Dot(b,tangent))));
                if(ring.Count==6)hexagons++;else if(ring.Count==5)pentagons++;
                int hub=Add(center,0);
                for(int i=0;i<ring.Count;i++)
                {
                    var a=Vector3.Lerp(center,ring[i],.94f).normalized;
                    var b=Vector3.Lerp(center,ring[(i+1)%ring.Count],.94f).normalized;
                    int faceA=Add(a,0),faceB=Add(b,0);indices.AddRange(new[]{hub,faceA,faceB});
                    int edge=Add(a*1.001f,1);Add(b*1.001f,1);
                    Add(Vector3.Lerp(center,b,.94f).normalized*1.001f,1);Add(Vector3.Lerp(center,a,.94f).normalized*1.001f,1);
                    indices.AddRange(new[]{edge,edge+1,edge+2,edge,edge+2,edge+3});
                    // Short inset corner contacts, with gaps along each edge:
                    // an engineered, layered shell rather than a uniform cage.
                    var insetA=Vector3.Lerp(center,a,.82f).normalized;
                    var insetB=Vector3.Lerp(center,b,.82f).normalized;
                    var end=Vector3.Lerp(insetA,insetB,.24f).normalized;
                    int contact=Add(insetA*1.002f,2);Add(end*1.002f,2);
                    Add(Vector3.Lerp(center,end,.96f).normalized*1.002f,2);
                    Add(Vector3.Lerp(center,insetA,.96f).normalized*1.002f,2);
                    indices.AddRange(new[]{contact,contact+1,contact+2,contact,contact+2,contact+3});
                }
            }
            return new BastionAegisHexGeometry{Vertices=vertices.ToArray(),Normals=normals.ToArray(),Triangles=indices.ToArray(),Kinds=kinds.ToArray(),Hexagons=hexagons,Pentagons=pentagons};
        }

        internal static Color Tint(byte kind,Vector3 normal,Vector3 view,float age,float impact,float visibility)
            =>TintWithScan(kind,normal,view,Scan(age),impact,visibility);
        internal static float Scan(float age)=>(float)Math.Sin(age*1.7f-.8f);
        internal static Color TintWithScan(byte kind,Vector3 normal,Vector3 view,float scan,float impact,float visibility)
        {
            double edge=1-Math.Abs(Vector3.Dot(normal,view));float rim=(float)(edge*edge*Math.Sqrt(edge));
            // A restrained recharge scan keeps the shell alive between impacts.
            float wave=Math.Max(0,1-Math.Abs(normal.y-scan)/.12f);
            float alpha=kind==0?.009f+.021f*rim+.065f*impact+.005f*wave:
                kind==1?.20f+.19f*rim+.36f*impact+.08f*wave:
                .31f+.18f*rim+.34f*impact+.15f*wave;
            var color=kind==0?new Color(1,.72f,.07f):
                kind==1?new Color(1,.82f+.12f*impact,.12f+.52f*impact):
                new Color(1,.91f+.08f*impact,.48f+.38f*impact);
            color.a=alpha*visibility;return color;
        }
    }
}
