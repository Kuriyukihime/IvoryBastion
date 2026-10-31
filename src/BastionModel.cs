using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;
namespace IvoryBastion
{
    internal static class BastionModel
    {
        internal static readonly Color Blue=new Color(.16f,.58f,1f),White=new Color(.87f,.94f,1f),Black=new Color(.025f,.035f,.055f);
        private static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        private static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        internal static Material Material(string name,Color color,float glow=0,float smooth=.3f,string texture=null)
        {
            string materialKey=(texture??"plain")+":"+name;if(materials.TryGetValue(materialKey,out var m))return m;
            m=new Material(Shader.Find("Hopoo Games/Deferred/Standard")??Shader.Find("Standard")){name="Ivory_"+name,color=color};
            m.EnableKeyword("DITHER");
            if(m.HasProperty("_Fade"))m.SetFloat("_Fade",1);
            if(m.HasProperty("_FadeBias"))m.SetFloat("_FadeBias",0);
            if(m.HasProperty("_MainTex"))m.SetTexture("_MainTex",texture!=null?BastionContent.Icon(texture).texture:name=="Ceramic"||name=="Porcelain"?Surface("ceramic_surface"):name=="Frame"||name=="Joint"||name=="Edge"?Surface("alloy_surface"):Texture2D.whiteTexture);
            foreach(var keyword in new[]{"LIMBREMOVAL","FRESNEL_EMISSION","FLOWMAP","SPLATMAP","NORMALMAP","_NORMALMAP"})m.DisableKeyword(keyword);
            if(m.HasProperty("_NormalStrength"))m.SetFloat("_NormalStrength",0);
            if(m.HasProperty("_LimbRemovalOn"))m.SetFloat("_LimbRemovalOn",0);
            if(m.HasProperty("_RimStrength"))m.SetFloat("_RimStrength",name=="Reconstructed"?.20f:.14f);
            if(m.HasProperty("_RimPower"))m.SetFloat("_RimPower",3);
            if(m.HasProperty("_RimTint"))m.SetColor("_RimTint",new Color(.72f,.82f,1));
            Texture2D emission=name=="Reconstructed"&&texture!=null?BastionContent.Icon(texture.Replace("_surface","_emission")).texture:Texture2D.whiteTexture;
            if(m.HasProperty("_EmTex"))m.SetTexture("_EmTex",emission);
            if(m.HasProperty("_EmissionMap"))m.SetTexture("_EmissionMap",emission);
            if(m.HasProperty("_EmColor"))m.SetColor("_EmColor",color);
            if(m.HasProperty("_EmPower"))m.SetFloat("_EmPower",glow);
            if(m.HasProperty("_EmissionColor")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*glow);}
            foreach(var p in new[]{"_Smoothness","_Glossiness"})if(m.HasProperty(p))m.SetFloat(p,smooth);
            bool metal=name.EndsWith("Edge")||name.EndsWith("Joint");
            // The native shader defines albedo alpha as specular scale. The
            // authored atlas separates satin ceramics, metal and dark machinery
            // without turning the whole model glossy or using alpha as opacity.
            if(name=="Reconstructed")
            {
                m.DisableKeyword("FORCE_SPEC");m.DisableKeyword("CUTOUT");
                if(m.HasProperty("_ForceSpecOn"))m.SetFloat("_ForceSpecOn",0);
                if(m.HasProperty("_EnableCutout"))m.SetFloat("_EnableCutout",0);
            }
            float specular=name=="Glass"?.50f:metal?.32f:name=="Reconstructed"?.35f:.14f;
            if(m.HasProperty("_SpecularStrength"))m.SetFloat("_SpecularStrength",specular);
            if(m.HasProperty("_SpecularExponent"))m.SetFloat("_SpecularExponent",name=="Reconstructed"?36:smooth>.5f?36:24);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal?.45f:.02f);
            materials[materialKey]=m;return m;
        }
        private static Texture2D Surface(string name)
        {var t=BastionContent.Icon(name).texture;t.wrapMode=TextureWrapMode.Repeat;t.anisoLevel=4;return t;}
        private static Vector2[] SurfaceUV(Vector3[] vertices,Vector3[] normals)
        {
            var uv=new Vector2[vertices.Length];for(int i=0;i<uv.Length;i++){var v=vertices[i];var n=normals[i];float x=Mathf.Abs(n.x),y=Mathf.Abs(n.y),z=Mathf.Abs(n.z);uv[i]=(z>=x&&z>=y?new Vector2(v.x,v.y):x>=y?new Vector2(v.z,v.y):new Vector2(v.x,v.z))*3;}
            return uv;
        }
        private static Vector3 V(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        internal static Transform Rig(GameObject model,string asset,float scale,List<CharacterModel.RendererInfo> infos,out Transform[] bones)
        {
            var rig=new GameObject("Ivory_"+asset).transform;rig.SetParent(model.transform,false);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("IvoryBastion.assets."+asset+".meshbin"))
            using(var r=new BinaryReader(stream??throw new FileNotFoundException(asset)))
            {
                string format=new string(r.ReadChars(5));bool four=format=="BAST3",textured=four||format=="BAST2";if(!textured&&format!="BAST1")throw new InvalidDataException(asset);
                int n=r.ReadInt32();bones=new Transform[n];var anchors=new Vector3[n];var names=new string[n];
                for(int i=0;i<n;i++){names[i]=r.ReadString();anchors[i]=V(r);r.ReadInt32();}
                for(int i=0;i<n;i++)
                {
                    string name=names[i],parent=BastionChoreography.Parent(name);
                    int p=Array.IndexOf(names,parent);bones[i]=new GameObject(name).transform;bones[i].SetParent(p<0?rig:bones[p],false);bones[i].position=rig.TransformPoint(anchors[i]);bones[i].rotation=rig.rotation;
                }
                var bind=bones.Select(b=>b.worldToLocalMatrix*rig.localToWorldMatrix).ToArray();int parts=r.ReadInt32();var levels=new List<Mesh>();
                bool rigidSiege=four&&asset=="siege";
                for(int p=0;p<parts;p++)
                {
                    string name=r.ReadString();var color=new Color(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),1);float smooth=r.ReadSingle(),glow=r.ReadSingle();int nv=r.ReadInt32(),ni=r.ReadInt32();
                    string key=asset+":"+name;
                    // Every new spider/drone shares the cached immutable mesh.
                    // Skip its binary payload instead of allocating and decoding
                    // vertex/weight/index arrays that would immediately be discarded.
                    bool cached=meshes.TryGetValue(key,out var mesh)&&mesh;
                    bool decode=!cached||asset=="siege"; // Retain camera-fade preparation.
                    Vector3[] v=null,normals=null;Vector2[] uv=null;BoneWeight[] weights=null;int[] indices=null;
                    if(!decode)r.BaseStream.Seek(BastionMeshPayload.Bytes(nv,ni,textured,four),SeekOrigin.Current);
                    else
                    {
                    v=new Vector3[nv];normals=new Vector3[nv];uv=textured?new Vector2[nv]:null;weights=new BoneWeight[nv];indices=new int[ni];
                    for(int i=0;i<nv;i++)
                    {
                        v[i]=V(r);normals[i]=V(r);if(textured)uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                        int b0=r.ReadInt32(),b1=r.ReadInt32();
                        if(four)
                        {
                            int b2=r.ReadInt32(),b3=r.ReadInt32();weights[i]=new BoneWeight{boneIndex0=b0,boneIndex1=b1,boneIndex2=b2,boneIndex3=b3,weight0=r.ReadSingle(),weight1=r.ReadSingle(),weight2=r.ReadSingle(),weight3=r.ReadSingle()};
                            if(rigidSiege&&(weights[i].weight0!=1||weights[i].weight1!=0||weights[i].weight2!=0||weights[i].weight3!=0))rigidSiege=false;
                        }
                        else{float w=r.ReadSingle();weights[i]=new BoneWeight{boneIndex0=b0,boneIndex1=b1,weight0=w,weight1=1-w};}
                    }
                    for(int i=0;i<ni;i++)indices[i]=r.ReadInt32();
                    }
                    if(!cached){mesh=new Mesh{name="Ivory_"+asset+"_"+name,indexFormat=IndexFormat.UInt32,vertices=v,normals=normals,triangles=indices,boneWeights=weights,bindposes=bind};mesh.uv=uv??SurfaceUV(v,normals);mesh.RecalculateTangents();mesh.RecalculateBounds();meshes[key]=mesh;}
                    if(asset=="siege")BastionCameraFade.Prepare(mesh,v,weights,bind);
                    if(four){levels.Add(mesh);continue;}
                    var go=new GameObject(mesh.name);go.transform.SetParent(rig,false);var sr=go.AddComponent<SkinnedMeshRenderer>();sr.sharedMesh=mesh;sr.bones=bones;sr.rootBone=bones[0];sr.quality=SkinQuality.Bone2;
                    var bounds=mesh.bounds;bounds.Expand(asset=="siege"?1.6f:asset=="mobile"?1f:asset=="spider_mine"?1.2f:asset=="mine_launcher"?BastionMinePresentation.LauncherBoundsExpansion:.3f);sr.localBounds=bounds;sr.updateWhenOffscreen=false;sr.sharedMaterial=Material(name,color,glow,smooth,textured?asset+"_surface":null);
                    infos.Add(new CharacterModel.RendererInfo{renderer=sr,defaultMaterial=sr.sharedMaterial,defaultShadowCastingMode=ShadowCastingMode.On,ignoreOverlays=false});
                }
                if(four)
                {
                    if(levels.Count!=3)throw new InvalidDataException("Reconstructed Seraph requires three detail levels.");
                    var go=new GameObject("Ivory_"+asset+"_Reconstructed");go.transform.SetParent(rig,false);
                    var bounds=levels[0].bounds;bounds.Expand(asset=="siege"?1.6f:1f);
                    var surfaceMaterial=Material("Reconstructed",Color.white,1.35f,.30f,asset+"_surface");
                    foreach(var sr in BastionMeshDetail.Create(go,levels.ToArray(),bones,surfaceMaterial,bounds))
                    {
                        // Every siege vertex is rigid to one assembly. One-bone
                        // skinning is visually identical and avoids evaluating
                        // three zero-weight influences. Mobile keeps Bone4;
                        // any future blended siege mesh also keeps Bone4.
                        if(rigidSiege)sr.quality=SkinQuality.Bone1;
                        infos.Add(new CharacterModel.RendererInfo{renderer=sr,defaultMaterial=sr.sharedMaterial,defaultShadowCastingMode=ShadowCastingMode.On,ignoreOverlays=false});
                    }
                }
                if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing mesh data");
            }
            rig.localScale=Vector3.one*scale;return rig;
        }
        internal static void Build(GameObject model,CharacterBody owner=null)
        {
            if(model.GetComponent<BastionAnimation>())return;
            var old=model.GetComponentsInChildren<Renderer>(true);foreach(var r in old)r.enabled=false;foreach(var lod in model.GetComponentsInChildren<LODGroup>(true))lod.enabled=false;
            var infos=new List<CharacterModel.RendererInfo>();var mobile=Rig(model,"mobile",.78f,infos,out var mb);var siege=Rig(model,"siege",1.45f,infos,out var sb);siege.gameObject.SetActive(false);
            var mineRig=Rig(siege.gameObject,"mine_launcher",1,infos,out var mineBones);
            var mobileInfos=infos.Where(i=>i.renderer.transform.IsChildOf(mobile)).ToArray();
            var siegeInfos=infos.Where(i=>i.renderer.transform.IsChildOf(siege)).ToArray();
            var cm=model.GetComponent<CharacterModel>();if(cm){cm.baseRendererInfos=mobileInfos;cm.body=owner;cm.itemDisplayRuleSet=null;cm.DisableAllItemDisplays();}
            if(cm)model.AddComponent<BastionCameraFadeState>();
            var skins=model.GetComponent<ModelSkinController>();if(skins){var skin=ScriptableObject.CreateInstance<SkinDef>();skin.name="IvoryDefault";skin.nameToken="BASTION_NAME";skin.icon=BastionContent.Portrait;skin.rootObject=model;skin.baseSkins=Array.Empty<SkinDef>();skin.skinDefParams=ScriptableObject.CreateInstance<SkinDefParams>();skin.skinDefParams.rendererInfos=infos.ToArray();skin.skinDefParams.gameObjectActivations=Array.Empty<SkinDefParams.GameObjectActivation>();skin.skinDefParams.meshReplacements=Array.Empty<SkinDefParams.MeshReplacement>();skins.skins=new[]{skin};}
            foreach(var spec in new[]{(mobile,new Vector3(0,1.12f,.22f),1.6f),(mobile,new Vector3(0,.85f,-.34f),1.2f),(siege,new Vector3(0,.85f,1.4f),3f),(siege,new Vector3(0,1.25f,-.35f),2f)})
            {var g=new GameObject("Blue reactor light");g.transform.SetParent(spec.Item1,false);g.transform.localPosition=spec.Item2;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=Blue;l.intensity=1.3f;l.range=spec.Item3;l.shadows=LightShadows.None;}
            var a=model.AddComponent<BastionAnimation>();a.Owner=owner;a.Mobile=mobile;a.Siege=siege;a.MobileBones=mb;a.SiegeBones=sb;a.Original=old;
            a.MobileRenderers=mobileInfos;a.SiegeRenderers=siegeInfos;
            var launchers=model.AddComponent<BastionMineLaunchers>();launchers.Model=a;launchers.Bones=mineBones;
        }
    }
}
