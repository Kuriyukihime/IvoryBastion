using System;
using System.Collections.Generic;
using MonoMod.Cil;
using RoR2;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionCameraFade
    {
        private struct PartBounds {internal int Bone;internal Vector3 Minimum,Maximum;}
        private static readonly Dictionary<Mesh,PartBounds[]> parts=new Dictionary<Mesh,PartBounds[]>();
        internal static void Prepare(Mesh mesh,Vector3[] vertices,BoneWeight[] weights,Matrix4x4[] bind)
        {
            if(parts.ContainsKey(mesh))return;
            var minimum=new Vector3[bind.Length];var maximum=new Vector3[bind.Length];var seen=new bool[bind.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                int bone=weights[i].boneIndex0;var point=bind[bone].MultiplyPoint3x4(vertices[i]);
                if(!seen[bone]){minimum[bone]=maximum[bone]=point;seen[bone]=true;}
                else{minimum[bone]=Vector3.Min(minimum[bone],point);maximum[bone]=Vector3.Max(maximum[bone],point);}
            }
            var result=new List<PartBounds>();
            for(int bone=0;bone<bind.Length;bone++)if(seen[bone])
                result.Add(new PartBounds{Bone=bone,Minimum=minimum[bone]-Vector3.one*.015f,Maximum=maximum[bone]+Vector3.one*.015f});
            parts.Add(mesh,result.ToArray());
        }
        internal static void Install()=>IL.RoR2.CharacterModel.UpdateForCamera+=Hook;
        internal static void Remove()=>IL.RoR2.CharacterModel.UpdateForCamera-=Hook;
        private static void Hook(ILContext context)=>BastionCameraHook.Apply(context,
            cursor=>cursor.EmitDelegate<Func<float,CharacterModel,CameraRigController,float>>(Fade));
        private static float Fade(float native,CharacterModel model,CameraRigController rig)
        {
            if(!model.body||!rig||rig.targetBody!=model.body||!rig.sceneCam)return native;
            var controller=model.body.GetComponent<BastionController>();
            if(!controller||!controller.Alive||!controller.Sieged)return native;
            var sceneCamera=rig.sceneCam;var camera=sceneCamera.transform;
            Vector3 center=camera.forward;
            Vector3 left=sceneCamera.ViewportPointToRay(new Vector3(.485f,.5f)).direction;
            Vector3 right=sceneCamera.ViewportPointToRay(new Vector3(.515f,.5f)).direction;
            Vector3 down=sceneCamera.ViewportPointToRay(new Vector3(.5f,.485f)).direction;
            Vector3 up=sceneCamera.ViewportPointToRay(new Vector3(.5f,.515f)).direction;
            float nearest=float.PositiveInfinity;int hits=0;
            var transition=model.GetComponent<BastionCameraFadeState>();
            if(transition)transition.Prepare(model);
            for(int rendererIndex=0;rendererIndex<model.baseRendererInfos.Length;rendererIndex++)
            {
                var surface=model.baseRendererInfos[rendererIndex].renderer as SkinnedMeshRenderer;
                if(!surface||!surface.gameObject.activeInHierarchy||!parts.TryGetValue(surface.sharedMesh,out var proxies))continue;
                var bones=transition?transition.Bones(rendererIndex,surface):surface.bones;
                foreach(var part in proxies)
                {
                    var bone=bones[part.Bone];
                    // The halo's center is empty; its enclosing box would hide
                    // the entire character whenever the player aims through it.
                    if(!bone||bone.name=="halo"||bone.name=="radar")continue;
                    var inverse=bone.worldToLocalMatrix;var origin=inverse.MultiplyPoint3x4(camera.position);
                    for(int probe=0;probe<5;probe++)
                    {
                        Vector3 direction=probe==0?center:probe==1?left:probe==2?right:probe==3?down:up;
                        if(!BastionCameraRules.RayBox(origin,inverse.MultiplyVector(direction),part.Minimum,part.Maximum,out float distance))continue;
                        hits|=1<<probe;nearest=Mathf.Min(nearest,distance);
                    }
                }
                // All permanent detail renderers share the same rig. One set
                // of tight part bounds is sufficient and avoids counting LODs.
                break;
            }
            float desired=BastionCameraRules.Opacity(true,hits!=0,nearest,center.y,BastionCameraRules.Coverage(hits));
            return Mathf.Min(native,transition?transition.Resolve(rig,desired):desired);
        }
    }
}
