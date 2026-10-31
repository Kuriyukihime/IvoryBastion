using System;

namespace IvoryBastion
{
    // Sample the run stopwatch even while production is ineligible, so waiting
    // in mobile form, while dead, or in a stopped-timer scene never banks income.
    internal sealed class BastionProductionClock
    {
        private float previousTime;
        private bool sampled,previouslyProducing;

        internal void Reset(){sampled=false;previouslyProducing=false;previousTime=0;}

        internal float Sample(float runTime,bool timerRunning,bool eligible)
        {
            if(float.IsNaN(runTime)||float.IsInfinity(runTime)||runTime<0){Reset();return 0;}
            float elapsed=sampled?runTime-previousTime:0;
            bool produce=sampled&&previouslyProducing&&timerRunning&&eligible&&elapsed>0;
            previousTime=runTime;sampled=true;previouslyProducing=timerRunning&&eligible;
            return produce?elapsed:0;
        }
    }
}
