"""Finish the licensed, continuous female performance as the combat AI laugh."""
from fractions import Fraction
from pathlib import Path
import hashlib
import json
import math
import wave

import numpy as np
from scipy.signal import butter, sosfilt, resample_poly

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'assets/source'
RATE = 48000


def main():
    current_edit = json.loads((SOURCE / 'voice_siege_edit.json').read_text(encoding='utf-8'))
    if current_edit.get('mode') == 'spoken_announcement':
        raise SystemExit('This archived laugh is retired. Run scripts/finish_bastion_voice.py to finish the current dialogue.')
    recipe = json.loads((SOURCE / 'voice_siege_rebuild_recipe.json').read_text(encoding='utf-8'))
    assert recipe['mode'] == 'performed_ai_crescendo'
    raw = SOURCE / recipe['raw_source']
    digest = hashlib.sha256(raw.read_bytes()).hexdigest()
    assert digest == recipe['source_sha256']
    credit = json.loads((SOURCE / recipe['source_metadata']).read_text(encoding='utf-8'))
    assert credit['license'] == 'CC0-1.0'
    with wave.open(str(raw), 'rb') as f:
        rate = f.getframerate()
        assert f.getnchannels() == 1 and f.getsampwidth() == 2
        original = np.frombuffer(f.readframes(f.getnframes()), dtype='<i2').astype(float) / 32768
    start, end = recipe['source_interval']
    assert 0 <= start < end <= len(original)/rate
    x = original[round(start*rate):round(end*rate)].copy()
    divisor = math.gcd(rate, RATE)
    x = resample_poly(x, RATE//divisor, rate//divisor)
    ratio = Fraction(2 ** (-recipe['register_lift_semitones']/12)).limit_denominator(2000)
    x = resample_poly(x, ratio.numerator, ratio.denominator)
    # Retain the accepted spectral finish, but keep a single dry signal.
    # Delayed copies made the laugh sound like multiple voices.
    x = sosfilt(butter(2, 75, btype='highpass', fs=RATE, output='sos'), x)
    x = sosfilt(butter(2, 12500, btype='lowpass', fs=RATE, output='sos'), x)
    assert not recipe['reflections'], 'The accepted siege laugh must remain undoubled'
    controls = np.asarray(recipe['crescendo_db'])
    db = np.interp(np.linspace(0, 1, len(x)), controls[:, 0], controls[:, 1])
    x *= 10 ** (db/20)
    edge = round(recipe['edge_seconds']*RATE)
    x[:edge] *= np.linspace(0, 1, edge)
    x[-edge:] *= np.linspace(1, 0, edge)
    peak = float(np.max(np.abs(resample_poly(x, 4, 1))))
    x *= .93/max(peak, .00001)
    assert np.isfinite(x).all()
    pcm = np.rint(x*32768).astype('<i2')
    output = SOURCE / 'voice_siege_performed.wav'
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
        minimum_active_seconds=4.0, source_edit=recipe,
        source_credit=credit,
        delivery='Continuous performed female maniacal laughter, brightened for the AI character, retaining its natural crescendo and cutting after the strongest burst',
        processing_description='CC0 human-performed source, one continuous interval. Fixed two-semitone playback lift; 75Hz/12.5kHz voice EQ on one dry signal, without delayed copies, chorus, doubling or added reverb; mild 2dB gain rise reinforces the original crescendo. Fixed true-peak normalization and 4ms click-prevention edges. No phrase loops, individual-syllable edits, speech resynthesis, added sigh or tempo ramp.')
    (SOURCE / 'voice_siege_edit.json').write_text(json.dumps(edit, indent=2), encoding='utf-8')
    print(json.dumps(dict(seconds=len(pcm)/RATE, true_peak_4x=float(np.max(np.abs(resample_poly(pcm.astype(float)/32768, 4, 1)))), source=recipe['raw_source'], license=credit['license']), indent=2))


if __name__ == '__main__':
    main()
