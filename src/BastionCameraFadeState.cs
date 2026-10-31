using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace IvoryBastion
{
    internal sealed class BastionCameraFadeState:MonoBehaviour
    {
        private sealed class View
        {
            internal int Frame=-1;
            internal float Opacity=1;
        }
        private readonly Dictionary<CameraRigController,View> views=new Dictionary<CameraRigController,View>();
        private sealed class Rig
        {
            internal SkinnedMeshRenderer Renderer;
            internal Mesh Mesh;
            internal Transform Root;
            internal Transform[] Bones;
        }
        private CharacterModel model;
        private CharacterModel.RendererInfo[] rendererInfos;
        private Rig[] rigs=Array.Empty<Rig>();
        internal void Prepare(CharacterModel current)
        {
            if(ReferenceEquals(model,current)&&ReferenceEquals(rendererInfos,current.baseRendererInfos))return;
            model=current;rendererInfos=current.baseRendererInfos;
            rigs=new Rig[rendererInfos?.Length??0];
        }
        internal Transform[] Bones(int index,SkinnedMeshRenderer renderer)
        {
            // Unity's bones getter copies its array. Keep one per active rig,
            // and refresh when a skin/model replaces the renderer or skeleton.
            if(index<0||index>=rigs.Length)return renderer.bones;
            var rig=rigs[index];
            var mesh=renderer.sharedMesh;var root=renderer.rootBone;
            if(rig==null||!ReferenceEquals(rig.Renderer,renderer)||
                !ReferenceEquals(rig.Mesh,mesh)||!ReferenceEquals(rig.Root,root))
            {
                rig=new Rig{Renderer=renderer,Mesh=mesh,Root=root,Bones=renderer.bones};
                rigs[index]=rig;
            }
            return rig.Bones;
        }
        private void Clear()
        {model=null;rendererInfos=null;rigs=Array.Empty<Rig>();views.Clear();}
        private void OnDisable()=>Clear();
        private void OnDestroy()=>Clear();

        internal float Resolve(CameraRigController rig,float target)
        {
            if(!views.TryGetValue(rig,out var view)){view=new View();views.Add(rig,view);}
            int frame=Time.frameCount;
            if(view.Frame!=frame)
            {
                // Packing/death stops consulting this state. A later deployment
                // starts opaque rather than reusing an old, almost hidden view.
                if(view.Frame<frame-1)view.Opacity=1;
                view.Opacity=BastionCameraRules.Approach(view.Opacity,target,Time.unscaledDeltaTime);
                view.Frame=frame;
            }
            return view.Opacity;
        }
    }
}
