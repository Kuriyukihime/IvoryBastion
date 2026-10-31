using System;

namespace IvoryBastion
{
    // Device ownership and retry policy run on the game thread. Native output
    // threads only publish a failure string; they never reopen a device or log.
    internal sealed class BastionAudioSession:IDisposable
    {
        private readonly BastionPcmMixer mixer;
        private readonly Func<BastionPcmMixer,Action<string>,IBastionAudioOutput> open;
        private readonly Action<string> report;
        private IBastionAudioOutput output;
        private volatile string failure;
        private float nextAttempt,retryDelay=2,startedAt;
        private volatile int generation;
        private bool reported;
        internal BastionAudioSession(BastionPcmMixer mixer,Func<BastionPcmMixer,Action<string>,IBastionAudioOutput> open,Action<string> report)
        {this.mixer=mixer;this.open=open;this.report=report;}
        internal void Tick(float now)
        {
            if(output!=null&&!output.IsRunning)Fault(now,failure??"The audio device stopped responding.");
            else if(output!=null&&now-startedAt>=5){retryDelay=2;reported=false;}
        }
        internal bool Ensure(float now)
        {
            Tick(now);if(output!=null)return true;if(now<nextAttempt)return false;
            try
            {
                failure=null;int opening=++generation;
                output=open(mixer,message=>{if(opening==generation)failure=message;});
                if(output==null||!output.IsRunning)throw new InvalidOperationException(failure??"The audio device did not start.");
                startedAt=now;return true;
            }
            catch(Exception error){Fault(now,error.Message);return false;}
        }
        private void Fault(float now,string message)
        {
            Retire();nextAttempt=now+retryDelay;retryDelay=Math.Min(30,retryDelay*2);
            if(!reported){reported=true;report?.Invoke("Ivory Bastion audio unavailable; retrying automatically. "+message);}
        }
        private void Retire()
        {
            generation++;var old=output;output=null;
            try{old?.Dispose();}catch(Exception){/* A failed device must not break the game update. */}
            finally{mixer.Clear();}
        }
        public void Dispose()
        {Retire();failure=null;nextAttempt=0;retryDelay=2;reported=false;}
    }
}
