using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;
using UnityEngine;

static class CombatVisualChecks
{
    static int count;
    static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
    internal static void Run(string root)
    {
        foreach(var aim in new[]{Vector3.up,Vector3.down,Vector3.forward,new Vector3(.2f,.7f,.4f)})
        {
            Check(BastionPilotMotion.Direction(Vector3.zero,aim,Vector3.forward,false,false)==Vector3.zero,"Idle hover must not acquire altitude from aim direction");
            Check(BastionPilotMotion.Direction(new Vector3(.01f,.01f,.01f),aim,Vector3.forward,false,false)==Vector3.zero,"Tiny input noise cannot start drifting");
            Check(BastionPilotMotion.Direction(Vector3.zero,aim,Vector3.forward,true,false).y>.99f,"Held jump ascends intentionally");
            Check(Vector3.Dot(BastionPilotMotion.Direction(Vector3.zero,aim,Vector3.forward,false,true),aim.normalized)>.99f,"Held boost flies along aim");
        }
        Check(BastionPilotMotion.Direction(Vector3.forward,new Vector3(0,-1,1),Vector3.forward,false,false).y<-.6f,"Moving toward a downward aim descends");
        Check(BastionPilotMotion.Direction(Vector3.right,Vector3.up,Vector3.forward,false,false).y==0,"Strafing while aiming upward cannot climb");
        Check(BastionPilotMotion.Direction(Vector3.forward,Vector3.forward,Vector3.forward,false,false).y==0,"Level movement holds height");
        var g=BastionAegisGeometry.Build();int n=g.Vertices.Length;
        Check(n==g.Normals.Length&&n==g.Kinds.Length&&n==g.Heights.Length&&n==g.EdgeWeights.Length,"Aegis vertex channels align");
        Check(n<60000&&g.Triangles.Length%3==0,"Aegis mesh fits 16-bit Unity indices");
        Check(g.Vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Aegis geometry is finite");
        Check(g.Triangles.All(i=>i>=0&&i<n),"Aegis topology uses valid vertices");
        Check(g.Normals.All(v=>Math.Abs(v.magnitude-1)<.001),"Aegis surface normals are normalized");
        Check(g.Vertices.All(v=>Math.Abs(v.x)<3.47&&Math.Abs(v.y)<2.62&&Math.Abs(v.z)<3.87),"Aegis stays within its fitted fortress dimensions");
        Check(g.Vertices.Max(v=>v.y)>2.4&&g.Vertices.Min(v=>v.y)<-2.4,"Aegis encloses top and bottom");
        Check(g.Kinds.Contains((byte)0)&&g.Kinds.Contains((byte)1)&&g.Kinds.Contains((byte)2),"Aegis has transparent panels, blue seams and white crowns");
        for(int i=0;i<g.Triangles.Length;i+=3)
        {
            var a=g.Vertices[g.Triangles[i]];var b=g.Vertices[g.Triangles[i+1]];var c=g.Vertices[g.Triangles[i+2]];
            Check(Vector3.Cross(b-a,c-a).sqrMagnitude>1e-14f,"Aegis has no degenerate triangles");
        }
        // Export the exact runtime geometry for an offline visual inspection.
        Directory.CreateDirectory(Path.Combine(root,".build"));
        File.WriteAllText(Path.Combine(root,".build","aegis-geometry.json"),JsonSerializer.Serialize(new{
            vertices=g.Vertices.Select(v=>new[]{v.x,v.y,v.z}),normals=g.Normals.Select(v=>new[]{v.x,v.y,v.z}),
            triangles=g.Triangles,kinds=g.Kinds.Select(k=>(int)k),heights=g.Heights,edgeWeights=g.EdgeWeights}));
        Console.WriteLine($"PASS: {count} idle/aim/boost flight and Aegis topology checks.");
    }
}
