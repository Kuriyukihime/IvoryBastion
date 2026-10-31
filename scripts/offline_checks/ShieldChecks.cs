using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;
using UnityEngine;

internal static class ShieldChecks
{
    private static int count;
    private static void Check(bool condition,string message){count++;if(!condition)throw new Exception(message);}
    private static void Near(float actual,float expected,string message)=>Check(Math.Abs(actual-expected)<.001f,message+$": {actual} != {expected}");

    internal static void Run(string root)
    {
        Near(BastionDefenseRules.RechargeStep(3.99f,.02f,true),0,"siege cannot recharge before four seconds");
        Near(BastionDefenseRules.RechargeStep(4.01f,.02f,true),.01f,"siege integrates only time past four seconds");
        Near(BastionDefenseRules.RechargeStep(6.99f,.02f,false),0,"mobile waits seven seconds");
        Near(BastionDefenseRules.RechargeStep(7.01f,.02f,false),.01f,"mobile recharge boundary");
        var defense=new BastionBank();
        Near(defense.DamageMultiplier(true),1,"siege has no permanent reduction");
        Near(defense.DamageMultiplier(true,50),.6f,"base siege innate shield reduces damage by forty percent");
        Near(defense.DamageMultiplier(false,15),.6f,"base mobile innate shield reduces damage by forty percent");
        defense.DefenseTime=6;
        Near(defense.DamageMultiplier(true),.65f,"Aegis protects even without innate shield");
        Near(defense.DamageMultiplier(true,50),.39f,"base Aegis and siege shield combine to 61 percent reduction");
        Near(defense.DamageMultiplier(false,15),.6f,"Aegis does not add damage reduction in mobile form");
        Near(defense.BarrierFraction,.4f,"buffed base Aegis barrier");
        defense.Ranks[1]=1;
        Near(BastionDefenseRules.ShieldCapacity(100,true,defense.ShieldCapacityMultiplier),60,"rank one siege shield");
        Near(BastionDefenseRules.ShieldCapacity(100,false,defense.ShieldCapacityMultiplier),18,"rank one mobile shield");
        Near(defense.BarrierFraction,.5f,"rank one Aegis barrier");
        defense.Ranks[1]=2;
        Near(BastionDefenseRules.ShieldCapacity(100,true,defense.ShieldCapacityMultiplier),70,"rank two siege shield");
        Near(BastionDefenseRules.ShieldCapacity(100,false,defense.ShieldCapacityMultiplier),21,"rank two mobile shield");
        Near(defense.DefenseHealFraction,.3f,"rank two Aegis healing");
        defense.Ranks[1]=3;
        Near(defense.DamageMultiplier(true,70),.336f,"hardening stacks to 66.4 percent reduction");
        Near(defense.DamageMultiplier(false,21),.56f,"hardening grants forty-four percent mobile shield reduction");
        Near(defense.DamageMultiplier(true),.6f,"hardening Aegis works after shield breaks");
        defense.DefenseTime=0;Near(defense.DamageMultiplier(true),1,"hardening cannot restore permanent siege resistance");
        defense.Choices[1]=1;Check(defense.RenewalShield,"renewal specialization enabled");Near(defense.DefenseHealFraction,.35f,"renewal Aegis healing");

        SeraphShieldChecks.Run(root);

        var sphere=BastionShieldGeometry.Build();
        Check(sphere.Normals.Length==8249,"smooth shield vertex count");
        Check(sphere.Triangles.Length==47712,"continuous shield triangle count");
        for(int i=0;i<sphere.Normals.Length;i++)Near(sphere.Normals[i].sqrMagnitude,1,"spherical surface");
        for(int i=0;i<sphere.Triangles.Length;i+=3)
        {
            var a=sphere.Normals[sphere.Triangles[i]];var b=sphere.Normals[sphere.Triangles[i+1]];var c=sphere.Normals[sphere.Triangles[i+2]];
            Check(Vector3.Dot(Vector3.Cross(b-a,c-a),a)>0,"shield faces outward without degenerate triangles");
        }
        for(int row=0;row<=BastionShieldGeometry.Rows;row++)
            Check((sphere.Normals[row*(BastionShieldGeometry.Columns+1)]-sphere.Normals[row*(BastionShieldGeometry.Columns+1)+BastionShieldGeometry.Columns]).sqrMagnitude<1e-8,"closed longitudinal seam");
        Check(BastionShieldGeometry.Ripple(1.16f,.2f,1)>.5f,"impact ring reaches expected angular distance");
        Check(BastionShieldGeometry.Ripple(0,.2f,1)<.1f,"impact ring travels away from contact point");
        Near(BastionShieldGeometry.Ripple(1,1,1),0,"ripple expires");
        var view=new Vector3(0,0,1);
        var calm=BastionShieldGeometry.Tint(view,view,0,0,1);
        var hit=BastionShieldGeometry.Tint(view,view,0,1,1);
        Check(calm.a<.04f,"calm center remains clear around model");Check(hit.a>calm.a+.5f,"impact visibly brightens shield");
        Near(BastionShieldGeometry.Tint(view,view,0,1,0).a,0,"inactive shield fully fades");
        Near(BastionShieldGeometry.FormRadius(0),1.22f,"mobile shield size retained");
        Near(BastionShieldGeometry.FormRadius(1),4.2f,"siege shield fits reconstructed fortress");
        Near(BastionShieldGeometry.FormRadius(.5f),2.71f,"shield smoothly resizes during deployment");
        var hex=BastionAegisHexGeometry.Build();
        Check(hex.Hexagons==150&&hex.Pentagons==12,"closed geodesic honeycomb topology");
        float outer=BastionShieldGeometry.SiegeRadius+BastionShieldGeometry.FortificationGap;
        for(int i=0;i<hex.Triangles.Length;i+=3)
        {
            var a=hex.Vertices[hex.Triangles[i]];var b=hex.Vertices[hex.Triangles[i+1]];var c=hex.Vertices[hex.Triangles[i+2]];
            Check(Vector3.Dot(Vector3.Cross(b-a,c-a),a)>0,"Aegis faces and luminous edges face outward");
            Check(((a+b+c)/3*outer).magnitude>BastionShieldGeometry.SiegeRadius*1.023f,"outer hex layer clears inner impact displacement");
        }
        using(var reader=new BinaryReader(File.OpenRead(Path.Combine(root,"assets","siege.meshbin"))))
        {
            string meshFormat=new string(reader.ReadChars(5));bool four=meshFormat=="BAST3",textured=four||meshFormat=="BAST2";int bones=reader.ReadInt32();
            for(int i=0;i<bones;i++){reader.ReadString();reader.ReadBytes(16);}
            int parts=reader.ReadInt32();float furthest=0;
            for(int part=0;part<parts;part++)
            {
                reader.ReadString();reader.ReadBytes(20);int vertices=reader.ReadInt32(),indices=reader.ReadInt32();
                for(int i=0;i<vertices;i++)
                {
                    var point=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle())*1.45f-Vector3.up*2;
                    furthest=Math.Max(furthest,point.magnitude);reader.ReadBytes(four?52:textured?32:24);
                }
                reader.ReadBytes(indices*4);
            }
            Check(furthest+.15f<BastionShieldGeometry.SiegeRadius,"shield encloses the actual siege mesh with clearance");
        }
        var yellow=BastionAegisHexGeometry.Tint(1,view,view,1,0,1);
        Check(yellow.r>.9f&&yellow.g>.7f&&yellow.b<.2f,"Aegis rims are yellow");
        Near(BastionAegisHexGeometry.Tint(1,view,view,1,1,0).a,0,"Aegis can fade independently of blue shield");
        Directory.CreateDirectory(Path.Combine(root,".build"));
        File.WriteAllText(Path.Combine(root,".build","aegis-hex-geometry.json"),JsonSerializer.Serialize(new{
            vertices=hex.Vertices.Select(v=>new[]{v.x,v.y,v.z}),normals=hex.Normals.Select(v=>new[]{v.x,v.y,v.z}),triangles=hex.Triangles,
            innerRadius=BastionShieldGeometry.SiegeRadius,outerRadius=outer,
            colors=hex.Normals.Select((n,i)=>BastionAegisHexGeometry.Tint(hex.Kinds[i],n,new Vector3(.25f,.12f,.96f).normalized,1,0,1)).Select(c=>new[]{c.r,c.g,c.b,c.a})}));
        File.WriteAllText(Path.Combine(root,".build","shield-geometry.json"),JsonSerializer.Serialize(new{
            normals=sphere.Normals.Select(v=>new[]{v.x,v.y,v.z}),triangles=sphere.Triangles,
            calm=sphere.Normals.Select(n=>BastionShieldGeometry.Tint(n,new Vector3(.25f,.12f,.96f).normalized,0,0,1)).Select(c=>new[]{c.r,c.g,c.b,c.a}),
            impact=sphere.Normals.Select(n=>BastionShieldGeometry.Tint(n,new Vector3(.25f,.12f,.96f).normalized,.22f,
                BastionShieldGeometry.Ripple((float)Math.Acos(Math.Clamp(Vector3.Dot(n,new Vector3(-.45f,.25f,.857f).normalized),-1,1)),.22f,1),1)).Select(c=>new[]{c.r,c.g,c.b,c.a})}));
        Console.WriteLine($"PASS: {count:N0} ability shield ownership, recharge, mode-switch and sphere/ripple checks.");
    }
}
