"""Bake crevice shading into original Seraph UVs; preserve all rig weights.

BAST2 adds a UV pair to each BAST1 vertex. A shared atlas supplies restrained
ambient-occlusion shading beneath the existing ceramic/metal color palette.
"""
from pathlib import Path
import struct,hashlib,json
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt
from numba import njit
import pymeshlab,xatlas
from presentation_mesh import load,ROOT

@njit(cache=True)
def raster(uv,faces,values,target,mask):
 h,w=target.shape
 for face in faces:
  a=uv[face[0]]*np.array([w-1,h-1]);b=uv[face[1]]*np.array([w-1,h-1]);c=uv[face[2]]*np.array([w-1,h-1])
  den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
  if abs(den)<1e-8:continue
  x0=max(0,int(min(a[0],b[0],c[0])));x1=min(w-1,int(max(a[0],b[0],c[0]))+1)
  y0=max(0,int(min(a[1],b[1],c[1])));y1=min(h-1,int(max(a[1],b[1],c[1]))+1)
  for y in range(y0,y1+1):
   for x in range(x0,x1+1):
    u=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/den;v=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/den;q=1-u-v
    if u>=-.001 and v>=-.001 and q>=-.001:
     target[y,x]=u*values[face[0]]+v*values[face[1]]+q*values[face[2]];mask[y,x]=True

def bake(asset):
 source=ROOT/f'assets/{asset}.meshbin';original=source.read_bytes();cache=ROOT/'.build/bake-source';cache.mkdir(exist_ok=True)
 if original[:5]==b'BAST2':
  print(asset+' already has baked UVs',flush=True);return
 (cache/f'{asset}.meshbin').write_bytes(original)
 bones,parts=load(asset);verts=np.concatenate([p['v'] for p in parts]);offsets=np.r_[0,np.cumsum([len(p['v']) for p in parts])];faces=np.concatenate([p['t']+off for p,off in zip(parts,offsets)])
 mesh=pymeshlab.MeshSet();mesh.add_mesh(pymeshlab.Mesh(vertex_matrix=verts,face_matrix=faces));print(asset+' baking surface occlusion',flush=True)
 mesh.compute_scalar_ambient_occlusion(rays=64);ao=np.clip(mesh.current_mesh().vertex_scalar_array(),0,1);assert len(ao)==len(verts)
 print(asset+' AO '+str(np.quantile(ao,[0,.1,.5,.9,1])),flush=True)
 atlas=xatlas.Atlas()
 for p in parts:atlas.add_mesh(np.asarray(p['v'],np.float32),np.asarray(p['t'],np.uint32),np.asarray(p['n'],np.float32))
 pack=xatlas.PackOptions();pack.resolution=4096 if asset in ['mobile','siege'] else 1024;pack.padding=5;pack.bilinear=True
 chart=xatlas.ChartOptions();chart.max_iterations=1
 print(asset+' packing material UVs',flush=True);atlas.generate(chart,pack)
 assert atlas.atlas_count==1,(asset,atlas.atlas_count)
 width,height=atlas.width,atlas.height;target=np.ones((height,width),np.float32);mask=np.zeros((height,width),np.bool_)
 mapped=[]
 for i,p in enumerate(parts):
  vmapping,tri,uv=atlas[i];v=p['v'][vmapping];n=p['n'][vmapping];values=.48+.52*ao[offsets[i]+vmapping]**1.25
  if p['glow']>0 or p['name'] in ['Glass','Ink']:values[:]=1
  raster(uv,tri,values,target,mask);mapped.append((p,vmapping,tri,uv))
 # Pad atlas gutters using nearest painted texels, avoiding dark UV seams.
 indices=distance_transform_edt(~mask,return_distances=False,return_indices=True);target=target[indices[0],indices[1]]
 grain=np.random.default_rng(42).normal(0,.0018,target.shape);rgb=np.uint8(np.clip((target+grain)*255,0,255));texture=np.repeat(rgb[:,:,None],3,axis=2)
 Image.fromarray(texture[::-1]).resize((pack.resolution,pack.resolution),Image.Resampling.LANCZOS).save(ROOT/f'assets/{asset}_surface.png')
 def string(f,s):b=s.encode();assert len(b)<128;f.write(bytes([len(b)]));f.write(b)
 with source.open('wb') as f:
  f.write(b'BAST2');f.write(struct.pack('<i',len(bones)))
  for name,pos in bones:string(f,name);f.write(struct.pack('<3fi',*pos,-1))
  f.write(struct.pack('<i',len(parts)))
  for p,mapping,tri,uv in mapped:
   string(f,p['name']);f.write(struct.pack('<5f',*p['color'],p['smooth'],p['glow']));f.write(struct.pack('<ii',len(mapping),tri.size))
   data=np.empty(len(mapping),dtype=np.dtype([('v','<f4',3),('n','<f4',3),('uv','<f4',2),('b','<i4',2),('w','<f4')]))
   for key in ['v','n','b','w']:data[key]=p[key][mapping]
   data['uv']=uv;f.write(data.tobytes());f.write(np.asarray(tri,dtype='<i4').tobytes())
 report=dict(asset=asset,texture=[width,height],original_vertices=len(verts),vertices=sum(len(x[1]) for x in mapped),triangles=len(faces),ao_quantiles=np.quantile(ao,[0,.1,.5,.9,1]).tolist(),source_sha256=hashlib.sha256(original).hexdigest())
 (ROOT/f'.build/{asset}-surface-bake.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)

if __name__=='__main__':
 for asset in ['mobile','siege','unit_guardian','unit_gunner','unit_interceptor','unit_repair']:bake(asset)
