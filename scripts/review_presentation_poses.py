"""Review shipped meshes posed by the compiled Unity choreography; not in-game captures."""
from pathlib import Path
import json,math,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import presentation_mesh as m
ROOT=m.ROOT;OUT=ROOT/'dist';FONT='C:/Windows/Fonts/segoeui.ttf'
VERSION=json.loads((ROOT/'package/manifest.json').read_text())['version_number']
poses=[('mobile','Hover / neutral',dict(Unfold=0,Time=1)),('mobile','Forward flight',dict(Unfold=0,Time=1,Forward=.9)),('mobile','Boost / bank',dict(Unfold=0,Time=1,Forward=1,Strafe=.5,Turn=.4,Boost=1)),('mobile','Air brake',dict(Unfold=0,Time=1,Forward=.2,Brake=1)),('mobile','Needlefire / left',dict(Unfold=0,Time=1,Aim=1,LeftKick=1)),('mobile','Aim / upward',dict(Unfold=0,Time=1,Aim=1,Pitch=-55)),('mobile','Aim / downward',dict(Unfold=0,Time=1,Aim=1,Pitch=55)),('mobile','Flight / rear',dict(Unfold=0,Time=1,Forward=.7,Strafe=-.4)),('siege','Deployment / first stage',dict(Unfold=.25,Time=1)),('siege','Deployment / racks',dict(Unfold=.65,Time=1)),('siege','Lance / recoil',dict(Unfold=1,Time=1,LaserKick=1,Pitch=-12)),('siege','Aegis / brace',dict(Unfold=1,Time=1,Defense=1,HaloPhase=12)),('siege','Onslaught / launch',dict(Unfold=1,Time=1,Salvo=1,MissileKick=1,HatchAges=[.15 if i%9==0 else -1 for i in range(72)])),('siege','Launcher / open',dict(Unfold=1,Time=1,HatchAges=[.15 if i==0 else -1 for i in range(72)])),('siege','Fortress / rear',dict(Unfold=1,Time=1)),('siege','Lance / upward',dict(Unfold=1,Time=1,Pitch=-40))]
frames=m.export([dict(Form=a,Input=p) for a,label,p in poses]);report=[]
for (asset,label,p),frame in zip(poses,frames):
 bones,parts=m.load(asset);posed=m.pose(bones,parts,frame);v=np.concatenate([v for p,v,n in posed]);assert np.isfinite(v).all()
 ratios=[]
 for part,pv,pn in posed:
  ids=part['t'][:,[0,1]];before=np.linalg.norm(part['v'][ids[:,0]]-part['v'][ids[:,1]],axis=1);after=np.linalg.norm(pv[ids[:,0]]-pv[ids[:,1]],axis=1)/frame['Scale'];mask=before>1e-5
  ratios.extend((after[mask]/before[mask]).tolist())
 assert max(ratios)<(3 if asset=='mobile' else 1.015),(label,max(ratios));report.append(dict(pose=label,bounds=[v.min(axis=0).tolist(),v.max(axis=0).tolist()],max_edge_stretch=max(ratios)))
 print('AUDIT',label,round(max(ratios),3),flush=True)
for group in ['mobile','siege']:
 if '--siege-motion-only' in sys.argv:continue
 sheet=Image.new('RGB',(1600,1240),'#0a101a');draw=ImageDraw.Draw(sheet)
 for index,((asset,label,p),frame) in enumerate([(a,b) for a,b in zip(poses,frames) if a[0]==group]):
  im=m.render(asset,(400,550),yaw=155 if 'rear' in label else 25,pitch=6,frame=frame)
  x=index%4*400;y=index//4*620;sheet.paste(im.convert('RGB'),(x,y));draw.text((x+14,y+557),label,font=ImageFont.truetype(FONT,17),fill='#c8deef')
 draw.text((18,1210),'Shipped mesh + compiled animation channels / studio lighting / offline pose review',font=ImageFont.truetype(FONT,16),fill='#7fa1bd')
 sheet.save(OUT/f'IvoryBastion-{VERSION}-{group}-poses.png');print('SHEET',group,flush=True)
# Fixed camera throughout each loop: posing never causes artificial zooming.
for asset,count in [('mobile',36),('siege',36)]:
 if '--siege-motion-only' in sys.argv and asset!='siege':continue
 requests=[]
 for i in range(count):
  t=i/12
  if asset=='mobile':
   wave=(1-math.cos(t/count*12*math.tau))*.5
   p=dict(Time=t,Unfold=0,Forward=wave,Strafe=math.sin(t/3*math.tau)*.5,Turn=math.sin(t/3*math.tau)*.25,Boost=wave,Aim=max(0,math.sin(t*math.pi))*.9,LeftKick=max(0,math.sin(t*math.pi*6))*.45,RightKick=max(0,-math.sin(t*math.pi*6))*.45,Brake=max(0,-math.sin(t/3*math.tau))*.4)
  else:
   ages=[-1]*72
   for k in range(7):
    shot=1.4+k*.17
    if 0<=t-shot<.72:ages[k]=t-shot
   p=dict(Time=t,Unfold=min(1,t/.8),LaserKick=math.exp(-(t-1.15)*7) if t>=1.15 else 0,Salvo=max(0,min(1,(t-1.4)*5)),HatchAges=ages,HaloPhase=t*5)
  requests.append(dict(Form=asset,Input=p))
 animation=m.export(requests);images=[]
 for i,frame in enumerate(animation):
  im=m.render(asset,(560,640),yaw=25,pitch=6,frame=frame,view=(0,.95,0,1.3) if asset=='mobile' else (0,1.65,.35,3.7))
  d=ImageDraw.Draw(im);d.text((16,15),'SERAPH / '+('FLIGHT' if asset=='mobile' else 'DEPLOY + FIRE'),font=ImageFont.truetype(FONT,18),fill='#c8deef');d.text((16,612),'Runtime mesh / offline choreography preview',font=ImageFont.truetype(FONT,13),fill='#7fa1bd')
  images.append(im.convert('RGB'));print('FRAME',asset,i,flush=True)
 images[0].save(OUT/f'IvoryBastion-{VERSION}-{asset}-motion.gif',save_all=True,append_images=images[1:],duration=83,loop=0,optimize=False)
# The Engineering flight card uses a genuine posed model rather than an idle duplicate.
m.render('mobile',(768,512),yaw=38,frame=frames[2],transparent=True).save(ROOT/'assets/art_skirmisher.png')
(ROOT/'.build/presentation-pose-review.json').write_text(json.dumps(report,indent=2));print('All pose and motion previews complete.',flush=True)
