"""Build the siege crescendo from the first accepted female AI performance.

Retain its existing subtle AI finish and long natural phrases. One phrase is
repeated to sustain the laugh. A fixed, modest playback-rate lift avoids pitch
resynthesis, and one broad volume rise leads to a short, clean cutoff.
"""
from fractions import Fraction
from pathlib import Path
import hashlib
import json
import wave

import numpy as np
from scipy.ndimage import maximum_filter1d, uniform_filter1d
from scipy.signal import resample_poly

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'assets/source'
RATE = 48000


def main():
    recipe = json.loads((SOURCE / 'voice_siege_rebuild_recipe.json').read_text(encoding='utf-8'))
    assert recipe['mode'] == 'original_ai_phrase_crescendo'
    raw = SOURCE / recipe['raw_source']
    assert hashlib.sha256(raw.read_bytes()).hexdigest() == recipe['source_sha256']
    accepted = SOURCE / recipe['accepted_source']
    assert hashlib.sha256(accepted.read_bytes()).hexdigest() == recipe['accepted_source_sha256']
    with wave.open(str(accepted), 'rb') as f:
        assert f.getframerate() == RATE and f.getnchannels() == 1 and f.getsampwidth() == 2
        original = np.frombuffer(f.readframes(f.getnframes()), dtype='<i2').astype(float) / 32768

    pieces = []
    for start, end in recipe['source_spans']:
        assert 0 <= start < end <= 2.91, 'Exclude the original sigh/outro.'
        pieces.append(original[round(start * RATE):round(end * RATE)].copy())
    # One quiet phrase-boundary join. No editing between individual laughs.
    overlap = round(recipe['phrase_crossfade_seconds'] * RATE)
    x = pieces[0]
    for piece in pieces[1:]:
        w = np.linspace(0, 1, overlap)
        x[-overlap:] = x[-overlap:] * (1 - w) + piece[:overlap] * w
        x = np.concatenate((x, piece[overlap:]))

    ratio = Fraction(2 ** (-recipe['register_lift_semitones'] / 12)).limit_denominator(2000)
    x = resample_poly(x, ratio.numerator, ratio.denominator)
    duration = len(x) / RATE
    position = np.arange(len(x)) / max(1, len(x) - 1)
    controls = np.asarray(recipe['crescendo_db'], dtype=float)
    db = np.interp(position, controls[:, 0], controls[:, 1])
    x *= 10 ** (db / 20)
    edge = round(recipe['edge_seconds'] * RATE)
    x[:edge] *= np.linspace(0, 1, edge)
    x[-edge:] *= np.linspace(1, 0, edge)
    # A small, smooth reduction of the tallest transients permits a fuller
    # average level at the same final peak. No hard clipping or waveshaping.
    control_db = recipe.get('peak_control_db', 0.)
    if control_db > 0:
        ceiling = float(np.max(np.abs(x))) * 10 ** (-control_db / 20)
        envelope = maximum_filter1d(np.abs(x), size=round(.006 * RATE) + 1)
        reduction = np.minimum(1., ceiling / np.maximum(envelope, .00001))
        x *= uniform_filter1d(reduction, size=round(.008 * RATE) + 1)
    peak = float(np.max(np.abs(resample_poly(x, 4, 1))))
    gain = .93 / max(peak, .00001)
    x *= gain
    pcm = np.rint(x * 32768).astype('<i2')
    output = SOURCE / 'voice_siege_original_crescendo.wav'
    with wave.open(str(output), 'wb') as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(pcm.tobytes())
    edit = dict(
        raw_source=recipe['raw_source'], source_sha256=recipe['source_sha256'],
        processed_source=output.name,
        processed_source_sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
        start_seconds=0, end_seconds=duration, end_fade_seconds=0,
        minimum_active_seconds=4.0, source_edit=recipe,
        delivery='Original accepted female AI laugh in a higher register, with a more energetic cadence and sustained level, rising into a crescendo and stopping before its sigh',
        processing_description=f"First accepted recording with its original subtle AI reflections preserved. One long laughing phrase repeats at a 12ms quiet-boundary join. Uniform {recipe['register_lift_semitones']:g}-semitone playback-rate lift, continuous {controls[-1, 1] - controls[0, 1]:g}dB volume crescendo, up to {control_db:g}dB of smooth transient control, fixed peak-safe gain, 4ms click prevention. No syllable reconstruction, PSOLA, hard clipping, tempo ramp or sigh outro.")
    (SOURCE / 'voice_siege_edit.json').write_text(json.dumps(edit, indent=2), encoding='utf-8')
    print(json.dumps(dict(seconds=duration, true_peak_4x=float(np.max(np.abs(resample_poly(pcm.astype(float) / 32768, 4, 1)))), fixed_gain=gain, phrase_join_count=len(pieces)-1, register_lift_semitones=recipe['register_lift_semitones']), indent=2))


if __name__ == '__main__':
    main()
