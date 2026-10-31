"""Actual runtime particle export + shipped atlas; no inferred in-game lighting."""
import json
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'dist/IvoryBastion-0.33.0-review/animation'
DATA=json.loads((ROOT/'.build/review-033-explosion-comparison.json').read_text())
ATLAS=Image.open(ROOT/'assets/missile_bluefire.png').convert('RGBA')
TEXTURES=[None]+[Image.open(ROOT/f'assets/missile_{name}.png').convert('RGBA') for name in ['smoke','ember','flash']]
FONT=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
SMALL=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',14)
WIDTH,HEIGHT=430,380

def composite(record,cluster=False):
    radius=record['radius'];scale=(WIDTH-80)/(radius*(3.2 if cluster else 2))
    canvas=Image.new('RGB',(WIDTH,HEIGHT),'#182332');d=ImageDraw.Draw(canvas)
    # The visible backplate and tick guides make lost contrast measurable;
    # these are preview guides rather than claimed game content.
    for x in range(20,WIDTH,40):d.line((x,30,x,HEIGHT-46),fill='#283a4d')
    for y in range(30,HEIGHT-40,40):d.line((20,y,WIDTH-20,y),fill='#283a4d')
    d.rectangle((WIDTH*.44,HEIGHT*.29,WIDTH*.56,HEIGHT*.68),fill='#596b7c',outline='#b6c4d0',width=2)
    pixels=np.asarray(canvas,dtype=float)/255;origin=np.array([WIDTH/2,HEIGHT*.49])
    positions=[(0,0,0)] if not cluster else [(-radius*.42,0,radius*.1),(radius*.4,radius*.03,-radius*.15),(0,-radius*.06,0)]
    particles=[]
    for delta in positions:
        for sample in record['samples']:
            s=dict(sample);s['position']=(np.array(sample['position'])+delta).tolist();particles.append(s)
    particles.sort(key=lambda s:(0 if s['layer']==1 else 1 if s['layer']==0 else 2,-s['position'][2]))
    for sample in particles:
        layer=sample['layer']
        if layer==0:
            f=min(31,int(sample['age']*32));sprite=ATLAS.crop(((f%8)*256,(f//8)*256,(f%8+1)*256,(f//8+1)*256))
        else:sprite=TEXTURES[layer]
        size=max(1,round(sample['size']*scale))
        sprite=sprite.resize((size,size),Image.Resampling.LANCZOS).rotate(-sample['rotation'],resample=Image.Resampling.BICUBIC,expand=True)
        rgba=np.asarray(sprite,dtype=float)/255;rgba[...,3]*=sample['alpha']
        if layer==2:rgba[...,:3]*=[.18,.57,1]
        if layer==3:rgba[...,:3]*=[.72,.90,1]
        x,y,z=sample['position'];cx,cy=origin+np.array([x,-y+z*.2])*scale
        left,top=int(cx-sprite.width/2),int(cy-sprite.height/2)
        x0,y0=max(0,left),max(0,top);x1,y1=min(WIDTH,left+sprite.width),min(HEIGHT,top+sprite.height)
        if x1<=x0 or y1<=y0:continue
        patch=rgba[y0-top:y1-top,x0-left:x1-left];alpha=patch[...,3:];old=pixels[y0:y1,x0:x1]
        pixels[y0:y1,x0:x1]=np.clip(old+patch[...,:3]*alpha if layer>=2 else old*(1-alpha)+patch[...,:3]*alpha,0,1)
    canvas=Image.fromarray(np.uint8(pixels*255));d=ImageDraw.Draw(canvas)
    d.text((14,12),f"{['NORMAL','HEAVY','SKYFALL'][record['tier']]} / {record['time']:.2f}s",font=FONT,fill='#deebf7')
    d.text((14,HEIGHT-34),f"{radius:g}m radius / {len(record['samples'])} visible particles",font=SMALL,fill='#9dbbd2')
    return canvas

def pick(review,tier,time,active=1):
    return min((r for r in DATA if r['review']==review and r['tier']==tier and r['active']==active),key=lambda r:abs(r['time']-time))

OUT.mkdir(parents=True,exist_ok=True)
sheet=Image.new('RGB',(WIDTH*4,HEIGHT*3+140),'#0a101a');d=ImageDraw.Draw(sheet)
d.text((18,16),'MISSILE IMPACT READABILITY / actual texture and particle study',font=FONT,fill='#c7e3f8')
for col,label in enumerate(['PUBLISHED / ignition','REVIEW / ignition','PUBLISHED / overlapping aftermath','REVIEW / overlapping aftermath']):
    d.text((col*WIDTH+14,53),label,font=SMALL,fill='#8fcef5' if col%2 else '#a1b7c8')
for tier in range(3):
    for col in range(4):
        review=col%2==1;cluster=col>=2;record=pick(review,tier,.22 if not cluster else .55,24 if cluster else 1)
        sheet.paste(composite(record,cluster),(col*WIDTH,82+tier*HEIGHT))
d=ImageDraw.Draw(sheet)
d.text((18,HEIGHT*3+96),'Full fireball size preserved. Right columns show three local impacts with the 24-active-effects budget.',font=SMALL,fill='#b5c8da')
d.text((18,HEIGHT*3+118),'Offline alpha composite with a contrast reference; Unity bloom, lighting and scene depth are not reproduced.',font=SMALL,fill='#7e96ab')
sheet.save(OUT/'explosions-before-after.png');print('Wrote',OUT/'explosions-before-after.png')
