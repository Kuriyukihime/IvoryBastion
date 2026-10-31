using System;
using System.Collections.Generic;
using System.IO;

namespace IvoryBastion
{
    internal enum BastionAudioBus { Effects, Engine, Dialogue, Alert }
    // Plain PCM, independent of Unity's disabled audio subsystem.
    internal sealed class BastionPcmClip
    {
        internal readonly float[] Samples;
        internal readonly int Rate, Channels;
        internal int Frames => Samples.Length / Channels;
        internal BastionPcmClip(float[] samples, int rate, int channels)
        {
            if (samples == null || rate <= 0 || channels < 1 || channels > 2 || samples.Length == 0 || samples.Length % channels != 0)
                throw new ArgumentException("Invalid Bastion PCM clip.");
            Samples = samples; Rate = rate; Channels = channels;
        }
        internal static BastionPcmClip Read(Stream stream)
        {
            using (var reader = new BinaryReader(stream ?? throw new FileNotFoundException("Missing Bastion audio.")))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Invalid Bastion WAV header.");
                reader.ReadInt32();
                if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Invalid Bastion WAV container.");
                int channels = 0, rate = 0, bits = 0, format = 0; byte[] pcm = null;
                while (stream.Position + 8 <= stream.Length)
                {
                    string chunk = new string(reader.ReadChars(4)); int length = reader.ReadInt32();
                    long next = stream.Position + length + (length & 1);
                    if (length < 0 || next > stream.Length) throw new InvalidDataException("Invalid Bastion WAV chunk.");
                    if (chunk == "fmt ")
                    {
                        if (length < 16) throw new InvalidDataException("Truncated Bastion WAV format.");
                        format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadInt32();
                        reader.ReadInt32(); reader.ReadUInt16(); bits = reader.ReadUInt16();
                    }
                    else if (chunk == "data") pcm = reader.ReadBytes(length);
                    stream.Position = next;
                }
                if (format != 1 || bits != 16 || channels < 1 || channels > 2 || rate <= 0 || pcm == null || pcm.Length % (channels * 2) != 0)
                    throw new InvalidDataException("Unsupported Bastion WAV format.");
                var samples = new float[pcm.Length / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = (short)(pcm[i * 2] | pcm[i * 2 + 1] << 8) / 32768f;
                return new BastionPcmClip(samples, rate, channels);
            }
        }
    }

    internal sealed class BastionPcmVoice : IDisposable
    {
        private readonly BastionPcmClip[] layers;
        private readonly bool loop;
        private double cursor;
        internal volatile float Volume = 1f, Pan, Pitch = 1f, GainA = 1f, GainB = 0f, GainC = 0f;
        internal volatile bool Paused = false, Stopped;
        internal BastionAudioBus Bus;
        internal BastionPcmVoice(bool looping, params BastionPcmClip[] clips)
        {
            if (clips == null || clips.Length < 1 || clips.Length > 3) throw new ArgumentException("Invalid Bastion audio layers.");
            foreach (var clip in clips)
                if (clip.Rate != clips[0].Rate || clip.Frames != clips[0].Frames)
                    throw new ArgumentException("Bastion layers must share one sample timeline.");
            layers = clips; loop = looping;
        }
        internal void Mix(float[] output, int frames, int outputRate,float busGain=1,float endBusGain=-1)
        {
            if (Stopped || Paused) return;
            float volume = Safe(Volume), pan = Math.Max(-1f, Math.Min(1f, Safe(Pan)));
            float gain=Math.Max(0,Safe(busGain));
            float gainStep=frames>0&&endBusGain>=0?(Math.Max(0,Safe(endBusGain))-gain)/frames:0;
            float leftGain = volume * Math.Min(1f, 1f - pan), rightGain = volume * Math.Min(1f, 1f + pan);
            float a = Safe(GainA), b = Safe(GainB), c = Safe(GainC);
            // Gameplay transformation cues cover the supported 0.1–3 second
            // state timings. Ordinary spatial cues retain their tighter Cue clamp.
            double step = layers[0].Rate / (double)outputRate * Math.Max(.08f, Math.Min(8f, Safe(Pitch)));
            for (int frame = 0; frame < frames; frame++)
            {
                if (cursor >= layers[0].Frames)
                {
                    if (!loop) { Stopped = true; break; }
                    cursor %= layers[0].Frames;
                }
                int first = (int)cursor, next = first + 1;
                if (next >= layers[0].Frames) next = loop ? 0 : first;
                float fraction = (float)(cursor - first);
                float left = Sample(layers[0], first, next, fraction, 0) * a;
                float right = Sample(layers[0], first, next, fraction, 1) * a;
                if (layers.Length > 1)
                { left += Sample(layers[1], first, next, fraction, 0) * b; right += Sample(layers[1], first, next, fraction, 1) * b; }
                if (layers.Length > 2)
                { left += Sample(layers[2], first, next, fraction, 0) * c; right += Sample(layers[2], first, next, fraction, 1) * c; }
                gain+=gainStep;
                output[frame * 2] += left * leftGain*gain; output[frame * 2 + 1] += right * rightGain*gain;
                cursor += step;
            }
        }
        private static float Sample(BastionPcmClip clip, int first, int next, float fraction, int channel)
        {
            channel = Math.Min(channel, clip.Channels - 1);
            float value = clip.Samples[first * clip.Channels + channel];
            return value + (clip.Samples[next * clip.Channels + channel] - value) * fraction;
        }
        private static float Safe(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        public void Dispose() { Stopped = true; }
    }

