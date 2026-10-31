"""Depth-tested textured-model study of the compiled thruster cards and atlas.

Geometry, UVs, crossfade colors and bounded flecks come from the C# exporter.
Lighting/compositing approximate Unity; this is explicitly not a game capture.
The two-second GIF replays the exported sample sequence rather than dropping
frames or inventing a separate animation clock.
"""
from pathlib import Path
import copy
import json
import math
import subprocess
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import pyrender
import trimesh
import presentation_mesh as model

ROOT = Path(__file__).resolve().parents[1]
VERSION = '0.9.0'
SIZE = (660, 660)
FONT = 'C:/Windows/Fonts/segoeui.ttf'
QUAD_TRI = np.array([[0, 1, 2], [0, 2, 3]])
QUAD_UV = np.array([[0, 0], [1, 0], [1, 1], [0, 1]], dtype=float)


def linear(rgb):
    return np.where(rgb <= .04045, rgb / 12.92, ((rgb + .055) / 1.055) ** 2.4)


def srgb(rgb):
    rgb = np.clip(rgb, 0, 1)
    return np.where(rgb <= .0031308, rgb * 12.92, 1.055 * np.power(rgb, 1 / 2.4) - .055)


def texture(name):
    rgba = np.asarray(Image.open(ROOT / f'assets/{name}.png').convert('RGBA'), dtype=np.float32) / 255
    rgba[..., :3] = linear(rgba[..., :3])
    return rgba


def poses():
    # Unique paths: never interfere with animation review's shared m.export files.
    source = ROOT / '.build/thruster-preview-poses-input.json'
    target = ROOT / '.build/thruster-preview-poses-output.json'
    source.write_text(json.dumps([
        dict(Form='mobile', Input=dict(Time=.8, Unfold=0, Forward=.95, Boost=1)),
        dict(Form='siege', Input=dict(Time=.8, Unfold=1))]))
    dll = max((ROOT / 'scripts/offline_checks/bin').rglob('Checks.dll'), key=lambda p: p.stat().st_mtime)
    subprocess.run(['dotnet', str(dll), '--poses', str(source), str(target)], check=True, capture_output=True)
    return json.loads(target.read_text())


def anchor_transforms(bones, pose, scale, offset):
    names = [n for n, p in bones]
    rotations, positions = [], []
    for name, anchor in bones:
        value = pose['Bones'].get(name, {})
        local = model.euler(model.vec(value['Rotation'])) if 'Rotation' in value else np.eye(3)
        shift = model.vec(value['Offset']) if 'Offset' in value else np.zeros(3)
        parent = model.parent(name)
        index = names.index(parent) if parent in names else -1
        if index < 0:
            rotations.append(local); positions.append(anchor + shift)
        else:
            rotations.append(rotations[index] @ local)
            positions.append(positions[index] + rotations[index] @ (anchor - bones[index][1] + shift))
    global_rotation = model.euler(model.vec(pose['Rotation']))
    world_offset = model.vec(pose['Offset']) + offset
    return {name: (global_rotation @ positions[i] * scale * pose['Scale'] + world_offset,
                   global_rotation @ rotations[i]) for i, name in enumerate(names)}


def jet_rotation(axis):
    # Quaternion.FromToRotation(+Y, axis), including Unity's antiparallel case.
    axis = np.asarray(axis, dtype=float); axis /= np.linalg.norm(axis)
    source = np.array([0., 1., 0.]); cross = np.cross(source, axis); dot = np.dot(source, axis)
    if dot > .99999: return np.eye(3)
    if dot < -.99999: return np.diag([1., -1., -1.])
    matrix = np.array([[0, -cross[2], cross[1]], [cross[2], 0, -cross[0]], [-cross[1], cross[0], 0]])
    return np.eye(3) + matrix + matrix @ matrix / (1 + dot)


