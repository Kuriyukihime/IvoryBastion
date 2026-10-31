using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;

static class ReviewChecks
{
    private static int checks;
    private static void Check(bool value,string why)
    {checks++;if(!value)throw new Exception(why);}
    static void Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--benchmark"){Benchmark(args[1]);return;}
#if !BASELINE
        if(args.Length>0&&args[0]=="--audio-preview"){AudioPreview(args[1],args[2]);return;}
        if(args.Length>0&&args[0]=="--readouts"){ExportReadouts(args[1]);return;}
        if(args.Length>0&&args[0]=="--detonations"){ExportDetonations(args[1]);return;}
        Readouts();Audio();AudioRecovery();Recoil();Detonations();
        Console.WriteLine($"PASS: {checks} review presentation, defense-readout history, audio-bus priority, mixer gain and recoil checks.");
#endif
    }
#if !BASELINE
    static void Readouts()
    {
        var h=new BastionReadoutHistory();h.Update(1,0);h.Update(.6f,.1f);
        Check(h.Fraction==.6f&&h.Trail==1,"Actual reserve updates immediately; only the trail lags");
        h.Update(.6f,.2f);Check(h.Trail==1,"Short damage hold");
        for(int i=3;i<30;i++)h.Update(.6f,i*.1f);
        Check(Math.Abs(h.Trail-.6f)<.0001f,"Spent trail settles to actual reserve");
        h.Update(.7f,3);Check(h.Rising&&h.Trail>=h.Fraction,"Restoration remains visible");
        h.Update(.7f,4);Check(!h.Rising,"Restoring label expires without further recharge");
        h.Update(float.NaN,5);Check(h.Fraction==0&&float.IsFinite(h.Trail),"Invalid reserve input cannot corrupt UI vertices");
        h.Update(.5f,0);Check(h.Fraction==h.Trail,"Clock/run reset does not retain old damage");
        h=new BastionReadoutHistory();h.Update(1,0);
        for(int i=1;i<=120;i++)h.Update(1-i*.005f,i/60f);
        Check(h.Trail-h.Fraction<.18f,"Continuous barrier decay cannot hold the whole old bar indefinitely");
        foreach(int rate in new[]{30,60,144})
        {
            h=new BastionReadoutHistory();h.Update(1,0);h.Update(.2f,.1f);
            for(int i=1;i<=rate*3;i++)h.Update(.2f,.1f+i/(float)rate);
            Check(Math.Abs(h.Trail-.2f)<.001f,"Frame-independent damage-trail settlement");
        }
    }
    static void Audio()
    {
        var clip=new BastionPcmClip(Enumerable.Repeat(.05f,44100*4).ToArray(),44100,1);
        var mixer=new BastionPcmMixer();var output=new float[2048];
        var alert=mixer.AddToBus(true,.5f,0,BastionAudioBus.Alert,clip);
        var dialogue=mixer.AddToBus(true,.5f,0,BastionAudioBus.Dialogue,clip);
        var engine=mixer.Add(true,.5f,0,clip);
        for(int i=0;i<90;i++)mixer.Add(false,.1f,0,clip);
        Check(!alert.Stopped&&!dialogue.Stopped&&!engine.Stopped,"Crowded weapon traffic preserves warning, dialogue and engine streams");
        mixer.Render(output,1024);Check(output.All(float.IsFinite)&&output.Max(Math.Abs)<=.921f,"Crowded mix remains finite and limited");
        mixer.MasterGain=0;mixer.Render(output,1024);Check(output.All(x=>x==0),"Game master mute affects every bus");
        mixer.MasterGain=1;mixer.EffectsGain=mixer.DialogueGain=mixer.AlertGain=0;
        mixer.Render(output,1024);Check(output.All(x=>x==0),"Local bus controls can mute every category independently");
        mixer=new BastionPcmMixer{EffectsGain=0};
        mixer.AddToBus(true,1,0,BastionAudioBus.Dialogue,clip);mixer.Add(true,1,0,clip);
        mixer.Render(output,1024);Check(output.Any(x=>x>0),"Muted engines do not mute dialogue");
        mixer.Paused=true;mixer.Render(output,1024);Check(output.All(x=>x==0),"Pause silences the output without removing voices");
        mixer.Paused=false;mixer.Render(output,1024);Check(output.Any(x=>x>0),"Unpause resumes audio");
        mixer.MasterGain=float.NaN;mixer.Render(output,1024);Check(output.All(x=>x==0),"Invalid master gain fails silent");
        // A constant background isolates gain discontinuity from clip content.
        // Starting a silent dialogue stream still exercises the priority envelope.
        mixer=new BastionPcmMixer();mixer.Add(true,.5f,0,clip);mixer.Render(output,1024);float previous=output[2046];
        var silence=new BastionPcmClip(new float[44100],44100,1);
        mixer.AddToBus(false,.65f,0,BastionAudioBus.Dialogue,silence);mixer.Render(output,1024);
        Check(Math.Abs(output[0]-previous)<.0001f,"Dialogue duck begins continuously at the block boundary");
        for(int i=2;i<output.Length;i+=2)Check(Math.Abs(output[i]-output[i-2])<.0001f,"Ducking has a smooth per-frame gain envelope");
        var alive=mixer.Add(true,.1f,0,clip);mixer.Clear();mixer.Render(output,1024);
        Check(alive.Stopped&&output.All(x=>x==0),"Device reset retires silent stale engine handles");
    }
    sealed class FakeOutput:IBastionAudioOutput
    {
        public bool IsRunning{get;set;}=true;
        internal bool Disposed,ThrowOnDispose;
        public void Dispose(){Disposed=true;IsRunning=false;if(ThrowOnDispose)throw new IOException("lost device");}
    }
    static void AudioRecovery()
    {
        int opens=0,reports=0;bool reject=true;FakeOutput output=null;Action<string> callback=null;
        var mixer=new BastionPcmMixer();
        var session=new BastionAudioSession(mixer,(m,failed)=>{opens++;callback=failed;if(reject)throw new IOException("device absent");return output=new FakeOutput();},_=>reports++);
        Check(!session.Ensure(0)&&opens==1&&reports==1,"First device failure remains recoverable and reports once");
        for(int i=0;i<100;i++)Check(!session.Ensure(i*.019f),"Device retry is bounded rather than every frame");
        Check(opens==1,"No repeated open while backoff is pending");
        reject=false;Check(session.Ensure(2),"Device availability recovers without restarting the game");
        var oldCallback=callback;var voice=mixer.Add(true,.1f,0,new BastionPcmClip(new float[200],44100,1));
        output.IsRunning=false;output.ThrowOnDispose=true;session.Tick(2.1f);
        Check(output.Disposed&&voice.Stopped&&reports==1,"Background failure clears stale streams and contains dispose errors");
        Check(!session.Ensure(4.2f)&&opens==2,"A device that fails immediately retains increasing backoff");
        Check(session.Ensure(6.2f)&&opens==3,"Device reopens after longer failure backoff");
        oldCallback("late old worker failure");session.Tick(6.3f);Check(output.IsRunning,"Retired worker cannot disrupt healthy replacement");
        session.Tick(12);output.IsRunning=false;session.Tick(12.1f);
        Check(reports==2&&!session.Ensure(13)&&session.Ensure(14.2f),"Sustained healthy playback resets backoff for a later disconnect");
        session.Dispose();Check(output.Disposed&&session.Ensure(0),"Shutdown resets retry state for a fresh session");session.Dispose();
    }
    static void Recoil()
    {
        Check(BastionChoreography.Recoil(-1)==0&&BastionChoreography.Recoil(1)==0,"Recoil is bounded in time");
        Check(BastionChoreography.Recoil(.04f)>.95f,"Cannon reaches a decisive early recoil peak");
        Check(BastionChoreography.Recoil(.2f)>BastionChoreography.Recoil(.4f),"Cannon settles instead of snapping forward");
        for(int i=0;i<80;i++)
        {float r=BastionChoreography.Recoil(i*.01f);Check(float.IsFinite(r)&&r>=0&&r<=1,"Finite recoil envelope");}
        var p=new BastionPoseInput{Unfold=0};var f=new BastionPoseFrame();BastionChoreography.Siege(p,f);
        Check(f.Scale>0&&f.Scale<.5f,"Initial assembly fits a compact stowed footprint");
        float previous=f.Scale;
        for(int i=1;i<=100;i++)
        {
            p.Unfold=i/100f;BastionChoreography.Siege(p,f);
            Check(float.IsFinite(f.Scale)&&f.Scale>=previous&&f.Scale<=1,"Compact emergence stays finite, bounded and monotonic");
            if(p.Unfold>=.26f)Check(f.Scale==1,"The rest of deployment uses physical hinges, not whole-model growth");
            previous=f.Scale;
        }
        p.Unfold=.35f;BastionChoreography.Siege(p,f);float barrel=f.Bones["cannon"].Offset.Z,rack=f.Bones["rack_l"].Rotation.X;
        p.Unfold=.6f;BastionChoreography.Siege(p,f);
        Check(f.Scale==1&&MathF.Abs(f.Bones["cannon"].Offset.Z-barrel)>.1f&&MathF.Abs(f.Bones["rack_l"].Rotation.X-rack)>5,"Barrel and banks keep unfolding after the initial compact emergence ends");
        p.Unfold=1;BastionChoreography.Siege(p,f);Check(f.Scale==1,"Finished deployment preserves existing model scale");
    }
    static void ExportReadouts(string output)
    {
        var shield=new BastionReadoutHistory();var barrier=new BastionReadoutHistory();
        var frames=new System.Collections.Generic.List<object>();
        for(int i=0;i<=60;i++)
        {
            float time=i*.1f;
            float charge=time<.5f?1:time<2?.42f:time<4?Math.Min(1,.42f+(time-2)*.29f):1;
            float outer=time<.5f?1:time<1.4f?.7f:time<4?.2f:0;
            shield.Update(charge,time);barrier.Update(outer,time);
            frames.Add(new{time,charge,trail=shield.Trail,restoring=shield.Rising,aegis=outer,aegisTrail=barrier.Trail});
        }
        File.WriteAllText(output,JsonSerializer.Serialize(frames,new JsonSerializerOptions{WriteIndented=true}));
    }
    static void Detonations()
    {
        for(int tier=0;tier<3;tier++)for(int active=1;active<100;active+=9)
        {
            Check(BastionDetonationPresentation.Count(BastionExplosionLayer.Fireball,tier,1,active)==1,"Barrage budget never removes the fireball or blast footprint");
            Check(BastionDetonationPresentation.Count(BastionExplosionLayer.Flash,tier,1,active)==1,"Ignition remains present at every load");
            int original=BastionExplosionGeometry.Count(BastionExplosionLayer.Spark,tier);
            int count=BastionDetonationPresentation.Count(BastionExplosionLayer.Spark,tier,original,active);
            Check(count>0&&count<=original,"Decorative particle budget is positive and bounded");
        }
        Check(BastionDetonationPresentation.Opacity(BastionExplosionLayer.Smoke)<.6f,"Smoke allows enemies to remain more readable");
    }
    static void ExportDetonations(string output)
    {
        var frames=new System.Collections.Generic.List<object>();
        for(int tier=0;tier<3;tier++)foreach(int active in new[]{1,24})foreach(float age in new[]{.08f,.22f,.55f,1f})
        {
            float radius=tier==0?5:tier==1?10:15;
            var samples=new System.Collections.Generic.List<object>();
            for(int layer=0;layer<4;layer++)
            {
                var kind=(BastionExplosionLayer)layer;int original=BastionExplosionGeometry.Count(kind,tier);
                int count=BastionDetonationPresentation.Count(kind,tier,original,active);
                for(int i=0;i<count;i++)
                {
                    var sample=BastionExplosionGeometry.At(kind,i*original/count,tier,radius,age,17);
                    if(sample.Alpha<=0||sample.Remaining<=0)continue;
                    samples.Add(new{layer,x=sample.Position.x,y=sample.Position.y,z=sample.Position.z,size=sample.Size,alpha=sample.Alpha*BastionDetonationPresentation.Opacity(kind),rotation=sample.Rotation,life=sample.Life,remaining=sample.Remaining});
                }
            }
            frames.Add(new{tier,active,age,radius,samples});
        }
        File.WriteAllText(output,JsonSerializer.Serialize(frames,new JsonSerializerOptions{WriteIndented=true}));
    }
    static void AudioPreview(string root,string output)
    {
        var mixer=new BastionPcmMixer();
        var clips=new System.Collections.Generic.Dictionary<string,BastionPcmClip>();
        foreach(string cue in new[]{"engine","pistol","voice_siege","missile","heavy","siege_impact","shieldbreak"})
        {using var stream=File.OpenRead(Path.Combine(root,"assets","sfx_"+cue+".wav"));clips[cue]=BastionPcmClip.Read(stream);}
        var events=new System.Collections.Generic.List<(float Time,string Cue,float Volume,BastionAudioBus Bus)>();
        for(int i=0;i<6;i++)events.Add((i*.15f,"pistol",.30f,BastionAudioBus.Effects));
        events.Add((1,"voice_siege",.65f,BastionAudioBus.Dialogue));
        for(int i=0;i<20;i++)
        {
            events.Add((1.15f+i*.20f,i%8==7?"heavy":"missile",i%8==7?.34f:.20f,BastionAudioBus.Effects));
            if(i%2==0)events.Add((1.65f+i*.20f,"siege_impact",.22f,BastionAudioBus.Effects));
        }
        events.Add((4.4f,"shieldbreak",.55f,BastionAudioBus.Alert));events.Sort((a,b)=>a.Time.CompareTo(b.Time));
        mixer.Add(true,.075f,0,clips["engine"]);
        const int totalFrames=44100*8;
        using var file=File.Create(output);using var writer=new BinaryWriter(file);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+totalFrames*4);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(44100);writer.Write(44100*4);writer.Write((short)4);writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(totalFrames*4);
        var buffer=new float[2048];int next=0;float peak=0;
        for(int frame=0;frame<totalFrames;)
        {
            float time=frame/44100f;
            while(next<events.Count&&events[next].Time<=time)
            {var e=events[next++];mixer.AddToBus(false,e.Volume,0,e.Bus,clips[e.Cue]);}
            int count=Math.Min(1024,totalFrames-frame);mixer.Render(buffer,count);
            for(int i=0;i<count*2;i++){peak=Math.Max(peak,Math.Abs(buffer[i]));writer.Write((short)(Math.Clamp(buffer[i],-1,1)*32767));}
            frame+=count;
        }
        Console.WriteLine("Rendered actual production PCM mixer preview; peak="+peak.ToString("0.000")+"; original voice/cue recordings unchanged.");
    }
