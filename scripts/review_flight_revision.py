"""Actual compiled flight poses; VFX are reviewed separately in the game."""
import json,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import presentation_mesh as m
ROOT=m.ROOT;VERSION=json.loads((ROOT/'package/manifest.json').read_text())['version_number'];font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)
phases=[('Boost / aligned flight',dict(Unfold=0,Time=1,Forward=.9,Boost=1)),('Blue Shift / gather',dict(Unfold=0,Time=1,PowerAge=.2,Overdrive=1)),('Blue Shift / release',dict(Unfold=0,Time=1,PowerAge=.46,Overdrive=1)),('Release / rear view',dict(Unfold=0,Time=1,PowerAge=.46,Overdrive=1)),('Fire during release',dict(Unfold=0,Time=1,PowerAge=.46,Overdrive=1,Aim=1,LeftKick=1)),('Power settles',dict(Unfold=0,Time=1,PowerAge=1.3,Overdrive=1))]
frames=m.export([dict(Form='mobile',Input=p) for label,p in phases]);sheet=Image.new('RGB',(1440,1420),'#0a101a')
for i,((label,p),frame) in enumerate(zip(phases,frames)):
 posed=m.pose(*m.load('mobile'),frame)
 for part,v,n in posed:
  assert np.isfinite(v).all() and np.isfinite(n).all()
  ids=part['t'][:,[0,1]];old=np.linalg.norm(part['v'][ids[:,0]]-part['v'][ids[:,1]],axis=1);new=np.linalg.norm(v[ids[:,0]]-v[ids[:,1]],axis=1);mask=old>1e-5;assert np.max(new[mask]/old[mask])<1.01,label
 im=m.render('mobile',(480,620),yaw=155 if i==3 else 25,pitch=5,frame=frame,view=(0,1.08,0,1.2));sheet.paste(im.convert('RGB'),(i%3*480,i//3*700));ImageDraw.Draw(sheet).text((i%3*480+14,i//3*700+634),label,font=font,fill='#c5def2')
ImageDraw.Draw(sheet).text((18,1390),'Shipped mesh / compiled pose channels / studio preview; energy effects and airflow are not shown',font=font,fill='#8ba6be')
sheet.save(ROOT/f'dist/IvoryBastion-{VERSION}-flight-poses.png');print('Pose sheet complete.',flush=True)
if '--sheet-only' in sys.argv:sys.exit(0)
requests=[dict(Form='mobile',Input=dict(Unfold=0,Time=i/12,PowerAge=i/12-.15,Overdrive=1 if i/12>=.15 else 0)) for i in range(23)]
images=[]
for i,frame in enumerate(m.export(requests)):
 im=m.render('mobile',(600,680),yaw=28,pitch=5,frame=frame,view=(0,1.06,0,1.18));d=ImageDraw.Draw(im);d.text((16,14),'BLUE SHIFT / REACTOR RELEASE POSE',font=font,fill='#c8e2f5');d.text((16,648),'Compiled mesh motion; energy effects appear in-game',font=font,fill='#8ba6be');images.append(im.convert('RGB'));print('Frame',i,flush=True)
images[0].save(ROOT/f'dist/IvoryBastion-{VERSION}-blue-shift-motion.gif',save_all=True,append_images=images[1:],duration=83,loop=0,optimize=False)
