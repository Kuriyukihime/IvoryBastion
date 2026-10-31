"""Original full-voiced female combat-AI laughter, authored offline.

New soprano performances; no source chuckles, real-person reference recordings,
or anime audio samples. Only finished PCM audio ships with the mod.
"""
import argparse
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROMPT = (
    'A powerful adult female soprano portraying a gleefully unhinged advanced combat AI. '
    'Bright high feminine register with a clear ringing resonant voice and confident precision. '
    'She throws her head back and laughs loudly with her mouth wide open: big voiced AH-HA-HA bursts, '
    'projected from the diaphragm like a theatrical anime villain celebrating an overwhelming victory. '
    'Every laugh has a full ringing vowel, exuberant and fierce. Continuous real maniacal laughter. '
    'Start with a strong delighted laugh, grow increasingly wild, higher and louder, '
    'and finish at the strongest point of a triumphant laughing crescendo. '
    'Her joyful, dangerous laughter fills the room for about five seconds. '
    'A clean close studio recording of just the female voice, dry and undistorted.'
)
TAKES = [
    (401, 'English', 'Ahahahahaha! Ahahahahahahaha!'),
    (402, 'Japanese', 'あっはははは！あははははははは！'),
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--seed', type=int)
    args = parser.parse_args()
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    import numpy as np
    import soundfile as sf
    import torch
    from qwen_tts import Qwen3TTSModel
    torch.set_num_threads(8)
    print('Loading local voice design model.', flush=True)
    model = Qwen3TTSModel.from_pretrained(str(args.model.resolve()), device_map='cpu',
        dtype=torch.float32, attn_implementation='sdpa')
    folder = ROOT / 'assets/source/theatrical_laugh_takes'
    folder.mkdir(parents=True, exist_ok=True)
    for seed, language, text in TAKES:
        if args.seed is not None and args.seed != seed:
            continue
        torch.manual_seed(seed)
        np.random.seed(seed)
        settings = dict(non_streaming_mode=True, max_new_tokens=140,
            temperature=.85, top_p=.95, repetition_penalty=1.02)
        print(f'Generating full-voiced take {seed} ({language}).', flush=True)
        waves, rate = model.generate_voice_design(text=text, language=language,
            instruct=PROMPT, **settings)
        raw = np.asarray(waves[0])
        path = folder / f'laugh_{seed}.wav'
        np.save(path.with_suffix('.npy'), raw)
        peak = float(np.max(np.abs(raw)))
        gain = min(1., .93 / max(peak, .00001))
        sf.write(path, raw * gain, rate, subtype='PCM_16')
        meta = dict(engine='Qwen3-TTS VoiceDesign',
            model='Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign',
            revision='5ecdb67327fd37bb2e042aab12ff7391903235d3', license='Apache-2.0',
            seed=seed, language=language, text=text, prompt=PROMPT, settings=settings,
            rate=rate, seconds=len(raw)/rate, float_peak=peak, gain_before_pcm16=gain,
            reference_recording=None)
        path.with_suffix('.json').write_text(json.dumps(meta, indent=2, ensure_ascii=False), encoding='utf-8')
        print(json.dumps(dict(take=seed, seconds=meta['seconds'], peak=peak)), flush=True)


if __name__ == '__main__':
    main()
