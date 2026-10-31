"""Original blue-white electrical chain accents, respecting the effects bus."""
from pathlib import Path
import json
import numpy as np
from scipy.signal import butter,sosfilt
from scipy.io import wavfile
ROOT=Path(__file__).resolve().parents[1];SR=48000
rng=np.random.default_rng(2471);report={}
for name,duration in [('mine_chain_prime',.16),('mine_chain_burst',.48)]:
    t=np.arange(round(duration*SR))/SR;noise=rng.normal(0,1,len(t))
    if name.endswith('prime'):
        phase=2*np.pi*(390*t+2200*t*t);envelope=np.sin(np.pi*np.minimum(1,t/duration))**.65
        noise=sosfilt(butter(2,[900,5000],btype='bandpass',fs=SR,output='sos'),noise)
        x=(.45*np.sin(phase)+.12*np.sin(phase*2.01)+noise*.18)*envelope
    else:
        phase=2*np.pi*(63*t+72*.07*(1-np.exp(-t/.07)))
        low=np.sin(phase)*np.exp(-t*9);crackle=sosfilt(butter(2,[500,6500],btype='bandpass',fs=SR,output='sos'),noise)
        snap=np.sin(2*np.pi*(880*t+1100*t*t))*np.exp(-t*35)
        x=.65*low+.14*crackle*np.exp(-t*24)+.19*snap
    fade=min(300,len(x)//5);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade);x*=.78/max(np.max(np.abs(x)),.001)
    wavfile.write(ROOT/f'assets/sfx_{name}.wav',SR,np.int16(x*32767));report[name]=dict(seconds=duration,rate=SR,peak=float(np.max(np.abs(x))),rms=float(np.sqrt(np.mean(x*x))))
(ROOT/'assets/mine-chain-audio-info.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
