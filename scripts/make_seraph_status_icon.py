"""Author the original, editable vector geometry for the Seraph Shield icon."""
from pathlib import Path
from math import sin,cos,pi,sqrt
from PIL import Image,ImageDraw

def build(size=256,aegis=False):
    scale=4;n=size*scale
    image=Image.new('RGBA',(n,n));d=ImageDraw.Draw(image)
    def hexagon(cx,cy,r):return [(cx+cos(pi/3*k-pi/2)*r,cy+sin(pi/3*k-pi/2)*r) for k in range(6)]
    edge=(19,22,29,255) if aegis else (28,103,215,255)
    face=(255,207,58,255) if aegis else (239,248,255,255)
    line=(22,24,31,255) if aegis else (38,116,221,255)
    outer=hexagon(n/2,n/2,n*.45);d.polygon(outer,fill=edge)
    inner=hexagon(n/2,n/2,n*.39);d.polygon(inner,fill=face)
    mask=Image.new('L',(n,n));ImageDraw.Draw(mask).polygon(inner,fill=255)
    cells=Image.new('RGBA',(n,n));cd=ImageDraw.Draw(cells)
    r=n*.085
    for row in range(-1,9):
        for col in range(-1,9):
            x=col*r*sqrt(3)+(row%2)*r*sqrt(3)/2;y=row*r*1.5
            points=hexagon(x,y,r)
            cd.line(points+[points[0]],fill=line,width=round(n*.009),joint='curve')
    image.alpha_composite(Image.composite(cells,Image.new('RGBA',(n,n)),mask))
    # A broad central keystone keeps the status readable at native buff size.
    d=ImageDraw.Draw(image);d.polygon(hexagon(n/2,n/2,n*.16),fill=edge if aegis else (25,91,188,255))
    d.polygon(hexagon(n/2,n/2,n*.085),fill=(255,239,170,255) if aegis else (247,252,255,255))
    return image.resize((size,size),Image.Resampling.LANCZOS)

if __name__=='__main__':
    output=Path(__file__).resolve().parent.parent/'assets/buff_seraphshield.png'
    build().save(output);print(output)
    output=output.with_name('buff_aegisbarrier.png');build(aegis=True).save(output);print(output)
