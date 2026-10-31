using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using IvoryBastion;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;

internal static class LaserCameraChecks
{
    internal static void Run(string root)
    {
        int count=0;
        void Check(bool ok,string name){if(!ok)throw new Exception("Lance/camera: "+name);count++;}
        float Score(Vector3 point)=>BastionLaserRules.TargetScore(Vector3.zero,Vector3.forward,point);
        Check(float.IsFinite(Score(new Vector3(0,0,120))),"a visible target at 120m can be acquired");
        Check(float.IsNegativeInfinity(Score(new Vector3(0,0,120.01f))),"no lock beyond 120m");
        Check(Score(new Vector3(0,0,110))>Score(new Vector3(20,0,25)),"crosshair alignment takes priority over a nearby off-axis enemy");
        Check(Score(new Vector3(0,0,20))>Score(new Vector3(0,0,100)),"aligned target tie favors the nearer enemy");
        Check(Score(new Vector3(0,0,30))>Score(new Vector3(0,0,-30)),"forward target outranks one behind the player");
        Check(float.IsFinite(Score(new Vector3(0,0,-30))),"an available visible enemy remains lockable without precise aim");
        Check(float.IsNegativeInfinity(Score(new Vector3(float.NaN,0,0))),"invalid target coordinates are rejected");
        Check(BastionLaserRules.NeedsFallback(true,false,true),"a missed collider still resolves its visible locked target");
        Check(!BastionLaserRules.NeedsFallback(true,true,true),"a native beam hit cannot receive fallback damage twice");
        Check(!BastionLaserRules.NeedsFallback(true,false,false),"fallback cannot hit through terrain");
        Check(!BastionLaserRules.NeedsFallback(false,false,true),"dead and friendly bodies cannot receive fallback damage");
        foreach(float pitch in new[]{-1f,0,.1f,.4f,.8f,1})
        {
            float previous=0;
            for(int distance=0;distance<=30;distance++)
            {
                float opacity=BastionCameraRules.Opacity(true,true,distance,pitch);
                Check(float.IsFinite(opacity)&&opacity>=.079f&&opacity<=1,"aim obstruction dither remains bounded with an eight percent silhouette");
                Check(opacity>=previous,"the fortress becomes more visible as the camera moves away");previous=opacity;
                Check(BastionCameraRules.Opacity(false,true,distance,pitch)==1,"other players and mobile models retain native fading");
                Check(BastionCameraRules.Opacity(true,false,distance,pitch)==1,"a clear camera ray does not fade the fortress");
            }
        }
        Check(BastionCameraRules.Opacity(true,true,8,1)<BastionCameraRules.Opacity(true,true,8,0),"upward aiming starts fading from farther away");
        Check(BastionCameraRules.Opacity(true,true,13,1)<.081f,"real aim obstruction still fades beyond the old distance cutoff");
        Check(float.IsFinite(BastionCameraRules.Opacity(true,true,4,float.NaN)),"invalid pitch cannot poison the shader fade");
        foreach(float distance in new[]{2f,4,6,8,11,20})foreach(float pitch in new[]{-.45f,-.25f,0,.10f,.14f})
            Check(BastionCameraRules.Opacity(true,true,distance,pitch)==1,"normal camera distance and aim keep the fortress fully opaque");
        Check(BastionCameraRules.Opacity(true,true,11,-.8f)<.081f,"steep downward aiming also clears obstructing armor");
        Check(BastionCameraRules.Opacity(true,true,0,0)<.081f,"camera inside real armor clears the view even at a normal pitch");
        Check(BastionCameraRules.Opacity(true,true,11,.5f,.2f)>BastionCameraRules.Opacity(true,true,11,.5f,1),"grazing the aim margin is gentler than blocking the crosshair");
        Check(BastionCameraRules.Coverage(0)==0&&BastionCameraRules.Coverage(1)==1,"only an actual hit can block the central crosshair");
        Check(BastionCameraRules.Coverage(30)<1&&BastionCameraRules.Coverage(30)>BastionCameraRules.Coverage(2),"surrounding armor softens the transition while preserving an empty center");
        float last=1;for(int step=0;step<=100;step++)
        {
            float opacity=BastionCameraRules.Opacity(true,true,11,step/100f);
            Check(opacity<=last+.00001f,"upward aim fades smoothly without reversing strength");last=opacity;
        }
        foreach(float fps in new[]{30f,60,144,240})
        {
            float opacity=1;
            for(int frame=0;frame<(int)Math.Ceiling(fps*.1f);frame++)opacity=BastionCameraRules.Approach(opacity,.08f,1/fps);
            Check(opacity<=.081f,"an obstructed aim clears within a tenth of a second at different frame rates");
            for(int frame=0;frame<(int)Math.Ceiling(fps*.18f);frame++)opacity=BastionCameraRules.Approach(opacity,1,1/fps);
            Check(opacity==1,"a clear default view settles fully opaque without an endless faint dither");
        }
        Check(BastionCameraRules.Approach(.5f,.08f,0)==.5f,"multiple camera passes cannot advance fading without elapsed time");
        Check(float.IsFinite(BastionCameraRules.Approach(float.NaN,float.NaN,float.NaN)),"invalid timing or state cannot poison camera opacity");
        var min=new Vector3(-.5f,-.5f,-.5f);var max=new Vector3(.5f,.5f,.5f);
        Check(BastionCameraRules.RayBox(new Vector3(0,0,-4),Vector3.forward,min,max,out float hit)&&Math.Abs(hit-3.5f)<.001f,"ray measures distance to an actual obstructing part");
        Check(!BastionCameraRules.RayBox(new Vector3(0,0,-4),Vector3.back,min,max,out _),"parts behind the camera cannot trigger a fade");
        Check(!BastionCameraRules.RayBox(new Vector3(1,0,-4),Vector3.forward,min,max,out _),"a ray alongside armor remains unobstructed");
        Check(BastionCameraRules.RayBox(new Vector3(0,0,-2),Vector3.forward*.5f,min,max,out hit)&&Math.Abs(hit-3)<.001f,"bone scaling preserves world-space intersection distance");
        Check(BastionCameraRules.RayBox(Vector3.zero,Vector3.forward,min,max,out hit)&&hit==0,"camera inside a real part is detected");
        Check(!BastionCameraRules.RayBox(Vector3.zero,Vector3.zero,min,max,out _),"invalid camera direction is rejected");
        Check(!BastionCameraRules.RayBox(new Vector3(0,0,-10),Vector3.forward,new Vector3(-3,-1,-1),new Vector3(-1,1,1),out _)&&
            !BastionCameraRules.RayBox(new Vector3(0,0,-10),Vector3.forward,new Vector3(1,-1,-1),new Vector3(3,1,1),out _),"empty space between armor parts does not count as obstruction");

        using var built=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll"));
        var controller=built.MainModule.Types.Single(t=>t.Name=="BastionController");
        var shoot=controller.Methods.Single(m=>m.Name=="Shoot").Body.Instructions;
        int find=shoot.ToList().FindIndex(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionLaserTracker"&&m.Name=="Find");
        int cooldown=shoot.ToList().FindIndex(i=>i.Operand is FieldReference f&&f.Name=="Laser"&&i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld);
        Check(find>=0&&cooldown>find,"the host acquires a target before spending laser cooldown");
        Check(shoot.Take(find).Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="NetworkServer"&&m.Name=="get_active"),"lock acquisition and damage remain server-authoritative");
        var combat=built.MainModule.Types.Single(t=>t.Name=="BastionCombat");
        var bullet=combat.Methods.Single(m=>m.Name=="Bullet").Body.Instructions;
        foreach(string callback in new[]{"TakeDamage","OnHitEnemy","OnHitAll"})
            Check(bullet.Any(i=>i.Operand is MethodReference m&&m.Name==callback),"locked fallback preserves native "+callback);
        using var game=AssemblyDefinition.ReadAssembly(@"X:\SteamLibrary\steamapps\common\Risk of Rain 2\Risk of Rain 2_Data\Managed\RoR2.dll");
        var characterModel=game.MainModule.Types.Single(t=>t.Name=="CharacterModel");
        var cameraUpdate=characterModel.Methods.Single(m=>m.Name=="UpdateForCamera");
        Check(cameraUpdate.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="fade"&&i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld),"native per-camera dither hook is present");
        var nativeInstructions=cameraUpdate.Body.Instructions.ToArray();
        int consumer=Array.FindIndex(nativeInstructions,i=>i.OpCode.Code==Code.Ldsfld&&i.Operand is FieldReference f&&f.Name=="fadeEpsilon"&&i.Previous?.Operand is FieldReference previous&&previous.Name=="fade");
        var entry=nativeInstructions[consumer-2];
        var incoming=nativeInstructions.Where(i=>ReferenceEquals(i.Operand,entry)).ToArray();
        var oldWrites=nativeInstructions.Where(i=>i.OpCode.Code==Code.Stfld&&i.Operand is FieldReference f&&f.Name=="fade").ToArray();
        Check(incoming.Length>0,"the normal camera path branches into the shared fade consumer");
        var fadeProbe=new MethodReference("OfflineFadeProbe",game.MainModule.TypeSystem.Single,characterModel){HasThis=false};
        fadeProbe.Parameters.Add(new ParameterDefinition(game.MainModule.TypeSystem.Single));
        fadeProbe.Parameters.Add(new ParameterDefinition(characterModel));
        fadeProbe.Parameters.Add(new ParameterDefinition(cameraUpdate.Parameters[0].ParameterType));
        using(var context=new ILContext(cameraUpdate))context.Invoke(il=>BastionCameraHook.Apply(il,c=>c.Emit(OpCodes.Call,fadeProbe)));
        var patched=cameraUpdate.Body.Instructions.ToList();
        int call=patched.FindIndex(i=>ReferenceEquals(i.Operand,fadeProbe));
        Check(call>=5&&oldWrites.All(i=>patched.IndexOf(i)<call),"the real patch runs after every native fade assignment");
        Check(incoming.All(i=>ReferenceEquals(i.Operand,patched[call-5])),"the normal branch enters the injected fade instead of skipping it");
        Check(patched[call+1].OpCode.Code==Code.Stfld&&patched[call+1].Operand is FieldReference result&&result.Name=="fade","the patched value is stored before visibility and material updates");
        var model=built.MainModule.Types.Single(t=>t.Name=="BastionModel");
        var material=model.Methods.Single(m=>m.Name=="Material").Body.Instructions;
        Check(material.Any(i=>i.OpCode.Code==Code.Ldstr&&(string)i.Operand=="DITHER")&&material.Any(i=>i.Operand is MethodReference m&&m.Name=="EnableKeyword"),"custom materials enable the native dither shader variant");
        Check(model.Methods.Single(m=>m.Name=="Rig").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionCameraFade"&&m.Name=="Prepare"),"part bounds are prepared while loading the model");
        var fade=built.MainModule.Types.Single(t=>t.Name=="BastionCameraFade").Methods.Single(m=>m.Name=="Fade");
        Check(!fade.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="Mesh"&&(m.Name=="get_vertices"||m.Name=="set_vertices")),"camera callback never reads or replaces mesh vertex buffers");
        Console.WriteLine("PASS: "+count+" laser lock, single-hit fallback, native item-event and owner-camera dither checks.");
    }
}
