"""Edit CC0 recordings into four mechanical transformation cues.

Replaces the rejected synthesized palette. Every audible layer comes from
recorded hydraulics, panel movement or metal catches. No oscillator, generated
ratchet train, beep, voice or weapon. Source audio/license snapshots/hashes are
in assets/source/transform_foley_cc0. Regeneration downloads nothing.
"""
from __future__ import annotations

import hashlib
import json
from math import gcd
from pathlib import Path

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, resample_poly, sosfilt


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/source/transform_foley_cc0"
SR = 48000
CC0 = "https://creativecommons.org/publicdomain/zero/1.0/"
SOURCES = {
    "hydraulics": {
        "file": "hydraulic_machine_hq_decoded.wav", "original": "hydraulic_machine_hq.mp3",
        "author": "qubodup (Iwan Gabovitch)", "title": "Short Machine Sound7 (CC0 mix3)",
        "page": "https://freesound.org/people/qubodup/sounds/871231/",
        "download": "https://cdn.freesound.org/previews/871/871231_71257-hq.mp3",
        "recorded_sources": [
            "kyles: scissor lifts machines up down hydraulic movements2.flac, Freesound637811 (CC0)",
            "zaphod1984: Freight Elevator, Freesound186731 (CC0)",
            "nuckan: hydraulics.wav, Freesound212941 (CC0)"]},
    "door": {
        "file": "hydraulic_door_hq_decoded.wav", "original": "hydraulic_door_hq.mp3",
        "author": "Jedimichael", "title": "hydraulicdoorsnd.wav",
        "page": "https://freesound.org/people/Jedimichael/sounds/83887/",
        "download": "https://cdn.freesound.org/previews/83/83887_1281618-hq.mp3",
        "recording_note": "Author states this is a hydraulic door recording made in2002."},
}
for name in ("mechanical1", "mechanical2", "clank1", "lightclunk1", "lightclunk2"):
    SOURCES[name] = {
        "file": name + ".wav", "original": name + ".wav",
        "author": "BMacZero (Brian MacIntosh)", "title": "Mechanical Sounds / " + name,
        "page": "https://opengameart.org/content/mechanical-sounds",
        "download": "https://opengameart.org/sites/default/files/" + name + ".wav"}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def edge(count, attack=.008, release=.022):
    t = np.arange(count) / SR
    duration = count / SR
    return (np.sin(np.clip(t / attack, 0, 1) * np.pi / 2) ** 2
            * np.sin(np.clip((duration - t) / release, 0, 1) * np.pi / 2) ** 2)


