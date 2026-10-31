"""Render the live VFX geometry exported by scripts/effect_study.

The wing attachment positions are schematic; no character model is fabricated.
Bloom here is only a restrained compositing aid, not a Unity shader simulation.
"""
import json
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DATA = json.loads((ROOT / ".build/effect-study.json").read_text())
S = 2
FONT = "C:/Windows/Fonts/segoeui.ttf"
BACKGROUND = (10, 17, 28, 255)
BLUE = (35, 122, 255)
WHITE = (196, 232, 255)


def label(canvas, xy, text, size=19, color=(198, 220, 243)):
    ImageDraw.Draw(canvas).text(tuple(int(v*S) for v in xy), text,
                               font=ImageFont.truetype(FONT, size*S), fill=color)


def layer(canvas, painter):
    fx = Image.new("RGBA", canvas.size)
    painter(ImageDraw.Draw(fx))
    glow = fx.filter(ImageFilter.GaussianBlur(3*S))
    glow.putalpha(glow.getchannel("A").point(lambda a: int(a*.3)))
    return Image.alpha_composite(Image.alpha_composite(canvas, glow), fx)


def point(p, center, zoom, plane="xy"):
    if plane == "xz":
        x, y = p[0], p[2]
    else:
        x, y = p[0], p[1]
    return ((center[0]+x*zoom)*S, (center[1]-y*zoom)*S)


def blue_shift(canvas, phase, center, zoom=100):
    def draw(d):
        for i, f in enumerate(phase["feathers"]):
            origin, tip = np.array(f["origin"]), np.array(f["tip"])
            d.line([point(origin, center, zoom), point(tip, center, zoom)], fill=(70, 88, 107, 180), width=3*S)
            charge, release = phase["charge"], phase["release"]
            head = origin+(tip-origin)*min(1, charge+release)
            d.line([point(origin, center, zoom), point(head, center, zoom)],
                   fill=(*BLUE, int(min(1,charge*.85+release*.55)*255)), width=3*S)
            flare = phase["release"]  # Individual stagger is already reflected in vertices.
            for j, verts in enumerate(f["layers"]):
                d.polygon([point(v,center,zoom) for v in verts],
                          fill=(*(BLUE if j == 0 else WHITE), int(flare*(.55 if j==0 else .82)*255)))
        charge, release = phase["charge"], phase["release"]
        for ring in range(5):
            radius = .14+charge*.12+release*.22 if ring==0 else .24+smooth((phase["age"]-.26)/.89)*.92
            if ring==0:
                angles = np.linspace(0,math.tau,7)+math.pi/6
            else:
                angles = np.linspace(-.56,.56,17)+(ring-1)*math.pi/2
            pts = [point((math.cos(a)*radius,math.sin(a)*radius,0),center,zoom) for a in angles]
            alpha = charge*.9+release*.48 if ring==0 else release*.65
            d.line(pts, fill=(*(WHITE if ring==0 else BLUE),int(min(1,alpha)*255)),width=3*S)
    return layer(canvas, draw)


def smooth(t):
    t = min(1,max(0,t))
    return t*t*(3-2*t)