#endif
    static void Benchmark(string output)
    {
        const int frames=8000;
        var inputs=Enumerable.Range(0,4).Select(_=>new BastionPoseInput{Unfold=1,HatchAges=new float[72]}).ToArray();
        var poses=Enumerable.Range(0,4).Select(_=>new BastionPoseFrame()).ToArray();
        void PoseStep(int frame)
        {
            for(int i=0;i<4;i++)
            {
                var p=inputs[i];p.Time=frame/60f;p.Forward=.8f;p.Strafe=(float)Math.Sin(frame*.01+i)*.8f;
                p.Boost=.8f;p.LaserKick=(frame%30)/30f;p.Salvo=.8f;
                if(i<2)BastionChoreography.Mobile(p,poses[i]);else BastionChoreography.Siege(p,poses[i]);
            }
        }
        for(int i=0;i<1000;i++)PoseStep(i);
        long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
        for(int i=0;i<frames;i++)PoseStep(i);
        watch.Stop();double poseMilliseconds=watch.Elapsed.TotalMilliseconds/frames;
        long poseBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        // Deterministic analytic simulation, without native particles or GPU rendering.
        float checksum=0;watch.Restart();
        for(int frame=0;frame<frames;frame++)for(int effect=0;effect<20;effect++)
        {
            int tier=effect<12?0:effect<16?1:2;
            for(int layer=0;layer<4;layer++)
            {
                int original=BastionExplosionGeometry.Count((BastionExplosionLayer)layer,tier),count=original;
#if !BASELINE
                count=BastionDetonationPresentation.Count((BastionExplosionLayer)layer,tier,original,20);
#endif
                for(int particle=0;particle<count;particle++)
                {var s=BastionExplosionGeometry.At((BastionExplosionLayer)layer,particle*original/count,tier,tier==0?5:tier==1?10:15,(frame%60)/60f,effect);checksum+=s.Alpha*.000001f;}
            }
        }
        watch.Stop();double particles=watch.Elapsed.TotalMilliseconds/frames;
        var mixer=new BastionPcmMixer();var clip=new BastionPcmClip(new float[44100],44100,1);var buffer=new float[2048];
        for(int i=0;i<40;i++)mixer.Add(true,.1f,0,clip);
        for(int i=0;i<20;i++)mixer.Render(buffer,1024);
        allocated=GC.GetAllocatedBytesForCurrentThread();watch.Restart();
        for(int i=0;i<400;i++)mixer.Render(buffer,1024);
        watch.Stop();double audio=watch.Elapsed.TotalMilliseconds/400;long audioBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        var report=new
        {
#if BASELINE
            build="0.32.0 baseline",
#else
            build="0.33.0 review",
#endif
            workload="Four actors (two mobile, two siege); twenty analytic explosion envelopes; forty audio streams. Managed CPU simulation only, not game FPS or GPU profiling.",
            poseMillisecondsPerSimulatedFrame=poseMilliseconds,poseAllocatedBytesPerSimulatedFrame=poseBytes/(double)frames,
            explosionMillisecondsPerSimulatedFrame=particles,audioMillisecondsPer1024FrameBlock=audio,audioAllocatedBytesPerBlock=audioBytes/400.0,
            audioBlockBudgetMilliseconds=1024.0/44100*1000,checksum
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(report));
    }
}
