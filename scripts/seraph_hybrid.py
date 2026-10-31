"""Seraph production geometry fitted to the approved reconstruction anchors.

The reconstruction supplies stable proportions and rig positions. Manufactured
ceramic shells, a closed undersuit, and mechanisms are authored explicitly so
their silhouettes and seams stay crisp. No photographed lighting is projected
onto surfaces. Independent assemblies retain their own articulation.
"""
from pathlib import Path
import argparse
import seraph_finish as finish
import json
import math
import struct
import hashlib
import numpy as np
import trimesh
import pymeshlab
import xatlas
from PIL import Image
from scipy.ndimage import distance_transform_edt
from scipy.sparse import coo_matrix, diags
from scipy.sparse.csgraph import connected_components
from numba import njit
import mesh_tools as m
import seraph_models as original
import seraph_refinement as d
import seraph_premium as premium
import seraph_siege_refit as siege_refit
from seraph_components import source
from import_seraph_reconstruction import simplify, unit_normals, fit_bones

ROOT = Path(__file__).resolve().parents[1]
PALETTE = original.PALETTE


def mobile_bones():
    # Fit the reconstruction once, then give the new guns and wings exact ports.
    body = source("body", 1.67)
    proxy = body.copy()
    proxy.metadata["reference_components"] = [("body", body)]
    proxy.metadata["piece_ids"] = np.zeros(len(body.vertices), np.int32)
    # fit_bones also samples wing tips; supply isolated marker points for it.
    markers = np.array([[-.48, 1.79, -.14], [-.49, .91, -.14],
                        [.48, 1.79, -.14], [.49, .91, -.14]])
    proxy.vertices = np.concatenate([body.vertices, markers])
    proxy.metadata["piece_ids"] = np.r_[proxy.metadata["piece_ids"], [1, 2, 3, 4]]
    bones = fit_bones("mobile", proxy)
    values = dict(bones)
    for side, sign in [("l", -1), ("r", 1)]:
        values["wing_" + side] = np.array([sign * .155, 1.36, -.065])
        values["vane_" + side] = np.array([sign * .15, 1.22, -.08])
        values["muzzle_" + side] = values["fore_" + side] + np.array([sign * .40, -.916, 0]) * .33 + [0, 0, .045]
    return [(name, values[name]) for name, _ in bones], body


def capture_geometry(callback, bones=None):
    """Collect the exact authoring parts without writing any shipped asset."""
    write, render = m.write_asset, m.render
    try:
        m.write_asset = lambda *a, **k: None
        m.render = lambda *a, **k: None
        if bones is not None:
            original.setup(bones)
        callback()
        parts = []
        for material, p in m.PARTS.items():
            if not p["v"]:
                continue
            mesh = trimesh.Trimesh(np.asarray(p["v"]), np.asarray(p["t"]), process=False)
            ids = np.array([weights[0][0] for weights in p["w"]], np.int32)
            parts.append(dict(mesh=mesh, material=material, bone=ids))
        return [(n, np.asarray(a)) for n, a, _ in m.BONES], parts
    finally:
        m.write_asset, m.render = write, render


def wing(bone, sign, upper):
    hinge = np.array([sign * .155, 1.36, -.065] if upper else [sign * .15, 1.22, -.08])
    tip = np.array([sign * .48, 1.79, -.14] if upper else [sign * .49, .91, -.14])
    start = hinge + [sign * .055, .055 if upper else -.04, -.015]
    direction = tip - start
    pts = np.array([start, start + direction * .24, start + direction * .62, tip])
    widths = [.016, .087 if upper else .081, .064, .0004]
    depths = [.017, .031, .025, .0004]
    d.rod("Frame", hinge, start, .021, bone, 24)
    d.joint(hinge, .031, bone, (0, 0, 1))
    d.disc("Blue", hinge + [0, 0, .032], .011, .003, bone, sides=32)
    d.profile("Porcelain", pts, list(zip(widths, depths)), bone, sides=48, steps=14)
    # Recess, luminous lens, and pale inner edge are modeled on both faces.
    for face in [-1, 1]:
        offset = np.array([0, 0, face * .026])
        lens = np.array([start + direction * t + offset for t in [.23, .40, .67, .91]])
        d.profile("Frame", lens, [(w, .007) for w in [.008, .038, .025, .0004]], bone, sides=32, steps=10)
        lens[:, 2] += face * .006
        d.profile("Blue", lens, [(w, .003) for w in [.003, .022, .014, .0004]], bone, sides=32, steps=10)
        lens[:, 2] += face * .003
        d.line("Core", lens[1:3], .0021, bone)
    for t in [.12, .24]:
        point = start + direction * t
        d.bolt(point + [0, 0, .031], .006, bone)
    premium.wing_detail(bone,sign,upper)


