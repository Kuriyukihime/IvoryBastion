using UnityEngine;
using UnityEngine.UI;

namespace IvoryBastion
{
    // Plain UGUI Images form the tracks, fine frames and buttons. Give their
    // silhouettes the same pixel coverage as the custom meters; sprite art
    // keeps Image's native sampling and masking.
    public sealed class BastionSmoothImage:Image
    {
        private BastionUiMesh edgeMesh;
        private float feather=1;
        private void LateUpdate()
        {
            if(sprite)return;
            float next=BastionUiAntialias.Feather(rectTransform,canvas);
            if(Mathf.Abs(next-feather)>.025f){feather=next;SetVerticesDirty();}
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if(sprite){base.OnPopulateMesh(mesh);return;}
            mesh.Clear();if(edgeMesh==null)edgeMesh=new BastionUiMesh(8,30);edgeMesh.Clear();var rect=GetPixelAdjustedRect();
            feather=BastionUiAntialias.Feather(rectTransform,canvas);
            edgeMesh.Quad(rect.xMin,rect.yMin,rect.width,rect.height,0,color,color,feather);
            BastionUiAntialias.Apply(mesh,edgeMesh);
        }
    }
}