def setup(scenario, pose, card_vertices):
    asset = scenario['name']; scale = .78 if asset == 'mobile' else 1.45
    # Siege's probe starts .03m above its nozzle and its clamp subtracts .07m.
    # Put the ground at y=0 with exactly the runtime .04m remaining tip clearance.
    offset = np.array([0., 0. if asset == 'mobile' else scenario['length']+.04-.16*scale, 0.])
    bones, parts = model.load(asset)
    # Unity applies model scale to the articulated mesh but not rig translation.
    zero_shift = copy.deepcopy(pose); zero_shift['Offset'] = dict(X=0, Y=0, Z=0)
    world_shift = model.vec(pose['Offset']) + offset
    posed = [(part, vertices * scale + world_shift, normals)
             for part, vertices, normals in model.pose(bones, parts, zero_shift)]
    transforms = anchor_transforms(bones, pose, scale, offset)
    if asset == 'mobile':
        names = [name for name, p in bones if name.startswith(('wing_', 'vane_'))]
        origins = [transforms[name][0] + transforms[name][1] @ np.array([0, 0, -.045]) * scale * pose['Scale'] for name in names]
        axis = np.array([0., 0., -1.]); yaw, pitch = 145, 10
    else:
        origins = [np.array([math.sin((i+.5)*math.pi/3)*.64, .16,
                            math.cos((i+.5)*math.pi/3)*.64]) * scale + offset for i in range(6)]
        axis = np.array([0., -1., 0.]); yaw, pitch = 28, 8
    rotation = jet_rotation(axis)
    dimensions = np.array([scenario['width'], scenario['length'], scenario['width']])
    cards = [card_vertices * dimensions @ rotation.T + p for p in origins]
    all_vertices = np.concatenate([v for part, v, n in posed] + cards)
    center = (all_vertices.min(0) + all_vertices.max(0)) * .5
    camera = model.camera(yaw, pitch, center, distance=12)
    projected = (all_vertices - camera[:3, 3]) @ camera[:3, :3]
    camera[:3, 3] += camera[:3, 0] * (projected[:, 0].min()+projected[:, 0].max())*.5
    camera[:3, 3] += camera[:3, 1] * (projected[:, 1].min()+projected[:, 1].max())*.5
    half = max(np.ptp(projected[:, 1]), np.ptp(projected[:, 0])) * .565
    scene = pyrender.Scene(bg_color=[.035, .047, .067, 1], ambient_light=[.18, .20, .24])
    for part, vertices, normals in posed:
        material = pyrender.MetallicRoughnessMaterial(baseColorFactor=(*part['color'], 1), metallicFactor=.02,
                     roughnessFactor=max(.25, 1-part['smooth']), emissiveFactor=np.ones(3)*.45)
        mesh = trimesh.Trimesh(vertices, part['t'], vertex_normals=normals, process=False)
        mesh.visual = trimesh.visual.TextureVisuals(uv=part['uv'])
        material.baseColorTexture = pyrender.Texture(source=np.asarray(Image.open(ROOT/f'assets/{asset}_surface.png').convert('RGBA')), source_channels='RGBA')
        material.emissiveTexture = pyrender.Texture(source=np.asarray(Image.open(ROOT/f'assets/{asset}_emission.png').convert('RGBA')), source_channels='RGBA')
        scene.add(pyrender.Mesh.from_trimesh(mesh, material=material, smooth=True))
    if asset == 'siege':
        # Match the six physical nozzles constructed by BastionSiegeThrusters.Nozzle().
        # They are runtime meshes, so they are not contained in siege.meshbin.
        nozzle_vertices=[]; nozzle_triangles=[]
        for ring,(radius,height) in enumerate(zip((.18,.17,.11,.09),(.12,0,0,.11))):
            for i in range(24):
                angle=i*math.tau/24; nozzle_vertices.append([math.cos(angle)*radius,height,math.sin(angle)*radius])
                a=ring*24+i; b=ring*24+(i+1)%24; c=((ring+1)%4)*24+i; d=((ring+1)%4)*24+(i+1)%24
                nozzle_triangles.extend([[a,b,c],[b,d,c]])
        nozzle_vertices=np.asarray(nozzle_vertices)*scale
        for origin in origins:
            mesh=trimesh.Trimesh(nozzle_vertices+origin,nozzle_triangles,process=False)
            scene.add(pyrender.Mesh.from_trimesh(mesh,material=pyrender.MetallicRoughnessMaterial(
                baseColorFactor=[.028,.035,.05,1],metallicFactor=.45,roughnessFactor=.45),smooth=True))
        floor = trimesh.creation.box(extents=[10, .04, 10]); floor.apply_translation([0, -.02, 0])
        scene.add(pyrender.Mesh.from_trimesh(floor, material=pyrender.MetallicRoughnessMaterial(baseColorFactor=[.035,.05,.07,1], roughnessFactor=1)))
    scene.add(pyrender.OrthographicCamera(xmag=half, ymag=half, znear=.01, zfar=100), pose=camera)
    for angle, elevation, power, color in [(yaw-35,35,1.8,[1,.94,.87]), (yaw+80,10,.70,[.56,.72,1]), (yaw+160,25,1.6,[.75,.85,1])]:
        scene.add(pyrender.DirectionalLight(color=color, intensity=power), pose=model.camera(angle,elevation,center))
    renderer = pyrender.OffscreenRenderer(*SIZE)
    try: image, depth = renderer.render(scene, flags=pyrender.RenderFlags.RGBA)
    finally: renderer.delete()
    # pyrender 0.1.45 unconditionally linearizes depth as perspective, including
    # orthographic cameras. Undo that mapping to recover actual camera distance;
    # otherwise every floor pixel wrongly occludes particles above the ground.
    valid=depth>0; corrected=np.zeros_like(depth)
    corrected[valid]=100+.01-(100*.01)/depth[valid]; depth=corrected
    assert np.isfinite(depth).all() and np.all(depth[valid]>.1)
    return dict(base=linear(image[..., :3].astype(np.float32)/255), depth=depth, camera=camera,
                half=half, cards=cards, origins=origins, axis=axis, rotation=rotation)


