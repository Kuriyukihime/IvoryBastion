"""Load and render exact BAST1/BAST2/BAST3 runtime assets and shared C# poses."""
from pathlib import Path
import struct,json,subprocess,math,tempfile
from functools import lru_cache
import numpy as np
from scipy.spatial.transform import Rotation
from PIL import Image,ImageDraw,ImageFont
import trimesh,pyrender
ROOT=Path(__file__).resolve().parents[1]
def save_png(image,path):
    # Preview consumers can memory-map an existing PNG on Windows. Write a
    # complete replacement instead of truncating the mapped file in place.
    path=Path(path)
    with tempfile.NamedTemporaryFile(dir=path.parent,suffix='.png',delete=False) as stream:
        pending=Path(stream.name)
    try:
        image.save(pending)
        pending.replace(path)
    finally:
        pending.unlink(missing_ok=True)


@lru_cache(maxsize=12)
def load(asset,lod=0):
 with (ROOT/f'assets/{asset}.meshbin').open('rb') as f:
  def read(fmt):return struct.unpack('<'+fmt,f.read(struct.calcsize('<'+fmt)))
  def string():return f.read(read('B')[0]).decode()
  fmt=f.read(5);assert fmt in [b'BAST1',b'BAST2',b'BAST3'];four=fmt==b'BAST3';textured=four or fmt==b'BAST2'
  bones=[]
  for _ in range(read('i')[0]):bones.append((string(),np.array(read('3f'))));read('i')
  parts=[]
  dtype=np.dtype([('v','<f4',3),('n','<f4',3)]+([('uv','<f4',2)] if textured else [])+([('b','<i4',4),('w','<f4',4)] if four else [('b','<i4',2),('w','<f4')]))
  for index in range(read('i')[0]):
   name=string();color=read('3f');smooth,glow=read('2f');nv,ni=read('2i');data=np.frombuffer(f.read(nv*dtype.itemsize),dtype=dtype);tri=np.frombuffer(f.read(ni*4),dtype='<i4').reshape(-1,3)
   if not four or index==lod:parts.append(dict(name=name,color=color,smooth=smooth,glow=glow,four=four,v=data['v'].astype(float),n=data['n'].astype(float),b=data['b'],w=data['w'],t=tri,uv=data['uv'] if textured else None))
  assert f.read()==b''
 return bones,parts

def parent(name):
 if name=='root':return None
 if name.startswith('mine_knee_'):return name.replace('mine_knee_','mine_hip_')
 if name.startswith('skyfall_muzzle_'):return 'skyfall_'+name.rsplit('_',1)[1]
 if name=='cannon_muzzle':return 'cannon'
 if name.startswith('muzzle_'):return name.replace('muzzle_','fore_')
 if name.startswith('tip_'):return name[4:]
 if name.startswith('hatch_'):return ['rack_l','rack_r','shield_l','shield_r'][int(name.split('_')[1])]
 if name.startswith('vent_'):return 'cannon'
 if name=='head' or name.startswith(('arm_','wing_','vane_')):return 'torso'
 for child,par in [('fore_','arm_'),('hand_','fore_'),('boot_','leg_')]:
  if name.startswith(child):return name.replace(child,par)
 return 'root'

def vec(d):return np.array([d['X'],d['Y'],d['Z']])
def euler(v):return Rotation.from_euler('zxy',[v[2],v[0],v[1]],degrees=True).as_matrix()
def pose(bones,parts,frame):
 if frame is None:return [(p,p['v'],p['n']) for p in parts]
 names=[b[0] for b in bones];rot=[];pos=[]
 for i,(name,anchor) in enumerate(bones):
  p=frame['Bones'].get(name,{'Rotation':dict(X=0,Y=0,Z=0),'Offset':dict(X=0,Y=0,Z=0)})
  local=euler(vec(p['Rotation']));offset=vec(p['Offset']);par=parent(name);ix=names.index(par) if par in names else -1
  if ix<0:rot.append(local);pos.append(anchor+offset)
  else:rot.append(rot[ix]@local);pos.append(pos[ix]+rot[ix]@(anchor-bones[ix][1]+offset))
 rot=np.array(rot);pos=np.array(pos);anchors=np.array([b[1] for b in bones]);trans=pos-np.einsum('nij,nj->ni',rot,anchors)
 rr=euler(vec(frame['Rotation']));ro=vec(frame['Offset']);scale=frame['Scale'];out=[]
 for p in parts:
  v=np.zeros_like(p['v']);n=np.zeros_like(v)
  for k in range(p['b'].shape[1]):
   ids=p['b'][:,k];w=(p['w'][:,k] if p.get('four') else p['w'] if k==0 else 1-p['w'])[:,None]
   v+=(np.einsum('nij,nj->ni',rot[ids],p['v'])+trans[ids])*w;n+=np.einsum('nij,nj->ni',rot[ids],p['n'])*w
  v=v@rr.T*scale+ro;n=n@rr.T;n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-9);out.append((p,v,n))
 return out

