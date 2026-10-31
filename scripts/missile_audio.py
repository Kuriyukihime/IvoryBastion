"""Original layered launcher and detonation cues, generated without recordings."""
import math,wave
import numpy as np
from scipy.signal import butter,sosfilt

def generate(folder,rate,stats):
    rng=np.random.default_rng(73193)
    def filtered_noise(length,low,high):
        return sosfilt(butter(2,[low,high],btype='bandpass',fs=rate,output='sos'),rng.normal(0,1,length))
    def save(name,t,signal,peak):
        signal*=np.minimum(t*800,1)*np.minimum((t[-1]-t)*80,1)
        signal=sosfilt(butter(2,30,btype='highpass',fs=rate,output='sos'),signal)
        signal*=peak/max(.001,np.max(np.abs(signal)))
        with wave.open(str(folder/('sfx_'+name+'.wav')),'wb') as f:
            f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes((signal*32767).astype('<i2').tobytes())
        stats[name]={'seconds':len(t)/rate,'peak':float(np.max(np.abs(signal))),'rms':float(np.sqrt(np.mean(signal**2))),'design':'mechanical launch / layered ion detonation'}
    for heavy in [False,True]:
        duration=.65 if heavy else .38;t=np.arange(int(rate*duration))/rate
        air=filtered_noise(len(t),180,5600);body=filtered_noise(len(t),45,420)
        phase=np.cumsum((65 if heavy else 95)+(65 if heavy else 100)*np.exp(-t*28))*2*math.pi/rate
        thud=np.sin(phase)*np.exp(-t*(12 if heavy else 21))
        clamp=filtered_noise(len(t),1600,7600)*np.exp(-t*110)
        rocket=air*(1-np.exp(-t*60))*np.exp(-t*(5 if heavy else 10))
        signal=thud*.65+clamp*.32+rocket*.62+body*np.exp(-t*8)*.28
        delay=int(rate*.032);signal[delay:]+=signal[:-delay]*.13
        save('heavy' if heavy else 'missile',t,signal,.70 if heavy else .50)
        duration=1.25 if heavy else .58;t=np.arange(int(rate*duration))/rate
        body=filtered_noise(len(t),40,650);crack=filtered_noise(len(t),1000,8500);rubble=filtered_noise(len(t),180,2200)
        phase=np.cumsum((46 if heavy else 70)+110*np.exp(-t*45))*2*math.pi/rate
        pressure=np.sin(phase)*np.exp(-t*(5 if heavy else 11))
        signal=pressure*.88+body*.9*np.exp(-t*(4.5 if heavy else 10))+crack*.65*np.exp(-t*70)
        signal+=rubble*.45*(1-np.exp(-t*80))*np.exp(-t*(3.4 if heavy else 8))*(.8+.2*np.sin(t*93)**2)
        dry=signal.copy()
        for delay,gain in [(.047,.15),(.093,.09)]:
            n=int(rate*delay);signal[n:]+=dry[:-n]*gain
        save('explosion' if heavy else 'impact',t,signal,.88 if heavy else .66)
