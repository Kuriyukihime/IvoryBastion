using UnityEngine;
namespace IvoryBastion
{
    internal sealed class BastionChromeGeometry
    {
        internal readonly BastionUiMesh Mesh=new BastionUiMesh();
        private readonly Vector2[] polygon=new Vector2[4];private float feather;
        private void Quad(float x,float y,float w,float h,float cut,Color left,Color right)
            =>Mesh.Quad(x,y,w,h,cut,left,right,feather);
        private void Hex(float x,float y,float radius,float stroke,Color tint)
        {
            for(int side=0;side<6;side++)
            {
                float a=side*Mathf.PI/3,b=(side+1)*Mathf.PI/3;
                polygon[0]=new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius);
                polygon[1]=new Vector2(x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius);
                polygon[2]=new Vector2(x+Mathf.Cos(b)*(radius-stroke),y+Mathf.Sin(b)*(radius-stroke));
                polygon[3]=new Vector2(x+Mathf.Cos(a)*(radius-stroke),y+Mathf.Sin(a)*(radius-stroke));
                Mesh.Polygon(polygon,4,(Color32)tint,feather);
            }
        }
        internal void Update(float width,float height,Color accent,bool angled,float edge)
        {
            Mesh.Clear();feather=edge;var r=new Rect(0,0,width,height);float cut=angled?14:0;
            Quad(r.xMin,r.yMin,r.width,r.height,cut,new Color(.72f,.83f,.94f,.8f),new Color(.21f,.37f,.57f,.8f));
            Quad(r.xMin+1,r.yMin+1,r.width-2,r.height-2,cut,new Color(.027f,.046f,.070f,.97f),new Color(.010f,.021f,.037f,.97f));
            Quad(r.xMin+cut+3,r.yMax-4,Mathf.Min(92,r.width*.23f),3,2,new Color(.84f,.9f,.96f,.95f),new Color(.53f,.66f,.78f,.85f));
            Quad(r.xMin+cut+100,r.yMax-3,r.width-cut-114,2,0,accent,new Color(accent.r,accent.g,accent.b,.07f));
            Quad(r.xMin+cut+3,r.yMax-15,r.width-cut-20,10,0,new Color(accent.r,accent.g,accent.b,.025f),new Color(accent.r,accent.g,accent.b,0));
            for(int x=0;x<3;x++)for(int y=0;y<3;y++)Hex(r.xMin+24+x*13,r.yMin+24+y*15+(x%2)*7.5f,8,.7f,new Color(.36f,.65f,1,.045f));
            Hex(r.xMax-13,r.yMax-13,4.5f,.85f,new Color(.86f,.95f,1,.75f));
            Quad(r.xMin+6,r.yMin+12,1,r.height-31,0,new Color(.38f,.55f,.72f,.55f),new Color(.38f,.55f,.72f,.55f));
            Quad(r.xMax-7,r.yMin+cut+4,1,24,0,accent,accent);
            Quad(r.xMax-cut-31,r.yMin+4,24,1,0,accent,accent);
            for(int i=0;i<3;i++)Quad(r.xMin+cut+7+i*7,r.yMin+4,4,1,0,new Color(.53f,.66f,.78f,.6f),new Color(.53f,.66f,.78f,.6f));
        }
    }
}