    internal sealed class BastionPcmMixer
    {
        internal const int Rate = 44100;
        private readonly object gate = new object();
        internal volatile bool Paused, Muted;
        internal volatile float MasterGain=1,EffectsGain=1,DialogueGain=1,AlertGain=1;
        private float duck=1;
        private readonly List<BastionPcmVoice> voices = new List<BastionPcmVoice>();
        private readonly BastionPeakLimiter limiter = new BastionPeakLimiter();
        internal void Clear()
        {lock(gate){foreach(var voice in voices)voice.Dispose();voices.Clear();duck=1;limiter.Reset();}}
        internal BastionPcmVoice Add(bool loop, float volume, float pan, params BastionPcmClip[] layers)
            =>AddToBus(loop,volume,pan,loop?BastionAudioBus.Engine:BastionAudioBus.Effects,layers);
        internal BastionPcmVoice AddToBus(bool loop,float volume,float pan,BastionAudioBus bus,params BastionPcmClip[] layers)
        {
            var voice = new BastionPcmVoice(loop, layers) { Volume = volume, Pan = pan,Bus=bus };
            lock (gate)
            {
                voices.RemoveAll(v => v.Stopped);
                if (voices.Count >= 64)
                {
                    int victim=-1;float score=float.PositiveInfinity;
                    for(int i=0;i<voices.Count;i++)
                    {
                        var v=voices[i];float rank=(v.Bus==BastionAudioBus.Alert?300:v.Bus==BastionAudioBus.Dialogue?200:v.Bus==BastionAudioBus.Engine?100:0)+Math.Max(0,v.Volume);
                        if(rank<score){score=rank;victim=i;}
                    }
                    // Busy batteries never evict an active warning or dialogue.
                    float incoming=(bus==BastionAudioBus.Alert?300:bus==BastionAudioBus.Dialogue?200:bus==BastionAudioBus.Engine?100:0)+Math.Max(0,volume);
                    if(incoming<score){voice.Dispose();return voice;}
                    voices[victim].Dispose();voices.RemoveAt(victim);
                }
                voices.Add(voice);
            }
            return voice;
        }
        internal void Render(float[] buffer, int frames)
        {
            if (frames < 0 || buffer.Length < frames * 2) throw new ArgumentException("Invalid Bastion output buffer.");
            Array.Clear(buffer, 0, frames * 2);
            if (Paused) return;
            lock (gate)
            {
                float speech=0,alert=0;
                foreach(var v in voices)if(!v.Stopped&&!v.Paused&&v.Volume>.001f)
                {if(v.Bus==BastionAudioBus.Dialogue)speech=Math.Max(speech,FiniteGain(v.Volume*DialogueGain*2));if(v.Bus==BastionAudioBus.Alert)alert=Math.Max(alert,FiniteGain(v.Volume*AlertGain*2));}
                float target=Math.Min(1-.66f*alert,1-.44f*speech),before=duck;
                float response=1f-(float)Math.Exp(-frames/(Rate*(target<duck?.035:.22)));
                duck+=(target-duck)*response;
                float master=FiniteGain(MasterGain);
                foreach (var voice in voices)
                {
                    float bus=voice.Bus==BastionAudioBus.Dialogue?FiniteGain(DialogueGain):voice.Bus==BastionAudioBus.Alert?FiniteGain(AlertGain):FiniteGain(EffectsGain);
                    bool backed=voice.Bus==BastionAudioBus.Engine||voice.Bus==BastionAudioBus.Effects;
                    float start=backed?(voice.Bus==BastionAudioBus.Engine?before*before:before):1;
                    float end=backed?(voice.Bus==BastionAudioBus.Engine?duck*duck:duck):1;
                    voice.Mix(buffer, frames, Rate,master*bus*start,master*bus*end);
                }
                voices.RemoveAll(v => v.Stopped);
            }
            // Muting suppresses every stream without freezing its playhead. Old
            // cues finish silently and music resumes at the current position.
            if (Muted) Array.Clear(buffer, 0, frames * 2);
            else limiter.Process(buffer, frames);
        }
        private static float FiniteGain(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(0,Math.Min(1,value));
    }
    // Linked stereo look-ahead over each already-rendered block. Quiet mixes are untouched;
    // crowded battles reduce gain instead of hitting the output driver's hard clip.
    internal sealed class BastionPeakLimiter
    {
        private float gain = 1f;
        internal void Reset(){gain=1;}
        internal void Process(float[] buffer, int frames)
        {
            float peak = 0f;
            for (int i = 0; i < frames * 2; i++)
            { if (float.IsNaN(buffer[i]) || float.IsInfinity(buffer[i])) buffer[i] = 0f; peak = Math.Max(peak, Math.Abs(buffer[i])); }
            float target = peak > .92f ? .92f / peak : 1f;
            if (target < gain) gain = target;
            float release = 1f - (float)Math.Exp(-1.0 / (44100.0 * .18));
            for (int frame = 0; frame < frames; frame++)
            {
                // Never release past this block's safe gain; preserve the stereo image.
                gain = Math.Min(target, gain + (1f - gain) * release);
                buffer[frame * 2] *= gain; buffer[frame * 2 + 1] *= gain;
            }
        }
    }
}
