using System;
using UnityEngine;
namespace IvoryBastion
{
// Preserve all packet ordinals; Armed is reserved for old review builds.
internal enum BastionMinePhase:byte {Airborne,Planted,Crawling,Leaping,Attached,Primed,Armed}
    internal static class BastionMineRules
    {
        internal const int MaximumMines=48,MaximumPerTarget=12;
        internal static int TargetLimit(bool boss,bool elite)=>boss?12:elite?8:5;
        internal static float ContactAngle(int slot)=>slot==1||slot==8?52:slot==2||slot==9?-52:slot==3||slot==5?26:slot==4||slot==6?-26:0;
        internal static float ContactHeight(int slot)=>slot==1?.18f:slot==2?-.18f:slot==3||slot==4?.65f:slot==5||slot==6?-.65f:slot==7?1.15f:slot==8||slot==9?1:slot==10?-1.1f:slot==11?1.7f:0;
        internal const float ArcHeight=3.5f,ArmDelay=.3f,DeployWindup=.15f,OverchargeFuse=.15f;
        internal const float LatchRange=2.5f,LatchTime=.28f,ShellClearance=.04f;
        // Shared projectile clearance sweeps a 0.12m sphere. Even on the
        // steepest supported ground (normal.y > .35), this lift keeps it clear.
        internal const float LeapProbeLift=.45f,StepProbeLift=.25f,MaximumStep=.7f;
        internal static Vector3 LandingOffset(int index,int count,float radius,float blastRadius)
        {
            int ring=Math.Max(0,index)%3,spoke=Math.Max(0,index)/3;
            int spokes=Math.Max(1,(Math.Max(1,count)+2-ring)/3);
            float reach=radius*(ring+1)/3;
            float a=(spoke+.5f*(ring%2))*(float)Math.PI*2/spokes;
            return new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a))*reach;
        }
        internal static Vector3 Flight(Vector3 from,Vector3 to,float age,float duration)
        {float t=Math.Max(0,Math.Min(1,age/Math.Max(.1f,duration)));return Vector3.Lerp(from,to,t)+Vector3.up*(4*ArcHeight*t*(1-t));}
        internal static Vector3 FlightVelocity(Vector3 from,Vector3 to,float age,float duration)
        {float t=Math.Max(0,Math.Min(1,age/Math.Max(.1f,duration)));return (to-from)/duration+Vector3.up*(4*ArcHeight*(1-2*t)/duration);}
        internal static bool CanWake(float groundedAge,float distance,float triggerRange)=>groundedAge>=ArmDelay&&distance<=triggerRange;
        internal static bool CanChase(float walked,float maximum)=>walked<maximum;
internal static Vector3 LatchFlight(Vector3 from,Vector3 to,float age)
{float t=Math.Max(0,Math.Min(1,age/LatchTime));return Vector3.Lerp(from,to,t)+Vector3.up*(2*t*(1-t));}
internal static Vector3 LatchVelocity(Vector3 from,Vector3 to,float age)
{float t=Math.Max(0,Math.Min(1,age/LatchTime));return (to-from)/LatchTime+Vector3.up*(2*(1-2*t)/LatchTime);}
internal static Vector3 Mount(int shot)=>new Vector3(shot%2==0?-.78f:.78f,.80f,.46f);
    }
}
