"""Generate expressive laughter with native vocalization tags.

Accepts another character's reference, output folder, seed and prompt for reuse.
Default reference: this mod's own generated combat lines, not its old chuckle.
"""
import argparse
import functools
import hashlib
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--output-dir', type=Path, default=ROOT/'assets/source/turbo_laugh_takes')
    parser.add_argument('--seed', type=int)
    parser.add_argument('--text', default='[laugh] [laugh] [laugh]')
    args = parser.parse_args()
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    import numpy as np
    import soundfile as sf
    import torch
    from chatterbox.tts_turbo import ChatterboxTurboTTS

    torch.set_num_threads(8)
    reference = args.reference
    if reference is None:
        reference = ROOT/'assets/source/voice_combat_reference.wav'
        parts = []
        sources = []
        for name in ('mobile', 'aegis'):
            path = ROOT/f'assets/source/voice_{name}_raw.wav'
            x, rate = sf.read(path)
            assert rate == 24000 and x.ndim == 1
            parts.extend([x, np.zeros(round(.08*rate))])
            sources.append(dict(file=path.name, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        sf.write(reference, np.concatenate(parts[:-1]), 24000, subtype='PCM_16')
        reference.with_suffix('.json').write_text(json.dumps(dict(
            description='The character own fictional AI-generated combat lines as a shared voice identity reference',
            sources=sources, pause_seconds=.08), indent=2), encoding='utf-8')
    print('Loading local native-laughter model.', flush=True)
    model = ChatterboxTurboTTS.from_local(str(args.model.resolve()), 'cpu')
    model.t3.inference_turbo = functools.partial(model.t3.inference_turbo, max_gen_len=350)
    model.prepare_conditionals(str(reference))
    folder = args.output_dir
    folder.mkdir(parents=True, exist_ok=True)
    takes = [(args.seed, args.text)] if args.seed is not None else [
        (501, '[laugh] [laugh] [laugh]'),
        (502, 'Ha-ha-ha-ha! [laugh] [laugh]'),
        (503, 'Ahahahahaha! [laugh] [laugh]')]
    for seed, text in takes:
        torch.manual_seed(seed)
        np.random.seed(seed)
        print(f'Generating take {seed}: {text}', flush=True)
        settings = dict(temperature=.85, top_p=.95, repetition_penalty=1.1)
        wav = model.generate(text, **settings).squeeze().cpu().numpy()
        path = folder/f'laugh_{seed}.wav'
        np.save(path.with_suffix('.npy'), wav)
        float_peak = float(np.max(np.abs(wav)))
        gain = min(1., .93/max(float_peak, .00001))
        sf.write(path, wav*gain, model.sr, subtype='PCM_16')
        meta = dict(engine='Chatterbox Turbo', model='ResembleAI/chatterbox-turbo',
                    license='MIT', seed=seed, text=text, settings=settings,
                    rate=model.sr, seconds=len(wav)/model.sr,
                    reference_recording=str(reference),
                    reference_sha256=hashlib.sha256(reference.read_bytes()).hexdigest(),
                    float_peak=float_peak, gain_before_pcm16=gain,
                    watermark='Original Chatterbox Perth watermark retained')
        metadata = args.model/'.cache/huggingface/download/t3_turbo_v1.safetensors.metadata'
        if metadata.exists():
            meta['revision'] = metadata.read_text(encoding='utf-8').splitlines()[0]
        path.with_suffix('.json').write_text(json.dumps(meta, indent=2), encoding='utf-8')
        print(json.dumps(dict(path=str(path), seconds=meta['seconds'])), flush=True)


if __name__ == '__main__':
    main()
