using System;
using System.Reflection;
using IvoryBastion;
using RoR2;
using UnityEngine;

static class Checks
{
    static int count;
    static void Check(bool value,string message){count++;if(!value)throw new Exception("Camera cache: "+message);}
    static void Main()
    {
        var state=new BastionCameraFadeState();
        var a=new SkinnedMeshRenderer();var b=new SkinnedMeshRenderer();var c=new SkinnedMeshRenderer();
        var model=new CharacterModel{baseRendererInfos=new[]{
            new CharacterModel.RendererInfo{renderer=a},new CharacterModel.RendererInfo{renderer=b},new CharacterModel.RendererInfo{renderer=c}}};
        state.Prepare(model);
        var original=state.Bones(0,a);
        Check(ReferenceEquals(original[0],a.Source[0]),"cached arrays retain actual skeleton transforms");
        Check(a.Reads==1&&b.Reads==0&&c.Reads==0,"first valid LOD does not fetch unused detail skeletons");
        for(int i=0;i<20000;i++){state.Prepare(model);state.Bones(0,a);}
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++){state.Prepare(model);state.Bones(0,a);}
        long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(a.Reads==1,"repeated camera updates do not call the native array-copy getter");
        Check(bytes==0,"unchanged camera rig cache has no managed frame allocations: "+bytes);
        Check(ReferenceEquals(state.Bones(0,a),original),"cached transform array identity is stable");

        model.baseRendererInfos[0]=new(){renderer=b};
        Check(ReferenceEquals(state.Bones(0,b)[0],b.Source[0])&&b.Reads==1,"renderer replaced inside the same list refreshes skeleton");
        b.sharedMesh=new Mesh();b.Source=new[]{new Transform()};
        Check(ReferenceEquals(state.Bones(0,b)[0],b.Source[0])&&b.Reads==2,"new mesh refreshes skin bindings");
        b.rootBone=new Transform();b.Source=new[]{new Transform()};
        Check(ReferenceEquals(state.Bones(0,b)[0],b.Source[0])&&b.Reads==3,"new root skeleton refreshes bindings");
        model.baseRendererInfos=(CharacterModel.RendererInfo[])model.baseRendererInfos.Clone();
        state.Prepare(model);state.Bones(0,b);
        Check(b.Reads==4,"native renderer-list replacement invalidates existing bindings");
        var replacement=new CharacterModel{baseRendererInfos=model.baseRendererInfos};
        state.Prepare(replacement);state.Bones(0,b);
        Check(b.Reads==5,"model replacement invalidates even a shared renderer list");

        var cameraA=new CameraRigController();var cameraB=new CameraRigController();
        Time.frameCount=10;Time.unscaledDeltaTime=.05f;
        float faded=state.Resolve(cameraA,.08f);
        Check(Math.Abs(faded-.4f)<.0001f,"original fade approach rate is retained");
        Check(state.Resolve(cameraA,1)==faded,"same-frame repeat cannot advance transition");
        Check(state.Resolve(cameraB,1)==1,"camera opacity remains independent");
        Time.frameCount=20;
        Check(Math.Abs(state.Resolve(cameraA,.08f)-faded)<.0001f,"returning to siege starts from opaque as before");

        foreach(string lifecycle in new[]{"OnDisable","OnDestroy"})
        {
            typeof(BastionCameraFadeState).GetMethod(lifecycle,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(state,null);
            var rigs=(Array)typeof(BastionCameraFadeState).GetField("rigs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(state);
            Check(rigs.Length==0,"model "+lifecycle+" releases rig references");
            Check(typeof(BastionCameraFadeState).GetField("model",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(state)==null,"model "+lifecycle+" clears owner reference");
            Check(state.Resolve(cameraA,1)==1,"model "+lifecycle+" clears old view opacity");
            state.Prepare(replacement);state.Bones(0,b);
        }
        Check(b.Reads==7,"disabled/revived model fetches fresh skeleton once per enable lifecycle");
        Console.WriteLine($"PASS: {count} production camera-state cache/invalidation/opacity checks; 10,000 unchanged updates allocated {bytes} managed bytes (native getter stand-in).");
    }
}
