using System;
using System.Collections.Generic;
namespace IvoryBastion
{
    // Local-listener arbitration covers every speaker and every voice cue.
    // Rejected events are skipped, never queued for an unrelated later moment.
    internal sealed class BastionDialogueGate
    {
        private readonly Dictionary<string,float> repeats=new Dictionary<string,float>();
        private float next,lastTime;
        internal bool CanStart(string line,uint speaker,float now,bool busy)
        {
            if(float.IsNaN(now)||float.IsInfinity(now)||string.IsNullOrEmpty(line))return false;
            if(now<lastTime)Reset();lastTime=now;
            return !busy&&now>=next&&(!repeats.TryGetValue(speaker+":"+line,out float until)||now>=until);
        }
        internal void Started(string line,uint speaker,float now,float spacing,float repeat)
        {next=now+Math.Max(0,spacing);repeats[speaker+":"+line]=now+Math.Max(0,repeat);}
        internal void Reset(){repeats.Clear();next=lastTime=0;}
    }
}