def gauntlet(side, sign, bones):
    finish.gauntlet(side, sign, bones)


def body_colors(v, n):
    """The retained reconstructed torso is the flexible black undersuit."""
    return np.tile(np.array(PALETTE["Frame"][0]), (len(v), 1))


def body_skin(v,tri,bones):
    """Weights for the retained anatomy, without looking for removed gun tips."""
    names={name:i for i,(name,_) in enumerate(bones)}
    anchors=dict(bones)
    x,y,z=v.T
    labels=np.full(len(v),names["root"],np.int32)
    labels[y>1.04]=names["torso"]
    labels[(y>anchors["head"][1])&(np.abs(x)<.145)]=names["head"]
    for side,sign in [("l",-1),("r",1)]:
        leg=(x*sign>0)&(y<1.04)
        labels[leg]=names["leg_"+side]
        labels[leg&(y<anchors["boot_"+side][1])]=names["boot_"+side]
        inner=.140+np.maximum(0,1.35-y)*.30
        arm=(x*sign>inner)&(y>1.125)&(y<1.40)
        labels[arm]=names["arm_"+side]
    edges=np.concatenate([tri[:,[0,1]],tri[:,[1,2]],tri[:,[2,0]]])
    edges=np.unique(np.sort(edges,axis=1),axis=0)
    edges=edges[edges[:,0]!=edges[:,1]]
    edges=np.concatenate([edges,edges[:,::-1]])
    values=(1/np.maximum(1e-6,np.linalg.norm(v[edges[:,0]]-v[edges[:,1]],axis=1))).astype(np.float32)
    graph=coo_matrix((values,(edges[:,0],edges[:,1])),shape=(len(v),len(v))).tocsr()
    graph=(diags(1/np.maximum(1,np.asarray(graph.sum(axis=1)).ravel()))@graph).astype(np.float32)
    w=np.zeros((len(v),len(bones)),np.float32)
    w[np.arange(len(v)),labels]=1
    # A broad physical transition at the armpit is needed for overhead aiming;
    # narrow surface-only transitions can still stretch its tiny seam triangles.
    for _ in range(max(40,round(900*np.sqrt(len(v)/160000)))):
        w=.5*w+.5*(graph@w)
    ids=np.argsort(w,axis=1)[:,-4:][:,::-1]
    weights=np.take_along_axis(w,ids,axis=1)
    weights/=np.maximum(weights.sum(axis=1)[:,None],1e-9)
    return ids.astype(np.int32),weights.astype(np.float32)


