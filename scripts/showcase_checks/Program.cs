using System;
using System.Linq;
using System.Reflection;
using IvoryBastion;
using RoR2;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic;
    private static BastionPcmMixer Mixer=>(BastionPcmMixer)typeof(BastionAudio).GetField("mixer",Hidden).GetValue(null);
    private static void Check(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
    private static void Near(float actual,float expected,string why,float tolerance=.0001f)=>Check(MathF.Abs(actual-expected)<=tolerance,$"{why}: {actual} expected {expected}");
    private static void Near(Vector3 actual,Vector3 expected,string why)=>Near((actual-expected).magnitude,0,why);
    private static void Call(object instance,string name)=>instance.GetType().GetMethod(name,Hidden).Invoke(instance,null);
    private static BastionPcmVoice Unfolding(BastionDisplayShowcase display)=>(BastionPcmVoice)typeof(BastionDisplayShowcase).GetField("unfolding",Hidden).GetValue(display);
    private static void Clock(float now){Time.time=Time.unscaledTime=now;}
    private static void Reset(float now=10)
    {
        BastionAudio.Shutdown();BastionAudio.SetEnabled(true);
        typeof(BastionAudio).GetField("nextPreviewTransform",Hidden).SetValue(null,0f);
        Run.instance=null;LocalUserManager.User=null;Application.isBatchMode=false;
        BastionPlugin.AudioVolume.Value=1;BastionPresentationSettings.EffectsVolume.Value=1;
        BastionGameAudio.Gain=1;Camera.main=SceneCamera._camera=null;Clock(now);BastionAudio.SetPaused(false);
    }
    private static float Energy(int frames=44100)
    {
        var pcm=new float[frames*2];Mixer.Render(pcm,frames);
        Check(pcm.All(float.IsFinite),"All mixed menu samples must be finite");
        return pcm.Sum(x=>MathF.Abs(x));
    }
    private static void MenuAudio()
    {
        Reset();int opened=BastionAudioOutput.Opened.Count;
        Check(BastionAudio.Cue("transform_deploy",Vector3.zero)==null,"Spatial gameplay cue requires a body listener");
        var voice=BastionAudio.PreviewTransformation();
        Check(voice!=null&&!voice.Stopped,"Selection transformation must play without any spawned body");
        Check(BastionAudioOutput.Opened.Count==opened+1,"Preview opens the shared output session");
        Check(voice.Bus==BastionAudioBus.Effects,"Transformation preview obeys the effects volume bus");
        Near(voice.Volume,.75f,"Preview effects gain");
        Check(Energy()>1,"Real embedded mechanical WAV produces audible PCM samples");
        Check(BastionAudio.PreviewTransformation()==null,"Repeated same-frame menu cue is throttled");
        Clock(10.29f);Check(BastionAudio.PreviewTransformation()==null,"Preview throttle covers rapid reselection");
        Clock(10.31f);Check(BastionAudio.PreviewTransformation()!=null,"Preview can play after its short selection throttle");

        foreach(string resource in new[]{"deploy","pack"})
        {
            using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream($"IvoryBastion.assets.sfx_transform_{resource}.wav");
            var clip=BastionPcmClip.Read(stream);
            Check(clip.Rate==48000,"Both mechanical transformation clips use 48 kHz PCM");
            Check(clip.Frames>24000,"Transformation clips have useful duration");
            Check(clip.Samples.All(float.IsFinite),"Embedded clip samples are finite");
            Check(clip.Samples.Max(x=>MathF.Abs(x))<1,"Mechanical clips retain sample headroom");
        }
    }
    private static void AudioSettings()
    {
        Reset();int opened=BastionAudioOutput.Opened.Count;
        BastionAudio.SetEnabled(false);Check(BastionAudio.PreviewTransformation()==null,"Master switch mutes menu transformation");
        Check(BastionAudioOutput.Opened.Count==opened,"Muted preview does not open an output device");
        BastionAudio.SetEnabled(true);BastionPlugin.AudioVolume.Value=0;
        Check(BastionAudio.PreviewTransformation()==null,"Zero mod volume suppresses preview");
        BastionPlugin.AudioVolume.Value=.4f;var scaled=BastionAudio.PreviewTransformation();Near(scaled.Volume,.3f,"Menu honors mod volume");
        Reset();BastionPresentationSettings.EffectsVolume.Value=0;BastionAudio.SetPaused(false);
        Check(BastionAudio.PreviewTransformation()!=null,"Zero effects bus remains a valid silent preview");
        Near(Energy(),0,"Effects volume zero silences menu transformation");
        Reset();BastionGameAudio.Gain=0;BastionAudio.SetPaused(false);
        Check(BastionAudio.PreviewTransformation()!=null,"Game volume is applied by shared mixer");
        Near(Energy(),0,"Game master/effects gain zero silences preview");
        Reset();BastionAudio.SetPaused(true);
        Check(!Mixer.Paused,"Menu pause state must not suppress selection audio");
        Check(BastionAudio.PreviewTransformation()!=null&&Energy()>1,"No-body menu plays even when gameplay pause requested");
        Reset();Run.instance=new Run();Check(BastionAudio.PreviewTransformation()==null,"Menu-only path rejects an active run");
        Reset();Application.isBatchMode=true;Check(BastionAudio.PreviewTransformation()==null,"Headless mode never opens preview audio");
        Reset();var active=BastionAudio.PreviewTransformation();BastionAudio.SetEnabled(false);
        Near(Energy(),0,"Live master mute silences already started preview");
        BastionAudio.Shutdown();Check(active.Stopped,"Shutdown stops active preview through shared session");
    }
    private static void OutputRecovery()
    {
        Reset();var first=BastionAudio.PreviewTransformation();var output=BastionAudioOutput.Opened.Last();
        int opened=BastionAudioOutput.Opened.Count;output.Fail();Clock(10.5f);BastionAudio.SetPaused(false);
        Check(first.Stopped&&output.Disposed,"Device loss retires preview and failed output");
        Check(BastionAudio.PreviewTransformation()==null,"Retry delay is honored in menu path");
        Clock(12.51f);var recovered=BastionAudio.PreviewTransformation();
        Check(recovered!=null&&!recovered.Stopped,"Menu transformation recovers without a CharacterBody after device loss");
        Check(BastionAudioOutput.Opened.Count==opened+1,"Menu reuses session recovery rather than opening competing outputs");
        Check(Energy()>1,"Recovered device receives real transformation PCM");
    }
    private static (BastionDisplayShowcase display,BastionAnimation model,Camera camera,SceneCamera scene) MakeDisplay()
    {
        var root=new GameObject();root.transform.localPosition=new(3,2,-7);root.transform.localRotation=Quaternion.Euler(0,37,0);root.transform.localScale=new(2,1.5f,.75f);
        var mannequin=new GameObject();mannequin.transform.parent=root.transform;mannequin.transform.localPosition=new(.3f,.6f,-.1f);
        var model=mannequin.AddComponent<BastionAnimation>();var display=mannequin.AddComponent<BastionDisplayShowcase>();
        var view=new GameObject().AddComponent<Camera>();var scene=new GameObject().AddComponent<SceneCamera>();scene.camera=view;
        Camera.main=view;return(display,model,view,scene);
    }
    private static void PositionAndLifecycle()
    {
        Reset();var (display,model,camera,scene)=MakeDisplay();var rest=display.transform.localPosition;
        var anchor=display.transform.parent.TransformPoint(rest);int subscribers=SceneCamera.Subscribers;
        Call(display,"OnEnable");Check(SceneCamera.Subscribers==subscribers+1,"Selection subscribes exactly one camera callback");
        Clock(10.2f);Call(display,"Update");Check(model.Showcase,"Preview activates display-only pose mode");
        Near(model.ShowcaseUnfold,0,"Mechanical unfolding waits for hold");Check(Unfolding(display)==null,"No transformation sound before unfolding");
        Near(display.transform.position,anchor-camera.transform.right*BastionDisplayRules.LeftShift,"Selection moves screen-left, independently of mannequin rotation and parent scale");
        for(int i=0;i<1000;i++){camera.transform.localRotation=Quaternion.Euler(i%27,i*.73f,i%9);scene.Cull();Near(display.transform.position,anchor-camera.transform.right*BastionDisplayRules.LeftShift,"Camera-relative offset has no accumulated drift");}
        Clock(10.35f);Call(display,"Update");var voice=Unfolding(display);Check(voice!=null,"Unfolding start triggers mechanical sound");
        Clock(10.7f);Call(display,"Update");Check(ReferenceEquals(voice,Unfolding(display)),"Update never retriggers selection sound during same enable");
        Clock(13);Call(display,"Update");Check(ReferenceEquals(voice,Unfolding(display)),"Completed showcase never loops its transformation sound");
        Near(model.ShowcaseUnfold,1,"Showcase reaches deployed pose");
        display.transform.parent.localPosition+=new Vector3(4,-1,3);anchor=display.transform.parent.TransformPoint(rest);scene.Cull();
        Near(display.transform.position,anchor-camera.transform.right*BastionDisplayRules.LeftShift,"Offset tracks moved native display anchor");
        Call(display,"OnDisable");Near(display.transform.localPosition,rest,"Disabling restores original local model position");
        Check(!model.Showcase&&voice.Stopped&&Unfolding(display)==null,"Leaving selection clears pose and active mechanical audio");
        Check(SceneCamera.Subscribers==subscribers,"Selection camera callback is removed on disable");
        var stopped=display.transform.position;camera.transform.localRotation=Quaternion.Euler(0,90,0);scene.Cull();Near(display.transform.position,stopped,"Disabled model no longer reacts to cameras");
        Clock(14);Call(display,"OnEnable");Call(display,"Update");Near(model.ShowcaseUnfold,0,"Reselect resets animation hold");
        Clock(14.36f);Call(display,"Update");var replay=Unfolding(display);Check(replay!=null&&!ReferenceEquals(voice,replay),"Reselection plays its own new transformation cue");
        Call(display,"OnDisable");Check(replay.Stopped,"Reselection cleanup stops its own preview");Near(display.transform.localPosition,rest,"Reselection restores exact original anchor");
    }
    private static void BodyAndMissingViewGuards()
    {
        Reset();var (display,model,camera,scene)=MakeDisplay();var rest=display.transform.localPosition;
        model.Owner=new GameObject().AddComponent<CharacterBody>();Call(display,"OnEnable");Clock(11);Call(display,"Update");scene.Cull();
        Near(display.transform.localPosition,rest,"Gameplay-owned model is not repositioned by showcase");
        Check(!model.Showcase&&Unfolding(display)==null,"Gameplay-owned model receives no menu animation or preview audio");Call(display,"OnDisable");
        Reset();(display,model,camera,scene)=MakeDisplay();Camera.main=null;SceneCamera._camera=null;
        rest=display.transform.localPosition;Call(display,"OnEnable");Clock(11);Call(display,"Update");
        Near(display.transform.localPosition,rest,"No camera causes no speculative world offset");
        scene.Cull();Near(display.transform.position,display.transform.parent.TransformPoint(rest)-camera.transform.right*BastionDisplayRules.LeftShift,"Native SceneCamera callback can position a display even without Camera.main");Call(display,"OnDisable");
        Check(SceneCamera.Subscribers==0,"All test lifecycle paths leave no camera event listeners");
    }
    private static void Scales()
    {
        Near(BastionDisplayRules.Unfold(0),0,"Initial hold");Near(BastionDisplayRules.Unfold(-1),0,"Negative age clamps");
        Near(BastionDisplayRules.Unfold(100),1,"Complete deployment clamps");Near(BastionDisplayRules.Scale(0),.75f,"Small initial selection scale");
        Near(BastionDisplayRules.Scale(1),.38f,"Smaller deployed selection scale");Near(BastionDisplayRules.FortressScale(0),0,"Fortress initially hidden");
        float prior=BastionDisplayRules.Scale(0);
        for(int i=1;i<=200;i++){var scale=BastionDisplayRules.Scale(i/200f);Check(scale<=prior+.000001f&&scale>=.38f,"Display scale moves smoothly toward smaller fortress frame");prior=scale;}
    }
    private static CharacterBody GameplayListener()
    {
        var body=new GameObject().AddComponent<CharacterBody>();body.corePosition=Vector3.zero;
        LocalUserManager.User=new LocalUser{cachedBody=body};Run.instance=new Run();BastionAudio.SetPaused(false);return body;
    }
    private static BastionPcmClip VoiceClip(BastionPcmVoice voice)
        =>((BastionPcmClip[])typeof(BastionPcmVoice).GetField("layers",Hidden).GetValue(voice))[0];
    private static float ClipEnergy(BastionPcmClip clip,float begin,float end)
    {
        int first=(int)(begin*clip.Rate)*clip.Channels,last=Math.Min(clip.Samples.Length,(int)(end*clip.Rate)*clip.Channels);
        return MathF.Sqrt(clip.Samples.Skip(first).Take(last-first).Average(x=>x*x));
    }
    private static void GameplayTransformAssets()
    {
        foreach(var spec in new[]{(Name:"deploy_game",Motion:.8f),(Name:"pack_game",Motion:.25f)})
        {
            using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream($"IvoryBastion.assets.sfx_transform_{spec.Name}.wav");
            var clip=BastionPcmClip.Read(stream);
            Check(clip.Rate==48000&&clip.Channels==1,"Gameplay variants are actual 48kHz mono PCM assets");
            Near(clip.Frames/(float)clip.Rate,spec.Motion+.04f,"Short arrangement retains40ms dry decay",1f/clip.Rate);
            Check(clip.Samples.All(float.IsFinite),"Short gameplay asset contains only finite samples");
            Check(clip.Samples.Max(MathF.Abs)<=.8f,"Short gameplay asset has clean headroom and no clipped PCM samples");
            Near(clip.Samples[0],0,"Gameplay WAV starts at literal zero");Near(clip.Samples[^1],0,"Gameplay WAV ends at literal zero");
            Check(ClipEnergy(clip,spec.Motion*.80f,spec.Motion)>.025f,"Locking window contains real mechanical audio near transition end");
            Check(ClipEnergy(clip,spec.Motion,spec.Motion+.04f)<ClipEnergy(clip,spec.Motion*.80f,spec.Motion),"Final tail decays below locking-window energy");
        }
    }
    private static void GameplayTransformTimings()
    {
        foreach(float signed in new[]{.8f,-.25f,.1f,3f,-.1f,-3f,.3f,1.6f,-.5f,-1.5f})
        {
            Reset();GameplayListener();var voice=BastionAudio.Transformation(Vector3.zero,signed);
            Check(voice!=null&&!voice.Stopped,"A valid gameplay transition starts a real embedded short clip");
            bool pack=signed<0;float nominal=pack?.25f:.8f,pitch=nominal/MathF.Abs(signed);
            Near(voice.Pitch,pitch,"Duration ratio supports entire0.1..3s configuration range without unwanted clamp");
            Check(voice.Bus==BastionAudioBus.Effects,"Gameplay machinery uses same effects bus as selection");
            Near(voice.Volume,.8f,"Gameplay mechanical gain at local listener");
            var clip=VoiceClip(voice);float baseDuration=pack?.29f:.84f;
            Near(clip.Frames/(float)clip.Rate,baseDuration,"Signed transition chooses matching short arrangement");
            if(signed==.8f||signed==-.25f)Near(voice.Pitch,1,"Default transitions retain natural unshifted motor pitch");
            double expected=(Math.Abs(signed)+.04/pitch)*44100;
            var samples=new float[512];int frames=0;float peak=0;double energy=0;bool finite=true;
            void Render(int count)
            {
                Mixer.Render(samples,count);frames+=count;
                for(int i=0;i<count*2;i++){finite&=float.IsFinite(samples[i]);peak=MathF.Max(peak,MathF.Abs(samples[i]));energy+=samples[i]*samples[i];}
            }
            int beforeEnd=Math.Max(0,(int)Math.Floor(expected)-2);
            while(frames<beforeEnd){Check(!voice.Stopped,"Voice cannot terminate before nominal motion and scaled decay");Render(Math.Min(256,beforeEnd-frames));}
            while(!voice.Stopped&&frames<expected+10)Render(1);
            Check(voice.Stopped,"Real production resampler must finish full custom-duration clip");
            // Stopped becomes true on the first empty output frame after its
            // final sample. Excluding that sentinel gives sample-accurate time.
            Check(Math.Abs((frames-1)-expected)<=1.1,"Actual completion matches nominal duration plus scaled40ms tail within one output sample");
            Check(finite&&peak<=.9201f&&energy>1,"All default/custom-speed output is finite, non-silent and limited");
        }
        foreach(float invalid in new[]{0f,.01f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            Reset();GameplayListener();var voice=BastionAudio.Transformation(Vector3.zero,invalid);
            Check(voice!=null,"Invalid transition spans fall back to a playable nominal arrangement");
            Near(voice.Pitch,1,"Invalid transition spans use natural-speed fallback");
        }
        Reset();GameplayListener();var low=BastionAudio.Cue("transform_deploy_game",Vector3.zero,1,.001f);
        var high=BastionAudio.Cue("transform_pack_game",Vector3.zero,1,100f);
        Near(low.Pitch,.5f,"Ordinary Cue lower pitch safety remains unchanged");Near(high.Pitch,3,"Ordinary Cue upper pitch safety remains unchanged");
        Reset();var menu=BastionAudio.PreviewTransformation();Near(menu.Pitch,1,"Selection still uses natural-speed original audio");
        Near(VoiceClip(menu).Frames/(float)VoiceClip(menu).Rate,1.65f,"Selection retains original longer deploy arrangement");
    }
    private static void GameplayTransformGates()
    {
        foreach(float transition in new[]{.8f,-.25f})
        {
            Reset();int opened=BastionAudioOutput.Opened.Count;
            Check(BastionAudio.Transformation(Vector3.zero,transition)==null,"Gameplay transformation requires spatial body listener");
            Check(BastionAudioOutput.Opened.Count==opened,"Missing body does not open audio device");
            Reset();GameplayListener();BastionAudio.SetEnabled(false);
            Check(BastionAudio.Transformation(Vector3.zero,transition)==null,"Master audio switch suppresses gameplay machinery");
            Reset();GameplayListener();BastionPlugin.AudioVolume.Value=0;
            Check(BastionAudio.Transformation(Vector3.zero,transition)==null,"Zero mod volume suppresses gameplay transformation");
            Reset();GameplayListener();BastionPlugin.AudioVolume.Value=.25f;
            Near(BastionAudio.Transformation(Vector3.zero,transition).Volume,.2f,"Gameplay transition obeys local mod gain");
            Reset();GameplayListener();BastionPresentationSettings.EffectsVolume.Value=0;BastionAudio.SetPaused(false);
            Check(BastionAudio.Transformation(Vector3.zero,transition)!=null,"Effects-muted transition keeps valid silent timeline");
            Near(Energy(),0,"Effects-volume gate silences short gameplay audio");
            Reset();GameplayListener();BastionGameAudio.Gain=0;BastionAudio.SetPaused(false);
            Check(BastionAudio.Transformation(Vector3.zero,transition)!=null,"Game volume flows through shared mixer");
            Near(Energy(),0,"Game master/effects gain gate silences gameplay transition");
            Reset();GameplayListener();BastionAudio.SetPaused(true);
            Check(BastionAudio.Transformation(Vector3.zero,transition)!=null,"Paused run preserves requested transformation timeline");
            Near(Energy(),0,"Gameplay pause silences active mechanical audio");
            Reset();GameplayListener();Application.isBatchMode=true;
            Check(BastionAudio.Transformation(Vector3.zero,transition)==null,"Dedicated server cannot open gameplay audio");
            Reset();GameplayListener();Check(BastionAudio.Transformation(new Vector3(61,0,0),transition)==null,"Distant transformation respects spatial cutoff");
            Reset();GameplayListener();var active=BastionAudio.Transformation(Vector3.zero,transition);BastionAudio.SetEnabled(false);
            Near(Energy(),0,"Live mute suppresses already-started gameplay machinery");Check(active.Stopped,"Muted short sounds finish silently without replay backlog");
        }
    }
    private static void MovingCueGain()
    {
        foreach(float modVolume in new[]{1f,.4f})
        {
            Reset();GameplayListener();BastionPlugin.AudioVolume.Value=modVolume;
            var moving=BastionAudio.Cue("mine_deploy",Vector3.zero,.65f);
            Near(moving.Volume,.65f*modVolume,"Moving utility starts with intended65% effect gain");
            foreach(float distance in new[]{0f,30f,59f,60f})
            {
                var at=new Vector3(distance,0,0);
                BastionAudio.PositionVoice(moving,at,.65f,60f);
                Near(moving.Volume,.65f*modVolume*MathF.Pow(Math.Clamp(1-distance/60,0,1),2),"Moving effect preserves original gain and60m attenuation");
                if(distance<60)
                {
                    var direct=BastionAudio.Cue("mine_deploy",at,.65f);
                    Near(moving.Volume,direct.Volume,"Tracked cue volume agrees with an equivalent newly spatialized Cue");
                    direct.Dispose();
                }
            }
            Near(moving.Volume,0,"Tracked utility is silent at original60m range");
            var listener=LocalUserManager.User;LocalUserManager.User=null;
            BastionAudio.PositionVoice(moving,Vector3.zero,.65f,60f);Near(moving.Volume,0,"Missing listener silences moving effects");
            LocalUserManager.User=listener;BastionAudio.PositionVoice(moving,Vector3.zero,.65f,60f);
            Near(moving.Volume,.65f*modVolume,"Restored listener recovers intended utility gain");
            moving.Dispose();BastionAudio.PositionVoice(moving,new Vector3(30,0,0),.65f,60f);
            Near(moving.Volume,.65f*modVolume,"Disposed moving cue stays untouched");

            foreach(float distance in new[]{0f,30f,60f,90f})
            {
                Reset();GameplayListener();BastionPlugin.AudioVolume.Value=modVolume;
                var dialogue=BastionAudio.Cue("transform_deploy_game",Vector3.zero,1,1,true,1);
                BastionAudio.PositionVoice(dialogue,new Vector3(distance,0,0));
                Near(dialogue.Volume,modVolume*MathF.Pow(Math.Clamp(1-distance/90,0,1),2),"Default dialogue retains unit gain and90m attenuation");
                Check(dialogue.Bus==BastionAudioBus.Dialogue,"Moving-effect correction leaves dialogue bus unchanged");
            }
        }
        BastionAudio.PositionVoice(null,Vector3.zero,.65f,60f);
    }
    private static void MineDeploymentDecay()
    {
        Reset();GameplayListener();var voice=BastionAudio.Cue("mine_deploy",Vector3.zero,.65f);
        Check(voice!=null&&!voice.Stopped,"Widow deployment uses its real dedicated compact audio resource");
        var clip=VoiceClip(voice);
        Near(clip.Frames/(float)clip.Rate,.76f,"Mine salvo has compact760ms duration, before default870ms deployment ends");
        Check(clip.Rate==48000&&clip.Channels==1&&clip.Samples.All(float.IsFinite),"Mine deployment is valid48kHz mono PCM");
        Check(clip.Samples.Max(MathF.Abs)<.8f,"Mine deployment retains peak headroom");
        Near(clip.Samples[0],0,"Mine salvo starts at literal zero");Near(clip.Samples[^1],0,"Mine salvo ends at literal zero");
        Check(ClipEnergy(clip,.74f,.76f)<.005f,"Last20ms decays quietly rather than hard-cutting a sustained Aegis sound");
        var buffer=new float[2*(int)(.87f*44100)];Mixer.Render(buffer,buffer.Length/2);
        Check(voice.Stopped,"Actual mixer finishes mine cue before normal utility cleanup");
        Check(buffer.Skip((int)(.77f*44100)*2).All(x=>x==0),"Deployment has true silent tail before870ms cleanup");
    }
    public static int Main()
    {
        try{MenuAudio();AudioSettings();OutputRecovery();PositionAndLifecycle();BodyAndMissingViewGuards();Scales();Check(checks==1271,"Preserve original1,271 selection/menu checks before new coverage");GameplayTransformAssets();GameplayTransformTimings();GameplayTransformGates();MovingCueGain();MineDeploymentDecay();BastionAudio.Shutdown();Console.WriteLine($"Showcase/menu-audio: {checks:N0} production-source checks passed; no game or audio device launched.");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
