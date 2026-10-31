using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace IvoryBastion
{
    // Optional Linux backend. Windows continues to use the proven waveOut sink.
    // The streaming thread never reads Unity state; the mixer receives gains
    // and pause state from the main thread. No native library is redistributed.
    internal sealed class BastionPulseAudio:IBastionAudioOutput
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SampleSpec { internal int Format;internal uint Rate;internal byte Channels; }
        [StructLayout(LayoutKind.Sequential)]
        private struct BufferAttributes { internal uint Maximum,Target,Prebuffer,Minimum,Fragment; }
        [DllImport("libpulse-simple.so.0",CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr pa_simple_new(string server,string name,int direction,string device,string stream,ref SampleSpec spec,IntPtr channels,ref BufferAttributes attributes,out int error);
        [DllImport("libpulse-simple.so.0",CallingConvention=CallingConvention.Cdecl)]
        private static extern int pa_simple_write(IntPtr stream,byte[] bytes,UIntPtr length,out int error);
        [DllImport("libpulse-simple.so.0",CallingConvention=CallingConvention.Cdecl)]
        private static extern void pa_simple_free(IntPtr stream);
        private readonly BastionPcmMixer mixer;
        private readonly Action<string> failure;
        private readonly Thread worker;
        private IntPtr device;
        private volatile bool running;
        private bool disposed;
        public bool IsRunning=>running;

        internal BastionPulseAudio(BastionPcmMixer mixer,Action<string> failure)
        {
            this.mixer=mixer;this.failure=failure;
            var spec=new SampleSpec{Format=3,Rate=BastionPcmMixer.Rate,Channels=2}; // PA_SAMPLE_S16LE
            var attributes=new BufferAttributes{Maximum=uint.MaxValue,Target=8192,Prebuffer=uint.MaxValue,Minimum=4096,Fragment=uint.MaxValue};
            device=pa_simple_new(null,"Ivory Bastion",1,null,"Seraph effects",ref spec,IntPtr.Zero,ref attributes,out int error);
            if(device==IntPtr.Zero)throw new InvalidOperationException("PulseAudio output could not open ("+error+").");
            running=true;worker=new Thread(Pump){IsBackground=true,Name="Ivory Bastion PulseAudio"};
            try{worker.Start();}catch{pa_simple_free(device);device=IntPtr.Zero;running=false;throw;}
        }
        private void Pump()
        {
            const int frames=1024;var samples=new float[frames*2];var bytes=new byte[frames*4];
            IntPtr stream=device;
            try
            {
                while(running)
                {
                    mixer.Render(samples,frames);
                    for(int i=0;i<samples.Length;i++)
                    {short value=(short)(Math.Max(-1,Math.Min(1,samples[i]))*32767);bytes[i*2]=(byte)value;bytes[i*2+1]=(byte)(value>>8);}
                    if(pa_simple_write(stream,bytes,(UIntPtr)bytes.Length,out int error)<0)
                        throw new InvalidOperationException("PulseAudio streaming failed ("+error+").");
                }
            }
            catch(Exception error){failure?.Invoke(error.Message);}
            finally{running=false;pa_simple_free(stream);device=IntPtr.Zero;}
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;running=false;
            // The pump owns its stream, so a stalled write can never race a free.
            if(worker!=null&&worker.IsAlive)worker.Join(250);
        }
    }
}
