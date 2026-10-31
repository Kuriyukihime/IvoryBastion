"""Actual BAST meshes and compiled old/new choreography; offline, not game footage."""
import argparse
import copy
import json
import math
import subprocess
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.spatial.transform import Rotation
import presentation_mesh as m

ROOT=m.ROOT
OUT=ROOT/'dist/IvoryBastion-0.33.0-review/animation'
BASE=ROOT/'.build/review-033-baseline'
FONT=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
SMALL=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',13)

def stage(t,a,b):
    s=max(0,min(1,(t-a)/(b-a)))
    return s*s*(3-2*s)

def envelope(t,a,b,c):
    return stage(t,0,a)*(1-stage(t,b,c)) if t>=0 else 0

def export(requests,baseline=False):
    project=ROOT/'scripts/animation_review/AnimationReview.csproj'
    subprocess.run(['dotnet','build',str(project),'-c','Release',f'-p:Baseline={str(baseline).lower()}','--nologo'],check=True,capture_output=True)
    folder='Baseline' if baseline else 'Review'
    dll=project.parent/f'bin/{folder}/net10.0/AnimationReview.dll'
    src=ROOT/f'.build/animation-quality-{folder}-input.json'
    dst=ROOT/f'.build/animation-quality-{folder}-poses.json'
    src.write_text(json.dumps(requests))
    subprocess.run(['dotnet',str(dll),'--export',str(src),str(dst)],check=True)
    return json.loads(dst.read_text())

def vector(a):return {'X':float(a[0]),'Y':float(a[1]),'Z':float(a[2])}

def blend_rotation(previous,target,alpha):
    qa=Rotation.from_matrix(m.euler(m.vec(previous))).as_quat()
    qb=Rotation.from_matrix(m.euler(m.vec(target))).as_quat()
    dot=float(qa@qb)
    if dot<0:qb=-qb;dot=-dot
    if dot>.9995:q=qa*(1-alpha)+qb*alpha
    else:
        theta=math.acos(min(1,dot));q=(qa*math.sin((1-alpha)*theta)+qb*math.sin(alpha*theta))/math.sin(theta)
    zxy=Rotation.from_quat(q/np.linalg.norm(q)).as_euler('zxy',degrees=True)
    return vector([zxy[1],zxy[2],zxy[0]])

def smooth_frames(frames,requests,baseline=False,step=1/20):
    # Mirror the presentation-only filters in BastionAnimation.Apply/LateUpdate.
    previous=copy.deepcopy(frames[0]);result=[]
    for target,request in zip(frames,requests):
        f=copy.deepcopy(target)
        for name,bone in f['Bones'].items():
            old=previous['Bones'].get(name,bone)
            rate=32 if name.startswith('hatch_') else 14 if name.startswith(('wing_','vane_')) else 11 if name.startswith(('leg_','boot_')) else 16 if name=='torso' else 24
            a=1-math.exp(-rate*step)
            bone['Rotation']=blend_rotation(old['Rotation'],bone['Rotation'],a)
            position_rate=70 if not baseline and name=='cannon' and request['Input'].get('LaserKick',0)>.01 else rate
            bone['Offset']=vector(m.vec(old['Offset'])+(m.vec(bone['Offset'])-m.vec(old['Offset']))*(1-math.exp(-position_rate*step)))
        if request['Form']=='mobile':
            a=1-math.exp(-12*step)
            f['Offset']=vector(m.vec(previous['Offset'])+(m.vec(target['Offset'])-m.vec(previous['Offset']))*a)
            f['Rotation']=blend_rotation(previous['Rotation'],target['Rotation'],a)
        previous=f;result.append(f)
    return result

def draw_pair(form,before,after,label,mesh_root=BASE,size=(420,510),smoothed=False):
    # A common mesh isolates animation changes. Final-model clips opt in below.
    if m.ROOT!=mesh_root:m.ROOT=mesh_root;m.load.cache_clear()
    view=(0,1.06,0,1.32) if form=='mobile' else (0,1.6,-.15,3.05)
    if label.startswith('LANCE'):view=(0,1.16,1.42,1.0)
    yaw=145 if form=='mobile' and label.startswith('FLIGHT') else 27
    pair=Image.new('RGB',(size[0]*2,size[1]+88),'#0a101a')
    for i,frame in enumerate([before,after]):
        im=m.render(form,size,yaw=yaw,pitch=8,frame=frame,view=view)
        pair.paste(im.convert('RGB'),(i*size[0],54))
    d=ImageDraw.Draw(pair)
    d.text((14,10),'PUBLISHED 0.32 MOTION',font=FONT,fill='#a2b4c7')
    d.text((size[0]+14,10),'0.33 REVIEW MOTION',font=FONT,fill='#68c7ff')
    d.text((14,32),label,font=SMALL,fill='#c6d5e4')
    proof='compiled poses + runtime smoothing' if smoothed else 'compiled target poses'
    d.text((14,size[1]+62),'Same mesh + camera / '+proof+' / offline study',font=SMALL,fill='#8298ac')
    return pair

