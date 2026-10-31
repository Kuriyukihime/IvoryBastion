"""Original icon artwork and synthesized mechanical cues. No borrowed recordings."""
from pathlib import Path
import math, wave, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.signal import butter, sosfilt
ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'assets'
WHITE='#ebf3ff'; BLUE='#428cff'; DARK='#080f19'
def icon(name):
    s=4; im=Image.new('RGBA',(256*s,256*s),DARK); d=ImageDraw.Draw(im)
    def poly(points,fill=WHITE):d.polygon([(x*s,y*s) for x,y in points],fill=fill)
    def line(points,fill=WHITE,w=10):d.line([(x*s,y*s) for x,y in points],fill=fill,width=w*s,joint='curve')
    def ellipse(box,fill=None,outline=WHITE,w=7):d.ellipse(tuple(x*s for x in box),fill=fill,outline=outline,width=w*s)
    poly([(12,24),(232,24),(244,12),(244,232),(232,244),(12,244)],'#101f31')
    line([(24,218),(24,38),(40,22),(218,22)],BLUE,3)
    if name=='pistol':
        for x,y in [(56,88),(91,139)]:poly([(x,y),(x+85,y-18),(x+103,y),(x+68,y+18),(x+49,y+62),(x+24,y+64),(x+33,y+24)]);line([(x+112,y-5),(x+140,y-12)],BLUE,7)
    elif name=='slip':
        for x in [45,100,155]:poly([(x,60),(x+53,128),(x,196),(x+25,128)],BLUE if x==155 else WHITE)
    elif name=='deploy':
        poly([(88,75),(128,48),(168,75),(168,160),(88,160)]);poly([(30,94),(72,77),(72,181),(30,181)]);poly([(184,77),(226,94),(226,181),(184,181)]);line([(49,189),(33,213),(76,213)],BLUE);line([(207,189),(223,213),(180,213)],BLUE);poly([(115,112),(141,112),(141,192),(115,192)],DARK)
    elif name=='redline':
        poly([(133,35),(77,137),(120,137),(101,220),(189,102),(143,102)],BLUE);line([(57,174),(40,128),(59,78)],WHITE,9);line([(197,77),(216,128),(198,175)],WHITE,9)
    elif name=='laser':
        poly([(33,153),(100,110),(137,143),(63,187)]);line([(116,126),(210,44)],BLUE,18);line([(119,124),(215,41)],WHITE,5);ellipse((61,124,96,159),DARK,BLUE,4)
    elif name=='protocol':
        poly([(128,35),(207,62),(196,148),(174,186),(128,220),(82,186),(60,148),(49,62)]);poly([(128,57),(184,77),(176,142),(156,173),(128,193),(100,173),(80,142),(72,77)],DARK);poly([(118,84),(138,84),(138,116),(170,116),(170,136),(138,136),(138,168),(118,168),(118,136),(86,136),(86,116),(118,116)],BLUE)
    elif name=='saturation':
        for x,y in [(55,42),(117,61),(179,42)]:poly([(x,y),(x+15,y+23),(x+15,y+94),(x-15,y+94),(x-15,y+23)]);line([(x,y+108),(x,y+130)],BLUE,7)
        line([(42,220),(70,192),(98,211),(128,184),(158,211),(186,192),(214,220)],BLUE,9)
    elif name=='factory':
        poly([(43,210),(43,117),(98,87),(98,117),(148,87),(148,117),(196,87),(196,210)]);poly([(51,48),(79,48),(79,101),(51,115)],BLUE)
        for x in [67,113,159]:poly([(x,147),(x+25,147),(x+25,178),(x,178)],DARK)
    im.resize((256,256),Image.Resampling.LANCZOS).save(ASSETS/(name+'.png'))
for name in ['pistol','slip','deploy','redline','laser','protocol','saturation','factory']:icon(name)

