using UnityEngine;
namespace IvoryBastion
{
    internal sealed class BastionResourceBarGeometry
    {
        internal readonly BastionUiMesh Mesh=new BastionUiMesh();
        internal void Update(float width,float height,float fraction,BastionResourceKind kind,int segments,float feather)
        {
            Mesh.Clear();fraction=float.IsNaN(fraction)||float.IsInfinity(fraction)?0:Mathf.Clamp01(fraction);segments=Mathf.Clamp(segments,1,64);
            float filled=width*fraction;if(filled<=0||height<=0)return;
            Color tint=BastionResourcePalette.Tint(kind),top=Color.Lerp(tint,Color.white,.18f);
            var low=new Color(tint.r*.75f,tint.g*.75f,tint.b*.75f,1);
            float cell=width/segments,gap=segments>1?Mathf.Min(.8f,cell*.15f):0;
            for(int i=0;i<segments;i++)
            {
                float left=cell*i;if(left>=filled)break;float right=Mathf.Min(filled,left+cell-gap);
                Mesh.Quad(left,0,right-left,height,0,low,tint,feather,true);
                float bottom=Mathf.Max(0,height-.65f);
                Mesh.Quad(left,bottom,right-left,height-bottom,0,top,top,feather);
            }
            Color cap=Color.Lerp(tint,Color.white,.4f);float capLeft=Mathf.Max(0,filled-.8f);
            Mesh.Quad(capLeft,0,filled-capLeft,height,0,cap,cap,feather);
        }
    }
}