def poses():
    rows=[('mobile','BLUE SHIFT / gather',dict(Unfold=0,Time=1,PowerAge=.22)),
          ('mobile','BLUE SHIFT / release',dict(Unfold=0,Time=1,PowerAge=.46)),
          ('mobile','FLIGHT / acceleration',dict(Unfold=0,Time=1,Forward=1,Boost=1,Acceleration=1)),
          ('mobile','FLIGHT / air brake',dict(Unfold=0,Time=1,Forward=.15,Brake=1,Acceleration=-1)),
          ('siege','BATTERY / rear-left launch',dict(Unfold=1,Time=1,MissileKick=1,RackKicks=[0,0,1,0])),
          ('siege','LANCE / cooling after shot',dict(Unfold=1,Time=1,LaserKick=0,CannonHeat=.8))]
    requests=[dict(Form=form,Input=pose) for form,_,pose in rows]
    before,after=export(requests,True),export(requests)
    sheet=Image.new('RGB',(1680,1794),'#0a101a')
    for i,((form,label,_),a,b) in enumerate(zip(rows,before,after)):
        pair=draw_pair(form,a,b,label)
        sheet.paste(pair,(i%2*840,i//2*598))
    sheet.save(OUT/'motion-before-after.png')
    print('Wrote motion-before-after.png',flush=True)

def motion(kind,final=False,mesh_root=None):
    duration=4 if kind=='flight' else 2.6 if kind=='lance' else 3
    requests=[];oldrequests=[]
    for i in range(round(duration*20)):
        t=i/20
        if kind=='flight':
            travel=stage(t,.15,.8)*(1-stage(t,1.6,2))
            boost=stage(t,.55,.9)*(1-stage(t,1.65,1.95))
            p=dict(Unfold=0,Time=t,Forward=travel,Boost=boost,Acceleration=envelope(t-.15,.18,.3,.8)-envelope(t-1.6,.08,.15,.55),Brake=envelope(t-1.6,.13,.22,.65),PowerAge=t-2.6)
            form='mobile'
        elif kind=='lance':
            age=t-.4;p=dict(Unfold=1,Time=t,LaserKick=envelope(age,.035,.065,.48),CannonHeat=envelope(age,.11,.36,1.9));form='siege'
        else:
            last=[-.99]*4;hatches=[-100]*72
            for shot in range(8):
                fired=.4+shot*.23
                if t>=fired:last[shot%4]=fired;hatches[shot]=t-fired
            p=dict(Unfold=1,Time=t,RackKicks=[envelope(t-x,.024,.06,.42) if x>=0 else 0 for x in last],HatchAges=hatches,MissileKick=max([math.exp(-(t-x)*12) if x>=0 else 0 for x in last]));form='siege'
        requests.append(dict(Form=form,Input=p))
        old=copy.deepcopy(p)
        if kind=='lance':old['LaserKick']=math.exp(-(t-.4)*6) if t>=.4 else 0
        oldrequests.append(dict(Form=form,Input=old))
    before=smooth_frames(export(oldrequests,True),oldrequests,True)
    after=smooth_frames(export(requests),requests)
    images=[]
    for i,(a,b) in enumerate(zip(before,after)):
        label={'flight':'FLIGHT / acceleration - brake - Blue Shift','lance':'LANCE / kick - recuperation - vent cooling','battery':'BATTERY / each bank responds to its own launch'}[kind]
        images.append(draw_pair(form,a,b,label,mesh_root or (ROOT if final else BASE),(360,440),smoothed=True))
        if i%20==0:print(kind,'frame',i,flush=True)
    images[0].save(OUT/f'{kind}-before-after.gif',save_all=True,append_images=images[1:],duration=50,loop=0,optimize=False)

def cooling_detail(mesh_root=None):
    requests=[dict(Form='siege',Input=dict(Unfold=1,Time=1,LaserKick=0,CannonHeat=1))]
    before,after=export(requests,True),export(requests)
    pair=draw_pair('siege',before[0],after[0],'LANCE / full cooling, recoil recovered',
                   mesh_root or ROOT,(640,650))
    pair.save(OUT/'lance-cooling-detail.png')
    print('Wrote lance-cooling-detail.png',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--motion',choices=['flight','lance','battery']);parser.add_argument('--cooling-detail',action='store_true');parser.add_argument('--final-model',action='store_true');parser.add_argument('--asset-root',type=Path);args=parser.parse_args()
    OUT.mkdir(parents=True,exist_ok=True)
    if args.cooling_detail:cooling_detail(args.asset_root)
    elif args.motion:motion(args.motion,args.final_model,args.asset_root)
    else:poses()