def body_geometry(body, bones, count):
    source_key=hashlib.sha256(
        np.asarray(body.vertices,np.float32).tobytes()
        +np.asarray([anchor for _,anchor in bones],np.float32).tobytes()
    ).hexdigest()[:12]
    cache = ROOT / f".build/seraph-clean-body-v7-{source_key}-{count}.npz"
    if cache.exists():
        cached = dict(np.load(cache))
        if "rig_version" not in cached or int(cached["rig_version"])!=4:
            cached["b"],cached["w"]=body_skin(cached["v"],cached["t"],bones)
            cached["rig_version"]=np.array(4)
            np.savez_compressed(cache,**cached)
        cached.pop("rig_version",None)
        cached["color"] = body_colors(cached["v"], cached["n"])
        cached["anatomy"] = np.ones(len(cached["v"]), np.float32)
        return cached
    mesh = simplify(body, count)
    # Remove the reconstructed weapons; their bent edges and ambiguous wrist
    # topology caused both neutral distortion and aim-pose stretching.
    v = np.asarray(mesh.vertices)
    tri = np.asarray(mesh.faces)
    edges=np.concatenate([tri[:,[0,1]],tri[:,[1,2]],tri[:,[2,0]]])
    remove=np.zeros(len(v),bool)
    for sign in [-1,1]:
        # A spatial crop alone also sliced the nearby flared hip plates.
        # Follow the connected arm inside a restricted region first.
        roi=(v[:,0]*sign>.10)&(v[:,1]>.60)&(v[:,1]<1.24)
        selected=edges[roi[edges].all(axis=1)]
        graph=coo_matrix((np.ones(len(selected)),(selected[:,0],selected[:,1])),shape=(len(v),len(v)))
        _,labels=connected_components(graph,directed=False)
        candidates=np.flatnonzero(roi)
        seed=candidates[np.argmin(np.linalg.norm(v[candidates]-[sign*.40,.77,.035],axis=1))]
        remove|=roi&(labels==labels[seed])&(v[:,1]<1.137)
    mesh = mesh.submesh([np.flatnonzero(~remove[mesh.faces].any(axis=1))], append=True, repair=False)
    trimesh.smoothing.filter_taubin(mesh, lamb=.45, nu=.47, iterations=12)
    mesh = premium.sculpt_body(mesh)
    v, tri = np.asarray(mesh.vertices), np.asarray(mesh.faces)
    n = unit_normals(v, np.asarray(mesh.vertex_normals))
    indices, weights = body_skin(v, tri, bones)
    result = dict(v=v, n=n, t=tri, b=indices, w=weights)
    np.savez_compressed(cache, **result, rig_version=np.array(4))
    return dict(result, color=body_colors(v, n), anatomy=np.ones(len(v), np.float32))


def mobile_details(bones):
    premium.core()
    premium.torso_and_helmet()
    premium.pelvis()
    for side, sign in [("l", -1), ("r", 1)]:
        wing("wing_" + side, sign, True)
        wing("vane_" + side, sign, False)
        gauntlet(side, sign, bones)
        premium.limbs(side,sign,bones)
        premium.greaves(side,sign)
        # A mechanical back spine makes every wing root visibly attached.
        d.box("Frame", (sign*.105, 1.30, -.076), (.065, .22, .052), "torso", bevel=.008)
        d.rod("Joint", (sign*.105, 1.34, -.084), (sign*.155, 1.36, -.065), .021, "torso", 24)
        d.rod("Joint", (sign*.105, 1.24, -.084), (sign*.15, 1.22, -.08), .019, "torso", 24)
        d.box("Blue", (sign*.105, 1.29, -.108), (.015, .125, .006), "torso", bevel=.002)
    finish.mobile()


def pack_parts(parts, bones, ratio=1):
    """Simplify independently per material/bone to preserve moving assemblies."""
    output = []
    for p in parts:
        mesh, material, ids = p["mesh"], p["material"], p["bone"]
        for bone in np.unique(ids):
            selected = np.flatnonzero((ids[mesh.faces] == bone).all(axis=1))
            if not len(selected):
                continue
            piece = mesh.submesh([selected], append=True, repair=False)
            # Small screws and planar panels are already cheap and stay crisp.
            target = max(24, int(len(piece.faces) * ratio))
            if ratio < 1 and len(piece.faces) > 96:
                ms=pymeshlab.MeshSet()
                ms.add_mesh(pymeshlab.Mesh(vertex_matrix=np.asarray(piece.vertices),
                                           face_matrix=np.asarray(piece.faces)))
                # Hard-face meshes have deliberately split edge normals. Keeping
                # every open edge would preserve almost every tiny bevel at LOD2.
                ms.apply_filter("meshing_decimation_quadric_edge_collapse",
                                targetfacenum=target, preservenormal=True,
                                preserveboundary=False, optimalplacement=True,
                                planarquadric=True)
                reduced=ms.current_mesh()
                piece=trimesh.Trimesh(reduced.vertex_matrix(),reduced.face_matrix(),process=False)
            v = np.asarray(piece.vertices)
            indices = np.zeros((len(v), 4), np.int32)
            indices[:, 0] = bone
            weights = np.zeros((len(v), 4), np.float32)
            weights[:, 0] = 1
            output.append(dict(v=v, n=unit_normals(v, piece.vertex_normals), t=np.asarray(piece.faces),
                               b=indices, w=weights, color=np.tile(PALETTE[material][0], (len(v), 1)),
                               anatomy=np.zeros(len(v), np.float32)))
    return output


