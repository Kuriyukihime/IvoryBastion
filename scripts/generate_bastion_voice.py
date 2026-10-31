"""Offline original voice design. Only finished WAV files ship with the mod."""
import argparse,hashlib,json,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
VOICE=('An adult woman with a clear smooth mezzo-soprano speaking voice, warm natural human resonance, '
       'a slightly smoky lower register, crisp precise American English and a sly confident smile. '
       'She portrays a highly advanced feminine artificial intelligence in a sleek combat fortress. '
       'An expressive human actress, articulate and controlled, with real breath and nuanced pitch. '
       'Clean close studio microphone, no background music, no sound effects, no robot filter. ')
LINES={
    'heavenfall':('Missile swarm engaged.',VOICE+'Deliver a bold, brisk combat announcement with delighted confidence. Emphasize MISSILE SWARM with a dangerous playful smile. The same poised female AI voice, clear words and natural flow.',832,155),
    'mines':('Defensive charges deployed. Watch your step.',VOICE+'A crisp, confident combat announcement. Emphasize DEFENSIVE CHARGES, then a sly dangerous smile on WATCH YOUR STEP. Same poised female AI voice, clear natural flow and precise words.',832,160),
    'mobile':('Combat limits released. Try to keep up.',VOICE+'Deliver a brisk confident combat challenge with playful dangerous excitement. Emphasize LIMITS RELEASED, then smile on try to keep up.',832,150),
    'aegis':('Defensive systems online. You will not break me.',VOICE+'Calm unshakable confidence under fire, firm grounded resolve. A cool defiant warning with weight on NOT BREAK ME. Speak clearly at a natural pace.',832,180),
    'siege':('Onslaught protocol engaged. Enjoy the fireworks.',VOICE+'Deliver a brisk, confident combat announcement with playful dangerous excitement. Emphasize ONSLAUGHT PROTOCOL, then smile slyly on enjoy the fireworks. Sound pleased with her overwhelming firepower. Speak clearly at a natural pace in the same poised female AI character voice.',832,150),
}
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--model',type=Path,required=True);parser.add_argument('--only',choices=LINES);args=parser.parse_args()
    os.environ['HF_HUB_OFFLINE']='1';os.environ['TRANSFORMERS_OFFLINE']='1'
    import torch,soundfile as sf
    from qwen_tts import Qwen3TTSModel
    torch.set_num_threads(8)
    device='cuda:0' if torch.cuda.is_available() else 'cpu'
    print('Loading local VoiceDesign on '+device,flush=True)
    model=Qwen3TTSModel.from_pretrained(str(args.model.resolve()),device_map=device,dtype=torch.bfloat16 if device.startswith('cuda') else torch.float32,attn_implementation='sdpa')
    folder=ROOT/'assets/source';folder.mkdir(parents=True,exist_ok=True)
    for name,(text,prompt,seed,limit) in LINES.items():
        if args.only and name!=args.only:continue
        torch.manual_seed(seed);print('Generating '+name,flush=True)
        settings=dict(non_streaming_mode=True,max_new_tokens=limit,temperature=.78,top_p=.95,repetition_penalty=1.04)
        wavs,rate=model.generate_voice_design(text=text,language='English',instruct=prompt,**settings)
        path=folder/f'voice_{name}_raw.wav';sf.write(path,wavs[0],rate,subtype='PCM_16')
        meta=dict(text=text,prompt=prompt,seed=seed,settings=settings,rate=rate,seconds=len(wavs[0])/rate,engine='Qwen3-TTS VoiceDesign',model='Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign',revision='5ecdb67327fd37bb2e042aab12ff7391903235d3',license='Apache-2.0',reference_recording=None)
        path.with_suffix('.json').write_text(json.dumps(meta,indent=2))
        if name=='siege':
            edit=dict(mode='spoken_announcement',raw_source=path.name,source_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),start_seconds=0,end_seconds=meta['seconds'],minimum_active_seconds=.8,final_burst_db=0,spoken_text=text)
            (folder/'voice_siege_edit.json').write_text(json.dumps(edit,indent=2),encoding='utf-8')
        print(name+': '+str(meta['seconds'])+'s',flush=True)
if __name__=='__main__':main()
