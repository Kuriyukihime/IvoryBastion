using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using IvoryBastion;

internal sealed class PoseRequest
{
    public string Form="mobile";
    public BastionPoseInput Input=new BastionPoseInput();
}
internal static class Program
{
    private static int checks;
    private static readonly JsonSerializerOptions Json=new JsonSerializerOptions { IncludeFields=true,PropertyNameCaseInsensitive=true,WriteIndented=true };
    private static void Require(bool condition,string message)
    {checks++;if(!condition)throw new InvalidOperationException(message);}
    private static BastionPoseFrame Pose(BastionPoseInput input,bool siege=false)
    {var frame=new BastionPoseFrame();if(siege)BastionChoreography.Siege(input,frame);else BastionChoreography.Mobile(input,frame);return frame;}
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
    static void Main(string[] args)
    {
#if !BASELINE
        if(args.Length==2&&args[0]=="--detonations")
        {
            var records=new System.Collections.Generic.List<object>();
            foreach(bool review in new[]{false,true})foreach(int activeCount in new[]{1,24})for(int tier=0;tier<3;tier++)foreach(float time in new[]{.08f,.22f,.55f,1f})
            {
                float radius=new[]{5f,10f,15f}[tier];var samples=new System.Collections.Generic.List<object>();
                for(int layer=0;layer<4;layer++)
                {
                    var kind=(BastionExplosionLayer)layer;int original=BastionExplosionGeometry.Count(kind,tier);
                    int count=review?BastionDetonationPresentation.Count(kind,tier,original,activeCount):original;
                    for(int i=0;i<count;i++)
                    {
                        var sample=BastionExplosionGeometry.At(kind,i*original/count,tier,radius,time,173);
                        if(sample.Alpha<=0||sample.Remaining<=0)continue;
                        samples.Add(new{layer,position=new[]{sample.Position.x,sample.Position.y,sample.Position.z},size=sample.Size,alpha=sample.Alpha*(review?BastionDetonationPresentation.Opacity(kind):1),rotation=sample.Rotation,age=1-sample.Remaining/sample.Life});
                    }
                }
                records.Add(new{review,active=activeCount,tier,radius,time,samples});
            }
            File.WriteAllText(args[1],JsonSerializer.Serialize(records,Json));Console.WriteLine("Exported actual old/new explosion geometry, budget and opacity");return;
        }
        if(args.Length==2&&args[0]=="--energy")
        {
            float[] V(UnityEngine.Vector3 v)=>new[]{v.x,v.y,v.z};
            var origin=UnityEngine.Vector3.zero;var tip=UnityEngine.Vector3.up;var side=UnityEngine.Vector3.right;
            var profile=Enumerable.Range(0,129).Select(i=>BastionEffectGeometry.EnergyFalloff(i/128f)).ToArray();
            var before=Enumerable.Range(0,2).Select(layer=>Enumerable.Range(0,4).Select(corner=>V(BastionEffectGeometry.Feather(origin,tip,side,1,layer,corner))).ToArray()).ToArray();
            var after=Enumerable.Range(0,2).Select(layer=>Enumerable.Range(0,BastionEffectGeometry.FeatherSections+1).Select(section=>Enumerable.Range(0,3).Select(edge=>V(BastionEffectGeometry.FeatherRibbon(origin,tip,side,1,layer,section/(float)BastionEffectGeometry.FeatherSections,edge-1))).ToArray()).ToArray()).ToArray();
            File.WriteAllText(args[1],JsonSerializer.Serialize(new{profile,before,after},Json));Console.WriteLine("Exported actual energy cross-section and feather geometry");return;
        }
#endif
        if(args.Length==3&&args[0]=="--export")
        {
            var requests=JsonSerializer.Deserialize<PoseRequest[]>(File.ReadAllText(args[1]),Json);
            File.WriteAllText(args[2],JsonSerializer.Serialize(requests.Select(r=>Pose(r.Input,r.Form=="siege")).ToArray(),Json));
            Console.WriteLine("Exported "+requests.Length+" compiled production poses");return;
        }
#if !BASELINE
        Require(BastionChoreography.Recoil(-1)==0&&BastionChoreography.Recoil(2)==0,"Inactive recoil must settle");
        Require(BastionChoreography.CannonHeat(.9f)>.3f&&BastionChoreography.Recoil(.9f)==0,"Cooling vents must remain open after breech returns");
        Require(BastionChoreography.CannonHeat(2)==0,"Cooling vents must close");
        Require(typeof(UnityEngine.Renderer).GetProperty("localBounds")?.CanWrite==true,"Installed renderer supports conservative custom local bounds");
        foreach(float radius in new[]{.5f,5f,8f,10f,15f,32f})for(int tier=0;tier<3;tier++)
        {
            float extent=BastionDetonationPresentation.BoundsExtent(radius);
            Require(extent>radius*1.4142f,"Full diagonal remains visible even if initial ignition is offscreen");
            for(float age=0;age<BastionExplosionGeometry.Lifetime(tier);age+=.025f)for(int layer=0;layer<4;layer++)
            {
                var kind=(BastionExplosionLayer)layer;
                for(int particle=0;particle<BastionExplosionGeometry.Count(kind,tier);particle++)
                {
                    var s=BastionExplosionGeometry.At(kind,particle,tier,radius,age,173);if(s.Alpha<=0||s.Remaining<=0)continue;
                    float corner=s.Size*.707107f;
                    Require(Math.Abs(s.Position.x)+corner<=extent&&Math.Abs(s.Position.y)+corner<=extent&&Math.Abs(s.Position.z)+corner<=extent,"Conservative bounds contain every billboard at every age");
                }
            }
        }
        Require(BastionDetonationPresentation.BoundsExtent(15)>BastionDetonationPresentation.BoundsExtent(5),"Reused larger blasts need larger bounds");
        for(int tier=0;tier<3;tier++)foreach(float step in new[]{1f/144,1f/60,1f/20,.2f})
        {
            float lastSample=0,firstSmoke=-1,delay=BastionExplosionGeometry.FireTime(tier)*.2f;
            int samples=1,frames=0;
            for(float age=step;age<BastionExplosionGeometry.Lifetime(tier);age+=step)
            {
                frames++;
                if(!BastionDetonationPresentation.RequiresSample(age,lastSample,false))continue;
                Require(age-lastSample<=.15f+step+.0001f,"Offscreen sample cadence must remain bounded through frame stalls");
                lastSample=age;samples++;
                var smoke=BastionExplosionGeometry.At(BastionExplosionLayer.Smoke,0,tier,5,age,173);
                if(firstSmoke<0&&smoke.Alpha>0&&smoke.Remaining>0)firstSmoke=age;
            }
            Require(firstSmoke>=delay&&firstSmoke<=delay+.15f+step+.0001f,"Delayed smoke must appear even when no particle renderer has ever been visible");
            if(step<=1f/60)Require(samples<frames/3,"Periodic offscreen sampling must retain substantial work savings");
            float reentry=BastionExplosionGeometry.FireTime(tier)+.05f;
            Require(BastionDetonationPresentation.RequiresSample(reentry,reentry-step,true),"Late visible re-entry must immediately refresh current particles");
        }
        var cooling=Pose(new BastionPoseInput{Unfold=1,CannonHeat=1},true);
        for(int i=0;i<4;i++)
        {
            float angle=(i+.5f)*(float)Math.PI/2;var radial=new Vector3((float)Math.Cos(angle),(float)Math.Sin(angle),0);
            // The authored vent centers are forward of their retained hinges.
            // Opening must increase their radial distance from the chamber.
            var arm=radial*.0625f+Vector3.UnitZ*.312f;var joint=cooling.Bones["vent_"+i];var radians=joint.Rotation*((float)Math.PI/180);
            var rotation=Quaternion.CreateFromYawPitchRoll(radians.Y,radians.X,radians.Z);
            float travel=Vector3.Dot(Vector3.Transform(arm,rotation)+joint.Offset-arm,radial);
            Require(travel>.10f,"Cooling vent opens outward instead of folding into the chamber");
        }
        Require(BastionEffectGeometry.EnergyFalloff(0)==0&&BastionEffectGeometry.EnergyFalloff(1)==0,"Energy edges disappear without a hard rectangle");
        Require(BastionEffectGeometry.EnergyFalloff(.5f)>.99f,"Energy core remains bright");
        for(int i=0;i<=128;i++)
        {
            float t=i/128f,alpha=BastionEffectGeometry.EnergyFalloff(t);
            Require(alpha>=0&&alpha<=1&&float.IsFinite(alpha),"Finite energy alpha");
            Require(Math.Abs(alpha-BastionEffectGeometry.EnergyFalloff(1-t))<.00001f,"Symmetric glow");
            foreach(int layer in new[]{0,1})
            {
                var l=BastionEffectGeometry.FeatherRibbon(UnityEngine.Vector3.zero,UnityEngine.Vector3.up,UnityEngine.Vector3.right,1,layer,t,-1);
                var r=BastionEffectGeometry.FeatherRibbon(UnityEngine.Vector3.zero,UnityEngine.Vector3.up,UnityEngine.Vector3.right,1,layer,t,1);
                Require(Math.Abs(l.x+r.x)<.00001f&&Math.Abs(l.y-r.y)<.00001f,"Ribbon tapers symmetrically around the wing axis");
                Require(l.y>=0&&l.y<=1.8f&&Math.Abs(l.x)<=.20f,"Ribbon remains in authored envelope");
            }
        }
        for(int bank=0;bank<4;bank++)
        {
            var input=new BastionPoseInput { Unfold=1,RackKicks=new float[4] };
            var before=Pose(input,true);input.RackKicks[bank]=1;var after=Pose(input,true);
            string[] names={"rack_l","rack_r","shield_l","shield_r"};
            for(int i=0;i<4;i++)
            {
                float movement=Vector3.Distance(before.Bones[names[i]].Offset,after.Bones[names[i]].Offset);
                Require(i==bank?movement>.05f:movement==0,"Only the firing missile bank may recoil");
            }
            Require(before.Bones["cannon"].Offset==after.Bones["cannon"].Offset,"Battery recoil must not move the laser");
        }
        // Early deployment must emerge compactly, then use real articulated
        // mechanisms. Permanent scale, firing direction and recoil stay fixed.
        var stored=Pose(new BastionPoseInput{Unfold=0},true);
        var emerging=Pose(new BastionPoseInput{Unfold=.12f},true);
        var middle=Pose(new BastionPoseInput{Unfold=.35f},true);
        var deployed=Pose(new BastionPoseInput{Unfold=1},true);
        Require(stored.Scale<.3f&&emerging.Scale<.9f,"Root-weighted deck emerges from a compact package rather than appearing full size");
        Require(Math.Abs(Pose(new BastionPoseInput{Unfold=.26f},true).Scale-deployed.Scale)<.00001f,"Whole-rig emergence ends in the first deployment quarter");
        Require(deployed.Scale==1,"Deployed fortress keeps its permanent physical size");
        Require(stored.Bones["cannon"].Rotation.X<-90&&middle.Bones["cannon"].Rotation.X<0,"Cannon pivots clear of its compact storage position");
        Require(stored.Bones["cannon"].Offset.Z<middle.Bones["cannon"].Offset.Z&&middle.Bones["cannon"].Offset.Z<deployed.Bones["cannon"].Offset.Z,"Cannon carriage telescopes forward after emergence");
        Require(stored.Bones["throne"].Offset.Y<-.6f&&middle.Bones["throne"].Offset.Y<0&&deployed.Bones["throne"].Offset.Y==0,"Seat travels on its own lift without changing deployed position");
        Require(stored.Bones["halo"].Rotation.X<-75&&deployed.Bones["halo"].Rotation.X==0,"Halo lifts from a folded horizontal storage position");
        foreach(string side in new[]{"l","r"})
        {
            Require(Math.Abs(stored.Bones["rack_"+side].Offset.X)>.7f&&deployed.Bones["rack_"+side].Offset==Vector3.Zero,"Front banks travel outward from central storage");
            Require(Math.Abs(stored.Bones["shield_"+side].Offset.X)>.7f&&deployed.Bones["shield_"+side].Offset==Vector3.Zero,"Rear banks travel outward independently");
        }
        for(int i=0;i<6;i++)
        {
            float a=(i+.5f)*(float)Math.PI/3;var foot=new Vector3((float)Math.Sin(a)*2,-.4f,(float)Math.Cos(a)*2);
            var joint=stored.Bones["anchor_"+i];var radians=joint.Rotation*((float)Math.PI/180);
            var folded=Vector3.Transform(foot,Quaternion.CreateFromYawPitchRoll(radians.Y,radians.X,radians.Z))+joint.Offset+Vector3.UnitY*.42f;
            Require(folded.Y>.5f,"Support foot folds up into storage instead of sweeping below the ground");
            Require(deployed.Bones["anchor_"+i].Rotation==Vector3.Zero&&deployed.Bones["anchor_"+i].Offset==Vector3.Zero,"Support retains its authored deployed ground contact");
        }
        foreach(float step in new[]{1f/144,1f/60,1f/30,1f/20})
        {
            BastionPoseFrame previous=null;
            for(float unfold=0;unfold<=1;unfold+=step)
            {
                var frame=Pose(new BastionPoseInput{Unfold=unfold},true);
                if(previous!=null)
                {
                    Require(Math.Abs(frame.Scale-previous.Scale)<step*8,"Compact emergence remains continuous at common frame cadences");
                    foreach(var bone in frame.Bones)Require(Vector3.Distance(bone.Value.Rotation,previous.Bones[bone.Key].Rotation)<step*1000,"Mechanical hinge motion remains continuous: "+bone.Key);
                }
                previous=frame;
            }
        }
        foreach(float pitch in new[]{-40f,0f,50f})
        {
            var aimed=Pose(new BastionPoseInput{Unfold=1,Pitch=pitch,LaserKick=1},true);
            Require(aimed.Bones["cannon"].Rotation.X==pitch,"Unfold staging leaves deployed cannon aim unchanged");
            Require(Math.Abs(aimed.Bones["cannon"].Offset.Z+.24f)<.00001f,"Unfold staging preserves deployed breech recoil");
        }
        // Activation must not wrench the muzzle off a shot already being aimed.
        var ready=Pose(new BastionPoseInput{Unfold=0,Aim=1,Pitch=-40,Forward=.6f});
        var active=Pose(new BastionPoseInput{Unfold=0,Aim=1,Pitch=-40,Forward=.6f,PowerAge=.22f});
        Require(ready.Bones["fore_l"].Rotation.Y==active.Bones["fore_l"].Rotation.Y,"Power gather respects gun aim");
        var random=new Random(330);
        for(int sample=0;sample<2000;sample++)
        {
            float R()=> (float)random.NextDouble();
            var p=new BastionPoseInput{Time=R()*30,Unfold=R(),Forward=R()*2-1,Strafe=R()*2-1,Turn=R()*2-1,Vertical=R()*2-1,Aim=R(),Boost=R(),Brake=R(),Pitch=R()*130-65,PowerAge=R()*2-.2f,Acceleration=R()*2-1,LeftKick=R(),RightKick=R(),LaserKick=R(),CannonHeat=R(),MissileKick=R(),Defense=R(),Salvo=R()};
            foreach(bool siege in new[]{false,true})
            {
                var f=Pose(p,siege);
                Require(Finite(f.Offset)&&Finite(f.Rotation)&&float.IsFinite(f.Scale)&&f.Scale>0,"Finite form transform");
                foreach(var bone in f.Bones)Require(Finite(bone.Value.Offset)&&Finite(bone.Value.Rotation),"Finite bone "+bone.Key);
                if(!siege)foreach(string side in new[]{"l","r"})Require(f.Bones["boot_"+side].Rotation.X>=5&&f.Bones["boot_"+side].Rotation.X<=62,"Knee stays inside authored flex range");
            }
        }
        // New utility poses use the same compiled production choreography.
        foreach(float age in Enumerable.Range(0,181).Select(i=>i/60f))foreach(float side in new[]{-1f,0,1f})
        {
            foreach(bool siege in new[]{false,true})
            {
                var frame=Pose(new BastionPoseInput{Unfold=siege?1:0,Time=age,WingburstAge=age,WingburstSide=side,MineAge=age},siege);
                Require(Finite(frame.Offset)&&Finite(frame.Rotation)&&float.IsFinite(frame.Scale)&&frame.Scale>0,"Utility root remains finite and uniformly scaled");
                foreach(var bone in frame.Bones)Require(Finite(bone.Value.Offset)&&Finite(bone.Value.Rotation),"Finite utility bone "+bone.Key);
            }
        }
        // Rush gives a short flowing recovery rather than a long locked pose.
        foreach(float age in new[]{.865f,.9f,1.05f})
        {
            var baseline=Pose(new BastionPoseInput{Time=age,Aim=1,Forward=1},false);
            var recovered=Pose(new BastionPoseInput{Time=age,Aim=1,Forward=1,WingburstAge=age,WingburstSide=.5f},false);
            Require(Vector3.Distance(baseline.Rotation,recovered.Rotation)<.0001f&&Vector3.Distance(baseline.Offset,recovered.Offset)<.0001f,"Rush root returns to normal flight within .15s of travel ending");
            foreach(var bone in baseline.Bones)Require(Vector3.Distance(bone.Value.Rotation,recovered.Bones[bone.Key].Rotation)<.0001f,"Rush bone recovery fully releases "+bone.Key);
        }
        BastionPoseFrame previousRush=null;
        for(float age=.68f;age<.9f;age+=.005f)
        {
            var pose=Pose(new BastionPoseInput{Time=age,Aim=1,Forward=1,WingburstAge=age,WingburstSide=.5f},false);
            if(previousRush!=null)
            {
                Require(Vector3.Distance(previousRush.Rotation,pose.Rotation)<8,"Short rush recovery keeps root rotation continuous");
                foreach(var bone in pose.Bones)Require(Vector3.Distance(previousRush.Bones[bone.Key].Rotation,bone.Value.Rotation)<12,"Short rush recovery keeps bone motion continuous "+bone.Key);
            }
            previousRush=pose;
        }
        // Envelope continuity and bounds at both common and low frame rates.
        foreach(float step in new[]{1f/144,1f/60,1f/30,1f/20})
        for(float t=-.1f;t<2.1f;t+=step)
        foreach(float value in new[]{BastionChoreography.CannonHeat(t),BastionChoreography.LaunchRecoil(t),BastionChoreography.Recoil(t)})
            Require(float.IsFinite(value)&&value>=0&&value<=1,"Recoil/heat envelope bounded at every frame cadence");
        Console.WriteLine("PASS "+checks+" choreography, aim-priority, local-recoil, cooling and finite-pose checks");
#else
        Console.WriteLine("Published baseline exporter ready");
#endif
    }
}
