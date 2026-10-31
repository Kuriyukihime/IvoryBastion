using UnityEngine;
using UnityEngine.UI;
namespace IvoryBastion
{
    public sealed class BastionChrome:MaskableGraphic
    {
        internal Color Accent=new Color(.20f,.60f,1);internal bool Angled=true;
        private readonly BastionChromeGeometry geometry=new BastionChromeGeometry();
        private float feather=1;
        internal void SetAccent(Color color){if(Accent==color)return;Accent=color;SetVerticesDirty();}
        private void LateUpdate()
        {float next=BastionUiAntialias.Feather(rectTransform,canvas);if(Mathf.Abs(next-feather)>.01f){feather=next;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=rectTransform.rect;feather=BastionUiAntialias.Feather(rectTransform,canvas);
            geometry.Update(rect.width,rect.height,Accent,Angled,feather);
            BastionUiAntialias.Apply(mesh,geometry.Mesh,new Vector2(rect.xMin,rect.yMin));
        }
    }
}
