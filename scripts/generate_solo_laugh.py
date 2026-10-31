"""Generate a single original female character laughing, without source recordings."""
import argparse
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VOICE = (
    'One adult woman, alone, with a clear bright feminine mezzo-soprano voice. '
    'She is a gleefully dangerous, highly advanced combat AI delighting in her overwhelming power. '
    'She laughs maniacally with open, full-voiced HA-HA laughter, sounding thrilled and wickedly amused. '
    'A confident, energetic, spontaneous laugh with natural human resonance and a smiling voice. '
    'Keep her laughter strong and continuous, rising into a louder, higher triumphant final burst. '
    'The performance ends at the height of her excitement. '
    'Exactly one solo female voice recorded close to the microphone in a dry quiet studio.'
)
TAKES = [
    (711, 'Ha ha ha! Hahaha! Ha ha ha ha! Hahaha!'),
    (712, 'Ahahaha! Hahahahaha! Ahahahahaha!'),
    (713, 'Ha ha ha ha! Ha ha ha ha! Hahahahaha!'),
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--seed', type=int)
    parser.add_argument('--text')
    parser.add_argument('--name')
    parser.add_argument('--prompt-file', type=Path)
    args = parser.parse_args()
    if (args.text or args.name) and args.seed is None:
        parser.error('--text and --name require one --seed')
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    import numpy as np
    import soundfile as sf
    import torch
    from qwen_tts import Qwen3TTSModel

    torch.set_num_threads(8)
    device = 'cuda:0' if torch.cuda.is_available() else 'cpu'
    print('Loading voice design on ' + device, flush=True)
    model = Qwen3TTSModel.from_pretrained(
        str(args.model.resolve()), device_map=device,
        dtype=torch.bfloat16 if device.startswith('cuda') else torch.float32,
        attn_implementation='sdpa')
    folder = ROOT / 'assets/source/solo_laugh_takes'
    folder.mkdir(parents=True, exist_ok=True)
    prompt = args.prompt_file.read_text(encoding='utf-8').strip() if args.prompt_file else VOICE
    takes = [(args.seed, args.text or TAKES[0][1])] if args.seed is not None else TAKES
    for seed, text in takes:
        text = args.text or text
        torch.manual_seed(seed)
        np.random.seed(seed)
        settings = dict(non_streaming_mode=True, max_new_tokens=150,
                        temperature=.78, top_p=.95, repetition_penalty=1.04)
        print('Generating solo take ' + str(seed), flush=True)
        waves, rate = model.generate_voice_design(
            text=text, language='English', instruct=prompt, **settings)
        raw = np.asarray(waves[0])
        assert raw.ndim == 1 and np.isfinite(raw).all()
        path = folder / f'{args.name or ("laugh_" + str(seed))}.wav'
        np.save(path.with_suffix('.npy'), raw)
        peak = float(np.max(np.abs(raw)))
        gain = min(1., .93 / max(peak, .00001))
        sf.write(path, raw * gain, rate, subtype='PCM_16')
        meta = dict(engine='Qwen3-TTS VoiceDesign',
                    model='Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign',
                    revision='5ecdb67327fd37bb2e042aab12ff7391903235d3',
                    license='Apache-2.0', seed=seed, text=text, prompt=prompt,
                    settings=settings, rate=rate, seconds=len(raw)/rate,
                    float_peak=peak, gain_before_pcm16=gain,
                    reference_recording=None, generated_voice_streams=1,
                    added_voice_layers=0)
        path.with_suffix('.json').write_text(json.dumps(meta, indent=2), encoding='utf-8')
        print(json.dumps(dict(take=seed, seconds=meta['seconds'], peak=peak)), flush=True)


if __name__ == '__main__':
    main()
