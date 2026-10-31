"""Original dry mechanical/blue-electric spider mine Foley, no external samples.

Ordinary arming and detonations are deliberately less spectacular than the
existing overcharge accents. The runtime controls density through its own
mine-event throttling; these WAVs contain one event each, never a sequence.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt


ROOT = Path(__file__).resolve().parents[1]
SR = 48000
SEED = 783118


def noise(rng, length, low, high):
    x = sosfilt(butter(2, [low, high], btype="bandpass", fs=SR, output="sos"),
                rng.normal(0, 1, length))
    return x / max(np.sqrt(np.mean(x * x)), 1e-9)


def edge(t, duration, attack=.002, release=.025):
    return (np.sin(np.minimum(1, t / attack) * np.pi / 2) ** 2
            * np.sin(np.clip((duration - t) / release, 0, 1) * np.pi / 2) ** 2)


def arm(rng):
    duration = .31
    t = np.arange(round(duration * SR)) / SR
    # Release click is a metal catch, not a pitched UI confirmation beep.
    click = noise(rng, len(t), 780, 5700) * np.exp(-t / .007) * .16
    click += sum(gain * np.sin(2 * np.pi * hz * t) * np.exp(-t / decay)
                 for hz, gain, decay in ((195, .20, .025), (451, .11, .018), (970, .055, .011)))

    local = np.clip(t - .025, 0, None)
    ramp = np.clip(local / .21, 0, 1)
    phase = 2 * np.pi * np.cumsum(133 + 95 * ramp ** 1.6) / SR
    whirr = (.07 * np.sin(phase) + .036 * np.sin(phase * 2.03)
             + .014 * np.sin(phase * 4.1))
    # A rising noise floor is the small capacitor/motor load; no alert tones.
    load = noise(rng, len(t), 180, 1900) * .034 * ramp ** 1.2
    powered = (t >= .025) & (t < .249)
    motor_envelope = edge(local, .224, .015, .025) * powered
    x = click + (whirr + load) * motor_envelope

    stop = np.clip(t - .242, 0, None)
    latch = (.20 * np.sin(2 * np.pi * 181 * stop) * np.exp(-stop / .018)
             + .095 * noise(rng, len(t), 650, 3900) * np.exp(-stop / .005))
    x += latch * (t >= .242) * edge(stop, duration - .242, .001, .014)
    return x, duration, [
        {"at": 0, "until": .055, "layer": "dry release catch"},
        {"at": .025, "until": .249, "layer": "loaded winding motor and capacitor air"},
        {"at": .242, "until": .31, "layer": "grounded locking latch"},
    ]


def blast(rng):
    duration = .59
    t = np.arange(round(duration * SR)) / SR
    # A low pressure transient and housing crack, with no melodic laser/chirp.
    phase = 2 * np.pi * np.cumsum(56 + 92 * np.exp(-t / .021)) / SR
    pressure = .42 * np.sin(phase) * np.exp(-t / .090)
    bass_air = .105 * noise(rng, len(t), 44, 390) * np.exp(-t / .115)
    housing = .105 * noise(rng, len(t), 900, 6800) * np.exp(-t / .009)
    fragments = .084 * noise(rng, len(t), 180, 2850) * np.exp(-t / .080)
    casing = (.055 * np.sin(2 * np.pi * 293 * t) * np.exp(-t / .035)
              + .038 * np.sin(2 * np.pi * 709 * t) * np.exp(-t / .023))
    # A soft electrical pressure tail gives the Seraph palette without crackly zaps.
    tail = (.035 * noise(rng, len(t), 260, 1850)
            * (1 - np.exp(-t / .022)) * np.exp(-t / .13))
    tail *= .84 + .16 * np.sin(2 * np.pi * 31 * t) ** 2
    x = pressure + bass_air + housing + fragments + casing + tail
    dry = x.copy()
    for delay, gain in ((.017, .075), (.038, .04)):
        count = round(delay * SR)
        x[count:] += dry[:-count] * gain
    return x, duration, [
        {"at": 0, "until": .055, "layer": "mechanical housing crack"},
        {"at": 0, "until": .36, "layer": "deep capacitor-pressure thump"},
        {"at": .015, "until": .59, "layer": "short electrical air and debris decay"},
    ]


def deploy(rng):
    duration = .76
    t = np.arange(round(duration * SR)) / SR
    x = np.zeros(len(t))
    # A short loaded actuator opens the wells. This never reuses Aegis's
    # sustained protocol sound, so the deployment can finish with clean decay.
    phase = 2 * np.pi * np.cumsum(112 + 49 * np.clip(t / .15, 0, 1)) / SR
    actuator = (.055 * np.sin(phase) + .022 * np.sin(phase * 2.07)
                + .025 * noise(rng, len(t), 160, 1850))
    x += actuator * edge(t, .17, .014, .035) * (t < .17)
    # Twelve small alternating mechanical launches: airy pushes, tiny casing
    # catches and a soft bearing bed, rather than bright warning/chirp tones.
    for shot in range(12):
        at = .105 + shot * .049
        local = np.clip(t - at, 0, None)
        active = t >= at
        air = noise(rng, len(t), 210, 2850) * np.exp(-local / .018)
        catch = (np.sin(2 * np.pi * (183 + rng.uniform(-13, 13)) * local)
                 * np.exp(-local / .015))
        grit = noise(rng, len(t), 1050, 4300) * np.exp(-local / .0045)
        push = (.067 * air + .082 * catch + .024 * grit)
        x += push * active * edge(local, .085, .0016, .027)
    motor = .023 * noise(rng, len(t), 110, 900)
    x += motor * edge(t, .66, .035, .075) * (t < .66)
    for at, gain, hz in ((.657, .115, 205), (.695, .07, 415)):
        local = np.clip(t - at, 0, None)
        lock = (.8 * np.sin(2 * np.pi * hz * local)
                + .23 * noise(rng, len(t), 620, 3600))
        x += gain * lock * np.exp(-local / .017) * (t >= at) * edge(local, duration - at, .001, .024)
    return x, duration, [
        {"at": 0, "until": .17, "layer": "compact launch-well actuator"},
        {"at": .105, "until": .73, "layer": "twelve alternating salvo ratchets and air ejections"},
        {"at": .657, "until": .76, "layer": "dry well-catch settling and clean decay"},
    ]


def write(name, rng, peak):
    x, duration, events = {"arm": arm, "blast": blast, "deploy": deploy}[name](rng)
    x = sosfilt(butter(2, 40, btype="highpass", fs=SR, output="sos"), x)
    x = sosfilt(butter(2, 8600, btype="lowpass", fs=SR, output="sos"), x)
    x *= edge(np.arange(len(x)) / SR, duration, .0018, .035)
    # Remove DC with a zero-ended window, preserving exact sample-zero endpoints.
    correction = np.sin(np.linspace(0, np.pi, len(x))) ** 2
    x -= correction * np.sum(x) / np.sum(correction)
    x *= peak / max(np.max(np.abs(x)), 1e-9)
    pcm = np.round(x * 32767).astype(np.int16)
    pcm[0] = pcm[-1] = 0
    path = ROOT / f"assets/sfx_mine_{name}.wav"
    wavfile.write(path, SR, pcm)
    samples = pcm.astype(np.float64) / 32768
    segments = [float(np.sqrt(np.mean(v * v))) for v in np.array_split(samples, round(duration / .025))]
    report = {
        "file": str(path.relative_to(ROOT)), "seconds": len(samples) / SR,
        "rate": SR, "channels": 1, "encoding": "PCM signed 16-bit little-endian",
        "peak": float(np.max(np.abs(samples))),
        "peak_dbfs": float(20 * np.log10(np.max(np.abs(samples)))),
        "rms": float(np.sqrt(np.mean(samples * samples))),
        "dc_offset": float(np.mean(samples)),
        "clipped_samples": int(np.count_nonzero(np.abs(pcm.astype(np.int32)) >= 32767)),
        "first_sample": int(pcm[0]), "last_sample": int(pcm[-1]),
        "rms_25ms": segments, "events": events,
    }
    assert len(samples) == round(duration * SR)
    assert report["clipped_samples"] == 0 and report["peak"] <= .8
    assert abs(report["dc_offset"]) < 1e-5
    assert np.max(np.abs(samples[-32:])) < .002
    return report


def main():
    protected = sorted(p for p in (ROOT / "assets").glob("sfx_*.wav")
                       if p.name != "sfx_mine_deploy.wav")
    before = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
    reports = [write("arm", np.random.default_rng(SEED), .64),
               write("blast", np.random.default_rng(SEED + 1), .74),
               write("deploy", np.random.default_rng(SEED + 2), .62)]
    after = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
    assert before == after, "Every accepted audio asset must remain unchanged"
    metadata = {
        "provenance": "Original procedural mechanical Foley and capacitor-pressure synthesis; no recorded samples, voices, downloaded models or third-party assets.",
        "generator": "scripts/make_mine_audio.py", "seed": SEED,
        "overcharge_assets_unchanged_sha256": {p: h for p, h in before.items() if "mine_chain_" in p},
        "accepted_assets_unchanged_sha256": before, "assets": reports,
    }
    path = ROOT / "assets/source/mine_audio_info.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    for result in reports:
        print(f"{result['file']}: {result['seconds']:.2f}s, {SR} Hz mono PCM16; "
              f"peak {result['peak_dbfs']:.2f} dBFS; RMS {result['rms']:.4f}; clips {result['clipped_samples']}")


if __name__ == "__main__":
    main()
