"""Offline mesh/material study of the exact Aegis vertices exported by the checks."""
from pathlib import Path
import struct,json
import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]

def main():
    faces=[]
    view=np.array((.75,.38,1.2));view/=np.linalg.norm(view)
    right=np.cross((0,1,0),view);right/=np.linalg.norm(right);up=np.cross(view,right)
    basis=np.array([right,up,view])
    light=np.array((-.4,.8,.7));light/=np.linalg.norm(light)
    with (ROOT/'assets/siege.meshbin').open('rb') as f:
        textured=f.read(5)==b'BAST2'
        def integer():return struct.unpack('<i',f.read(4))[0]
        def string():return f.read(f.read(1)[0]).decode()
        for _ in range(integer()):string();f.read(16)
        for _ in range(integer()):
            name=string();r,g,b,smooth,glow=struct.unpack('<5f',f.read(20));nv=integer();ni=integer()
            raw=[struct.unpack('<8fiif' if textured else '<6fiif',f.read(44 if textured else 36)) for _ in range(nv)]
            vertices=np.array([a[:3] for a in raw])*1.45;normals=np.array([a[3:6] for a in raw])
            ids=np.array(struct.unpack('<'+str(ni)+'i',f.read(ni*4))).reshape(-1,3)
            for triangle in ids:
                normal=normals[triangle].mean(0)
                if np.dot(normal,view)<-.02:continue
                intensity=.38+.62*max(0,np.dot(normal,light));intensity=max(intensity,min(1,glow*.6))
                color=tuple(np.uint8(np.clip(np.array([r,g,b])*intensity,0,1)*255))+(255,)
                faces.append((vertices[triangle],color))
    data=json.loads((ROOT/'.build/aegis-geometry.json').read_text())
    vertices=np.array(data['vertices'])+np.array((0,2,.4));normals=np.array(data['normals']);kinds=np.array(data['kinds'])
    weights=np.array(data['edgeWeights']);rim=(1-abs(normals@view))**2
    colors=np.zeros((len(vertices),4))
    for k,rgb in enumerate([(.035,.30,.78),(.16,.58,1),(.87,.94,1)]):colors[kinds==k,:3]=rgb
    colors[:,3]=np.where(kinds==0,(.026+.1*rim)*(.45+.55*weights),np.where(kinds==1,.27,.43))
    for triangle in np.array(data['triangles']).reshape(-1,3):
        faces.append((vertices[triangle],tuple(np.uint8(np.clip(colors[triangle].mean(0),0,1)*255))))
    width,height=1440,1080;factor=2
    canvas=Image.new('RGB',(width*factor,height*factor),(14,20,32));draw=ImageDraw.Draw(canvas,'RGBA')
    transformed=[]
    for vertices,color in faces:
        p=(vertices-np.array((0,2,0)))@basis.T
        xy=np.column_stack((width/2+p[:,0]*125,height*.55-p[:,1]*125))*factor
        transformed.append((p[:,2].mean(),xy,color))
    for _,xy,color in sorted(transformed,key=lambda t:t[0]):draw.polygon([tuple(p) for p in xy],fill=color)
    canvas=canvas.resize((width,height),Image.Resampling.LANCZOS);draw=ImageDraw.Draw(canvas)
    draw.text((30,25),'AEGIS / FITTED BLUE PANELS + WHITE EDGES',fill=(222,235,255))
    draw.text((30,49),'OFFLINE GEOMETRY STUDY — NOT AN IN-GAME CAPTURE',fill=(132,161,195))
    canvas.save(ROOT/'dist/IvoryBastion-aegis-geometry-preview.png')
    print('Rendered exact exported Aegis geometry around the siege model.')

if __name__=='__main__':main()
