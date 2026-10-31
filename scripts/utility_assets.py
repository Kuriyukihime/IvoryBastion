"""Author the new skill icons and electrical weapon sounds; no voice changes."""
from pathlib import Path
import math
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
from scipy.io import wavfile
from scipy.signal import butter,sosfilt,resample
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'dist/IvoryBastion-1.0.0-review'
SR=48000

def icon(name):
    scale=3;size=512*scale
    im=Image.new('RGB',(size,size),'#0b1522');d=ImageDraw.Draw(im)
    def pts(points):return [(int(x*scale),int(y*scale)) for x,y in points]
    def line(points,color,width=2):d.line(pts(points),fill=color,width=max(1,int(width*scale)),joint='curve')
    def poly(points,fill,edge=None):
        d.polygon(pts(points),fill=fill)
        if edge:line(points+[points[0]],edge,2)
    line([(38,106),(38,54),(62,30),(438,30),(478,70),(478,442),(442,478),(72,478)],'#52799b',1.5)
    line([(62,30),(157,30)],'#45adff',4)
    for y in (132,158,184):line([(38,y),(48,y)],'#a9d9ff',1.5)
    glow=Image.new('RGB',im.size);g=ImageDraw.Draw(glow)
    if name=='wingburst':
        for s in (-1,1):
            wing=[(256+s*23,286),(256+s*74,133),(256+s*187,72),(256+s*117,263),(256+s*37,341)]
            poly(wing,'#708fa9','#c9eaff');poly([(256+s*65,244),(256+s*161,102),(256+s*98,247)],'#e3f2fc')
        poly([(241,246),(256,204),(271,246),(280,340),(256,379),(232,340)],'#dcebf7','#f9fdff')
        for i in range(8):
            s=-1 if i%2==0 else 1;x=256+s*(60+(i//2)*29);y=305+(i//2)*29
            line([(x-s*8,y-15),(x+s*10,y+8)],'#55baff',8)
            line([(x,y-5),(x+s*10,y+8)],'#f4fcff',3)
            line([(x+s*10,y+8),(x+s*25,y+43)],'#3999ed',2)
        line([(256,393),(256,445)],'#4babff',3)
    else:
        center=np.array([211.,289.]);beam=np.array([.72,-.69]);side=np.array([.69,.72])
        for i in range(8):
            a=i*math.tau/8;rad=np.array([math.cos(a),math.sin(a)]);tangent=np.array([-rad[1],rad[0]])
            p=center+rad*94
            points=[p+rad*47+tangent*16,p+rad*55-tangent*8,p-rad*3-tangent*25,p-rad*13+tangent*6]
            poly([tuple(v) for v in points],'#91b4ce','#e7f5ff')
        end=np.array([422.,86.]);
        poly([tuple(center-side*24),tuple(end-side*24),tuple(end+side*24),tuple(center+side*24)],'#287acb')
        poly([tuple(center-side*10),tuple(end-side*10),tuple(end+side*10),tuple(center+side*10)],'#6cdcff')
        line([tuple(center),tuple(end)],'#ffffff',7)
        for t in (.18,.5,.8):
            p=center*(1-t)+end*t;line([tuple(p-side*34),tuple(p+side*34)],'#a4edff',2)
        d.ellipse((int((center[0]-24)*scale),int((center[1]-24)*scale),int((center[0]+24)*scale),int((center[1]+24)*scale)),fill='#f5fdff')
    im.resize((512,512),Image.Resampling.LANCZOS).save(ROOT/f'assets/{name}.png')

def source(name):
    sr,y=wavfile.read(ROOT/f'assets/sfx_{name}.wav')
    if np.issubdtype(y.dtype,np.integer):y=y.astype(np.float64)/np.iinfo(y.dtype).max
    if y.ndim>1:y=y.mean(axis=1)
    return resample(y,round(len(y)*SR/sr))

def texture(n,rng,low,high):
    return sosfilt(butter(3,[low,high],btype='bandpass',fs=SR,output='sos'),rng.normal(0,1,n))

def save(name,y,peak=.73):
    y=np.nan_to_num(y);fade=min(round(.018*SR),len(y)//4)
    y[:fade]*=np.linspace(0,1,fade)**2;y[-fade:]*=np.linspace(1,0,fade)**2
    y=np.tanh(y*1.2);y*=peak/max(np.max(np.abs(y)),1e-6)
    stereo=np.stack([y,np.roll(y,11)*.985],axis=1);stereo[-12:,1]*=np.linspace(1,0,12)
    wavfile.write(ROOT/f'assets/sfx_{name}.wav',SR,np.int16(np.clip(stereo,-1,1)*32767))
    return stereo

def audio():
    rng=np.random.default_rng(100033)
    n=round(.64*SR);t=np.arange(n)/SR;dash=source('dash');dash=resample(dash,n)
    air=texture(n,rng,550,6500);rise=np.sin(2*np.pi*(180*t+180*t*t))
    launch=dash*.6+air*.22*np.exp(-t*4)+rise*.045*np.exp(-t*7)
    save('wingburst_start',launch,.68)
if __name__=='__main__':
    OUT.mkdir(parents=True,exist_ok=True)
    icon('wingburst');audio()
    print('Authored Heavenfall Rush icon and start sound. Use make_spider_assets.py and make_chain_burst_audio.py for Widow Array.')