def export(requests):
 dll=max((ROOT/'scripts/offline_checks/bin').rglob('Checks.dll'),key=lambda p:p.stat().st_mtime)
 # Separate render jobs may run together; never share a pose request/output file.
 with tempfile.TemporaryDirectory(prefix='seraph-poses-',dir=ROOT/'.build') as temp:
  src=Path(temp)/'input.json';dest=Path(temp)/'output.json';src.write_text(json.dumps(requests))
  subprocess.run(['dotnet',str(dll),'--poses',str(src),str(dest)],check=True,capture_output=True)
  return json.loads(dest.read_text())

def camera(yaw,pitch,center,distance=7):
 a=math.radians(yaw);b=math.radians(pitch);z=np.array([math.sin(a)*math.cos(b),math.sin(b),math.cos(a)*math.cos(b)]);x=np.cross([0,1,0],z);x/=np.linalg.norm(x);y=np.cross(z,x)
 p=np.eye(4);p[:3,:3]=np.stack([x,y,z],axis=1);p[:3,3]=center+z*distance;return p

def shield_overlay(image,cam,half,kind):
 # Composite the native vertex colours. This avoids platform-specific GL alpha
 # sorting and keeps the preview honest about its simpler offline material.
 inner=json.loads((ROOT/'.build/shield-geometry.json').read_text());outer=json.loads((ROOT/'.build/aegis-hex-geometry.json').read_text());layers=[]
 if kind!=3:layers.append((np.array(inner['normals'])*outer['innerRadius']/1.45,np.array(inner['normals']),np.array(inner['triangles']).reshape(-1,3),np.array(inner['calm'])))
 if kind!=1:layers.append((np.array(outer['vertices'])*outer['outerRadius']/1.45,np.array(outer['normals']),np.array(outer['triangles']).reshape(-1,3),np.array(outer['colors'])))
 width,height=image.size
 for vertices,normals,triangles,colors in layers:
  projected=(vertices+np.array((0,2/1.45,0))-cam[:3,3])@cam[:3,:3];xy=np.column_stack((width/2+projected[:,0]*height/(half*2),height/2-projected[:,1]*height/(half*2)))
  buffer=np.zeros((height,width,4),dtype=np.float32)
  for tri in triangles:
   if normals[tri].mean(0)@cam[:3,2]<=0:continue
   points=xy[tri];lo=np.maximum(0,np.floor(points.min(0)).astype(int));hi=np.minimum([width,height],np.ceil(points.max(0)).astype(int)+1)
   if np.any(hi<=lo):continue
   yy,xx=np.mgrid[lo[1]:hi[1],lo[0]:hi[0]];xx=xx+.5;yy=yy+.5;a,b,c=points
   den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
   if abs(den)<1e-9:continue
   u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den;v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w=1-u-v
   rgba=u[...,None]*colors[tri[0]]+v[...,None]*colors[tri[1]]+w[...,None]*colors[tri[2]];alpha=rgba[...,3:4]*((u>=0)&(v>=0)&(w>=0))[...,None]
   region=buffer[lo[1]:hi[1],lo[0]:hi[0]];old=region[...,3:4].copy();total=alpha+old*(1-alpha)
   region[...,:3]=(rgba[...,:3]*alpha+region[...,:3]*old*(1-alpha))/np.maximum(total,1e-9);region[...,3:4]=total
  image=Image.alpha_composite(image,Image.fromarray(np.uint8(np.clip(buffer,0,1)*255)))
 return image

