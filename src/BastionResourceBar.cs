using UnityEngine;
using UnityEngine.UI;
namespace IvoryBastion
{
    // Solid resource colors remain distinct from the two defense hex patterns.
    public sealed class BastionResourceBar:MaskableGraphic
    {
        private float fraction,feather=1;private int segments=1;private BastionResourceKind kind;
        private readonly BastionResourceBarGeometry geometry=new BastionResourceBarGeometry();
        internal void Set(float value,BastionResourceKind resource,int cells=1)
        {
            value=float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);cells=Mathf.Clamp(cells,1,64);
            if(Mathf.Abs(value-fraction)<.00001f&&resource==kind&&segments==cells)return;
            fraction=value;kind=resource;segments=cells;SetVerticesDirty();
        }
        private void LateUpdate()
        {float next=BastionUiAntialias.Feather(rectTransform,canvas);if(Mathf.Abs(next-feather)>.01f){feather=next;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();feather=BastionUiAntialias.Feather(rectTransform,canvas);
            geometry.Update(rect.width,rect.height,fraction,kind,segments,feather);
            BastionUiAntialias.Apply(mesh,geometry.Mesh,new Vector2(rect.xMin,rect.yMin));
        }
    }
}
