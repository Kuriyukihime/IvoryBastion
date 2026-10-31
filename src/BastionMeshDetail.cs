using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionMeshDetail
    {
        internal static SkinnedMeshRenderer[] Create(GameObject root,Mesh[] levels,Transform[] bones,Material material,Bounds bounds)
        {
            // A skinned renderer must retain its mesh throughout rendering.
            // Swapping topology from onSceneCameraPreCull invalidates Unity's
            // already prepared skin buffers, especially with multiple cameras.
            // Native LOD culling selects stable renderers without replacing meshes.
            var surfaces=new SkinnedMeshRenderer[levels.Length];
            var lods=new LOD[levels.Length];
            for(int i=0;i<levels.Length;i++)
            {
                var child=new GameObject(levels[i].name+"_Reconstructed");
                child.transform.SetParent(root.transform,false);
                var surface=child.AddComponent<SkinnedMeshRenderer>();
                surface.sharedMesh=levels[i];
                surface.bones=bones;surface.rootBone=bones[0];
                surface.quality=SkinQuality.Bone4;surface.updateWhenOffscreen=false;
                surface.localBounds=bounds;surface.sharedMaterial=material;
                surfaces[i]=surface;
                lods[i]=new LOD(i==0?.16f:i==1?.045f:0,new Renderer[]{surface});
            }
            var group=root.AddComponent<LODGroup>();
            group.fadeMode=LODFadeMode.None;
            group.animateCrossFading=false;
            group.SetLODs(lods);
            group.localReferencePoint=levels[0].bounds.center;
            group.size=Mathf.Max(levels[0].bounds.size.x,levels[0].bounds.size.y);
            return surfaces;
        }
    }
}