def render(asset,size=(800,1000),yaw=25,pitch=5,crop=None,frame=None,transparent=False,title=None,view=None,shield=0,extras=None):
 bones,parts=load(asset);posed=pose(bones,parts,frame)
 if extras:posed+=extras
 vertices=np.concatenate([v for p,v,n in posed]);center=(vertices.min(axis=0)+vertices.max(axis=0))*.5
 if crop: center[1]=(crop[0]+crop[1])*.5
 cam=camera(yaw,pitch,center);project=(vertices-cam[:3,3])@cam[:3,:3]
 if crop:
  subset=(vertices[:,1]>=crop[0])&(vertices[:,1]<=crop[1]);selected=project[subset] if subset.any() else project;half=(crop[1]-crop[0])*.54
 else:selected=project;half=max(np.ptp(selected[:,1])*.55,np.ptp(selected[:,0])*.55/(size[0]/size[1]))
 if not crop:cam[:3,3]+=cam[:3,0]*((selected[:,0].min()+selected[:,0].max())*.5)+cam[:3,1]*((selected[:,1].min()+selected[:,1].max())*.5)
 if view is not None:cam=camera(yaw,pitch,np.array(view[:3]));half=view[3]
 scene=pyrender.Scene(bg_color=[.035,.047,.067,0 if transparent else 1],ambient_light=[.18,.20,.24])
 textures={}
 for p,v,n in posed:
  name=p['name'];material=pyrender.MetallicRoughnessMaterial(baseColorFactor=(*p['color'],1),metallicFactor=.45 if name in ['Edge','Joint'] else .02,roughnessFactor=max(.25,1-p['smooth']),emissiveFactor=np.array(p['color'])*min(.55,p['glow']*.20))
  if p.get('four'):
   material.emissiveFactor=np.ones(3)*.45
   material.emissiveTexture=pyrender.Texture(source=np.asarray(Image.open(ROOT/f'assets/{asset}_emission.png').convert('RGBA')),source_channels='RGBA')
  mesh=trimesh.Trimesh(v,p['t'],vertex_normals=n,process=False)
  texture=asset+'_surface' if p['uv'] is not None else 'ceramic_surface' if name in ['Ceramic','Porcelain'] else 'alloy_surface' if name in ['Frame','Joint','Edge'] else None
  if texture:
   if p['uv'] is not None:uv=p['uv']
   else:
    ns=np.abs(p['n']);major=ns.argmax(axis=1);uv=np.empty((len(v),2));uv[major==2]=p['v'][major==2][:,[0,1]];uv[major==0]=p['v'][major==0][:,[2,1]];uv[major==1]=p['v'][major==1][:,[0,2]];uv*=3
   if texture not in textures:
    surface=Image.open(ROOT/f'assets/{texture}.png')
    textures[texture]=pyrender.Texture(source=np.asarray(surface.convert('RGBA')),source_channels='RGBA')
    if p.get('four') and surface.mode=='RGBA':
     # HGStandard consumes alpha as specular strength. Pyrender is a PBR
     # renderer, so this is an explicitly approximate studio response, not
     # an emulation of Unity's lighting ramp or exact highlight shader.
     spec=np.asarray(surface)[:,:,3].astype(np.float32)/255
     # RGBA keeps row stride aligned even when xatlas emits an odd width;
     # packed RGB rows can overrun the native OpenGL upload's 4-byte alignment.
     mr=np.empty((*spec.shape,4),np.uint8);mr[:,:,0]=255;mr[:,:,3]=255
     mr[:,:,1]=np.uint8(np.clip(.80-.48*spec,.25,1)*255);mr[:,:,2]=255
     textures[texture+'_response']=pyrender.Texture(source=mr,source_channels='RGBA')
   mesh.visual=trimesh.visual.TextureVisuals(uv=uv);material.baseColorTexture=textures[texture]
   if texture+'_response' in textures:
    material.alphaMode='OPAQUE';material.metallicFactor=.02;material.roughnessFactor=1
    material.metallicRoughnessTexture=textures[texture+'_response']
  scene.add(pyrender.Mesh.from_trimesh(mesh,material=material,smooth=True))
 scene.add(pyrender.OrthographicCamera(xmag=half*size[0]/size[1],ymag=half,znear=.01,zfar=100),pose=cam)
 for angle,elevation,intensity,color in [(yaw-35,35,1.8,[1,.94,.87]),(yaw+80,10,.70,[.56,.72,1]),(yaw+160,25,1.6,[.75,.85,1])]:scene.add(pyrender.DirectionalLight(color=color,intensity=intensity),pose=camera(angle,elevation,center))
 renderer=pyrender.OffscreenRenderer(*size)
 try:color,_=renderer.render(scene,flags=pyrender.RenderFlags.RGBA)
 finally:renderer.delete()
 image=Image.fromarray(color)
 if shield:image=shield_overlay(image,cam,half,shield)
 if title:
  draw=ImageDraw.Draw(image);draw.text((24,20),title,font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',max(15,size[0]//65)),fill='#c1d1df')
 return image

if __name__=='__main__':
 version=json.loads((ROOT/'package/manifest.json').read_text())['version_number']
 for job in json.loads((ROOT/'scripts/presentation_render_jobs.json').read_text()):
  path=ROOT/job['path'];crop=job.get('crop');im=render(job['asset'],tuple(job['size']),job['yaw'],crop=crop[:2] if crop else None,title=job['title']);save_png(im,path);print(path.name,flush=True)
  if job['asset'] in ['mobile','siege'] and path.parent.name=='dist':
   save_png(im,path.with_name(path.name.replace('IvoryBastion-','IvoryBastion-'+version+'-')))
 for name,asset,yaw,crop in [('hud_mobile','mobile',-18,(.96,1.68)),('hud_siege','siege',25,None),('art_ordnance','siege',28,None),('art_aegis','siege',-30,None),('art_legion','unit_guardian',-20,None),('art_skirmisher','mobile',30,None)]:
  im=render(asset,(768,512),yaw,frame=None,crop=crop,transparent=True,shield=2 if name=='art_aegis' else 0,view=(0,1.38,0,3.25) if name=='art_aegis' else None);save_png(im,ROOT/f'assets/{name}.png');print(name,flush=True)

 if __name__=='__main__':
  for kind in ['guardian','gunner','interceptor','repair']:
   render('unit_'+kind,(512,384),25,transparent=True).save(ROOT/f'assets/hud_unit_{kind}.png')
