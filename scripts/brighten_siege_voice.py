"""Raise the fresh fictional voice's register without changing its rhythm.

Requires praat-parselmouth, NumPy and SciPy. The checked-in result is sufficient
for normal builds; neither Praat nor the speech model is a runtime dependency.
"""
from pathlib import Path
import hashlib
import json
import wave

import numpy as np
import parselmouth
from parselmouth.praat import call
from scipy.signal import resample_poly

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'assets/source'
SEMITONES = 4.0


def main():
    raw = SOURCE / 'expressive_laugh_takes/laugh_203.wav'
    sound = parselmouth.Sound(str(raw))
    ratio = 2 ** (SEMITONES / 12)
    manipulation = call(sound, 'To Manipulation', .005, 90, 650)
    pitch = call(manipulation, 'Extract pitch tier')
    call(pitch, 'Multiply frequencies', sound.xmin, sound.xmax, ratio)
    call([pitch, manipulation], 'Replace pitch tier')
    raised = call(manipulation, 'Get resynthesis (overlap-add)')
    assert raised.values.shape == sound.values.shape
    assert raised.sampling_frequency == sound.sampling_frequency
    assert raised.duration == sound.duration
    samples = raised.values[0].copy()
    assert np.isfinite(samples).all()
    peak = float(np.max(np.abs(resample_poly(samples, 4, 1))))
    gain = min(1., .93 / max(.00001, peak))
    pcm = np.rint(samples * gain * 32768).astype('<i2')
    output = raw.with_name('laugh_203_bright.wav')
    with wave.open(str(output), 'wb') as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(round(sound.sampling_frequency))
        f.writeframes(pcm.tobytes())
    metadata = dict(
        input_source=raw.relative_to(SOURCE).as_posix(),
        input_sha256=hashlib.sha256(raw.read_bytes()).hexdigest(),
        output_sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
        semitones=SEMITONES, pitch_ratio=ratio,
        method='Praat pitch-tier multiplication and PSOLA resynthesis; unchanged duration and formants',
        seconds=sound.duration, samples=len(pcm),
        fixed_gain=gain, praat_parselmouth_version=parselmouth.__version__)
    output.with_suffix('.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    recipe_path = SOURCE / 'voice_siege_rebuild_recipe.json'
    recipe = json.loads(recipe_path.read_text(encoding='utf-8'))
    recipe['raw_source'] = output.relative_to(SOURCE).as_posix()
    recipe['source_sha256'] = metadata['output_sha256']
    recipe['register_shift_semitones'] = SEMITONES
    recipe['notes'] = (
        'One fresh performance in the original generated character voice, raised uniformly by four semitones '
        'without changing duration or rhythm. Remove two pauses only; use every retained source interval '
        'once in chronological order. No looping, repeated audio, slow-down effect or volume ramp. '
        'Stop after the last laugh, before any further sound.')
    recipe_path.write_text(json.dumps(recipe, indent=2), encoding='utf-8')
    print(json.dumps(metadata, indent=2))


if __name__ == '__main__':
    main()
