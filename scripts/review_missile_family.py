"""Check and preview the actual missile assets without rebuilding survivor art."""
import hashlib
import json
import re
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import presentation_mesh as m

ROOT = m.ROOT
VERSION = json.loads((ROOT / 'package/manifest.json').read_text())['version_number']
OUT = ROOT / 'dist' / f'IvoryBastion-{VERSION}-missile-review'
OUT.mkdir(exist_ok=True)
SKYFALL_SCALE = float(re.search(r'VisualScale=([0-9.]+)f', (ROOT / 'src/BastionWarheadGeometry.cs').read_text()).group(1))
ROTATION = m.euler(np.array([-90, 0, 0]))


def geometry(asset):
    _, parts = m.load(asset)
    return parts, np.concatenate([p['v'] for p in parts])


def family(before=False):
    prefix = '../.build/' if before else ''
    suffix = '-0.12-before' if before else ''
    regular_name = prefix + 'missile' + suffix
    regular, vertices = geometry(regular_name)
    extras = []
    for asset, scale, x in [('warhead', 1, 0), ('skyfall', SKYFALL_SCALE, 1.4)]:
        parts, v = geometry(prefix + asset + suffix)
        bottom = v[:, 2].min() * scale
        for p in parts:
            extras.append((p, p['v'] @ ROTATION.T * scale + [x, -bottom, 0], p['n'] @ ROTATION.T))
    frame = dict(Scale=1, Rotation=dict(X=-90, Y=0, Z=0),
                 Offset=dict(X=-1.4, Y=-float(vertices[:, 2].min()), Z=0), Bones={})
    return m.render(regular_name, (1500, 1200), 20, pitch=5, frame=frame, extras=extras,
                    view=(0, 2.42, 0, 2.72))


def main():
    report = []
    for asset, scale, expected_length in [('missile', 1, 1.12), ('warhead', 1, 2.016), ('skyfall', SKYFALL_SCALE, 4.8384)]:
        parts, vertices = geometry(asset)
        old_parts, old_vertices = geometry('../.build/' + asset + '-0.12-before')
        assert np.isfinite(vertices).all()
        assert all(np.array_equal(p['t'], old['t']) for p, old in zip(parts, old_parts))
        assert np.allclose(vertices[:, 2], old_vertices[:, 2], atol=1e-7), 'Preserve missile length and axial detailing'
        ratio = 1 if asset == 'skyfall' else .8 / (7 / 12)
        assert np.allclose(vertices[:, :2], old_vertices[:, :2] * ratio, atol=1e-7), 'Only battery radial dimensions may change'
        length = float(np.ptp(vertices[:, 2])) * scale
        span = float(np.linalg.norm(vertices[:, :2], axis=1).max()) * 2 * scale
        assert abs(length - expected_length) < 1e-6
        if asset == 'skyfall':
            assert hashlib.sha256((ROOT / 'assets/skyfall.meshbin').read_bytes()).digest() == hashlib.sha256((ROOT / '.build/skyfall-0.12-before.meshbin').read_bytes()).digest()
        report.append(dict(asset=asset, length_m=length, fin_span_m=span, radial_change=ratio))
    assert report[0]['fin_span_m'] < report[1]['fin_span_m'] < report[2]['fin_span_m']
    assert abs(report[1]['fin_span_m'] / report[0]['fin_span_m'] - 1.8) < 1e-6
    _, heavy = geometry('warhead')
    _, skyfall = geometry('skyfall')
    assert np.allclose(heavy[:, :2] / .8, skyfall[:, :2] / (7 / 12), atol=1e-7)
    font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 24)
    after = family()
    draw = ImageDraw.Draw(after)
    draw.text((32, 24), f'IVORY BASTION / {VERSION} / ACTUAL MISSILE MESHES / SAME SCALE', font=font, fill='#c6dbea')
    draw.text((32, 1134), 'Regular: 1.12m x 0.24m    |    Heavy: 2.02m x 0.43m    |    Skyfall: 4.84m x 0.76m', font=font, fill='#c6dbea')
    after.save(OUT / f'IvoryBastion-{VERSION}-missile-family.png')
    before = family(True)
    ImageDraw.Draw(before).text((32, 24), 'BEFORE / 0.12.0 / SAME CAMERA AND SCALE', font=font, fill='#c6dbea')
    pair = Image.new('RGB', (3000, 1200), '#09101a')
    pair.paste(before, (0, 0)); pair.paste(after, (1500, 0))
    pair.save(OUT / f'IvoryBastion-{VERSION}-missile-before-after.png')

    bones, _ = m.load('siege'); anchors = dict(bones)
    ages = [-1] * 72
    for rack, row, col in [(0, 2, 1), (1, 3, 1)]: ages[rack * 18 + row * 3 + col] = .15
    frame = m.export([dict(Form='siege', Input=dict(Unfold=1, Time=1, MissileKick=.6, HatchAges=ages))])[0]
    extras = []
    for asset, hatch, advance in [('missile', 'hatch_0_2_1', 1.05), ('warhead', 'hatch_1_3_1', 1.6)]:
        parts, _ = geometry(asset)
        at = anchors[hatch] + [0, 0, advance / 1.45]
        for p in parts: extras.append((p, p['v'] / 1.45 + at, p['n']))
    m.render('siege', (1500, 1150), 28, pitch=12, frame=frame, extras=extras,
             title=f'IVORY BASTION / {VERSION} / BATTERY MISSILES LEAVING THE FORWARD LAUNCHERS').save(
                 OUT / f'IvoryBastion-{VERSION}-missile-launch-context.png')
    (ROOT / '.build/missile-family-review.json').write_text(json.dumps(dict(version=VERSION, family=report), indent=2))
    print('PASS: missile lengths, radial-only edits, shared fin design, size hierarchy, unchanged Skyfall mesh and actual-asset previews.', flush=True)
    for item in report: print(json.dumps(item), flush=True)


if __name__ == '__main__':
    main()
