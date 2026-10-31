"""Export a single continuous generated laugh; no voice layers or echo effects."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import wave

import numpy as np
from scipy.signal import resample_poly


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--start', type=float, default=0)
    parser.add_argument('--end', type=float, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--crescendo-db', type=float, default=1.5)
    args = parser.parse_args()
    source = args.source.resolve()
    meta = json.loads(source.with_suffix('.json').read_text(encoding='utf-8'))
    assert meta['reference_recording'] is None
    assert meta['generated_voice_streams'] == 1 and meta['added_voice_layers'] == 0
    raw = np.load(source.with_suffix('.npy')).astype(float)
    assert raw.ndim == 1 and np.isfinite(raw).all()
    source_rate = meta['rate']
    assert 0 <= args.start < args.end <= len(raw) / source_rate
    x = raw[round(args.start * source_rate):round(args.end * source_rate)].copy()
    rate = 48000
    divisor = math.gcd(source_rate, rate)
    x = resample_poly(x, rate // divisor, source_rate // divisor)
    # A gentle gain rise emphasizes the ending without altering vocal pitch/timing.
    x *= 10 ** (np.linspace(-args.crescendo_db, 0, len(x)) / 20)
    edge = round(.004 * rate)
    x[:edge] *= np.linspace(0, 1, edge)
    x[-edge:] *= np.linspace(1, 0, edge)
    true_peak = float(np.max(np.abs(resample_poly(x, 4, 1))))
    x *= .9 / max(true_peak, .00001)
    assert len(x) / rate >= 3 and np.max(np.abs(x)) < 1
    pcm = np.rint(x * 32768).astype('<i2')
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(output), 'wb') as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(rate)
        f.writeframes(pcm.tobytes())
    report = dict(source=str(source), source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                  generation=meta, source_interval=[args.start, args.end], seconds=len(pcm)/rate,
                  sample_rate=rate, channels=1, added_voice_layers=0,
                  processing=f'One continuous generated take; original pitch and timing. Mild {args.crescendo_db:g}dB gain rise, fixed true-peak normalization, and 4ms click prevention. No loops, splices, additional voices, pitch doubling, chorus, delay or reverb.',
                  true_peak_4x=float(np.max(np.abs(resample_poly(pcm.astype(float)/32768,4,1)))),
                  output_sha256=hashlib.sha256(output.read_bytes()).hexdigest())
    output.with_suffix('.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
