using System;

namespace IvoryBastion
{
    // Time-based, frame-rate-independent presentation; never touches a reserve.
    internal sealed class BastionReadoutHistory
    {
        internal float Fraction { get; private set; }
        internal float Trail { get; private set; }
        internal bool Rising { get; private set; }
        private bool initialized;
        private float previousTime, holdUntil, risingUntil;

        internal void Update(float value,float now)
        {
            value=float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(0,Math.Min(1,value));
            if(!initialized||now<previousTime)
            {initialized=true;Fraction=Trail=value;previousTime=now;holdUntil=risingUntil=now;Rising=false;return;}
            float dt=Math.Min(.25f,Math.Max(0,now-previousTime));previousTime=now;
            if(value<Fraction-.0001f)
            {if(Trail<=Fraction+.002f)holdUntil=now+.36f;Trail=Math.Max(Trail,Fraction);}
            if(value>Fraction+.0001f){risingUntil=now+.30f;Trail=Math.Max(Trail,value);}
            Fraction=value;Rising=now<risingUntil;
            if(now>=holdUntil)Trail=Math.Max(value,Trail-dt*.8f);
            Trail=Math.Max(value,Math.Min(1,Trail));
        }
    }
}