def merge(pieces):
    offsets = np.r_[0, np.cumsum([len(p["v"]) for p in pieces])]
    out = {key: np.concatenate([p[key] for p in pieces]) for key in ["v", "n", "b", "w", "color", "anatomy"]}
    out["t"] = np.concatenate([p["t"] + offset for p, offset in zip(pieces, offsets)])
    return out


def build_mobile():
    bones, _ = mobile_bones()
    _, parts = capture_geometry(lambda: mobile_details(bones), bones)
    levels = []
    for ratio in [1, .30, .08]:
        print(f"mobile: manufactured ceramic assemblies {ratio}", flush=True)
        levels.append(merge(pack_parts(parts, bones, ratio)))
    return bones, levels


def siege_transform(name, points):
    """Fit the clean assemblies to the broader, lower Seraph fortress."""
    points = np.asarray(points)
    if name.startswith("hatch_"):
        name = ["rack_l", "rack_r", "shield_l", "shield_r"][int(name.split("_")[1])]
    if name.startswith("vent_"):
        name = "cannon"
    if name == "cannon":
        return (points - [0,.94,.42]) * [1.15,1.15,1.02] + [0,1.02,.77]
    if name in ["rack_l", "rack_r", "shield_l", "shield_r"]:
        sign = -1 if name.endswith("_l") else 1
        rear = name.startswith("shield")
        return (points - [sign*.8,.47,-.59 if rear else .28]) * [1.17,.88,1.18] + [sign*1.01,.51,-.81 if rear else .52]
    if name in ["halo", "radar"]:
        return (points - [0,1.55,-.65]) * .94 + [0,1.98,-.70]
    if name in ["torso", "head"] or name.startswith(("arm_", "fore_", "hand_")):
        return (points - [0,1.08,0]) * .78 + siege_refit.PILOT_CHEST
    return points * [1.24,1.05,1.24]


def siege_extras():
    premium.siege_detail(siege_transform)
    finish.siege()
    # Lower skirt has restrained raised ribs and service recesses, not a
    # photo-painted suggestion of paneling.
    for i in range(12):
        angle=i*math.tau/12
        radial=np.array([math.sin(angle),0,math.cos(angle)])
        tangent=np.array([math.cos(angle),0,-math.sin(angle)])
        for sign in [-1,1]:
            at=radial*1.20+tangent*sign*.18+[0,.51,0]
            d.box("Joint",at,(.026,.18,.035),u=tangent,bevel=.004)
            d.bolt(at+radial*.024+[0,.057,0],.009,normal=radial)

def retain_siege_parts(parts, names, remove_cockpit=False, replace_hatches=False):
    retained=[]
    for part in parts:
        mesh=part['mesh'];vertices=np.asarray(mesh.vertices);faces=np.asarray(mesh.faces)
        ids=part['bone'];face_bone=ids[faces[:,0]]
        keep=np.ones(len(faces),bool)
        for bone in np.unique(face_bone):
            name=names[bone]
            if name=='cannon' or name.startswith('vent_') or (replace_hatches and name.startswith('hatch_')):keep[face_bone==bone]=False
        if remove_cockpit:
            center=vertices[faces].mean(axis=1)
            cockpit=(face_bone==names.index('root'))&(np.abs(center[:,0])<.49)&(center[:,1]>.74)&(center[:,2]>-.70)&(center[:,2]<.56)
            keep&=~cockpit
        if keep.any():
            part=dict(part)
            part['mesh']=trimesh.Trimesh(vertices,faces[keep],process=False)
            retained.append(part)
    return retained


