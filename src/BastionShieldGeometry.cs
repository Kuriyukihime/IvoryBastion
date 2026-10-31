using System;
using UnityEngine;

namespace IvoryBastion
{
    internal sealed class BastionShieldGeometry
    {
        internal const int Columns=112,Rows=72;
        internal const float Radius=1.22f,SiegeRadius=4.2f,FortificationGap=.22f,RippleDuration=.85f;
        internal static float FormRadius(float deployed)=>Radius+(SiegeRadius-Radius)*Math.Max(0,Math.Min(1,deployed));
        internal Vector3[] Normals;
        internal int[] Triangles;

        // A continuous surface: no tile borders, lattice, or circular openings.
        internal static BastionShieldGeometry Build()
        {
            var normals=new Vector3[(Rows+1)*(Columns+1)];
            var triangles=new int[Columns*(Rows-1)*6];int t=0;
            for(int row=0;row<=Rows;row++)for(int column=0;column<=Columns;column++)
            {
                double latitude=Math.PI*row/Rows,yaw=2*Math.PI*column/Columns;
                normals[row*(Columns+1)+column]=new Vector3((float)(Math.Sin(latitude)*Math.Cos(yaw)),
                    (float)Math.Cos(latitude),(float)(Math.Sin(latitude)*Math.Sin(yaw)));
                if(row==Rows||column==Columns)continue;
                int a=row*(Columns+1)+column,b=a+1,c=a+Columns+1,d=c+1;
                if(row>0){triangles[t++]=a;triangles[t++]=b;triangles[t++]=c;}
                if(row<Rows-1){triangles[t++]=b;triangles[t++]=d;triangles[t++]=c;}
            }
            return new BastionShieldGeometry{Normals=normals,Triangles=triangles};
        }

        internal struct RippleFrame
        {
            private readonly float front;private readonly double envelope,contactFade;private readonly bool active;
            internal RippleFrame(float age,float strength)
            {active=age>=0&&age<RippleDuration;front=age*5.8f;envelope=active?Math.Pow(1-age/RippleDuration,1.3)*strength:0;contactFade=active?Math.Exp(-age*16):0;}
            internal float At(float angle)
            {
            if(!active)return 0;
            // A tight pressure crest followed by a broad, quieter wake. Keeping
            // them distinct makes a hit read as a wave travelling over glass.
            double crest=(angle-front)/.12f,wake=(angle-front+.24f)/.18f;
            double contact=Math.Exp(-angle*angle/.025f)*contactFade;
            return (float)((Math.Exp(-crest*crest)+.32*Math.Exp(-wake*wake)+contact)*envelope);
            }
        }
        internal static float Ripple(float angle,float age,float strength)=>new RippleFrame(age,strength).At(angle);

        internal static Color Tint(Vector3 normal,Vector3 view,float time,float ripple,float visibility)
        {
            float facing=Vector3.Dot(normal,view);
            double r=1-Math.Abs(facing),r2=r*r,r4=r2*r2,r8=r4*r4,r16=r8*r8;
            float rim=(float)r4,edge=(float)(r16*r2);
            double s=.5+.5*Math.Sin(normal.x*9+normal.y*13+normal.z*6-time*1.1),s2=s*s,s4=s2*s2,s8=s4*s4;
            float shimmer=(float)(s8*s);
            double g=Math.Max(0,normal.y*.65f-normal.x*.35f+normal.z*.2f),g2=g*g,g4=g2*g2,g8=g4*g4;
            float glint=(float)(g8*g8)*.16f;
            float glow=Math.Min(1,ripple);
            return new Color(Math.Min(1,.12f+.80f*glow+.20f*rim+.12f*edge),
                Math.Min(1,.46f+.49f*glow+.18f*rim+.12f*edge),1,
                Math.Min(.8f,.009f+.12f*rim+.075f*edge+.008f*shimmer+glint+.64f*glow)*visibility);
        }
    }
}
