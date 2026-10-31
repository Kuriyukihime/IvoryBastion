"""Subtle synthetic finish over a predominantly natural female performance."""
from pathlib import Path
import hashlib,json,math,wave
import numpy as np
from scipy.signal import butter,sosfilt,resample_poly
from scipy.ndimage import maximum_filter1d,uniform_filter1d

ROOT=Path(__file__).resolve().parents[1];ASSETS=ROOT/'assets';RATE=48000
def trim_accepted(name,edit):
    """Keep the approved rendered performance byte-for-byte before its end fade."""
    source=ASSETS/'source'/edit['processed_source']
    if hashlib.sha256(source.read_bytes()).hexdigest()!=edit['processed_source_sha256']:
        raise RuntimeError('The accepted performance changed; its end trim must be reviewed.')
    with wave.open(str(source),'rb') as f:
        assert f.getframerate()==RATE and f.getnchannels()==1 and f.getsampwidth()==2
        original=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2')
    pcm=original[:round(edit['end_seconds']*RATE)].copy()
    fade=round(edit['end_fade_seconds']*RATE)
    if fade>0:pcm[-fade:]=np.rint(pcm[-fade:].astype(float)*np.linspace(1,0,fade)).astype('<i2')
    assert np.array_equal(pcm[:len(pcm)-fade],original[:len(pcm)-fade]),'Accepted performance changed before end fade'
    x=pcm.astype(float)/32768
    rms=np.sqrt(uniform_filter1d(x*x,size=round(RATE*.03)))
    active=np.flatnonzero(rms>max(.002,rms.max()*.035))
    active_seconds=(active[-1]-active[0])/RATE
    minimum=edit.get('minimum_active_seconds',.8)
    assert active_seconds>=minimum,f'Authored voice must retain {minimum:.2f}s of active performance'
    output=ASSETS/f'sfx_voice_{name}.wav'
    with wave.open(str(output),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(RATE);f.writeframes(pcm.tobytes())
    return dict(name=name,seconds=len(pcm)/RATE,active_seconds=active_seconds,peak=float(max(np.abs(x))),rms=float(np.sqrt(np.mean(x*x))),sample_rate=RATE,
                processing=edit.get('processing_description','Original accepted 0.1.6 audio; end trim and 25ms release only; prefix samples unchanged'),performance_edit=edit)

def main(names=('mobile','aegis','siege')):
    stats=json.loads((ASSETS/'audio-info.json').read_text());report=[]
    for name in names:
        edit_path=ASSETS/f'source/voice_{name}_edit.json'
        edit=json.loads(edit_path.read_text(encoding='utf-8')) if edit_path.exists() else None
        source=ASSETS/'source'/(edit.get('raw_source',f'voice_{name}_raw.wav') if edit else f'voice_{name}_raw.wav')
        with wave.open(str(source),'rb') as f:
            rate=f.getframerate();channels=f.getnchannels();x=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype(float)/32768
        if channels>1:x=x.reshape(-1,channels).mean(axis=1)
        if edit:
            if hashlib.sha256(source.read_bytes()).hexdigest()!=edit['source_sha256']:
                raise RuntimeError(f'{name} source changed: review the authored performance trim before applying it.')
            if 'processed_source' in edit:
                row=trim_accepted(name,edit);stats['voice_'+name]=row;report.append(row)
                (ROOT/'dist'/f'IvoryBastion-voice-{name}.wav').write_bytes((ASSETS/f'sfx_voice_{name}.wav').read_bytes())
                continue
            x=x[round(edit['start_seconds']*rate):round(edit['end_seconds']*rate)]
        rms=np.sqrt(uniform_filter1d(x*x,size=max(1,int(rate*.03))))
        active=np.flatnonzero(rms>max(.002,rms.max()*.035))
        if not len(active):raise RuntimeError('Silent voice '+name)
        start=max(0,active[0]-int(rate*.06));end=min(len(x),active[-1]+int(rate*.12));x=x[start:end]
        active_seconds=(active[-1]-active[0])/rate
        minimum=edit.get('minimum_active_seconds',.8) if edit else .8
        if active_seconds<minimum:raise RuntimeError(f'Voice too short: {active_seconds:.2f}s; regenerate, do not pad silence.')
        divisor=math.gcd(rate,RATE);x=resample_poly(x,RATE//divisor,rate//divisor)
        x=sosfilt(butter(2,75,btype='highpass',fs=RATE,output='sos'),x)
        x=sosfilt(butter(2,12500,btype='lowpass',fs=RATE,output='sos'),x)
        envelope=np.sqrt(uniform_filter1d(x*x,size=int(RATE*.025)))
        gain=np.minimum(1,(.11/np.maximum(envelope,.0001))**.40)
        x*=uniform_filter1d(gain,size=int(RATE*.012))
        # Low-level double/reverb adds a polished AI edge without pitch changes,
        # bit reduction, ring modulation, or a synthetic replacement of the voice.
        dry=x.copy()
        for seconds,level in ((.011,.035),(.047,.055),(.083,.025)):
            n=int(RATE*seconds);x[n:]+=dry[:-n]*level
        if edit:
            # Preserve the new take's natural pitch rise; reinforce its final
            # voiced burst instead of appending a louder sigh or an impact SFX.
            buildup=np.clip((np.arange(len(x))/RATE-(len(x)/RATE-1.15))/1.0,0,1)
            x*=10**(edit.get('final_burst_db',0)*buildup/20)
        x*=.20/max(.001,np.sqrt(np.mean(x*x)))
        peak=maximum_filter1d(np.abs(x),size=int(RATE*.006)+1)
        x*=np.minimum(1,.86/np.maximum(peak,.0001))
        fade=min(int(RATE*.012),len(x)//2);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
        output=ASSETS/f'sfx_voice_{name}.wav'
        with wave.open(str(output),'wb') as f:
            f.setnchannels(1);f.setsampwidth(2);f.setframerate(RATE);f.writeframes((x*32767).astype('<i2').tobytes())
        row=dict(name=name,seconds=len(x)/RATE,active_seconds=active_seconds,peak=float(max(np.abs(x))),rms=float(np.sqrt(np.mean(x*x))),sample_rate=RATE,processing='75Hz HP, 12.5kHz LP, gentle dynamic control, subtle short reflections, no pitch shift/distortion')
        if edit:row['performance_edit']=edit
        stats['voice_'+name]=row;report.append(row)
        (ROOT/'dist'/f'IvoryBastion-voice-{name}.wav').write_bytes(output.read_bytes())
    (ASSETS/'audio-info.json').write_text(json.dumps(stats,indent=2))
    existing=json.loads((ASSETS/'voice-info.json').read_text())
    merged={row['name']:row for row in existing}
    merged.update({row['name']:row for row in report})
    (ASSETS/'voice-info.json').write_text(json.dumps(list(merged.values()),indent=2))
    print(json.dumps(report,indent=2))
if __name__=='__main__':
    import sys
    main(tuple(sys.argv[1:]) or ('mobile','aegis','siege'))
