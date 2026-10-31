using System;
using System.Collections.Generic;
using System.Reflection;
using RoR2;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionAudio
    {
        private static readonly Dictionary<string, BastionPcmClip> clips = new Dictionary<string, BastionPcmClip>();
        private static readonly BastionPcmMixer mixer = new BastionPcmMixer();
        private static readonly HashSet<string> unavailableClips=new HashSet<string>();
        private static readonly BastionAudioSession session=new BastionAudioSession(mixer,BastionAudioOutput.Open,message=>BastionPlugin.Log.LogWarning(message));
        private static float nextHit,nextPreviewTransform;
        private static readonly BastionDialogueGate dialogue=new BastionDialogueGate();
        private static BastionPcmVoice dialogueVoice;
        private static BastionPcmVoice shieldRecharge;
        internal static void SetEnabled(bool enabled) { mixer.Muted = !enabled; }
        internal static void SetPaused(bool paused)
        {
            mixer.Paused=paused&&Run.instance;mixer.MasterGain=BastionGameAudio.Gain;
            mixer.EffectsGain=BastionPresentationSettings.EffectsVolume?.Value??1;
            mixer.DialogueGain=BastionPresentationSettings.DialogueVolume?.Value??1;
            mixer.AlertGain=BastionPresentationSettings.AlertVolume?.Value??1;
            session.Tick(Time.unscaledTime);
        }
        internal static void Shutdown() { StopShieldRecharge(); dialogueVoice?.Dispose();dialogueVoice=null;dialogue.Reset();session.Dispose(); }
        internal static void StopShieldRecharge(){shieldRecharge?.Dispose();shieldRecharge=null;}
        internal static void BeginShieldRecharge(Vector3 position)
        {StopShieldRecharge();shieldRecharge=Cue("shieldrecharge",position,.48f);}
        internal static void PositionVoice(BastionPcmVoice voice,Vector3 at,float gain=1f,float audibleRange=90f)
        {
            if(voice==null||voice.Stopped)return;
            var listener=LocalUserManager.GetFirstLocalUser()?.cachedBody;
            if(!listener){voice.Volume=0;return;}
            var delta=at-listener.corePosition;
            voice.Volume=Mathf.Clamp01(gain)*BastionPlugin.AudioVolume.Value*Mathf.Pow(Mathf.Clamp01(1-delta.magnitude/Mathf.Max(1,audibleRange)),2);
            voice.Pan=Mathf.Clamp(Vector3.Dot(listener.transform.right,delta.normalized),-.8f,.8f);
        }
        internal static BastionPcmVoice Engine()
        {
            if(Application.isBatchMode)return null;
            var clip=Clip("engine");
            return clip!=null&&session.Ensure(Time.unscaledTime)?mixer.Add(true,0,0,clip):null;
        }
        // Menus have no CharacterBody listener. Use the same local settings,
        // output recovery and effects bus without requiring a spawned survivor.
        internal static BastionPcmVoice PreviewTransformation()
        {
            if(Application.isBatchMode||Run.instance||mixer.Muted||BastionPlugin.AudioVolume.Value<=0||Time.unscaledTime<nextPreviewTransform)return null;
            var clip=Clip("transform_deploy");if(clip==null||!session.Ensure(Time.unscaledTime))return null;
            nextPreviewTransform=Time.unscaledTime+.3f;
            return mixer.AddToBus(false,.75f*BastionPlugin.AudioVolume.Value,0,BastionAudioBus.Effects,clip);
        }
        internal static BastionPcmVoice Transformation(Vector3 position,float signedDuration)
        {
            bool packing=signedDuration<0;
            float nominal=packing?.25f:.8f,span=Mathf.Abs(signedDuration);
            if(float.IsNaN(span)||float.IsInfinity(span)||span<.1f)span=nominal;
            var playback=Cue(packing?"transform_pack_game":"transform_deploy_game",position,.8f);
            // These short arrangements match the default state timings at natural
            // pitch. Configured timings scale their motion and final locking cues.
            if(playback!=null)playback.Pitch=Mathf.Clamp(nominal/span,.08f,8f);
            return playback;
        }
        internal static BastionPcmVoice Cue(string name, Vector3 position, float gain = 1f, float pitch = 1f, bool voice = false,uint speaker=0)
        {
            voice=voice||name.StartsWith("voice_",StringComparison.Ordinal);
            if (Application.isBatchMode || mixer.Muted || BastionPlugin.AudioVolume.Value <= 0f) return null;
            var listener = LocalUserManager.GetFirstLocalUser()?.cachedBody;
            if (!listener) return null;
            float distance = Vector3.Distance(position, listener.corePosition);
            float audibleRange=voice?90:name=="siege_impact"?140f:name=="siege_launch"?100f:name=="explosion"?100f:name=="impact"?75f:60f;
            if (distance >= audibleRange) return null;
            if (name == "hit") { if (Time.unscaledTime < nextHit) return null; nextHit = Time.unscaledTime + .065f; }
            var clip=Clip(name);if(clip==null||!session.Ensure(Time.unscaledTime))return null;
            if(voice&&!dialogue.CanStart(name,speaker,Time.time,dialogueVoice!=null&&!dialogueVoice.Stopped))return null;
            float volume = Mathf.Clamp01(gain) * BastionPlugin.AudioVolume.Value * Mathf.Pow(1f - distance / audibleRange, 2f) * (name == "hook" ? .55f : 1f);
            float pan = Mathf.Clamp(Vector3.Dot(listener.transform.right, (position - listener.corePosition).normalized), -.8f, .8f);
            var bus=voice?BastionAudioBus.Dialogue:name=="shieldbreak"?BastionAudioBus.Alert:BastionAudioBus.Effects;
            var playback=mixer.AddToBus(false,volume,pan,bus,clip);playback.Pitch=Mathf.Clamp(pitch,.5f,3f);
            if(voice&&!playback.Stopped){dialogueVoice=playback;dialogue.Started(name,speaker,Time.time,BastionPresentationSettings.DialogueSpacing?.Value??8,BastionPresentationSettings.DialogueRepeatCooldown?.Value??30);}
            return playback;
        }
        private static BastionPcmClip Clip(string name)
        {
            if(clips.TryGetValue(name,out var clip))return clip;
            if(unavailableClips.Contains(name))return null;
            try
            {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("IvoryBastion.assets.sfx_"+name+".wav"))
                    clip=BastionPcmClip.Read(stream);
                clips[name]=clip;return clip;
            }
            catch(Exception error)
            {unavailableClips.Add(name);BastionPlugin.Log.LogWarning("Ivory Bastion skipped unavailable cue '"+name+"': "+error.Message);return null;}
        }
    }
}