def main():
    canvas=Image.new("RGBA",(1760*S,1250*S),BACKGROUND)
    label(canvas,(40,28),"IVORY BASTION / SERAPH EFFECT STUDY",30)
    label(canvas,(42,76),"Compiled runtime geometry · schematic attachment positions · Unity bloom/lighting are not reproduced",17,(128,157,188))
    for i,(age,title) in enumerate(((.13,"01  REACTOR GATHER"),(.47,"02  FEATHER RELEASE"),(.87,"03  CONTROLLED AFTERGLOW"))):
        phase=min(DATA["phases"],key=lambda f:abs(f["age"]-age))
        left=40+i*575
        label(canvas,(left,130),title,20)
        canvas=blue_shift(canvas,phase,(left+255,350),90)
        label(canvas,(left,522),f'{phase["age"]:.2f}s  ·  articulated petals and rear halo',16,(139,165,194))
    label(canvas,(40,585),"04  ALIGNED COMPRESSION EXHAUST",20)
    def exhaust(d):
        widths=DATA["exhaust"]
        for index,(y,scale,name) in enumerate(((665,.7,"Regular missile"),(760,1,"Heavy missile"),(870,1.28,"Skyfall warhead"))):
            origin=70
            top=[((origin+i/(len(widths)-1)*320*scale)*S,(y-w*20*scale)*S) for i,w in enumerate(widths)]
            bottom=[((origin+i/(len(widths)-1)*320*scale)*S,(y+w*20*scale)*S) for i,w in reversed(list(enumerate(widths)))]
            d.polygon(top+bottom,fill=(*BLUE,210))
            d.polygon([(origin*S,(y-6*scale)*S),((origin+230*scale)*S,y*S),(origin*S,(y+6*scale)*S)],fill=(*WHITE,240))
    canvas=layer(canvas,exhaust)
    for y,name in ((696,"Regular missile / travel-axis aligned"),(798,"Heavy missile / tapered blue combustion"),(917,"Skyfall / longer high-pressure plume")):
        label(canvas,(66,y),name,16,(139,165,194))
    label(canvas,(620,585),"05  MECHANICAL DEPLOYMENT LOCK",20)
    def deployment(d):
        for index,f in enumerate(DATA["deployment"]):
            for line in f["lines"]:
                pts=[point(p,(890,800),74,"xz") for p in line]
                d.line(pts,fill=(*(BLUE if index!=2 else WHITE),int(80+index*70)),width=(2 if index<2 else 3)*S)
    canvas=layer(canvas,deployment)
    label(canvas,(620,1024),"Nested positions show extension, not simultaneous rings.",16,(139,165,194))
    label(canvas,(1194,585),"06  PRECISE SKYFALL FOOTPRINT",20)
    def skyfall(d):
        center=(1448,805);zoom=10
        r=DATA["skyfallRadius"]*zoom
        d.ellipse(((center[0]-r)*S,(center[1]-r)*S,(center[0]+r)*S,(center[1]+r)*S),outline=(*BLUE,145),width=2*S)
        for vertices in DATA["skyfall"]:
            d.polygon([point(v,center,zoom,"xz") for v in vertices],fill=(*BLUE,135))
        d.line(((center[0]-5)*S,center[1]*S,(center[0]+5)*S,center[1]*S),fill=(*WHITE,120),width=S)
        d.line((center[0]*S,(center[1]-5)*S,center[0]*S,(center[1]+5)*S),fill=(*WHITE,120),width=S)
    canvas=layer(canvas,skyfall)
    label(canvas,(1194,1024),f'{DATA["skyfallRadius"]:g} m radius · overlapping footprints are suppressed',16,(139,165,194))
    label(canvas,(40,1094),"LASER IMPACT CORONA",19)
    def corona(d):
        for i,f in enumerate(DATA["corona"]):
            for line in f["lines"]:
                d.line([point(v,(355+i*110,1124),65) for v in line],fill=(*(WHITE if i==0 else BLUE),int((1-f["age"]/.36)*255)),width=3*S)
    canvas=layer(canvas,corona)
    label(canvas,(775,1106),"Laser: held core + traveling current + short endpoint corona.",17,(139,165,194))
    label(canvas,(775,1141),"Wing air trails remain boost-only. Blue explosion radii are unchanged.",17,(139,165,194))
    label(canvas,(40,1210),"VFX geometry study only; this is not an in-game capture.",16,(109,139,170))
    output=ROOT/"dist/IvoryBastion-0.8.0-effect-study.png"
    canvas.convert("RGB").resize((1760,1250),Image.Resampling.LANCZOS).save(output)
    frames=[]
    for p in DATA["phases"]:
        frame=Image.new("RGBA",(720*S,720*S),BACKGROUND)
        label(frame,(26,22),"BLUE SHIFT / REACTOR RELEASE",22)
        frame=blue_shift(frame,p,(360,350),160)
        label(frame,(26,660),"Actual geometry on schematic wing attachments",16,(139,165,194))
        label(frame,(26,686),f'{p["age"]:.2f}s · no character outline or coating',16,(139,165,194))
        frames.append(frame.convert("RGB").resize((720,720),Image.Resampling.LANCZOS))
    frames[0].save(ROOT/"dist/IvoryBastion-0.8.0-blue-shift-effects.gif",save_all=True,
                   append_images=frames[1:],duration=33,loop=0,optimize=False)
    print(output)


if __name__=="__main__":main()