def load_sources():
    audio, catalog = {}, []
    downloads = {r["file"]: r for r in json.loads((SOURCE / "downloads.json").read_text(encoding="utf-8"))}
    for key, info in SOURCES.items():
        path, original = SOURCE / info["file"], SOURCE / info["original"]
        assert sha(original) == downloads[original.name]["sha256"], "Licensed original changed: " + original.name
        rate, pcm = wavfile.read(path)
        x = pcm.astype(np.float64) / 32768
        if x.ndim > 1:
            x = x.mean(axis=1)
        if rate != SR:
            divisor = gcd(rate, SR)
            x = resample_poly(x, SR // divisor, rate // divisor)
        # Gentle cleaning preserves real machine transients. No bit reduction,
        # distortion, pitch shifting, oscillator layers or tuned resonators.
        x = sosfilt(butter(2, 55, btype="highpass", fs=SR, output="sos"), x)
        x = sosfilt(butter(2, 8200, btype="lowpass", fs=SR, output="sos"), x)
        audio[key] = x
        catalog.append(dict(key=key, **info, license="CC0-1.0", license_url=CC0,
                            original_sha256=sha(original), decoded_source_sha256=sha(path),
                            original_download_quality="Public provider-HQ preview MP3" if original.suffix == ".mp3" else "Original PCM WAV"))
    return audio, catalog


def render(name, duration, nominal, sources):
    x = np.zeros(round(duration * SR))
    edits = []

    def add(at, source, begin, end, gain, label, attack=.006, release=.020):
        layer = sources[source][round(begin * SR):round(end * SR)].copy()
        layer *= edge(len(layer), attack, release) * gain
        start = round(at * SR)
        size = min(len(layer), len(x) - start)
        assert size > 0
        x[start:start + size] += layer[:size]
        edits.append(dict(at=at, until=round((start + size) / SR, 5), source=source,
                          source_begin=begin, source_end=end, gain=gain,
                          attack=attack, release=release, layer=label))

    if name == "deploy":
        add(.008, "mechanical2", .19, .29, .66, "release catch", .002, .024)
        add(.055, "hydraulics", .075, 1.405, .85, "hydraulic mechanism unfolds", .020, .09)
        add(.14, "door", .23, 1.42, 2.1, "pressure and sliding housing", .04, .09)
        add(.34, "mechanical1", .12, .285, .55, "first panel slides", .018, .032)
        add(.66, "mechanical2", .0, .18, .67, "second plate moves", .014, .030)
        add(1.20, "mechanical2", .31, .465, .38, "rear housing seats", .012, .035)
        add(1.455, "clank1", .11, .305, .52, "main frame locking catch", .002, .045)
        add(1.54, "lightclunk1", .17, .28, .64, "final retaining pin", .002, .032)
    elif name == "pack":
        add(.005, "lightclunk2", .035, .15, 1.1, "travel catch releases", .003, .025)
        add(.03, "hydraulics", .13, .77, .95, "retracting machinery", .014, .075)
        add(.055, "door", 2.40, 2.96, 1.7, "pressure and sliding chamber", .02, .07)
        add(.18, "mechanical2", .03, .18, .66, "inner panel slides", .012, .025)
        add(.46, "mechanical1", .12, .285, .40, "outer housing glides closed", .012, .025)
        add(.70, "clank1", .115, .285, .40, "retracted frame locks", .002, .042)
        add(.80, "lightclunk1", .20, .30, .69, "final travel catch", .002, .032)
    elif name == "deploy_game":
        add(.005, "mechanical2", .20, .28, .61, "release catch", .002, .020)
        add(.025, "hydraulics", .15, .765, .88, "short hydraulic opening", .014, .055)
        add(.065, "door", .38, .94, 2.1, "pressure and housing movement", .02, .055)
        add(.245, "mechanical1", .12, .285, .49, "armor plate moves", .012, .030)
        add(.485, "mechanical2", .02, .17, .59, "rear panel settles", .012, .030)
        add(.71, "clank1", .115, .245, .54, "main locking catch near800ms end", .002, .035)
        add(.753, "lightclunk1", .195, .282, .60, "final pin and dry decay", .002, .028)
    else:
        add(.001, "lightclunk2", .045, .11, 1.0, "unlocking pin", .002, .015)
        add(.009, "hydraulics", .15, .33, .89, "compact retraction", .008, .035)
        add(.006, "door", 2.60, 2.80, 1.6, "pressure and slide", .009, .035)
        add(.050, "mechanical2", .31, .405, .43, "housing closes", .008, .025)
        add(.176, "lightclunk1", .195, .309, .77, "travel catch near250ms end", .002, .030)
        add(.214, "mechanical2", .415, .491, .23, "restrained metal seating", .002, .025)

    x *= edge(len(x), .003, .016)
    dc = np.sin(np.linspace(0, np.pi, len(x))) ** 2
    x -= dc * np.sum(x) / np.sum(dc)
    normalization = .76 / max(np.max(np.abs(x)), 1e-9)
    x *= normalization
    pcm = np.round(x * 32767).astype(np.int16)
    pcm[0] = pcm[-1] = 0
    path = ROOT / f"assets/sfx_transform_{name}.wav"
    wavfile.write(path, SR, pcm)
    data = pcm.astype(np.float64) / 32768
    report = dict(
        file=str(path.relative_to(ROOT)), duration_seconds=len(data) / SR,
        nominal_animation_seconds=nominal, decay_tail_seconds=round(duration - nominal, 5),
        rate=SR, channels=1, encoding="PCM signed16-bit little-endian",
        peak=float(np.max(np.abs(data))), peak_dbfs=float(20 * np.log10(np.max(np.abs(data)))),
        rms=float(np.sqrt(np.mean(data * data))), dc_offset=float(np.mean(data)),
        clipped_samples=int(np.count_nonzero(np.abs(pcm.astype(np.int32)) >= 32767)),
        first_sample=int(pcm[0]), last_sample=int(pcm[-1]), peak_normalization_gain=normalization,
        rms_25ms=[float(np.sqrt(np.mean(v * v))) for v in np.array_split(data, round(duration / .025))],
        edits=edits, sha256=sha(path))
    assert len(data) == round(duration * SR) and report["rate"] == 48000
    assert report["clipped_samples"] == 0 and report["peak"] < .8
    assert pcm[0] == 0 and pcm[-1] == 0 and abs(report["dc_offset"]) < 1e-5
    assert np.max(np.abs(data[-32:])) < .005
    return report


def main():
    replaced = {f"sfx_transform_{name}.wav" for name in ("deploy", "pack", "deploy_game", "pack_game")}
    protected = sorted(p for p in (ROOT / "assets").glob("sfx_*.wav")
                       if p.name not in replaced)
    before = {str(p.relative_to(ROOT)): sha(p) for p in protected}
    audio, catalog = load_sources()
    reports = [render("deploy", 1.65, 1.65, audio), render("pack", .9, .9, audio),
               render("deploy_game", .84, .8, audio), render("pack_game", .29, .25, audio)]
    after = {str(p.relative_to(ROOT)): sha(p) for p in protected}
    assert before == after, "Other accepted sounds and voices must not change"
    info = dict(
        provenance="Edits/layers of actual CC0 hydraulic machine/elevator/door and metal panel/catch recordings. No synthesized oscillator, ratchet train, note, beep, voice, weapon, bit reduction, saturation or pitch shifting.",
        generator="scripts/make_transform_audio.py", source_folder=str(SOURCE.relative_to(ROOT)),
        sources=catalog, assets=reports, existing_audio_preserved_sha256=before,
        processing="Recorded PCM downmixed/resampled to48kHz; gentle55Hz highpass/8.2kHz lowpass, native-pitch excerpts, crossfades, linear layer gain, transparent peak normalization and zero-ended DC correction.",
        gameplay_timing="Short native-speed edits have real catches near800ms/250ms and40ms dry decay; no global acceleration of selection recordings.",
        license="All component recordings CC0-1.0. License pages, originals and download hashes retained in source folder. Attribution optional; authors credited here.",
        audition="No audio audition available; waveform/source-provenance checks do not establish perceived quality.")
    (ROOT / "assets/source/transform_audio_info.json").write_text(json.dumps(info, indent=2) + "\n", encoding="utf-8")
    for result in reports:
        print(f"{result['file']}: {result['duration_seconds']:.2f}s, {SR}Hz monoPCM16; "
              f"peak{result['peak_dbfs']:.2f}dBFS; RMS{result['rms']:.4f}; clips{result['clipped_samples']}")
    print(f"Preserved {len(before)} other accepted WAVs byte-for-byte.")


if __name__ == "__main__":
    main()