def add_cards(buffer, depth, camera, half, vertices, triangles, uv, colors, atlas, additive=True):
    height, width = buffer.shape[:2]
    projected = (vertices-camera[:3, 3]) @ camera[:3, :3]
    xy = np.column_stack((width/2+projected[:, 0]*height/(half*2), height/2-projected[:, 1]*height/(half*2)))
    z = -projected[:, 2]
    tw, th = atlas.shape[1], atlas.shape[0]
    for triangle in triangles:
        p = xy[triangle]; lo=np.maximum(0,np.floor(p.min(0)).astype(int)); hi=np.minimum([width,height],np.ceil(p.max(0)).astype(int)+1)
        if np.any(hi <= lo): continue
        yy,xx=np.mgrid[lo[1]:hi[1],lo[0]:hi[0]]; xx=xx+.5; yy=yy+.5
        a,b,c=p; den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-8: continue
        u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
        v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den; w=1-u-v
        point_depth=u*z[triangle[0]]+v*z[triangle[1]]+w*z[triangle[2]]
        model_depth=depth[lo[1]:hi[1],lo[0]:hi[0]]
        mask=(u>=0)&(v>=0)&(w>=0)&((model_depth==0)|(point_depth<=model_depth+.003))
        tex=u[...,None]*uv[triangle[0]]+v[...,None]*uv[triangle[1]]+w[...,None]*uv[triangle[2]]
        x=np.clip(tex[...,0]*tw-.5,0,tw-1); y=np.clip((1-tex[...,1])*th-.5,0,th-1)
        x0=x.astype(int); y0=y.astype(int); x1=np.minimum(x0+1,tw-1); y1=np.minimum(y0+1,th-1)
        dx=(x-x0)[...,None]; dy=(y-y0)[...,None]
        sample=(atlas[y0,x0]*(1-dx)+atlas[y0,x1]*dx)*(1-dy)+(atlas[y1,x0]*(1-dx)+atlas[y1,x1]*dx)*dy
        tint=u[...,None]*colors[triangle[0]]+v[...,None]*colors[triangle[1]]+w[...,None]*colors[triangle[2]]
        alpha=sample[...,3:4]*tint[...,3:4]*mask[...,None]
        region=buffer[lo[1]:hi[1],lo[0]:hi[0]]
        if additive: region += sample[...,:3]*tint[...,:3]*alpha
        else: region[:]=region*(1-alpha)+sample[...,:3]*tint[...,:3]*alpha


