using System;
using RoR2;
using UnityEngine;
namespace IvoryBastion
{
    // Cosmetic only. The host retains the body-local anchor for damage and
    // replication; each client projects that anchor onto its visible skin.
    // Bake once on attachment, then follow a real animation bone without
    // baking meshes or allocating vertex arrays every rendered frame.
    internal sealed class BastionMineSurfaceAttachment
    {
        private Transform joint;private Vector3 point,normal;
        internal bool Bound=>joint;
        internal void Clear(){joint=null;}
        internal bool Follow(out Vector3 position,out Vector3 outward)
        {
            position=outward=Vector3.zero;if(!joint)return false;
            position=joint.TransformPoint(point);outward=joint.TransformDirection(normal).normalized;
            return Finite(position)&&Finite(outward)&&outward.sqrMagnitude>.5f;
        }
        internal bool Bind(CharacterBody body,Vector3 seed)
        {
            Clear();if(!body||!Finite(seed))return false;
            var models=body.GetComponentsInChildren<CharacterModel>();
            float best=Mathf.Clamp(body.radius*.8f,.75f,3);best*=best;
            // Only base character renderers: item displays, temporary shields
            // and weapon-effect meshes must never become a mounting surface.
            foreach(var model in models)
            {
                if(model.baseRendererInfos==null)continue;
                foreach(var info in model.baseRendererInfos)
                {
                    var renderer=info.renderer;if(!renderer||!renderer.enabled)continue;
                    Mesh baked=null;
                    try
                    {
                        var skin=renderer as SkinnedMeshRenderer;Mesh mesh;
                        if(skin)
                        {
                            if(!skin.sharedMesh)continue;
                            baked=new Mesh{name="Widow temporary contact projection"};skin.BakeMesh(baked);mesh=baked;
                        }
                        else
                        {var filter=renderer.GetComponent<MeshFilter>();mesh=filter?filter.sharedMesh:null;}
                        if(!mesh||!mesh.isReadable)continue;
                        var vertices=mesh.vertices;var triangles=mesh.triangles;
                        for(int i=0;i<vertices.Length;i++)vertices[i]=renderer.transform.TransformPoint(vertices[i]);
                        int va=-1,vb=0,vc=0;Vector3 contact=default,face=default,blend=default;
                        for(int i=0;i+2<triangles.Length;i+=3)
                        {
                            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                            if(a<0||b<0||c<0||a>=vertices.Length||b>=vertices.Length||c>=vertices.Length)continue;
                            var n=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                            if(!Finite(n)||n.sqrMagnitude<.00000001f)continue;
                            var p=Closest(seed,vertices[a],vertices[b],vertices[c],out var bary);
                            float distance=(seed-p).sqrMagnitude;if(!Finite(p)||distance>=best)continue;
                            // A hurtbox can sit inside protruding armor. Face
                            // away from the body rather than toward that seed,
                            // otherwise a perfectly valid contact turns inward.
                            n=n.normalized;var outside=p-body.corePosition;if(outside.sqrMagnitude<.0001f)outside=seed-p;
                            if(Vector3.Dot(n,outside)<0)n=-n;
                            best=distance;va=a;vb=b;vc=c;contact=p;face=n;blend=bary;
                        }
                        if(va<0)continue;
                        var mount=skin?Joint(skin,va,vb,vc,blend,contact):renderer.transform;
                        if(!mount)continue;
                        joint=mount;point=mount.InverseTransformPoint(contact+face*BastionMineRules.ShellClearance);
                        normal=mount.InverseTransformDirection(face).normalized;
                    }
                    // An unreadable/custom renderer must not break its owner's
                    // VFX update. Other skin parts and the host anchor remain.
                    catch(Exception){}
                    finally{if(baked)UnityEngine.Object.Destroy(baked);}
                }
            }
            return Bound;
        }
        private static Transform Joint(SkinnedMeshRenderer skin,int a,int b,int c,Vector3 bary,Vector3 p)
        {
            var bones=skin.bones;if(bones==null||bones.Length==0)return skin.transform;
            // Read weights once for the winning triangle, not per candidate.
            // Some imported skins discard CPU weights; nearest real joint is
            // the safe fallback, while BakeMesh still gives an exact contact.
            BoneWeight[] weights=null;
            if(skin.sharedMesh.isReadable)weights=skin.sharedMesh.boneWeights;
            if(weights!=null&&a<weights.Length&&b<weights.Length&&c<weights.Length)
            {
                var indices=new int[12];var strengths=new float[12];int used=0;
                Add(weights[a],bary.x,indices,strengths,ref used);Add(weights[b],bary.y,indices,strengths,ref used);Add(weights[c],bary.z,indices,strengths,ref used);
                int winner=-1;float strength=0;
                for(int i=0;i<used;i++)if(indices[i]>=0&&indices[i]<bones.Length&&bones[indices[i]]&&strengths[i]>strength){winner=indices[i];strength=strengths[i];}
                if(winner>=0)return bones[winner];
            }
            Transform closest=skin.rootBone?skin.rootBone:skin.transform;float distance=float.PositiveInfinity;
            foreach(var bone in bones)if(bone){float d=(bone.position-p).sqrMagnitude;if(d<distance){distance=d;closest=bone;}}
            return closest;
        }
        private static void Add(BoneWeight w,float scale,int[] indices,float[] strengths,ref int used)
        {Add(w.boneIndex0,w.weight0*scale,indices,strengths,ref used);Add(w.boneIndex1,w.weight1*scale,indices,strengths,ref used);Add(w.boneIndex2,w.weight2*scale,indices,strengths,ref used);Add(w.boneIndex3,w.weight3*scale,indices,strengths,ref used);}
        private static void Add(int index,float strength,int[] indices,float[] strengths,ref int used)
        {
            if(strength<=0)return;
            for(int i=0;i<used;i++)if(indices[i]==index){strengths[i]+=strength;return;}
            indices[used]=index;strengths[used]=strength;used++;
        }
        private static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
        // Closest point on a nondegenerate triangle, including its edges.
        internal static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c,out Vector3 bary)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0){bary=new Vector3(1,0,0);return a;}
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);
            if(d3>=0&&d4<=d3){bary=new Vector3(0,1,0);return b;}
            float vc=d1*d4-d3*d2;
            if(vc<=0&&d1>=0&&d3<=0){float v=d1/(d1-d3);bary=new Vector3(1-v,v,0);return a+ab*v;}
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);
            if(d6>=0&&d5<=d6){bary=new Vector3(0,0,1);return c;}
            float vb=d5*d2-d1*d6;
            if(vb<=0&&d2>=0&&d6<=0){float w=d2/(d2-d6);bary=new Vector3(1-w,0,w);return a+ac*w;}
            float va=d3*d6-d5*d4;
            if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0){float w=(d4-d3)/((d4-d3)+(d5-d6));bary=new Vector3(0,1-w,w);return b+(c-b)*w;}
            float sum=va+vb+vc;if(Mathf.Abs(sum)<.00000001f){bary=new Vector3(1,0,0);return a;}
            float inverse=1/sum;bary=new Vector3(va*inverse,vb*inverse,vc*inverse);return a*bary.x+b*bary.y+c*bary.z;
        }
    }
}