def build_siege():
    old_bones, parts = capture_geometry(original.siege)
    names = [n for n, _ in old_bones]
    bones = [(name, siege_transform(name, point)) for name, point in old_bones]
    for part in parts:
        v = np.asarray(part["mesh"].vertices).copy()
        for bone in np.unique(part["bone"]):
            selection = part["bone"] == bone
            v[selection] = siege_transform(names[bone], v[selection])
        part["mesh"].vertices = v
    parts=retain_siege_parts(parts,names,remove_cockpit=True,replace_hatches=True)
    bones.append(("cannon_muzzle", siege_transform("cannon", np.array([0,.94,2.25]))))
    bones.append(('throne',siege_refit.THRONE_ANCHOR))
    bones.extend(siege_refit.skyfall_bones())
    _,extra=capture_geometry(siege_extras,bones)
    parts+=retain_siege_parts(extra,[n for n,_ in bones])
    _,refit=capture_geometry(siege_refit.draw,bones)
    parts+=refit
    _,hatches=capture_geometry(lambda: finish.missile_hatches(siege_transform),bones)
    parts+=hatches
    _,hinges=capture_geometry(finish.cooling_hinges,bones)
    parts+=hinges
    levels = []
    for ratio in [1, .32, .09]:
        print(f"siege: separate rigid assemblies {ratio}", flush=True)
        levels.append(merge(pack_parts(parts, bones, ratio)))
    return bones, levels


@njit(cache=True)
def raster_color(uv, faces, values, target, mask):
    h, w, _ = target.shape
    for face in faces:
        a, b, c = uv[face] * np.array([w-1, h-1])
        den = (b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den) < 1e-8:
            continue
        for y in range(max(0,int(min(a[1],b[1],c[1]))), min(h,int(max(a[1],b[1],c[1]))+2)):
            for x in range(max(0,int(min(a[0],b[0],c[0]))), min(w,int(max(a[0],b[0],c[0]))+2)):
                u=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/den
                v=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/den
                q=1-u-v
                if min(u,v,q) >= -.001:
                    target[y,x] = u*values[face[0]]+v*values[face[1]]+q*values[face[2]]
                    mask[y,x] = True


