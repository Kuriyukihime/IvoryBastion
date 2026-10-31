"""Original Seraph shield cues: three soft low beeps and a recharge buzz."""
from pathlib import Path
import json
import wave
import numpy as np
from scipy.signal import butter, sosfilt

ROOT = Path(__file__).resolve().parent.parent
RATE = 48000


def save(name, signal, peak):
    t = np.arange(len(signal)) / RATE
    signal = sosfilt(butter(2, 38, btype='highpass', fs=RATE, output='sos'), signal)
    signal *= np.minimum(t / .004, 1) * np.minimum((t[-1] - t) / .025, 1)
    signal *= peak / np.max(np.abs(signal))
    path = ROOT / f'assets/sfx_{name}.wav'
    with wave.open(str(path), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes((signal * 32767).astype('<i2').tobytes())
    frequencies = np.fft.rfftfreq(len(signal), 1 / RATE)
    power = np.abs(np.fft.rfft(signal)) ** 2
    print(json.dumps({'path': str(path), 'duration': len(t) / RATE,
                      'peak': float(np.max(np.abs(signal))),
                      'rms': float(np.sqrt(np.mean(signal ** 2))),
                      'power_below_500_hz': float(power[frequencies < 500].sum() / power.sum())}))


def generate():
    rng = np.random.default_rng(26026)
    t = np.arange(round(RATE * .78)) / RATE
    signal = np.zeros_like(t)
    # Three matching 180Hz beeps, without the old impact/discharge layers.
    for start in [.035, .27, .505]:
        local = t - start
        elapsed = np.maximum(local, 0)
        attack = .5 - .5 * np.cos(np.pi * np.clip(local / .018, 0, 1))
        release = .5 - .5 * np.cos(np.pi * np.clip((.16 - local) / .04, 0, 1))
        phase = 2 * np.pi * 180 * elapsed
        signal += attack * release * (np.sin(phase) + .06 * np.sin(phase * 2))
    save('shieldbreak', signal, .58)

    # Smooth rising electrical charge: harmonic buzz, restrained modulation,
    # and filtered air. The audible cue is a one-shot, not a looping alarm.
    t = np.arange(round(RATE * 1.0)) / RATE
    frequency = (142 + 172 * (1 - np.exp(-t * 2.7))) * 2 ** (-3 / 12)
    phase = np.cumsum(frequency) * (2 * np.pi / RATE)
    modulation = .84 + .16 * np.sin(2 * np.pi * (16 * t + 5 * t ** 2))
    buzz = .30 * np.sin(phase) + .115 * np.sin(phase * 2)
    buzz += .035 * np.sin(phase * 3) + .012 * np.sin(phase * 5)
    undertone = .12 * np.sin(phase * .5)
    noise = rng.normal(0, 1, len(t))
    air = sosfilt(butter(2, [350, 1500], btype='bandpass', fs=RATE, output='sos'), noise)
    envelope = np.clip(t / .035, 0, 1) * np.clip((1.0 - t) / .21, 0, 1)
    signal = (buzz * modulation + undertone + .065 * air) * envelope
    dry = signal.copy()
    delay = round(RATE * .024)
    signal[delay:] += dry[:-delay] * .075
    save('shieldrecharge', signal, .60)


if __name__ == '__main__':
    generate()
