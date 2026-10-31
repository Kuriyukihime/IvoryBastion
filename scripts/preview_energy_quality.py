"""Raster proof of exported runtime energy geometry/profile; not Unity footage."""
import json
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'dist/IvoryBastion-0.33.0-review/animation'
DATA=json.loads((ROOT/'.build/review-033-energy-geometry.json').read_text())
FONT=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',24)
SMALL=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)

def triangle(buffer,points,uv,alphas,color,soft):
    h,w=buffer.shape[:2]
    lo=np.maximum(0,np.floor(points.min(0)).astype(int));hi=np.minimum([w,h],np.ceil(points.max(0)).astype(int)+1)
    if np.any(hi<=lo):return
    yy,xx=np.mgrid[lo[1]:hi[1],lo[0]:hi[0]];xx=xx+.5;yy=yy+.5;a,b,c=points
    den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
    if abs(den)<1e-9:return
    u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
    v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;z=1-u-v
    alpha=(u*alphas[0]+v*alphas[1]+z*alphas[2])*((u>=0)&(v>=0)&(z>0))
    if soft:
        transverse=np.clip(u*uv[0,1]+v*uv[1,1]+z*uv[2,1],0,1)
        alpha*=np.interp(transverse,np.linspace(0,1,129),DATA['profile'])
    region=buffer[lo[1]:hi[1],lo[0]:hi[0]]
    region[:]=np.maximum(region,alpha[...,None]*color)

def feather(soft):
    canvas=np.zeros((410,300,3),float)
    for layer in range(2):
        surface=np.zeros_like(canvas)
        color=np.array([.08,.38,1] if layer==0 else [.68,.94,1])
        if not soft:
            vertices=np.array(DATA['before'][layer]);faces=[[0,1,2],[0,2,3]];alpha=np.array([0,.55,.90,.55]);uv=np.zeros((4,2))
        else:
            grid=np.array(DATA['after'][layer]);vertices=grid.reshape(-1,3);faces=[];alpha=[];uv=[]
            for section in range(len(grid)):
                t=section/(len(grid)-1)
                for edge in range(3):
                    alpha.append(max(0,np.sin(t*np.pi))**.4*(.88 if edge==1 else .46));uv.append([t,edge*.5])
                    k=section*3+edge
                    if section<len(grid)-1 and edge<2:faces.extend([[k,k+3,k+1],[k+1,k+3,k+4]])
            alpha=np.array(alpha);uv=np.array(uv)
        points=np.column_stack((150+vertices[:,0]*420,395-vertices[:,1]*210))
        for face in faces:
            face=np.array(face);triangle(surface,points[face],uv[face],alpha[face],color,soft)
        canvas+=surface
    return np.clip(canvas,0,1)

def beam(soft):
    h,w=160,600;canvas=np.zeros((h,w,3),float)
    widths=[1.8,.9,.30];colors=[[.10,.36,1],[.15,.70,1],[1,1,1]];alphas=[.32,.95,1]
    y=(np.arange(h)+.5-h/2)/(h*.5)*2
    for width,color,alpha in zip(widths,colors,alphas):
        across=y/width+.5;mask=(across>=0)&(across<=1)
        opacity=np.interp(np.clip(across,0,1),np.linspace(0,1,129),DATA['profile']) if soft else np.ones(h)
        canvas+=mask[:,None,None]*opacity[:,None,None]*np.array(color)[None,None,:]*alpha
    return np.clip(canvas,0,1)

def paste_glow(image,layer,position):
    x,y=position;array=np.asarray(image).astype(float)/255
    array[y:y+layer.shape[0],x:x+layer.shape[1]]=np.clip(array[y:y+layer.shape[0],x:x+layer.shape[1]]+layer,0,1)
    return Image.fromarray(np.uint8(array*255))

OUT.mkdir(parents=True,exist_ok=True)
image=Image.new('RGB',(1360,850),'#0a101a')
for i in range(2):
    image=paste_glow(image,beam(i==1),(40+i*680,110))
    image=paste_glow(image,feather(i==1),(190+i*680,340))
d=ImageDraw.Draw(image)
d.text((40,25),'PUBLISHED / FLAT ENERGY SURFACES',font=FONT,fill='#a8bed0')
d.text((720,25),'REVIEW / FEATHERED ENERGY SURFACES',font=FONT,fill='#80d4ff')
d.text((40,75),'ION LANCE',font=SMALL,fill='#d8e6ef');d.text((720,75),'ION LANCE',font=SMALL,fill='#d8e6ef')
d.text((40,305),'BLUE SHIFT / one feather at full release',font=SMALL,fill='#d8e6ef');d.text((720,305),'BLUE SHIFT / one feather at full release',font=SMALL,fill='#d8e6ef')
d.text((40,780),'Actual shared geometry and alpha profile. Existing colors and event timing retained.',font=SMALL,fill='#bdd0e0')
d.text((40,813),'Offline material study: lighting, bloom, scene depth and animated wing attachment are not simulated.',font=SMALL,fill='#8298ac')
image.save(OUT/'energy-surfaces-before-after.png')
print('Wrote',OUT/'energy-surfaces-before-after.png')
