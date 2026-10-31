"""Offline layout preview using meshes exported by the actual gauge geometry."""
from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).resolve().parent.parent
data=json.loads((root/'.build/seraph-defense-preview.json').read_text(encoding='utf-8'))
scale=2
im=Image.new('RGB',(840*scale,470*scale),(15,24,34));d=ImageDraw.Draw(im)
def font(size,bold=False):return ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf' if bold else 'C:/Windows/Fonts/segoeui.ttf',round(size*scale))
def text(x,y,value,color=(221,232,244),size=12,bold=False):d.text((x*scale,y*scale),value,font=font(size,bold),fill=color)
def rect(bounds,color):d.rectangle(tuple(round(v*scale) for v in bounds),fill=color)
def mesh(value,x,y):
    verts=value['vertices'];colors=value['colors'];triangles=value['triangles'];height=value['height']
    for k in range(0,len(triangles),3):
        ids=triangles[k:k+3];c=colors[ids[0]]
        d.polygon([(round((x+verts[i][0])*scale),round((y+height-verts[i][1])*scale)) for i in ids],fill=tuple(c[:3]))
text(36,20,'IVORY BASTION  /  DEFENSE STATUS',size=18,bold=True)
text(36,49,'Offline UI layout preview — not an in-game screenshot',size=11,color=(139,162,184))
for row,(caption,shield,aegis) in enumerate([
    ('BOTH DEFENSE LAYERS ACTIVE',True,True),
    ('SHIELD RECHARGING BEHIND AEGIS',False,True),
    ('AEGIS DEPLETED · SHIELD STILL READABLE',True,False)]):
    y=90+row*116;x=88
    text(36,y,caption,size=10,bold=True,color=(129,151,172))
    text(x,y+18,'SERAPH  '+('340 / 500' if shield else '0 / 500'),size=10,bold=True)
    text(x+471,y+18,'AEGIS  '+('320' if aegis else '—'),color=(255,212,92),size=10,bold=True)
    rect((x,y+34,x+576,y+39),(44,34,14));rect((x,y+41,x+576,y+50),(20,43,69))
    if aegis:mesh(data['aegis'],x+.75,y+34.75)
    if shield:mesh(data['shield'],x+.75,y+41.75)
    # Native XP and health are separate and remain fully visible below.
    rect((x+65,y+57,x+576,y+61),(40,53,65));rect((x+65,y+57,x+410,y+61),(158,220,224))
    rect((x,y+65,x+576,y+91),(38,67,40));rect((x,y+65,x+442,y+91),(135,191,79))
    text(x+255,y+67,'768 / 1000',size=13,bold=True,color=(250,253,246))
    text(x-48,y+68,'Lv. 18',size=11,bold=True)
text(36,449,'White / blue: Seraph Shield       Yellow / black: independent Aegis Barrier',size=11,color=(156,179,199))
out=root/'dist/IvoryBastion-0.24.0-defense-ui-preview.png';im.resize((840,470),Image.Resampling.LANCZOS).save(out);print(out)