def bake(form, levels):
    fingerprint = hashlib.sha256()
    for p in levels:
        for key in ["v", "t"]:
            fingerprint.update(p[key].tobytes())
    cache = ROOT / f".build/seraph-hybrid-{form}-{fingerprint.hexdigest()[:12]}.npz"
    if cache.exists():
        data = np.load(cache)
        mapped = [(data[f"m{i}"],data[f"t{i}"],data[f"u{i}"]) for i in range(3)]
        width, height = map(int, data["size"])
    else:
        atlas = xatlas.Atlas()
        for p in levels:
            atlas.add_mesh(np.asarray(p["v"],np.float32),np.asarray(p["t"],np.uint32),np.asarray(p["n"],np.float32))
        pack, chart = xatlas.PackOptions(), xatlas.ChartOptions()
        pack.resolution, pack.padding, pack.bilinear = 4096, 6, True
        chart.max_iterations = 1
        print(form+": unwrapping clean surfaces", flush=True)
        atlas.generate(chart,pack)
        assert atlas.atlas_count == 1
        mapped = [atlas[i] for i in range(3)]
        width,height=atlas.width,atlas.height
        data={"size":np.array([width,height])}
        for i,(mapping,tri,uv) in enumerate(mapped):
            data.update({f"m{i}":mapping,f"t{i}":tri,f"u{i}":uv})
        np.savez_compressed(cache,**data)
    ratio=min(1,4096/max(width,height))
    width,height=round(width*ratio),round(height*ratio)
    target=np.zeros((height,width,3),np.float32)
    specular=np.zeros_like(target)
    mask=np.zeros((height,width),bool)
    anatomy_positions=np.zeros_like(target)
    anatomy_mask=np.zeros_like(mask)
    lighting=np.zeros_like(target) if form=="mobile" else None
    for p,(mapping,tri,uv) in zip(levels,mapped):
        # Restrained crevice shading; no baked directional lighting or specular.
        ms=pymeshlab.MeshSet()
        ms.add_mesh(pymeshlab.Mesh(vertex_matrix=p["v"],face_matrix=p["t"]))
        ms.compute_scalar_ambient_occlusion(rays=32)
        ao=np.clip(ms.current_mesh().vertex_scalar_array(),0,1)
        # Clean ceramic faces keep their value in dark stages; exposed machinery
        # receives stronger crevice shading for readable separation and depth.
        luminance=p["color"].mean(axis=1)
        base=np.where(luminance>.55,.84,.62)
        colors=p["color"]*(base+(1-base)*ao)[:,None]
        emission=(p["color"][:,2]-p["color"][:,0])>.35
        colors[emission]=p["color"][emission]
        raster_color(uv,tri,colors[mapping].astype(np.float32),target,mask)
        # Installed HGStandard explicitly defines main-texture alpha as
        # Specular Scale. Keep RGB artwork independent of that material mask.
        spec_by_material={'Porcelain':.52,'Ceramic':.42,'Edge':.85,'Joint':.64,
                          'Frame':.12,'Ink':.08,'Glass':1.0,'Blue':.68,'Core':.75}
        names=list(PALETTE)
        palette=np.array([PALETTE[name][0] for name in names])
        ids=((p['color'][:,None,:]-palette[None,:,:])**2).sum(axis=2).argmin(axis=1)
        values=np.array([spec_by_material.get(names[i],.35) for i in ids],np.float32)
        raster_color(uv,tri,np.repeat(values[mapping,None],3,axis=1),specular,mask)
        if np.any(p["anatomy"]):
            body_faces=tri[(p["anatomy"][mapping][tri]>.5).all(axis=1)]
            raster_color(uv,body_faces,p["v"][mapping].astype(np.float32),anatomy_positions,anatomy_mask)
            light=np.repeat((.72+.28*ao[mapping])[:,None],3,axis=1).astype(np.float32)
            raster_color(uv,body_faces,light,lighting,anatomy_mask)
    # Material borders are evaluated per texel, not interpolated from irregular
    # reconstruction vertices. That keeps the visor and panel seams smooth.
    for row in range(0,height,64):
        active=anatomy_mask[row:row+64]
        if active.any():
            color=body_colors(anatomy_positions[row:row+64][active],None)
            color*=lighting[row:row+64][active]
            target[row:row+64][active]=color
    _,nearest=distance_transform_edt(~mask,return_indices=True)
    linear=np.clip(target[nearest[0],nearest[1]][::-1],0,1)
    # Palette values are linear material colors. PNG albedo is sampled as sRGB
    # by Unity; encoding it avoids crushed black structure and dark royal blue.
    srgb=np.where(linear<=.0031308,linear*12.92,1.055*np.power(linear,1/2.4)-.055)
    rgb=np.uint8(srgb*255)
    alpha=np.uint8(np.clip(specular[nearest[0],nearest[1]][::-1,:,0],0,1)*255)
    albedo=Image.fromarray(np.dstack([rgb,alpha]),'RGBA')
    albedo.save(ROOT/f"assets/{form}_surface.png")
    strength=np.clip((rgb[:,:,2].astype(float)-rgb[:,:,0]*1.15-30)/100,0,1)
    Image.fromarray(np.uint8(rgb*strength[:,:,None])).save(ROOT/f"assets/{form}_emission.png")
    return mapped,albedo


