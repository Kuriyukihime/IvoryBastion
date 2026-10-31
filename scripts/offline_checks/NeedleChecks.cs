using System;
using System.IO;
using System.Linq;
using IvoryBastion;
using Mono.Cecil;
using UnityEngine;

internal static class NeedleChecks
{
    internal static void Run(string root)
    {
        int count=0;
        void Check(bool ok,string message){count++;if(!ok)throw new Exception("Needlefire assist: "+message);}
        Vector3 At(float degrees,float distance,float azimuth=0)
        {
            double a=degrees*Math.PI/180,b=azimuth*Math.PI/180;
            return new Vector3((float)(Math.Sin(a)*Math.Cos(b)*distance),(float)(Math.Sin(a)*Math.Sin(b)*distance),(float)(Math.Cos(a)*distance));
        }
        float Score(Vector3 point,Vector3? aim=null,float range=45)=>BastionNeedleRules.Score(Vector3.zero,aim??Vector3.forward,point,range);
        Check(BastionNeedleRules.ConeDegrees==6,"mobile assist uses the requested six-degree cone");
        foreach(float distance in new[]{1f,10,30,44.99f})
        foreach(float azimuth in new[]{0f,45,90,180,270})
        {
            foreach(float angle in new[]{0f,1,3,3.99f,4.01f,5,5.99f})Check(float.IsFinite(Score(At(angle,distance,azimuth))),"expanded cone accepts horizontal and vertical offsets, including the former four-to-six-degree gap");
            foreach(float angle in new[]{6.01f,7,15,90,180})Check(float.IsNegativeInfinity(Score(At(angle,distance,azimuth))),"outside six degrees cannot snap to an off-screen enemy");
        }
        Check(float.IsFinite(Score(new Vector3(0,0,45)))&&float.IsNegativeInfinity(Score(new Vector3(0,0,45.01f))),"reticle and bullet respect current range");
        Check(Score(At(.5f,44))>Score(At(3,2)),"cursor proximity wins over a much closer off-axis enemy");
        Check(Score(new Vector3(0,0,5))==Score(new Vector3(0,0,40)),"exact angular ties reach the tracker's distance tie-break");
        Check(Score(At(3,30),Vector3.forward*3)==Score(At(3,30)),"aim magnitude does not widen assistance");
        Check(float.IsFinite(Score(new Vector3(0,0,70),range:80))&&float.IsNegativeInfinity(Score(new Vector3(0,0,70))),"host-configured range remains effective");
        foreach(var point in new[]{Vector3.zero,new Vector3(float.NaN,0,10),new Vector3(float.PositiveInfinity,0,10)})Check(float.IsNegativeInfinity(Score(point)),"invalid or zero-length targets are rejected");
        foreach(var aim in new[]{Vector3.zero,new Vector3(float.NaN,0,1),new Vector3(0,float.PositiveInfinity,1)})Check(float.IsNegativeInfinity(Score(Vector3.forward*10,aim)),"invalid aim cannot acquire anything");
        foreach(float range in new[]{0f,-1,float.NaN,float.PositiveInfinity})Check(float.IsNegativeInfinity(Score(Vector3.forward*10,range:range)),"invalid ranges cannot acquire anything");

        using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll"));
        var tracker=assembly.MainModule.Types.Single(t=>t.Name=="BastionNeedleTracker");
        string[] Calls(MethodDefinition method)=>method.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>{var m=(MethodReference)i.Operand;return m.DeclaringType.Name+"::"+m.Name;}).ToArray();
        var find=Calls(tracker.Methods.Single(m=>m.Name=="Find"));
        foreach(string call in new[]{"BastionCombat::Enemy","BastionNeedleRules::Score","Physics::Linecast","BastionLaserTracker::Point"})Check(find.Contains(call),"live selection retains "+call);
        var direction=Calls(tracker.Methods.Single(m=>m.Name=="Direction"));
        Check(direction.Contains("BastionNeedleTracker::Find")&&direction.Contains("BastionLaserTracker::Point"),"shots and reticle use the same selector and hit point");
        var update=Calls(tracker.Methods.Single(m=>m.Name=="Update"));
        foreach(string call in new[]{"BastionController::get_Alive","CharacterBody::get_hasEffectiveAuthority","BastionNeedleTracker::Find","Indicator::set_active","BastionBalance::Get"})Check(update.Contains(call),"local reticle lifecycle retains "+call);
        Check(tracker.Methods.Single(m=>m.Name=="Update").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.DeclaringType.Name=="Indicator"&&f.Name=="targetTransform"),"native indicator is anchored to the selected enemy");
        var bullet=assembly.MainModule.Types.Single(t=>t.Name=="BastionCombat").Methods.Single(m=>m.Name=="Bullet");
        var bulletCalls=Calls(bullet);
        Check(bulletCalls.Count(c=>c=="BastionNeedleTracker::Direction")==1&&bulletCalls.Contains("BulletAttack::Fire"),"one aim adjustment retains native hitscan and item procs");
        var shoot=Calls(assembly.MainModule.Types.Single(t=>t.Name=="BastionController").Methods.Single(m=>m.Name=="Shoot"));
        Check(shoot.Contains("NetworkServer::get_active")&&shoot.Contains("BastionCombat::Bullet")&&shoot.Contains("BastionBank::LoadNeedleHit"),"host still owns damage and hit-confirmed ammunition recovery");
        var create=assembly.MainModule.Types.Single(t=>t.Name=="BastionContent").Methods.Single(m=>m.Name=="Create");
        Check(create.Body.Instructions.Any(i=>i.Operand is GenericInstanceMethod m&&m.Name=="AddComponent"&&m.GenericArguments.Any(a=>a.Name=="BastionNeedleTracker")),"survivor prefab actually installs the reticle");
        Console.WriteLine($"PASS: {count} Needlefire aim-cone, range and native integration checks.");
    }
}
