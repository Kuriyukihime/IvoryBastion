"""Offline speech transcription and voice pitch/level checks; no game launch."""
from pathlib import Path
import json,re
import numpy as np
import soundfile as sf
import librosa,torch
from transformers import WhisperProcessor,WhisperForConditionalGeneration
ROOT=Path(__file__).resolve().parents[1]
MODEL=ROOT.parent/'TheDiva/.build/voice-asr'
torch.set_num_threads(8)
processor=WhisperProcessor.from_pretrained(MODEL,local_files_only=True)
model=WhisperForConditionalGeneration.from_pretrained(MODEL,local_files_only=True)
report=[]
expected={'mobile':'combat limits released try to keep up','aegis':'defensive systems online you will not break me','siege':'onslaught protocol engaged enjoy the fireworks'}
# Whisper spells the American-English pronunciation of "onslaught" as "on-slot".
# Preserve its literal transcript/exact flag, and allow only this observed lexical split.
recognized_variants={'siege':'on slot protocol engaged enjoy the fireworks'}
expected.update(heavenfall='missile swarm engaged',mines='defensive charges deployed watch your step')
for name in expected:
    x,rate=sf.read(ROOT/f'assets/sfx_voice_{name}.wav')
    wave=librosa.resample(x,orig_sr=rate,target_sr=16000)
    inputs=processor(wave,sampling_rate=16000,return_tensors='pt',return_attention_mask=True)
    with torch.inference_mode():tokens=model.generate(**inputs,max_new_tokens=96,num_beams=5)
    transcript=processor.batch_decode(tokens,skip_special_tokens=True)[0]
    f0,voiced,prob=librosa.pyin(wave,fmin=80,fmax=650,sr=16000,frame_length=1024,hop_length=160)
    pitch=f0[voiced&(prob>.25)]
    row=dict(name=name,transcript=transcript,seconds=len(x)/rate,peak=float(max(np.abs(x))),voiced_median_hz=float(np.median(pitch)) if len(pitch) else None)
    if name in expected:
        words=re.findall(r'[a-z]+',transcript.lower())
        row['expected']=expected[name];row['exact']=words==expected[name].split()
        row['words_match']=row['exact'] or words==recognized_variants.get(name,'').split()
        if row['words_match'] and not row['exact']:row['recognizer_note']='On-slot is the recognizer phonetic spelling of onslaught; all other words match exactly.'
    print(json.dumps(row),flush=True);report.append(row)
(ROOT/'.build/voice-checks.json').write_text(json.dumps(report,indent=2))
assert all(row.get('words_match',False) for row in report),'A generated voice line did not match its expected spoken words'
