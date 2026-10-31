using System;
using UnityEngine;

namespace IvoryBastion
{
    // Presentation geometry shared by the live effects and their offline study.
    // No collision, damage, random-state or network state is changed here.
    internal static class BastionEffectGeometry
    {
        internal static readonly float[] ExhaustWidths={.34f,.83f,.43f,1,.35f,.73f,.25f,.48f,.15f,.28f,.10f,.12f,0};
        private static float Clamp(float x)=>Math.Max(0,Math.Min(1,x));
        private static float Ease(float x){x=Clamp(x);return x*x*(3-2*x);}
        internal static float Ignition(float age)=>age<0?0:Ease(age/.025f)*(1-Ease((age-.07f)/.24f));
        internal static float LanceFade(float age)=>1-Ease((age-.15f)/.57f);
        internal static float EnergyFalloff(float across)
        {
            float x=Math.Abs(across*2-1);
            float core=(float)Math.Exp(-x*x*7.5f);
            return core*(1-Ease((x-.72f)/.28f));
        }
        internal const int FeatherSections=16;
        internal static Vector3 FeatherRibbon(Vector3 origin,Vector3 tip,Vector3 side,float release,int layer,float along,float across)
        {
            var axis=(tip-origin).normalized;
            var start=Vector3.Lerp(origin,tip,layer==0?.25f:.43f);
            var end=tip+axis*(release*(layer==0?.66f:.79f));
            float belly=(float)Math.Pow(Math.Max(0,Math.Sin(along*Math.PI)),.72);
            float width=(layer==0?.045f+release*.15f:.015f+release*.045f)*belly;
            return Vector3.Lerp(start,end,along)+side*(across*width);
        }
        internal static Vector3 LanceCrown(int spoke,int point,float age)
        {
            float a=spoke*(float)Math.PI/3;
            float progress=Ease(age/.28f),along=point/3f;
            float radius=(.08f+progress*.95f)*along;
            return new Vector3((float)Math.Cos(a)*radius,(float)Math.Sin(a)*radius,-along*along*(.10f+progress*.32f));
        }

        internal static Vector3 Feather(Vector3 origin,Vector3 tip,Vector3 side,float release,int layer,int corner)
        {
            var axis=(tip-origin).normalized;
            float width=layer==0?.055f+release*.15f:.018f+release*.045f;
            float extension=release*(layer==0?.66f:.79f);
            if(corner==0)return origin+(tip-origin)*(layer==0?.32f:.50f);
            if(corner==2)return tip+axis*extension;
            return origin+(tip-origin)*.78f+axis*extension*.16f+side*width*(corner==1?1:-1);
        }

        // Six expanding anchor chevrons followed by six separated assembly arcs.
        // They are on the machine's base, not a cage around the pilot.
        internal static Vector3 Deployment(int segment,int point,float open)
        {
            float angle=(segment%6+.5f)*(float)Math.PI/3;
            var radial=new Vector3((float)Math.Cos(angle),0,(float)Math.Sin(angle));
            var side=new Vector3(-radial.z,0,radial.x);
            if(segment<6)
            {
                float radius=.42f+open*2.05f;
                float sideOffset=point==0?-.15f:point==1?-.15f:point==2?0:point==3?.15f:.15f;
                float radialOffset=point==0||point==4?-.30f:point==2?.12f:0;
                return radial*(radius+radialOffset)+side*sideOffset+Vector3.up*(-.54f+open*.06f);
            }
            float a=angle+(point/4f-.5f)*.32f;
            float r=.42f+open*1.94f;
            return new Vector3((float)Math.Cos(a)*r,-.55f+open*.06f,(float)Math.Sin(a)*r);
        }

        internal static Vector3 SkyfallArrow(int sector,int corner,float radius)
        {
            float a=(sector+.5f)*(float)Math.PI/2;
            var direction=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));
            var side=new Vector3(-direction.z,0,direction.x);
            float inner=radius*.82f,outer=radius*.94f;
            if(corner==0)return direction*inner;
            if(corner==2)return direction*(inner+radius*.045f);
            return direction*outer+side*radius*.035f*(corner==1?1:-1);
        }
    }
}
