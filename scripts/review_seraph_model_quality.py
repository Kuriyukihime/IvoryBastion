"""Same-camera model evidence against the actual published 0.32 meshes.

Both columns use the identical offline renderer, exposure, camera and crop.
These are authored runtime mesh renders, not generated concept art or game captures.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import presentation_mesh as renderer

ROOT=Path(__file__).resolve().parents[1]
BASELINE=ROOT/'.build/review-033-baseline'
OUT=ROOT/'dist/IvoryBastion-0.33.0-model-review'


def font(size):
    for name in ['C:/Windows/Fonts/segoeuib.ttf','C:/Windows/Fonts/arial.ttf']:
        if Path(name).exists(): return ImageFont.truetype(name,size)
    return ImageFont.load_default()


def draw_form(root, asset, yaw, view, size):
    renderer.ROOT=root
    renderer.load.cache_clear()
    return renderer.render(asset,size=size,yaw=yaw,pitch=5,view=view)


def compare(name,asset,yaw,view,size,detail):
    before=draw_form(BASELINE,asset,yaw,view,size)
    after=draw_form(ROOT,asset,yaw,view,size)
    w,h=size
    sheet=Image.new('RGB',(w*2,h+146),(10,14,20));d=ImageDraw.Draw(sheet)
    sheet.paste(before,(0,80));sheet.paste(after,(w,80))
    d.text((22,13),'PUBLISHED 0.32.0',font=font(27),fill=(185,200,215))
    d.text((w+22,13),'REVIEW 0.33.0',font=font(27),fill=(105,192,255))
    d.text((22,49),'Identical camera, lighting and scale',font=font(17),fill=(134,151,170))
    d.text((w+22,49),'Actual exported model geometry',font=font(17),fill=(134,151,170))
    d.line((w,0,w,h+100),fill=(58,75,92),width=2)
    d.text((22,h+94),detail,font=font(20),fill=(212,221,231))
    d.text((22,h+122),'Offline mesh study; studio PBR response approximates Unity materials. In-game lighting may differ.',font=font(15),fill=(134,151,170))
    path=OUT/(name+'.png');sheet.save(path)
    after.save(OUT/(name+'-review-only.png'))
    delta=np.abs(np.asarray(before.convert('RGB'),dtype=float)-np.asarray(after.convert('RGB'),dtype=float))
    return dict(file=path.name,asset=asset,yaw=yaw,view=view,changed_pixels_fraction=float((delta.max(axis=2)>8).mean()))


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    jobs=[
        ('mobile-front','mobile',24,(0,.92,0,.97),(760,1120),'Layered crown, swept pauldrons, fitted visor and stepped gauntlet rails.'),
        ('mobile-rear','mobile',158,(0,.92,0,.97),(760,1120),'Rear crown and pauldron shells preserve detail from the gameplay camera.'),
        ('mobile-side','mobile',90,(0,.92,0,.97),(760,1120),'Side profile: unchanged body proportions and sockets, layered head and shoulder armor.'),
        ('mobile-armor-detail','mobile',22,(0,1.365,.015,.365),(800,820),'The head and shoulder assemblies are rebuilt, with the approved body proportions retained.'),
        ('mobile-gameplay-distance','mobile',158,(0,.92,0,1.12),(420,600),'Same-scale distant view: silhouette and rear armor, without closeup magnification.'),
        ('siege-front','siege',25,(0,1.28,.36,1.92),(1050,900),'All 72 forward-facing missile doors have depth, split armor, hinges and blue side indicators.'),
        ('siege-rear','siege',160,(0,1.28,.05,1.92),(1050,900),'Compact rear Skyfall silos and all original launcher clearances are preserved.'),
        ('siege-gameplay-distance','siege',160,(0,1.28,.05,1.96),(620,500),'Same-scale distant rear view; launchers and throne retain their approved proportions.'),
        ('siege-battery-detail','siege',12,(1.13,1.55,.55,.66),(800,880),'Mechanical missile hatch framing replaces the thin white covers; hinge positions are unchanged.'),
    ]
    report=[compare(*job) for job in jobs]
    renderer.ROOT=ROOT;renderer.load.cache_clear()
    (OUT/'comparison.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2))


if __name__=='__main__': main()
