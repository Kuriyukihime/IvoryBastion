using UnityEngine;
using UnityEngine.Rendering;

namespace IvoryBastion
{
    // A shared, softly feathered cross-section for energy ribbons. The core
    // remains crisp while the wide blue layer no longer has rectangular edges.
    internal static class BastionEnergySurface
    {
        private static Texture2D texture;
        internal static void Apply(Material material)
        {
            if(!texture)
            {
                const int width=32,height=128;
                texture=new Texture2D(width,height,TextureFormat.RGBA32,true){name="Seraph energy falloff",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};
                var pixels=new Color32[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    float alpha=BastionEffectGeometry.EnergyFalloff(y/(float)(height-1));
                    pixels[y*width+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha*255));
                }
                texture.SetPixels32(pixels);texture.Apply(true,true);
            }
            material.SetTexture("_MainTex",texture);
            if(material.HasProperty("_SrcBlend"))material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            if(material.HasProperty("_DstBlend"))material.SetFloat("_DstBlend",(float)BlendMode.One);
        }
    }
}