def export(form,bones,levels):
    mapped,albedo=bake(form,levels)
    def string(f,value):
        raw=value.encode()
        assert len(raw)<128
        f.write(bytes([len(raw)]));f.write(raw)
    target=ROOT/f"assets/{form}.meshbin"
    stats=[]
    with target.with_suffix(".meshbin.tmp").open("wb") as f:
        f.write(b"BAST3");f.write(struct.pack("<i",len(bones)))
        for name,anchor in bones:
            string(f,name);f.write(struct.pack("<3fi",*anchor,-1))
        f.write(struct.pack("<i",3))
        for i,(p,(mapping,tri,uv)) in enumerate(zip(levels,mapped)):
            dtype=np.dtype([("v","<f4",3),("n","<f4",3),("uv","<f4",2),("b","<i4",4),("w","<f4",4)])
            data=np.empty(len(mapping),dtype=dtype)
            for key in ["v","n","b","w"]:
                data[key]=p[key][mapping]
            data["uv"]=uv
            string(f,"Seraph_LOD"+str(i))
            f.write(struct.pack("<5f2i",1,1,1,.30,1.35,len(data),tri.size))
            f.write(data.tobytes());f.write(np.asarray(tri,"<i4").tobytes())
            stats.append(dict(lod=i,vertices=len(data),triangles=len(tri)))
            if i==0:
                mesh=trimesh.Trimesh(data["v"],tri,vertex_normals=data["n"],process=False)
                # Alpha stores native specular scale, never transparency.
                # External glTF viewers must treat this editable model as opaque.
                material=trimesh.visual.material.PBRMaterial(baseColorTexture=albedo,
                    metallicFactor=.02,roughnessFactor=.65,alphaMode='OPAQUE')
                mesh.visual=trimesh.visual.TextureVisuals(uv=uv,material=material)
                mesh.export(ROOT/f"dist/IvoryBastion-Seraph-{form}-model.glb")
    target.with_suffix(".meshbin.tmp").replace(target)
    info=dict(format="BAST3",authoring="scripts/seraph_hybrid.py",bones=len(bones),lods=stats,
              bounds=[levels[0]["v"].min(0).tolist(),levels[0]["v"].max(0).tolist()],texture=list(albedo.size),
              source="Seraph reconstruction-fitted rig with authored ceramic armor, closed mechanical undersuit and articulated engineering assemblies",
              shell_authoring="scripts/seraph_premium.py")
    (ROOT/f"assets/{form}-info.json").write_text(json.dumps(info,indent=2))
    (ROOT/f".build/seraph-{form}-rig.json").write_text(json.dumps({n:p.tolist() for n,p in bones},indent=2))
    print(json.dumps(info),flush=True)


def preview(form,bones,levels,yaw=None,suffix=""):
    import pyrender
    from presentation_mesh import camera
    scene=pyrender.Scene(bg_color=[.035,.047,.067,1],ambient_light=[.18,.20,.24])
    p=levels[0]
    mesh=trimesh.Trimesh(p["v"],p["t"],vertex_normals=p["n"],process=False)
    mesh.visual.vertex_colors=np.uint8(np.c_[p["color"],np.ones(len(p["v"]))]*255)
    material=pyrender.MetallicRoughnessMaterial(roughnessFactor=.55,metallicFactor=.08)
    primitive=pyrender.Primitive(positions=np.asarray(p["v"],np.float32),
                                normals=np.asarray(p["n"],np.float32),
                                indices=np.asarray(p["t"],np.uint32),
                                color_0=np.c_[p["color"],np.ones(len(p["v"]))].astype(np.float32),
                                material=material)
    scene.add(pyrender.Mesh([primitive]))
    center=(p["v"].min(0)+p["v"].max(0))/2
    size=(960,1100) if form=="mobile" else (1400,1100)
    cam=camera(yaw if yaw is not None else (25 if form=="mobile" else 35),5,center)
    proj=(p["v"]-cam[:3,3])@cam[:3,:3]
    half=max(np.ptp(proj[:,1])*.55,np.ptp(proj[:,0])*.55/(size[0]/size[1]))
    scene.add(pyrender.OrthographicCamera(xmag=half*size[0]/size[1],ymag=half),pose=cam)
    for angle,elevation,intensity,color in [(-10,35,1.8,[1,.94,.87]),(100,10,.7,[.56,.72,1]),(185,25,1.6,[.75,.85,1])]:
        scene.add(pyrender.DirectionalLight(color=color,intensity=intensity),pose=camera(angle,elevation,center))
    renderer=pyrender.OffscreenRenderer(*size)
    try:
        image,_=renderer.render(scene,flags=pyrender.RenderFlags.RGBA)
    finally:
        renderer.delete()
    Image.fromarray(image).save(ROOT/f".build/seraph-hybrid-{form}{suffix}.png")


if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("form",choices=["mobile","siege"])
    parser.add_argument("--preview",action="store_true")
    args=parser.parse_args()
    bones,levels=(build_mobile if args.form=="mobile" else build_siege)()
    if args.preview:
        preview(args.form,bones,levels)
        preview(args.form,bones,levels,155,"-rear")
        preview(args.form,bones,levels,88,"-side")
    else:
        export(args.form,bones,levels)
