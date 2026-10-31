"""Finish one new performance, removing pauses without repeating any audio."""
from pathlib import Path
import hashlib
import json
import math
import wave
import numpy as np
from scipy.signal import butter, sosfilt, resample_poly

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT/'assets/source'
RATE = 48000


def main():
    recipe = json.loads((SOURCE/'voice_siege_rebuild_recipe.json').read_text(encoding='utf-8'))
    assert recipe['mode'] == 'single_take_pause_trim'
    assert 'repeat_opening' not in recipe
    raw = SOURCE/recipe['raw_source']
    digest = hashlib.sha256(raw.read_bytes()).hexdigest()
    assert digest == recipe['source_sha256'], 'Review a new source before applying the trim.'
    with wave.open(str(raw), 'rb') as f:
        rate = f.getframerate()
        assert f.getnchannels() == 1 and f.getsampwidth() == 2
        original = np.frombuffer(f.readframes(f.getnframes()), dtype='<i2').astype(float)/32768
    x = np.empty(0)
    previous_end = 0
    crossfade = round(recipe['crossfade_seconds']*rate)
    for start, end in recipe['source_spans']:
        # Chronological, nonoverlapping spans prohibit duplicated source audio.
        assert previous_end <= start < end <= len(original)/rate
        piece = original[round(start*rate):round(end*rate)].copy()
        if len(x):
            weight = np.linspace(0, 1, crossfade)
            x[-crossfade:] = x[-crossfade:]*(1-weight)+piece[:crossfade]*weight
            x = np.concatenate((x, piece[crossfade:]))
        else:
            x = piece
        previous_end = end
    assert 3.8 <= len(x)/rate <= 4.3, 'Keep the requested roughly four-second duration.'
    divisor = math.gcd(rate, RATE)
    x = resample_poly(x, RATE//divisor, rate//divisor)
    x = sosfilt(butter(2, 70, btype='highpass', fs=RATE, output='sos'), x)
    edge = round(.005*RATE)
    x[:edge] *= np.linspace(0, 1, edge)
    x[-edge:] *= np.linspace(1, 0, edge)
    true_peak = float(np.max(np.abs(resample_poly(x, 4, 1))))
    x *= .93/max(.00001, true_peak)
    pcm = np.rint(x*32768).astype('<i2')
    output = SOURCE/'voice_siege_forceful.wav'
    with wave.open(str(output), 'wb') as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(pcm.tobytes())
    edit = dict(
        raw_source=recipe['raw_source'], source_sha256=digest,
        processed_source=output.name,
        processed_source_sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
        start_seconds=0, end_seconds=len(pcm)/RATE, end_fade_seconds=0,
        minimum_active_seconds=3.75, source_edit=recipe,
        delivery='New four-second maniacal female laugh in a higher register, directed to maintain explosive delivery and a steady rapid rhythm until the stop',
        processing_description='One newly generated performance raised uniformly by four semitones using formant-preserving PSOLA with unchanged duration. Two pauses removed in chronological order; 5ms joins at quiet boundaries; 70Hz high-pass, fixed peak-safe gain and 5ms click-prevention edges. Every source span is used once. No loops, repeated phrases, inserted silence, time stretching, tempo ramp or volume ramp.')
    (SOURCE/'voice_siege_edit.json').write_text(json.dumps(edit, indent=2), encoding='utf-8')
    print(json.dumps(dict(seconds=len(pcm)/RATE, source_spans=recipe['source_spans'],
                         true_peak_4x=float(np.max(np.abs(resample_poly(pcm.astype(float)/32768, 4, 1))))), indent=2))


if __name__ == '__main__':
    main()
