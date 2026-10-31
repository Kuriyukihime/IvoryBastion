using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using IvoryBastion;
internal static class PresentationChecks
{
    internal sealed class Request {public string Form="mobile";public BastionPoseInput Input=new BastionPoseInput();}
    internal static void Export(string source,string destination)
    {
        var options=new JsonSerializerOptions{IncludeFields=true,PropertyNameCaseInsensitive=true};
        var rows=JsonSerializer.Deserialize<Request[]>(File.ReadAllText(source),options);
        var frames=rows.Select(row=>{var f=new BastionPoseFrame();if(row.Form=="siege")BastionChoreography.Siege(row.Input,f);else BastionChoreography.Mobile(row.Input,f);return f;}).ToArray();
        File.WriteAllText(destination,JsonSerializer.Serialize(frames,options));
    }
    internal static void Run()
    {
        for(int i=0;i<=100;i++)foreach(bool siege in new[]{false,true})
        {
            var p=new BastionPoseInput{Time=i*.04f,PowerAge=i*.015f,Forward=.9f,Strafe=.7f,Vertical=-.4f,Brake=.8f,Turn=.7f,Pitch=45,Aim=1,Boost=1,Unfold=i/100f,LaserKick=1,LeftKick=1,RightKick=.4f,MissileKick=1,LeftSkyfallKick=1,RightSkyfallKick=1,Defense=1,Overdrive=1,Salvo=1,HatchAges=Enumerable.Repeat(i*.009f,72).ToArray()};
            var f=new BastionPoseFrame();if(siege)BastionChoreography.Siege(p,f);else BastionChoreography.Mobile(p,f);
            if(!float.IsFinite(f.Scale)||f.Scale<=0)throw new Exception("Invalid presentation scale");
            foreach(var b in f.Bones.Values)foreach(var v in new[]{b.Rotation,b.Offset})if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new Exception("Invalid animation channel");
            if(siege&&f.Bones.Count!=101)throw new Exception("Missing articulated fortress channels: "+f.Bones.Count);
            if(siege)foreach(var name in new[]{"skyfall_l","skyfall_r"})
                if(f.Bones[name].Offset.Y<-.036f)throw new Exception("Cradles must stay above the rear feet during deployment");
        }
        if(BastionChoreography.Hatch(-.1f)!=0||BastionChoreography.Hatch(1)!=0||BastionChoreography.Hatch(.15f)<.99f)throw new Exception("Launcher hatch timing regression");
        var deployed=new BastionPoseFrame();BastionChoreography.Siege(new BastionPoseInput{Unfold=1,Salvo=1,Defense=1},deployed);
        foreach(var name in new[]{"rack_l","rack_r","shield_l","shield_r"})
            if(Math.Abs(deployed.Bones[name].Rotation.Y)>.001f)throw new Exception("Deployed missile bank must face forward: "+name);
        if(Math.Abs(deployed.Bones["throne"].Offset.Y-deployed.Bones["torso"].Offset.Y)>.001f)throw new Exception("Seat and pilot lifts must agree");
        if(BastionBank.LaserBlastRadius!=8)throw new Exception("Ion Lance impact radius must be eight metres");
        foreach(var name in new[]{"skyfall_l","skyfall_r"})if(deployed.Bones[name].Rotation!=Vector3.Zero)throw new Exception("Skyfall magazines must launch vertically");
        foreach(var size in new[]{new Vector2(1280,720),new Vector2(1920,1080),new Vector2(3440,1440)})foreach(float angle in new[]{-10f,0f,10f})
        {
            var screen=new HudPlacement.Box(0,0,size.X,size.Y);var skills=new HudPlacement.Box(size.X-440,24,size.X-24,150);
            HudPlacement.AboveSkills(screen,skills,HudPlacement.CombatWidth,HudPlacement.CombatHeight,angle,out float x,out float y);var b=HudPlacement.Bounds(x,y,HudPlacement.CombatWidth,HudPlacement.CombatHeight,angle);
            if(b.Bottom<=skills.Top||b.Left<0||b.Right>size.X||b.Top>size.Y)throw new Exception("Cockpit overlaps skills or screen edge");
        }
        Console.WriteLine("PASS compiled Seraph choreography, launcher doors and responsive cockpit placement.");
    }
}
