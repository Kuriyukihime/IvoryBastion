"""Generate new emphatic laughter with the original fictional AI voice reference."""
import argparse
import hashlib
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--seed', type=int)
    parser.add_argument('--emotion', type=float, default=1.0)
    parser.add_argument('--reference', type=Path)
    args = parser.parse_args()
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    import numpy as np
    import soundfile as sf
    import torch
    from chatterbox.tts import ChatterboxTTS

    torch.set_num_threads(8)
    reference = args.reference or ROOT/'assets/source/voice_siege_raw.wav'
    model_path = ROOT.parent/'TheDiva/.build/chatterbox'
    print('Loading installed expressive voice model.', flush=True)
    model = ChatterboxTTS.from_local(str(model_path), 'cpu')
    folder = ROOT/'assets/source/expressive_laugh_takes'
    folder.mkdir(exist_ok=True)
    text = 'Ha ha ha ha ha ha ha ha ha ha ha ha ha ha!'
    takes = [(args.seed, args.emotion)] if args.seed is not None else [(201, 1.0), (202, 1.2), (203, .8)]
    for seed, emotion in takes:
        torch.manual_seed(seed)
        np.random.seed(seed)
        print(f'Generating expressive take {seed}.', flush=True)
        settings = dict(exaggeration=emotion, cfg_weight=.3,
                        temperature=.8, repetition_penalty=1.2)
        wav = model.generate(text, audio_prompt_path=str(reference), **settings).squeeze().cpu().numpy()
        path = folder/f'laugh_{seed}.wav'
        # The generator may return floating samples outside [-1, 1]. Preserve
        # them first and lower one fixed gain before PCM conversion, never clip.
        np.save(path.with_suffix('.npy'), wav)
        float_peak = float(np.max(np.abs(wav)))
        pcm_gain = min(1., .93/max(.00001, float_peak))
        wav = wav*pcm_gain
        sf.write(path, wav, model.sr, subtype='PCM_16')
        meta = dict(engine='Chatterbox', model='ResembleAI/chatterbox',
                    license='MIT', seed=seed, text=text, settings=settings,
                    rate=model.sr, seconds=len(wav)/model.sr,
                    reference_recording=str(reference.resolve()),
                    reference_sha256=hashlib.sha256(reference.read_bytes()).hexdigest(),
                    watermark='Original Chatterbox Perth watermark retained',
                    float_peak=float_peak, gain_before_pcm16=pcm_gain)
        path.with_suffix('.json').write_text(json.dumps(meta, indent=2), encoding='utf-8')
        print(json.dumps(dict(path=str(path), seconds=meta['seconds'])), flush=True)


if __name__ == '__main__':
    main()