def billboard(buffer, scene, position, size, color, sprite, additive=True):
    camera=scene['camera']; right=camera[:3,0]*size*.5; up=camera[:3,1]*size*.5
    verts=np.array([position-right-up,position+right-up,position+right+up,position-right+up])
    add_cards(buffer,scene['depth'],camera,scene['half'],verts,QUAD_TRI,QUAD_UV,np.tile(color,(4,1)),sprite,additive)


def groundwash(buffer,scene,time,vapor):
    particles=[]
    # Warm stable hover, sampled at60Hz. The runtime .14s scheduling threshold
    # emits every9frames at this reference rate; this only affects sparse wash.
    now=time+1.2
    for index,origin in enumerate(scene['origins']):
        distance=origin[1]+.03; spread=.09+min(distance,3.2)*.11; closeness=1-min(1,distance/3.2)
        if closeness<=0: continue
        for sequence in range(max(0,int((now-.52)/.15)),int(now/.15)+1):
            age=now-sequence*.15; lifetime=.38+(sequence%4)*.045
            if age<0 or age>=lifetime: continue
            phase=sequence*2.399963+index*1.7
            radial=np.array([math.cos(phase),0,-math.sin(phase)])
            point=np.array([origin[0],.075,origin[2]])+radial*spread+(radial*1.05+np.array([0,.22,0]))*age
            t=age/lifetime
            alpha=np.interp(t,[0,.2,.55,1],[0,.16,.09,0])*closeness
            color=np.array([.50,.65,.80])*(1-t)+np.array([.26,.36,.48])*t
            u=t/.45 if t<.45 else (t-.45)/.55; u=u*u*(3-2*u)
            size=(.26+spread*.65)*(.55+.45*u if t<.45 else 1+.5*u)
            particles.append((point,size,[*color,alpha]))
    particles.sort(key=lambda p:-np.linalg.norm(p[0]-scene['camera'][:3,3]))
    for point,size,color in particles: billboard(buffer,scene,point,size,color,vapor,False)


def render_frame(scenario, frame, scene, triangles, atlas, glow, vapor):
    buffer=scene['base'].copy(); width=scenario['width']; length=scenario['length']
    for i,jet in enumerate(frame['jets']):
        add_cards(buffer,scene['depth'],scene['camera'],scene['half'],scene['cards'][i],triangles,
                  np.asarray(jet['uv']),np.asarray(jet['colors']),atlas)
        origin=scene['origins'][i]
        for g in range(2):
            billboard(buffer,scene,origin+scene['axis']*(.012 if g==0 else length*.035),
                      width*(.68 if g==0 else .35),[.60,.88,1,.65 if g==0 else .45],glow)
        for fleck in jet['flecks']:
            local=np.asarray(fleck['position'])*[width,length,width]
            billboard(buffer,scene,origin+scene['rotation']@local,width*fleck['size'],[.30,.65,1,fleck['alpha']],glow)
    if scenario['name']=='siege': groundwash(buffer,scene,frame['time'],vapor)
    return Image.fromarray(np.uint8(np.clip(srgb(buffer)*255+.5,0,255)))


