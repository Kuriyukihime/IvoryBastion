"""Offline composite using runtime-exported particle positions and shipped textures."""
from pathlib import Path
import json, math
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
WIDTH, HEIGHT = 440, 350
atlas = Image.open(ROOT/'assets/missile_bluefire.png').convert('RGBA')
textures = [None] + [Image.open(ROOT/f'assets/missile_{name}.png').convert('RGBA') for name in ['smoke','ember','flash']]
data = json.loads((ROOT/'.build/detonation-geometry.json').read_text())


def composite(record):
    r = record['radius']; scale = (WIDTH-70)/(r*2)
    canvas = Image.new('RGB',(WIDTH,HEIGHT),(27,30,36))
    draw = ImageDraw.Draw(canvas)
    # The coordinate grid and blast-radius line are preview guides, not in-game VFX.
    horizon = HEIGHT*.52
    for x in np.arange(-r,r+.1,max(2,r/3)):
        draw.line((WIDTH/2+x*scale,horizon,WIDTH/2+x*scale*1.15,HEIGHT-46),fill=(47,50,57))
    for y in range(int(horizon),HEIGHT-46,25):
        draw.line((15,y,WIDTH-15,y),fill=(47,50,57))
    origin=np.array([WIDTH/2,HEIGHT*.52])
    def project(pos):
        x,y,z=pos
        return origin+np.array([x,-y+z*.2])*scale
    pixels=np.asarray(canvas,dtype=float)/255
    # Smoke behind the body, then emissive sparks/ignition in front.
    order=sorted(record['samples'],key=lambda s:(0 if s['layer']==1 else 1 if s['layer']==0 else 2,-s['position'][2]))
    for sample in order:
        layer=sample['layer']
        if layer==0:
            f=min(31,int(sample['age']*32)); sprite=atlas.crop(((f%8)*256,(f//8)*256,(f%8+1)*256,(f//8+1)*256))
        else: sprite=textures[layer]
        size=max(1,round(sample['size']*scale))
        sprite=sprite.resize((size,size),Image.Resampling.LANCZOS).rotate(-sample['rotation'],resample=Image.Resampling.BICUBIC,expand=True)
        rgba=np.asarray(sprite,dtype=float)/255;rgba[...,3]*=sample['alpha']
        if layer==2:rgba[...,:3]*=[.18,.57,1]
        if layer==3:rgba[...,:3]*=[.72,.90,1]
        cx,cy=project(sample['position']);left=int(cx-sprite.width/2);top=int(cy-sprite.height/2)
        x0,y0=max(0,left),max(0,top);x1,y1=min(WIDTH,left+sprite.width),min(HEIGHT,top+sprite.height)
        if x1<=x0 or y1<=y0:continue
        patch=rgba[y0-top:y1-top,x0-left:x1-left];a=patch[...,3:]
        old=pixels[y0:y1,x0:x1]
        pixels[y0:y1,x0:x1]=np.clip(old+(patch[...,:3]*a) if layer>=2 else old*(1-a)+patch[...,:3]*a,0,1)
    canvas=Image.fromarray(np.uint8(pixels*255));draw=ImageDraw.Draw(canvas)
    bar_y=HEIGHT-36
    draw.line((35,bar_y,WIDTH-35,bar_y),fill=(125,158,199),width=1)
    for x in [35,WIDTH-35]:draw.line((x,bar_y-4,x,bar_y+4),fill=(125,158,199))
    draw.text((14,12),f"{['REGULAR','HEAVY','SKYFALL'][record['tier']]}  /  {record['time']:.3f}s",fill=(222,231,242))
    draw.text((WIDTH/2-70,HEIGHT-22),f"{r*2:g}m damage diameter",fill=(190,211,238))
    return canvas


sheet=Image.new('RGB',(WIDTH*3,HEIGHT*3+56),(13,16,22))
ImageDraw.Draw(sheet).text((14,15),'BLUE COMBUSTION DETONATION | OFFLINE PARTICLE / TEXTURE COMPOSITE | NOT AN IN-GAME CAPTURE',fill=(217,231,251))
for tier in range(3):
    for col,t in enumerate([.10,.22,.8]):
        record=min((r for r in data if r['tier']==tier),key=lambda r:abs(r['time']-t))
        sheet.paste(composite(record),(col*WIDTH,56+tier*HEIGHT))
sheet.save(ROOT/'dist/IvoryBastion-0.1.34-blue-detonation-preview.png')
print('Rendered three explosion sizes at ignition, full expansion and cooling.')
