using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace IvoryBastion
{
    // Windows shared PCM output: RoR2 disables Unity Audio in its player build.
    // The native buffers and mixing thread never access Unity objects.
    internal sealed class BastionWaveOut : IBastionAudioOutput
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct Format
        {
            internal ushort Tag, Channels;
            internal uint SamplesPerSecond, AverageBytesPerSecond;
            internal ushort BlockAlign, BitsPerSample, ExtraSize;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Header
        {
            internal IntPtr Data;
            internal uint Length, BytesRecorded;
            internal UIntPtr User;
            internal uint Flags, Loops;
            internal IntPtr Next;
            internal UIntPtr Reserved;
        }
        private sealed class Buffer
        {
            internal IntPtr Data, Header;
            internal bool Prepared, Queued;
        }
        [DllImport("winmm.dll")] private static extern uint waveOutOpen(out IntPtr handle, uint device, ref Format format, IntPtr callback, UIntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr handle);

        private const int Frames = 1024, BufferBytes = Frames * 2 * 2;
        private readonly uint headerSize = (uint)Marshal.SizeOf<Header>();
        private readonly Buffer[] buffers = new Buffer[3];
        private readonly BastionPcmMixer mixer;
        private readonly Action<string> failure;
        private IntPtr device;
        private Thread thread;
        private volatile bool running;
        private bool disposed;
        private long submittedFrames;
        internal long SubmittedFrames => Interlocked.Read(ref submittedFrames);
        public bool IsRunning => running;
        internal BastionWaveOut(BastionPcmMixer mixer, Action<string> failure = null)
        {
            this.mixer = mixer; this.failure = failure;
            try
            {
                var format = new Format { Tag = 1, Channels = 2, SamplesPerSecond = BastionPcmMixer.Rate,
                    AverageBytesPerSecond = BastionPcmMixer.Rate * 4, BlockAlign = 4, BitsPerSample = 16 };
                Check(waveOutOpen(out device, uint.MaxValue, ref format, IntPtr.Zero, UIntPtr.Zero, 0), "open audio device");
                for (int i = 0; i < buffers.Length; i++)
                {
                    var buffer = buffers[i] = new Buffer();
                    buffer.Data = Marshal.AllocHGlobal(BufferBytes); buffer.Header = Marshal.AllocHGlobal((int)headerSize);
                    Marshal.StructureToPtr(new Header { Data = buffer.Data, Length = BufferBytes }, buffer.Header, false);
                    Check(waveOutPrepareHeader(device, buffer.Header, headerSize), "prepare PCM buffer"); buffer.Prepared = true;
                }
                running = true;
                thread = new Thread(Pump) { IsBackground = true, Name = "Bastion PCM output" }; thread.Start();
            }
            catch { Dispose(); throw; }
        }
        private static void Check(uint result, string action)
        { if (result != 0) throw new InvalidOperationException("Bastion could not " + action + " (Windows audio result " + result + ")."); }
        private void Pump()
        {
            var samples = new float[Frames * 2]; var pcm = new short[samples.Length];
            try
            {
                while (running)
                {
                    foreach (var buffer in buffers)
                    {
                        if (buffer.Queued && (Marshal.PtrToStructure<Header>(buffer.Header).Flags & 1u) == 0) continue;
                        mixer.Render(samples, Frames);
                        for (int i = 0; i < samples.Length; i++)
                            pcm[i] = (short)(Math.Max(-.98f, Math.Min(.98f, samples[i])) * 32767f);
                        Marshal.Copy(pcm, 0, buffer.Data, pcm.Length);
                        Check(waveOutWrite(device, buffer.Header, headerSize), "submit PCM buffer");
                        buffer.Queued = true; Interlocked.Add(ref submittedFrames, Frames);
                    }
                    Thread.Sleep(3);
                }
            }
            catch (Exception error) { running = false; failure?.Invoke(error.Message); }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; running = false;
            if (thread != null && thread != Thread.CurrentThread) thread.Join();
            if (device != IntPtr.Zero) waveOutReset(device);
            foreach (var buffer in buffers)
            {
                if (buffer == null) continue;
                if (device != IntPtr.Zero && buffer.Prepared) waveOutUnprepareHeader(device, buffer.Header, headerSize);
                if (buffer.Header != IntPtr.Zero) Marshal.FreeHGlobal(buffer.Header);
                if (buffer.Data != IntPtr.Zero) Marshal.FreeHGlobal(buffer.Data);
            }
            if (device != IntPtr.Zero) { waveOutClose(device); device = IntPtr.Zero; }
        }
    }
}
