using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using IvoryBastion;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
internal static class Program
{
    private static int count;private static BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private static Dictionary<string,BastionSpiderVisual> Mines=>(Dictionary<string,BastionSpiderVisual>)typeof(BastionSpiderVisual).GetField("live",Hidden).GetValue(null);
    private static Dictionary<CharacterBody,BastionMineTargetMark> Marks=>(Dictionary<CharacterBody,BastionMineTargetMark>)typeof(BastionMineTargetMark).GetField("live",Hidden).GetValue(null);
    private static void Check(bool pass,string why){count++;if(!pass)throw new Exception(why);}
    private static void Near(Vector3 a,Vector3 b,string why)=>Check((a-b).magnitude<.001f,$"{why}: {a}, expected {b}");
    private static void Call(object instance,string method)=>instance.GetType().GetMethod(method,Hidden).Invoke(instance,null);
    private static void Reset()
    {
        foreach(var mine in Mines.Values.ToArray())UnityEngine.Object.Destroy(mine.gameObject);
        foreach(var mark in Marks.Values.ToArray())UnityEngine.Object.Destroy(mark.gameObject);
        ((Dictionary<string,float>)typeof(BastionSpiderVisual).GetField("ended",Hidden).GetValue(null)).Clear();
        Util.objects.Clear();Time.time=0;Time.deltaTime=.02f;Application.isBatchMode=false;
        Camera.main=new GameObject("camera").AddComponent<Camera>();
    }
    private static CharacterBody Target(uint id=7)
    {
        var go=new GameObject("target");var body=go.AddComponent<CharacterBody>();body.healthComponent=go.AddComponent<HealthComponent>();body.mainHurtBox=go.AddComponent<HurtBox>();go.AddComponent<Collider>();Util.objects[id]=go;return body;
    }
    private static BastionEffect Message(uint id,CharacterBody target,Vector3 local,float age=2,BastionMinePhase phase=BastionMinePhase.Attached)
        =>new(){Owner=new(1),Id=id,Target=new(7),A=target?target.transform.TransformPoint(local):new Vector3(.55f,1,0),B=local,Value=(int)phase+age*.01f};
    private static BastionSpiderVisual Receive(uint id,CharacterBody target,Vector3 local,float age=2,BastionMinePhase phase=BastionMinePhase.Attached)
    {BastionSpiderVisual.Receive(Message(id,target,local,age,phase),null,target);return Mines["1:"+id];}
    private static void Tick(float dt=.02f)
    {
        Time.deltaTime=dt;Time.time+=dt;
        foreach(var mine in Mines.Values.ToArray())if(mine)Call(mine,"Update");
        foreach(var mark in Marks.Values.ToArray())if(mark)Call(mark,"LateUpdate");
    }
    private static void FollowAndRefresh()
    {
        Reset();var target=Target();var anchor=new Vector3(.55f,.95f,.1f);var mine=Receive(1,target,anchor);
        Near(mine.transform.position,target.transform.TransformPoint(anchor),"Initial snapshot clamps to host local anchor");
        for(int i=0;i<240;i++)
        {
            target.transform.position=new(i*.17f,MathF.Sin(i*.03f),-i*.1f);target.transform.rotation=Quaternion.Euler(i%20,i*2,0);
            target.transform.localScale=new(1+(i%7)*.07f,1.1f,1);
            if(i%9==0)BastionSpiderVisual.Receive(Message(1,target,anchor,2+i*.02f),null,target);
            Tick();Near(mine.transform.position,target.transform.TransformPoint(anchor),"Attached mine follows movement, rotation and scale between snapshots");
            Check(mine.AttachedTarget==target&&Marks.Count==1,"Attached target retains exactly one visible mark");
        }
        target.transform.position=new(1500,80,-200);Tick();Near(mine.transform.position,target.transform.TransformPoint(anchor),"Teleport carries the attached mine immediately");
        var stale=Message(1,target,new Vector3(-9,0,0),1,BastionMinePhase.Planted);BastionSpiderVisual.Receive(stale,null,target);
        Check(mine.Attached,"Old snapshot cannot revert attached mine to ground phase");
        var fresh=Message(1,target,anchor,8);fresh.A=new(-900,-900,-900);BastionSpiderVisual.Receive(fresh,null,target);
        Near(mine.transform.position,target.transform.TransformPoint(anchor),"Authoritative local B wins over latency-stale world A");
        Check(Vector3.Dot(mine.transform.up,(mine.transform.position-target.corePosition).normalized)>.999f,"Mine carapace faces out from the collider surface");
        Check(Marks.Values.Single().gameObject.GetComponentsInChildren<LineRenderer>().All(l=>l.points.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z))),"All electrical mark geometry remains finite after teleport");
    }
    private static void SharedMarkerAndCleanup()
    {
        Reset();var target=Target();for(uint i=1;i<=24;i++)Receive(i,target,new Vector3(.55f,1,i*.002f));Tick();
        Check(Mines.Count==24&&Marks.Count==1,"Legacy/excess snapshots still share one target halo instead of allocating per-mine target effects");
        for(uint i=1;i<24;i++)BastionSpiderVisual.End(1,i);Check(Marks.Count==1,"Other attached mines retain shared target mark");
        Check(BastionSpiderVisual.End(1,24),"First terminal event accepted");Check(Mines.Count==0&&Marks.Count==0,"Last mine removes target marker without lingering state");
        Check(!BastionSpiderVisual.End(1,24),"Duplicate terminal feedback suppressed");
        BastionSpiderVisual.Receive(Message(24,target,new(.55f,1,0),10),null,target);Check(Mines.Count==0,"Delayed snapshot cannot resurrect an ended mine");
        Receive(25,target,new(.55f,1,0));target.healthComponent.alive=false;Tick();
        Check(Mines.Count==0&&Marks.Count==0,"Unprimed target death removes mine and marker harmlessly");
        Reset();target=Target();Receive(30,target,new(.55f,1,0));UnityEngine.Object.Destroy(target.gameObject);Tick();
        Check(Marks.Count==0,"Destroyed target cannot leave a stale marker registry entry");
    }
    private static void PrimedCarrierDeath()
    {
        Reset();var target=Target();var anchor=new Vector3(.55f,1,0);var mine=Receive(1,target,anchor,2,BastionMinePhase.Primed);var at=mine.transform.position;
        target.healthComponent.alive=false;target.transform.position=new(300,30,0);Tick(.05f);
        Check(mine&&mine.Overcharged,"Primed mine remains visible when its triggering weapon kills the carrier");Near(mine.transform.position,at,"Primed corpse transition preserves last valid impact point");
        BastionSpiderVisual.End(1,1);Check(Marks.Count==0,"Special detonation terminal event cleans primed marker");
    }
    private static void MissingTargetRecoveryAndTimeout()
    {
        Reset();var local=new Vector3(.55f,1,0);var mine=Receive(1,null,local);Tick(.1f);Check(Marks.Count==0,"Unresolved target never displays a misleading detached target halo");
        Check(mine.gameObject.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"Waiting attachment stays hidden until its carrier exists, rather than floating at a stale world position");
        var target=Target();target.transform.position=new(100,3,-40);Tick(.11f);
        Check(mine.AttachedTarget==target&&Marks.Count==1,"Late network body recovers attachment without needing a new spawn effect");Near(mine.transform.position,target.transform.TransformPoint(local),"Late-resolved body uses stored host anchor");
        Check(mine.gameObject.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"Resolved attachment restores its physical mine renderers");
        Reset();Receive(1,null,local);for(int i=0;i<14;i++)Tick(.1f);Check(Mines.Count==0&&Marks.Count==0,"Missing target cannot leave a mine floating at an old world point forever");
        Reset();target=Target();Receive(2,target,local);for(int i=0;i<14;i++)Tick(.1f);Check(Mines.Count==0&&Marks.Count==0,"Lost state stream removes both mine and target halo");
    }
    private static (SkinnedMeshRenderer Skin,Transform Torso) Skin(CharacterBody target)
    {
        var model=new GameObject("actual character skin");model.transform.SetParent(target.transform);
        var torso=new GameObject("animated torso").transform;torso.SetParent(model.transform);
        var skin=model.AddComponent<SkinnedMeshRenderer>();skin.rootBone=model.transform;skin.bones=new[]{model.transform,torso};
        skin.sharedMesh=new Mesh{vertices=new[]{new Vector3(.3f,.2f,-.5f),new Vector3(.3f,1.8f,-.5f),new Vector3(.3f,1.8f,.5f),new Vector3(.3f,.2f,.5f)},triangles=new[]{0,1,2,0,2,3},boneWeights=Enumerable.Repeat(new BoneWeight{boneIndex0=1,weight0=1},4).ToArray()};
        model.AddComponent<CharacterModel>().baseRendererInfos=new[]{new CharacterModel.RendererInfo{renderer=skin}};return(skin,torso);
    }
    private static void ModelSurfaceAndAnimation()
    {
        Reset();var target=Target();var(skin,torso)=Skin(target);var seed=new Vector3(.8f,1,0);var mine=Receive(1,target,seed);
        Near(mine.transform.position,seed,"Contact starts from the authoritative arriving mine, avoiding a collider-to-model teleport");
        int baked=SkinnedMeshRenderer.Bakes;Tick(.1f);float halfway=mine.transform.position.x;
        Check(halfway>.34f&&halfway<.8f,"Magnetic contact ease closes the gap toward the actual rendered surface");
        Tick(.1f);Near(mine.transform.position,new(.34f,1,0),"Contact rests exactly four centimetres above the rendered triangle rather than floating on a broad hurtbox");
        Check(Vector3.Dot(mine.transform.up,Vector3.right)>.99f,"Clamped feet align to the actual triangle normal");
        for(int i=0;i<200;i++)
        {
            torso.localPosition=new(MathF.Sin(i*.02f)*.2f,i*.001f,MathF.Sin(i*.03f)*.1f);torso.localRotation=Quaternion.Euler(i*.2f,0,i*.15f);
            target.transform.position=new(i*.1f,0,-i*.03f);target.transform.localScale=new(1+i*.001f,1.1f,1);target.transform.rotation=Quaternion.Euler(0,i,0);
            if(i%8==0)BastionSpiderVisual.Receive(Message(1,target,seed,2+i*.02f),null,target);
            Tick();Near(mine.transform.position,torso.TransformPoint(new(.34f,1,0)),"Attached model follows real animated bone motion, body rotation and scale without waiting on another host packet");
        }
        Check(SkinnedMeshRenderer.Bakes==baked,"Repeated snapshots and rendered updates do not rebake the mesh");
        Check(skin.Floats.Count==0&&skin.Colors.Count==0,"Blue latch glow only affects the spider, never the entire enemy's model");
        var shell=mine.gameObject.GetComponent<Renderer>();Check(shell.Floats["_EmPower"]>2&&shell.Colors["_EmColor"].b>shell.Colors["_EmColor"].r,"Attached mine retains a distinct blue emissive core");
        var ring=(LineRenderer)typeof(BastionSpiderVisual).GetField("contactRim",Hidden).GetValue(mine);
        Check(ring.enabled&&ring.color.b>ring.color.r&&ring.points.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),"Blue contact corona stays finite and visible while carried");
        Reset();target=Target();(skin,torso)=Skin(target);skin.sharedMesh.isReadable=false;
        var surface=new BastionMineSurfaceAttachment();Vector3 contact=default,normal=default;Check(surface.Bind(target,seed)&&surface.Follow(out contact,out normal),"GPU-only source skin can still bake a contact and fall back to a real joint");
        Check(MathF.Abs(contact.x-.34f)<.001f,"Unreadable CPU source still projects to the actual baked model");
        Reset();target=Target();(skin,torso)=Skin(target);skin.FailBake=true;mine=Receive(1,target,seed);Tick(.25f);
        Near(mine.transform.position,seed,"Unsupported custom skin safely retains host attachment without breaking its VFX update");
        Reset();target=Target();(skin,torso)=Skin(target);skin.enabled=false;surface=new();Check(!surface.Bind(target,seed),"Hidden model surfaces cannot steal contact placement");
        Reset();target=Target();var rigid=new GameObject("rigid armor");rigid.transform.SetParent(target.transform);var renderer=rigid.AddComponent<MeshRenderer>();rigid.AddComponent<MeshFilter>().sharedMesh=new Mesh{vertices=new[]{new Vector3(.3f,0,-1),new Vector3(.3f,2,-1),new Vector3(.3f,1,1)},triangles=new[]{0,1,2}};
        rigid.AddComponent<CharacterModel>().baseRendererInfos=new[]{new CharacterModel.RendererInfo{renderer=renderer}};surface=new();Check(surface.Bind(target,seed)&&surface.Follow(out contact,out normal),"Rigid mesh armor also provides an actual model surface");Near(contact,new(.34f,1,0),"Rigid mesh projection is accurate");
        var p=BastionMineSurfaceAttachment.Closest(new(2,2,2),Vector3.zero,Vector3.right,Vector3.up,out var bary);Near(p,new(.5f,.5f,0),"Closest-triangle projection clamps an outside query to the edge");Near(bary,new(0,.5f,.5f),"Triangle contact keeps matching barycentric weights");
        Check(BastionMinePresentation.Clamp(0)==0&&BastionMinePresentation.Clamp(.22f)==1&&BastionMinePresentation.LatchFlash(0)==1&&BastionMinePresentation.LatchFlash(.38f)==0,"Latch has a timed brace, contact flash and settled pose");
        Reset();target=Target();(skin,torso)=Skin(target);surface=new();Check(surface.Bind(target,new(.1f,1,0))&&surface.Follow(out contact,out normal),"An undersized hurtbox seed still finds the visible skin");Near(contact,new(.34f,1,0),"A seed inside armor cannot invert the shell into the model");
        foreach(var category in new[]{(Boss:false,Elite:false,Count:5),(Boss:false,Elite:true,Count:8),(Boss:true,Elite:true,Count:12)})
        {
            Reset();target=Target();target.isBoss=category.Boss;target.isElite=category.Elite;
            for(uint i=1;i<=category.Count;i++)Receive(i,target,new(.55f,1,i*.05f));Tick(.3f);
            var ticks=(LineRenderer[])typeof(BastionMineTargetMark).GetField("ticks",Hidden).GetValue(Marks.Values.Single());
            Check(ticks.Count(t=>t.color.a>.5f)==category.Count,"Carrier mark displays the correct normal/elite/boss number of occupied pips");
            Check(ticks.Skip(category.Count).All(t=>t.color.a==0),"Unused category slots stay hidden rather than implying extra capacity");
        }
    }
    private static object V(Vector3 p)=>new[]{p.x,p.y,p.z};private static object Q(Quaternion p)=>new[]{p.q.X,p.q.Y,p.q.Z,p.q.W};
    private static void Export(string path)
    {
        Reset();Camera.main.transform.rotation=Quaternion.Euler(-7,35,0);var target=Target();var mine=Receive(1,target,new(.52f,.95f,.15f));for(int i=0;i<20;i++)Tick();
        var marker=Marks.Values.Single();var bones=(Transform[])typeof(BastionSpiderVisual).GetField("bones",Hidden).GetValue(mine);
        var data=new{position=V(mine.transform.position),rotation=Q(mine.transform.rotation),bones=bones.Select(b=>new{name=b.name,rotation=Q(b.localRotation)}),lines=marker.gameObject.GetComponentsInChildren<LineRenderer>().Select(l=>new{points=l.points.Select(V),width=l.width,color=new[]{l.color.r,l.color.g,l.color.b,l.color.a}})};
        System.IO.File.WriteAllText(path,JsonSerializer.Serialize(data));Console.WriteLine("Exported real attached mine pose and target-mark geometry");
    }
    public static int Main(string[] args)
    {
        try{if(args.Length==2&&args[0]=="--export"){Export(args[1]);return 0;}FollowAndRefresh();SharedMarkerAndCleanup();PrimedCarrierDeath();MissingTargetRecoveryAndTimeout();ModelSurfaceAndAnimation();Reset();Console.WriteLine($"Mine attached visuals: {count:N0} production-source assertions passed; no game/device launched.");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
