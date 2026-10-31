using UnityEngine;
using UnityEngine.UI;

namespace IvoryBastion
{
    internal static class BastionUiAntialias
    {
        internal static float Feather(RectTransform rect,Canvas canvas)
        {
            if(!rect)return 1;
            Camera camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var origin=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(Vector3.zero));
            var x=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(Vector3.right))-origin;
            var y=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(Vector3.up))-origin;
            float pixels=Mathf.Min(x.magnitude,y.magnitude);
            if(float.IsNaN(pixels)||float.IsInfinity(pixels)||pixels<.01f)return 1;
            // Screen-space projection has tiny position-dependent roundoff.
            // Quantization keeps cached honeycomb meshes stable while staying
            // far below a visible pixel at ordinary HUD scales.
            return Mathf.Clamp(Mathf.Round(128/pixels)/128,.025f,4);
        }
        internal static void Apply(VertexHelper target,BastionUiMesh source,Vector2 origin=default(Vector2))
        {
            int offset=target.currentVertCount;
            for(int i=0;i<source.Vertices.Count;i++)target.AddVert(origin+source.Vertices[i],source.Colors[i],Vector2.zero);
            for(int i=0;i<source.Triangles.Count;i+=3)target.AddTriangle(offset+source.Triangles[i],offset+source.Triangles[i+1],offset+source.Triangles[i+2]);
        }
    }
}