RATE=48000;rng=np.random.default_rng(4242);stats={}
def cue(name,duration,frequency,noise=.15,decay=4,level=.7,swelling=False,mechanical=False):
    t=np.arange(int(duration*RATE))/RATE
    env=(1-np.exp(-t*120))*np.exp(-t*decay/duration)
    if swelling:env=(.2+.8*np.sin(np.minimum(t/duration,1)*math.pi/2)**2)*np.minimum((duration-t)*15,1)
    freq=frequency*(.55+.45*np.exp(-t*9))
    phase=np.cumsum(freq)*2*math.pi/RATE
    signal=np.sin(phase)*.55+np.sin(phase*2.01)*.14+np.sin(phase*.5)*.22
    hiss=sosfilt(butter(2,[180,5200],btype='bandpass',fs=RATE,output='sos'),rng.normal(0,1,len(t)))
    signal=(signal+noise*hiss)*env
    if mechanical:
        for delay in [.03,.12,.23,.36]:
            at=t-delay;gate=np.where(at>=0,np.exp(-np.maximum(at,0)*75),0);signal+=.18*hiss*gate+.10*np.sin(2*math.pi*700*at)*gate
    if name=='protocol':
        for delay in np.arange(.25,5.9,.5):
            at=t-delay;gate=np.where(at>=0,np.exp(-np.maximum(at,0)*13),0);signal+=.13*np.sin(2*math.pi*220*at)*gate
    # A soft early reflection gives depth without harsh clipping or a long music tail.
    for delay,gain in [(.037,.15),(.079,.08)]:
        n=int(RATE*delay);signal[n:]+=signal[:-n]*gain
    signal*=np.minimum(t*160,1)*np.minimum((duration-t)*90,1)
    signal=signal/(np.max(np.abs(signal))+.001)*level
    pcm=np.int16(np.clip(signal,-.99,.99)*32767)
    with wave.open(str(ASSETS/('sfx_'+name+'.wav')),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())
    stats[name]={'seconds':duration,'peak':float(np.max(np.abs(signal))),'rms':float(np.sqrt(np.mean(signal**2)))}
cue('pistol',.16,900,.35,6,.36)
cue('laser',.65,260,.16,5,.8)
cue('missile',.30,180,.9,5,.40)
cue('heavy',.65,95,.75,3,.70)
cue('impact',.25,110,.8,7,.43)
cue('explosion',1.05,65,1.2,4,.84)
cue('protocol',6,330,.06,7,.52,mechanical=True)
cue('dash',.40,360,.65,3,.65)
cue('transform',.80,90,.55,3,.72,mechanical=True)
cue('blueshift',.9,440,.12,3,.65,swelling=True)
cue('barrage',1.1,130,.35,3,.78,mechanical=True)
cue('upgrade',.75,660,.02,3,.5)
cue('guardian',.65,140,.35,4,.6)
cue('shutdown',1.1,190,.65,4,.6,mechanical=True)
from missile_audio import generate as missile_audio
missile_audio(ASSETS,RATE,stats)
# A seamless, quiet turbine bed. Integer cycles over two seconds avoid a loop click.
t=np.arange(RATE*2)/RATE
engine=(np.sin(2*math.pi*90*t)*.28+np.sin(2*math.pi*180*t)*.13+np.sin(2*math.pi*360*t)*.06)*(1+.06*np.sin(2*math.pi*2*t))
with wave.open(str(ASSETS/'sfx_engine.wav'),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(np.int16(engine*32767).tobytes())
stats['engine']={'seconds':2,'peak':float(np.max(np.abs(engine))),'loop':True}
(ASSETS/'audio-info.json').write_text(json.dumps(stats,indent=2))

from make_package_icon import generate as package_icon
package_icon()
from make_warhead_media import generate as warhead_media
warhead_media()
from make_combat_vfx import main as combat_vfx
combat_vfx()
if all((ASSETS/f'source/voice_{name}_raw.wav').exists() for name in ('mobile','aegis','siege')):
    from finish_bastion_voice import main as finish_voice
    finish_voice()
print('Generated skill icons, original audio cues, siege warhead media, engine loop, and package icon.')

# Apply the final visual family after legacy media generators.
from make_presentation_media import main as presentation_media
presentation_media()
