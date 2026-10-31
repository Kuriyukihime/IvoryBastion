using System;
using UnityEngine;

namespace IvoryBastion
{
    internal enum BastionExplosionLayer { Fireball, Smoke, Spark, Flash }

    // Shared by the renderer and offline footprint checks. All positions are relative
    // to the damage event; no free particle velocity can escape the blast envelope.
    internal static class BastionExplosionGeometry
    {
        internal struct Sample
        {
            internal Vector3 Position;
            internal float Size,Alpha,Rotation,Life,Remaining;
        }
        internal static float Lifetime(int tier)=>tier==2?2.8f:tier==1?2.1f:1.35f;
        internal static float FireTime(int tier)=>tier==2?1.6f:tier==1?1.2f:.85f;
        internal static float ExpansionTime(int tier)=>tier==2?.22f:tier==1?.18f:.12f;
        internal static int Count(BastionExplosionLayer layer,int tier)=>layer==BastionExplosionLayer.Smoke?(tier==2?9:tier==1?6:4):
            layer==BastionExplosionLayer.Spark?(tier==2?32:tier==1?22:12):1;
        private static float Clamp(float v)=>Math.Max(0,Math.Min(1,v));
        private static float Ease(float t){t=Clamp(t);return 1-(1-t)*(1-t)*(1-t);}
        private static float Unit(int i,int salt)
        {
            unchecked{uint n=(uint)(i*73856093+salt*19349663);n^=n>>13;n*=1274126177;return (n&65535)/65535f;}
        }
        internal static Sample At(BastionExplosionLayer layer,int i,int tier,float radius,float age,int seed)
        {
            var s=new Sample();if(radius<=0||age<0)return s;
            float fire=FireTime(tier),life=Lifetime(tier);
            float u=Unit(i,seed+17),v=Unit(i,seed+29);
            s.Rotation=v*360;
            if(layer==BastionExplosionLayer.Fireball)
            {
                s.Life=fire;s.Remaining=fire-age;
                float t=Clamp(age/fire);
                // Diameter reaches exactly twice the damage radius, then stays there
                // while the hot volume cools. Texture support is bounded by a circle.
                s.Size=2*radius*(.06f+.94f*Ease(age/ExpansionTime(tier)));
                s.Alpha=.94f*(1-Clamp((t-.30f)/.70f));
                s.Rotation=(v-.5f)*35;
            }
            else if(layer==BastionExplosionLayer.Flash)
            {
                s.Life=.12f;s.Remaining=s.Life-age;s.Size=radius*(.30f+Ease(age/.05f));
                s.Alpha=.9f*(1-Clamp(age/s.Life));
            }
            else
            {
                bool smoke=layer==BastionExplosionLayer.Smoke;
                float delay=smoke?fire*.20f:0;
                s.Life=(smoke?life-delay:fire*.85f)*(.8f+.2f*u);
                s.Remaining=s.Life-(age-delay);if(age<delay){s.Alpha=0;return s;}
                float t=Clamp((age-delay)/s.Life);
                float vertical=smoke?-.15f+.95f*u:-.6f+1.6f*u;
                float angle=i*2.399963f+v*1.2f;
                float horizontal=(float)Math.Sqrt(1-vertical*vertical);
                var direction=new Vector3((float)Math.Cos(angle)*horizontal,vertical,(float)Math.Sin(angle)*horizontal);
                if(smoke)
                {
                    s.Size=radius*(.42f+.22f*Ease(t))*(.8f+.2f*v);
                    s.Position=direction*(radius*(.12f+.48f*Ease(t)))+Vector3.up*(radius*.12f*t);
                    s.Alpha=.42f*Clamp(t/.16f)*(1-Clamp((t-.25f)/.75f));
                    s.Rotation+=t*(v-.5f)*34;
                }
                else
                {
                    s.Size=Math.Min(.35f,radius*.045f)*(1-.65f*t);
                    s.Position=direction*(radius*.94f*Ease(t))-Vector3.up*(radius*.18f*t*t);
                    s.Alpha=(1-t)*(1-t);s.Rotation=-angle*57.29578f;
                }
                float distance=s.Position.magnitude,limit=Math.Max(0,radius-s.Size*.5f);
                if(distance>limit&&distance>0)s.Position*=limit/distance;
            }
            if(s.Remaining<=0)s.Alpha=0;
            return s;
        }
    }
}
