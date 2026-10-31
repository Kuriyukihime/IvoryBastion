using System;
using UnityEngine;

namespace IvoryBastion
{
    // The same atlas coordinates and bounded gas motion are used by the preview.
    // All positions are local to a unit plume pointing along +Y.
    internal static class BastionThrusterGeometry
    {
        internal const int Frames=32,Columns=8,Rows=4,Planes=3,Flecks=14;
        internal const int TileWidth=256,TileHeight=512;
        private static float Frac(float v)=>v-(float)Math.Floor(v);
        internal static float Advance(float phase,float deltaTime,float power)=>Frac(phase+
            Math.Max(0,Math.Min(.1f,deltaTime))*(.72f+.44f*Math.Max(0,Math.Min(1,power))));
        internal static int Frame(float phase,int seed,out float blend)
        {
            float value=Frac(phase+seed*.173205f)*Frames;
            int frame=(int)value;blend=value-frame;return frame;
        }
        internal static Vector3 Vertex(int plane,int corner)
        {
            float angle=plane*(float)Math.PI/Planes;
            float side=corner==0||corner==3?-.5f:.5f;
            return new Vector3((float)Math.Cos(angle)*side,corner>=2?1:0,(float)Math.Sin(angle)*side);
        }
        internal static Vector2 UV(int corner,int frame)
        {
            // Half-texel gutters prevent bilinear sampling of a neighboring jet.
            float u=corner==0||corner==3?.5f/TileWidth:1-.5f/TileWidth;
            float v=corner>=2?1-.5f/TileHeight:.5f/TileHeight;
            return new Vector2((frame%Columns+u)/Columns,(Rows-1-frame/Columns+v)/Rows);
        }
        internal static Vector3 Fleck(int index,int seed,float time,out float alpha,out float size)
        {
            float t=Frac(time*(1.15f+(index%3)*.15f)+index*.618034f+seed*.137f);
            float angle=index*2.399963f+seed+time*.45f;
            float spread=(.08f+.18f*t)*(index%2==0?1:-1);
            alpha=(float)Math.Pow(Math.Sin(t*Math.PI),2)*(.18f+(index%3)*.045f);
            size=.045f+.06f*t;
            return new Vector3((float)Math.Cos(angle)*spread,.12f+t*.86f,(float)Math.Sin(angle)*spread);
        }
    }
}
