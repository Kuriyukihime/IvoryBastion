"""Offline audio-event screening, not a substitute for listening.

Compare candidate takes against laughter/chuckle/speech/scream classes. Results
are model scores, not ratings of naturalness or perceptual approval.
"""
import argparse
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('files', nargs='+', type=Path)
    parser.add_argument('--report', type=Path, default=ROOT / '.build/laugh-event-screen.json')
    args = parser.parse_args()
    import numpy as np
    import soundfile as sf
    import torch
    from scipy.signal import resample_poly
    from transformers import AutoFeatureExtractor, AutoModelForAudioClassification
    torch.set_num_threads(4)
    model_path = ROOT / '.build/audio-event-model'
    features = AutoFeatureExtractor.from_pretrained(model_path, local_files_only=True)
    model = AutoModelForAudioClassification.from_pretrained(model_path, local_files_only=True).eval()
    rows = []
    for file in args.files:
        x, rate = sf.read(file)
        if x.ndim == 2:
            x = x.mean(axis=1)
        divisor = math.gcd(rate, 16000)
        x = resample_poly(x, 16000 // divisor, rate // divisor)
        inputs = features(x, sampling_rate=16000, return_tensors='pt')
        with torch.inference_mode():
            scores = model(**inputs).logits.sigmoid()[0].numpy()
        top = np.argsort(scores)[::-1][:12]
        row = dict(file=str(file), top=[dict(label=model.config.id2label[int(i)], score=float(scores[i])) for i in top])
        rows.append(row)
        print(json.dumps(row), flush=True)
    args.report.write_text(json.dumps(rows, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
