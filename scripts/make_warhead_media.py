"""Original Skyfall heavy launch/pressure-blast audio and four-quadrant icon."""
from pathlib import Path
import json, math, wave
import numpy as np
from scipy.signal import butter, sosfilt
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'assets'
RATE=48000

def generate():
    rng=np.random.default_rng(19624)
    stats=json.loads((ASSETS/'audio-info.json').read_text())
    def noise(t,lo,hi):
        return sosfilt(butter(2,[lo,hi],btype='bandpass',fs=RATE,output='sos'),rng.normal(size=len(t)))
    def save(name,t,x):
        x=sosfilt(butter(2,32,btype='highpass',fs=RATE,output='sos'),x)
        x*=np.minimum(t*500,1)*np.minimum((t[-1]-t)*40,1)
        x*=.86/max(np.abs(x))
        with wave.open(str(ASSETS/f'sfx_{name}.wav'),'wb') as f:
            f.setnchannels(1);f.setsampwidth(2);f.setframerate(RATE);f.writeframes((x*32767).astype('<i2').tobytes())
        stats[name]={'seconds':len(t)/RATE,'peak':float(max(np.abs(x))),'rms':float(np.sqrt(np.mean(x*x))),'design':'original layered siege rocket / pressure wave'}
    t=np.arange(int(RATE*1.15))/RATE
    ignition=np.sin(2*math.pi*(95*t-22*t*t))*np.exp(-t*11)
    exhaust=noise(t,75,1700)*(1-np.exp(-t*42))*np.exp(-t*3.2)
    clamp=noise(t,1500,6800)*np.exp(-t*65)
    save('siege_launch',t,ignition*.8+exhaust*1.3+clamp*.35)
    t=np.arange(int(RATE*2.4))/RATE
    phase=np.cumsum(43+105*np.exp(-t*35))*2*math.pi/RATE
    pressure=np.sin(phase)*np.exp(-t*3.1)
    body=noise(t,40,700)*np.exp(-t*2.5)
    crack=noise(t,1200,8000)*np.exp(-t*65)
    debris=noise(t,140,2400)*(1-np.exp(-t*24))*np.exp(-t*2.0)
    x=pressure+body*1.2+crack*.7+debris*.5
    dry=x.copy()
    for seconds,gain in ((.09,.22),(.19,.13),(.31,.07)):
        n=int(RATE*seconds);x[n:]+=dry[:-n]*gain
    save('siege_impact',t,x)
    (ASSETS/'audio-info.json').write_text(json.dumps(stats,indent=2))

    size=1024;im=Image.new('RGBA',(size,size),'#080f19');d=ImageDraw.Draw(im)
    def line(points,color,width):d.line([(int(x*4),int(y*4)) for x,y in points],fill=color,width=int(width*4),joint='curve')
    def polygon(points,color):d.polygon([(int(x*4),int(y*4)) for x,y in points],fill=color)
    polygon([(12,24),(232,24),(244,12),(244,232),(232,244),(12,244)],'#101f31')
    line([(24,218),(24,38),(40,22),(218,22)],'#428cff',3)
    for angle in (45,135,225,315):
        a=math.radians(angle);c,s=math.cos(a),math.sin(a)
        def rotate(x,y):return (128+x*c-y*s,128+x*s+y*c)
        points=[rotate(x,y) for x,y in [(37,0),(57,-13),(91,-13),(105,-23),(101,-5),(109,0),(101,5),(105,23),(91,13),(57,13)]]
        polygon(points,'#ebf3ff');line([rotate(62,0),rotate(91,0)],'#080f19',5)
        line([rotate(114,0),rotate(124,0)],'#428cff',5)
    d.ellipse((446,446,578,578),fill='#428cff')
    im.resize((256,256),Image.Resampling.LANCZOS).save(ASSETS/'saturation.png')
    print('Generated two Skyfall cues and the four-warhead skill icon.')

if __name__=='__main__':generate()
