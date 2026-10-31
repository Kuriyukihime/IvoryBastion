using System;
using UnityEngine;
namespace IvoryBastion
{
    internal static class BastionMineChainGeometry
    {
        internal const float LinkLifetime=.22f,BurstLifetime=.5f;
        internal const int ArcCount=10,ForkCount=6,CoreCount=6,ArcPoints=9,ForkPoints=5,CorePoints=7;
        private static float Clamp(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(0,Math.Min(1,value));
        private static float Radius(float radius)=>float.IsNaN(radius)||float.IsInfinity(radius)?0:Math.Max(0,Math.Min(1000,radius));
        private static float Smooth(float t){t=Clamp(t);return t*t*(3-2*t);}
        private static float Ease(float t){t=Clamp(t);return 1-(1-t)*(1-t)*(1-t);}
        private static float Unit(int i,int salt)
        {unchecked{uint n=(uint)(i*73856093+salt*19349663);n^=n>>13;n*=1274126177;return(n&65535)/65535f;}}
        private static int Wrap(int i,int count)=>(i%count+count)%count;
        private static Vector3 Direction(int index)
        {
            index=Wrap(index,ArcCount);float y=-.8f+1.6f*(index+.5f)/ArcCount;
            float angle=index*2.399963f+.17f,side=(float)Math.Sqrt(1-y*y);
            return new Vector3((float)Math.Cos(angle)*side,y,(float)Math.Sin(angle)*side);
        }
        private static Vector3 Tangent(Vector3 direction)
        {
            float length=(float)Math.Sqrt(direction.x*direction.x+direction.z*direction.z);
            return length>.001f?new Vector3(-direction.z/length,0,direction.x/length):new Vector3(1,0,0);
        }
        private static Vector3 Cross(Vector3 a,Vector3 b)=>new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        private static Vector3 Bound(Vector3 point,float radius,float width)
        {
            float limit=Math.Max(0,radius-width*.5f-radius*.004f),distance=point.magnitude;
            return distance>limit&&distance>0?point*(limit/distance):point;
        }
        // All new samples include their visible half-width in the physical
        // envelope. A filament never promises damage beyond the real radius.
        internal static float BurstWidth(int layer,float age,float radius)
        {
            radius=Radius(radius);float t=Clamp(age);
            return layer==2?Math.Min(1.12f,radius*.14f)*(1-.65f*t):
                layer==1?Math.Min(.15f,radius*.019f)*(1-.55f*t):Math.Min(.28f,radius*.035f)*(1-.6f*t);
        }
        internal static float Opacity(int layer,int index,float age)
        {
            float t=Clamp(age);if(t>=1)return 0;
            float fade=(1-t)*(1-t);
            if(layer==2)return .54f*fade*(1-Smooth((t-.28f)/.5f));
            // Three short, irregular electrical re-strikes, no rotating ring.
            float flicker=.58f+.42f*Unit(index,(int)(t*16)+23);
            return fade*flicker*(layer==1?.82f:1);
        }
        internal static Vector3 Arc(int index,int point,float age,float radius)
        {
            radius=Radius(radius);float t=Clamp(age),u=Math.Max(0,Math.Min(ArcPoints-1,point))/(float)(ArcPoints-1);
            index=Wrap(index,ArcCount);var direction=Direction(index);var side=Tangent(direction);var up=Cross(direction,side);
            float extent=.17f+.80f*Ease(t/.28f),distance=radius*(.035f+(extent-.035f)*u);
            int frame=(int)(t*16);float bend=radius*(.018f+.045f*u)*(float)Math.Sin(u*Math.PI);
            var offset=side*(bend*(Unit(point+index*11,frame+7)*2-1))+up*(bend*(Unit(point+index*17,frame+41)*2-1));
            return Bound(direction*distance+offset,radius,BurstWidth(0,t,radius));
        }
        internal static Vector3 Fork(int index,int point,float age,float radius)
        {
            radius=Radius(radius);float t=Clamp(age),u=Math.Max(0,Math.Min(ForkPoints-1,point))/(float)(ForkPoints-1);
            index=Wrap(index,ForkCount);int trunk=(index*3+1)%ArcCount;var direction=Direction(trunk);var side=Tangent(direction);
            var start=Arc(trunk,4,t,radius);float turn=index%2==0?1:-1;
            var end=start+direction*(radius*(.13f+.19f*Ease(t/.32f)))+side*(radius*turn*(.14f+.13f*Ease(t/.3f)));
            var p=start+(end-start)*u+Cross(direction,side)*(radius*.055f*(Unit(index*7+point,(int)(t*16)+19)*2-1)*(float)Math.Sin(u*Math.PI));
            return Bound(p,radius,BurstWidth(1,t,radius));
        }
        internal static Vector3 Core(int index,int point,float age,float radius)
        {
            radius=Radius(radius);float t=Clamp(age);index=Wrap(index,CoreCount);point=Wrap(point,CorePoints-1);
            float u=point/(float)(CorePoints-1),angle=u*(float)Math.PI*2+index*.43f+t*(index%2==0?2.1f:-1.7f);
            var normal=Direction(index+2);var side=Tangent(normal);var up=Cross(normal,side);
            float irregular=.66f+.34f*Unit(point+index*7,31),size=radius*(.06f+.105f*Ease(t/.22f))*irregular;
            var p=(side*(float)Math.Cos(angle)+up*(float)Math.Sin(angle))*size+normal*(radius*.025f*(Unit(point+index*3,67)*2-1));
            return Bound(p,radius,BurstWidth(2,t,radius));
        }
        internal static float Expansion(float age)=>.15f+.85f*Smooth(age/.55f);
        internal static Vector3 Chevron(int index,int point,float age,float radius)
        {
            radius=Radius(radius);age=Clamp(age);float a=index*(float)Math.PI/3+(point-1)*.14f;
            float r=radius*.99f*Expansion(age)*(point==1?1:.79f);
            return new Vector3((float)Math.Cos(a)*r,Math.Min(.12f,radius*.05f),(float)Math.Sin(a)*r);
        }
        internal static Vector3 Spark(int index,int point,float age,float radius)
        {
            radius=Radius(radius);age=Clamp(age);float a=(index+.5f)*(float)Math.PI/3;
            float travel=Math.Min(.92f,.08f+age*.84f);float r=radius*Math.Max(0,travel-(2-point)*.13f);
            return new Vector3((float)Math.Cos(a)*r,r*.12f,(float)Math.Sin(a)*r);
        }
    }
}
