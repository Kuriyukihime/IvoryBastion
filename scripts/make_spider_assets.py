"""Original articulated mines and low-profile hull launchers. One mesh per material;
no extra LOD system, external model, or gameplay collider is introduced."""
import math
import numpy as np
from PIL import Image,ImageDraw
import mesh_tools as m
PALETTE={'Ceramic':((.87,.94,1),.42,0),'Frame':((.025,.035,.055),.3,0),'Edge':((.4,.49,.6),.55,0),'MineBlue':((.13,.57,1),.25,2.3)}

def setup(bones):
    m.BONES=bones;m.BN={b[0]:i for i,b in enumerate(bones)};m.ANCHORS=np.array([b[1] for b in bones],float)
    m.PALETTE=PALETTE;m.PARTS={k:{'v':[],'t':[],'w':[]} for k in PALETTE}

def mine():
    bones=[('root',(0,0,0),None)];segments=[]
    for i in range(8):
        a=(i+.5)*math.tau/8;d=np.array([math.cos(a),0,math.sin(a)])
        hip=d*.15+[0,.18,0];knee=d*.36+[0,.24,0];toe=d*.53
        bones.extend([(f'mine_hip_{i}',hip,'root'),(f'mine_knee_{i}',knee,f'mine_hip_{i}')]);segments.append((hip,knee,toe))
    setup(bones)
    m.ellipsoid('Frame',(0,.16,0),(.25,.115,.31),m.rigid('root'),sides=32,rings=14)
    m.ellipsoid('Ceramic',(0,.21,-.015),(.235,.09,.285),m.rigid('root'),sides=40,rings=18)
    m.ellipsoid('Edge',(0,.22,.17),(.17,.035,.1),m.rigid('root'),sides=28,rings=10)
    m.ellipsoid('MineBlue',(0,.242,.2),(.125,.025,.06),m.rigid('root'),sides=28,rings=10)
    # Raised ceramic carapace seams and a single luminous forward sensor.
    for sign in (-1,1):
        points=[(sign*.14,.25,z) for z in (-.17,-.08,.03)]
        m.tube('Frame',points,[(.008,.008)]*3,m.rigid('root'),sides=8,steps=3)
    for i,(hip,knee,toe) in enumerate(segments):
        m.ellipsoid('Edge',hip,(.052,.05,.052),m.rigid(f'mine_hip_{i}'),sides=14,rings=8)
        m.tube('Ceramic',[hip,knee],[(.039,.04),(.029,.03)],m.rigid(f'mine_hip_{i}'),sides=12,steps=2,front=(0,1,0))
        m.ellipsoid('Frame',knee,(.038,.038,.038),m.rigid(f'mine_knee_{i}'),sides=14,rings=8)
        m.tube('Edge',[knee,toe],[(.027,.027),(.009,.012)],m.rigid(f'mine_knee_{i}'),sides=12,steps=2,front=(0,1,0))
    m.write_asset('spider_mine')

def launchers():
    mounts=[np.array([s*.78,.80,.46]) for s in (-1,1)]
    bones=[('root',(0,0,0),None)]+[(f'mine_hatch_{i}',tuple(p+[s*.21,.08,0]),'root') for i,(p,s) in enumerate(zip(mounts,(-1,1)))]
    setup(bones)
    for i,p in enumerate(mounts):
        # Narrow upward firing well with a flush ceramic hinged cover.
        m.ellipsoid('Frame',p,(.265,.075,.405),m.rigid('root'),sides=32,rings=10)
        m.ellipsoid('Edge',p+[0,.035,0],(.245,.035,.385),m.rigid('root'),sides=32,rings=10)
        m.ellipsoid('Frame',p+[0,.06,0],(.215,.015,.345),m.rigid('root'),sides=32,rings=8)
        m.ellipsoid('Ceramic',p+[0,.081,0],(.237,.025,.368),m.rigid(f'mine_hatch_{i}'),sides=32,rings=10)
        m.tube('MineBlue',[p+[-.12,.108,-.14],p+[.12,.108,-.14]],[(.009,.009)]*2,m.rigid(f'mine_hatch_{i}'),sides=8,steps=1)
        for z in (-.22,0,.22):m.ellipsoid('Edge',p+[(1 if i else -1)*.224,.076,z],(.025,.028,.035),m.rigid('root'),sides=12,rings=8)
    m.write_asset('mine_launcher')

def icon():
    scale=3;im=Image.new('RGB',(512*scale,512*scale),'#0b1522');d=ImageDraw.Draw(im)
    def line(points,fill,width):d.line([(int(x*scale),int(y*scale)) for x,y in points],fill=fill,width=round(width*scale),joint='curve')
    line([(38,106),(38,54),(62,30),(438,30),(478,70),(478,442),(442,478),(72,478)],'#52799b',1.5)
    line([(62,30),(157,30)],'#45adff',4)
    for r in (80,166):d.ellipse(tuple(round(v*scale) for v in (256-r,256-r,256+r,256+r)),outline='#245274',width=round(2*scale))
    for x,y,size in [(256,257,1.1),(150,152,.54),(361,153,.54),(150,361,.54),(361,361,.54)]:
        for j in range(4):
            for s in (-1,1):
                yy=(j-1.5)*21*size
                line([(x+s*21*size,y+yy*.45),(x+s*58*size,y+yy),(x+s*77*size,y+yy*1.7)],'#e6f2ff',7*size)
        d.ellipse(tuple(round(v*scale) for v in (x-33*size,y-41*size,x+33*size,y+41*size)),fill='#8da9c2',outline='#f1f9ff',width=max(1,round(2*scale)))
        d.ellipse(tuple(round(v*scale) for v in (x-18*size,y-21*size,x+18*size,y+6*size)),fill='#4fcaff')
    im.resize((512,512),Image.Resampling.LANCZOS).save(m.ROOT/'assets/mines.png')
if __name__=='__main__':mine();launchers();icon()
