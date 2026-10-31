namespace UnityEngine
{
    class MonoBehaviour { }
    class Transform { }
    class Mesh { }
    class Renderer { }
    class SkinnedMeshRenderer:Renderer
    {
        internal Mesh sharedMesh=new();
        internal Transform rootBone=new();
        internal Transform[] Source=new[]{new Transform()};
        internal int Reads;
        internal Transform[] bones {get {Reads++;return (Transform[])Source.Clone();}}
    }
    static class Time {internal static int frameCount;internal static float unscaledDeltaTime;}
    struct Vector3
    {
        internal float x,y,z;
        internal Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        internal float sqrMagnitude=>x*x+y*y+z*z;
        internal float this[int i]=>i==0?x:i==1?y:z;
    }
}
namespace RoR2
{
    class CameraRigController { }
    class CharacterModel
    {
        internal struct RendererInfo {internal UnityEngine.Renderer renderer;}
        internal RendererInfo[] baseRendererInfos;
    }
}
