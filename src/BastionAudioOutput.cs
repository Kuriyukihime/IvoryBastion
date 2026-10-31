using System;

namespace IvoryBastion
{
    internal interface IBastionAudioOutput:IDisposable { bool IsRunning { get; } }
    internal static class BastionAudioOutput
    {
        internal static IBastionAudioOutput Open(BastionPcmMixer mixer,Action<string> error)
        {
            if(Environment.OSVersion.Platform==PlatformID.Win32NT)return new BastionWaveOut(mixer,error);
            if(Environment.OSVersion.Platform==PlatformID.Unix)return new BastionPulseAudio(mixer,error);
            throw new PlatformNotSupportedException("Ivory Bastion PCM output supports Windows or Linux with PulseAudio.");
        }
    }
}
