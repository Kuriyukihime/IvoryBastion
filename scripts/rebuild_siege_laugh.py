"""Generate a continuous laugh using the original siege voice direction.

Offline generation only; this never overwrites the original accepted source.
The model is prompted with the original voice description, not a cloned voice.
"""
import argparse
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TEXT = 'Ha ha ha! Hahahahaha! Ha ha ha ha! Hahahahaha!'
PROMPT = (
    'An expressive adult woman with a clear natural mezzo-soprano voice, a slightly smoky lower register '
    'and bright resonant laughter in her middle register. She performs a real, wild, explosive maniacal laugh. '
    'Uncontrollable gleeful cackling with strong projection and a full open mouth. '
    'Laugh rapidly and continuously for four seconds, with the same energetic intensity and rhythm throughout. '
    'Short distinct ha-ha-ha pulses tumbling naturally into each other, without phrase pauses. '
    'This is delighted hysterical laughter, not grunting, shouting words, or sustaining a vowel. '
    'Start at full laughing force, stay there, finish with one emphatic laugh and stop abruptly. '
    'Clean dry studio recording. No quiet chuckles, slowing, sighing, gasping, moaning, music or effects.'
)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', required=True, type=Path)
    parser.add_argument('--seed', type=int, default=920)
    parser.add_argument('--output', type=Path,
                        default=ROOT/'assets/source/voice_siege_forceful_raw.wav')
    args = parser.parse_args()
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    import torch
    import soundfile as sf
    from qwen_tts import Qwen3TTSModel

    torch.set_num_threads(8)
    print('Loading existing offline voice model on CPU.', flush=True)
    model = Qwen3TTSModel.from_pretrained(
        str(args.model.resolve()), device_map='cpu', dtype=torch.float32,
        attn_implementation='sdpa')
    torch.manual_seed(args.seed)
    settings = dict(non_streaming_mode=True, max_new_tokens=84,
                    temperature=.78, top_p=.95, repetition_penalty=1.04)
    print('Generating a new continuous performance.', flush=True)
    waves, rate = model.generate_voice_design(
        text=TEXT, language='English', instruct=PROMPT, **settings)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    sf.write(args.output, waves[0], rate, subtype='PCM_16')
    metadata = dict(text=TEXT, prompt=PROMPT, seed=args.seed,
                    settings=settings, rate=rate, seconds=len(waves[0])/rate,
                    engine='Qwen3-TTS VoiceDesign',
                    model='Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign',
                    revision='5ecdb67327fd37bb2e042aab12ff7391903235d3',
                    license='Apache-2.0', reference_recording=None,
                    based_on='Original strong female siege-laugh direction; new continuous performance, no reference audio or reused samples')
    args.output.with_suffix('.json').write_text(
        json.dumps(metadata, indent=2), encoding='utf-8')
    print(json.dumps(dict(output=str(args.output), seconds=metadata['seconds'])), flush=True)


if __name__ == '__main__':
    main()