def validate(data):
    vertices=np.asarray(data['vertices']); triangles=np.asarray(data['triangles']).reshape(-1,3)
    assert vertices.shape==(24,3) and triangles.shape==(12,3)
    assert np.isfinite(vertices).all() and triangles.min()>=0 and triangles.max()<24
    report={}
    for scenario in data['scenarios']:
        frames=scenario['frames']; assert len(frames)==48
        times=np.array([f['time'] for f in frames]); assert np.allclose(np.diff(times),1/24,atol=1e-6)
        for index,frame in enumerate(frames):
            for j,jet in enumerate(frame['jets']):
                uv=np.asarray(jet['uv']); colors=np.asarray(jet['colors'])
                assert uv.shape==(24,2) and np.isfinite(uv).all() and (uv>0).all() and (uv<1).all()
                assert colors.shape==(24,4) and np.allclose(colors[:12,3]+colors[12:,3],.48,atol=1e-6)
                assert 0<=jet['frame']<32 and 0<=jet['blend']<1
                if index:
                    before=frames[index-1]['jets'][j]
                    advance=(jet['frame']+jet['blend']-before['frame']-before['blend'])%32
                    assert abs(advance-32*1.16/24)<.003, f'Unexpected phase jump {advance}'
        report[scenario['name']]=dict(samples=len(frames),jets=len(frames[0]['jets']),sample_rate=24,
                                     uv_bounds='pass',crossfade_energy='pass',temporal_gaps=0)
    return vertices,triangles,report


def text(image,xy,value,size=18,color='#b8d3e9'):
    ImageDraw.Draw(image).text(xy,value,font=ImageFont.truetype(FONT,size),fill=color)


def main():
    data=json.loads((ROOT/'.build/thruster-geometry.json').read_text())
    vertices,triangles,report=validate(data)
    if '--validate-only' in sys.argv:
        print(json.dumps(report,indent=2)); return
    atlas=texture('thruster_plume'); glow=texture('thruster_glow'); vapor=texture('thruster_vapor'); frames=poses()
    scenarios=data['scenarios']; rendered=[]
    for scenario,pose in zip(scenarios,frames):
        scene=setup(scenario,pose,vertices); clips=[]
        for i,frame in enumerate(scenario['frames']):
            clips.append(render_frame(scenario,frame,scene,triangles,atlas,glow,vapor))
            if i==0: clips[-1].save(ROOT/f'.build/thruster-{scenario["name"]}-model-first-frame.png')
            if i%12==0: print(f"{scenario['name']}: rendered frame {i+1}/48",flush=True)
        rendered.append(clips)
    sheet=Image.new('RGB',(1980,1550),'#0b121e')
    text(sheet,(26,20),'IVORY BASTION / CONTINUOUS THRUSTER PLUMES',28)
    text(sheet,(26,65),'Actual textured models, compiled plume geometry and atlas UVs. Offline material/depth study; not a game capture.',17)
    for row,scenario in enumerate(scenarios):
        for col,index in enumerate((0,11,23)):
            x,y=col*660,110+row*710; sheet.paste(rendered[row][index],(x,y))
            title='Mobile / rear three-quarter boost' if row==0 else 'Siege / suspended lift jets'
            text(sheet,(x+20,y+666),f'{title}  |  {scenario["frames"][index]["time"]:.2f}s',17)
    path=ROOT/f'dist/IvoryBastion-{VERSION}-thruster-models.png'; sheet.save(path)
    animation=[]
    for index in range(48):
        image=Image.new('RGB',(1320,760),'#0b121e')
        text(image,(24,15),'IVORY BASTION / MOBILE + SIEGE PLASMA FLOW',23)
        image.paste(rendered[0][index],(0,57)); image.paste(rendered[1][index],(660,57))
        text(image,(24,725),'Compiled 24 fps samples / actual attachments / approximate lighting / two-second replay',16)
        animation.append(image)
    animation[0].save(ROOT/f'dist/IvoryBastion-{VERSION}-thruster-models.gif',save_all=True,
                      append_images=animation[1:],duration=[50 if i%6==5 else 40 for i in range(48)],loop=0,optimize=False)
    report['output_frames']=len(animation); report['expected_frames']=48
    report['geometry_source']='.build/thruster-geometry.json'
    report['scope']='Actual model/rig/cards/atlas and source-matched groundwash at60Hz; approximate offline lighting/additive compositing; no Unity bloom.'
    (ROOT/'.build/thruster-model-preview-review.json').write_text(json.dumps(report,indent=2))
    print(path); print(json.dumps(report,indent=2))


if __name__=='__main__': main()
