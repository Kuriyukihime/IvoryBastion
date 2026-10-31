"""Aligned visual review of the old/candidate siege assets, never mobile."""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_fill_holes, distance_transform_edt
import presentation_mesh as renderer
from optimize_siege_mesh import ROOT, read_mesh


def review(before, after, output):
    output.mkdir(parents=True, exist_ok=True)
    data = {str(path): read_mesh(path) for path in (before, after)}
    assert data[str(before)][0] == data[str(after)][0], 'The complete rig header must be identical'
    before_count, after_count = (len(data[str(path)][2][0][3]) for path in (before, after))
    saved_load = renderer.load

    def provider(path, lod=0):
        _, bones, levels = data[str(path)]
        name, material, vertices, triangles = levels[lod]
        part = dict(name=name, color=material[:3], smooth=material[3], glow=material[4], four=True,
                    v=vertices['v'].astype(float), n=vertices['n'].astype(float), uv=vertices['uv'],
                    b=vertices['b'], w=vertices['w'], t=triangles)
        return [(name, np.array(anchor[:3])) for name, anchor in bones], [part]

    frame_inputs = [dict(Time=1, Unfold=1), dict(Time=1, Unfold=.58),
                    dict(Time=1, Unfold=1, LaserKick=1), dict(Time=1, Unfold=1, Defense=1, HaloPhase=8),
                    dict(Time=1, Unfold=1, Pitch=-40), dict(Time=1, Unfold=1, Salvo=1)]
    frames = renderer.export([dict(Form='siege', Input=values) for values in frame_inputs])
    deformation = []
    for lod in range(3):
        bones, parts = provider(after, lod)
        for index, frame in enumerate(frames):
            for part, positions, normals in renderer.pose(bones, parts, frame):
                assert np.isfinite(positions).all() and np.isfinite(normals).all()
                triangles = part['t']
                assert np.all(part['b'][triangles, 0] == part['b'][triangles[:, :1], 0])
                edges = np.concatenate([triangles[:, [0, 1]], triangles[:, [1, 2]], triangles[:, [2, 0]]])
                original = np.linalg.norm(part['v'][edges[:, 0]] - part['v'][edges[:, 1]], axis=1)
                posed = np.linalg.norm(positions[edges[:, 0]] - positions[edges[:, 1]], axis=1) / frame['Scale']
                valid = original > 1e-5
                stretch = np.max(np.abs(posed[valid] / original[valid] - 1))
                assert stretch < .005, (lod, index, stretch)
                deformation.append(dict(lod=lod, pose=index, maximumRigidEdgeError=float(stretch)))

    views = [
        ('Front', 0, 5, (0, 1.3, .15, 2.25), frames[0]),
        ('Three quarter', 28, 12, (0, 1.3, .15, 2.55), frames[0]),
        ('Side', 90, 5, (0, 1.3, .15, 2.6), frames[0]),
        ('Rear', 155, 5, (0, 1.3, .15, 2.55), frames[0]),
        ('Cannon close-up', 25, 8, (0, 1.05, 1.45, .95), frames[2]),
        ('Cockpit close-up', 15, 5, (0, 1.55, -.35, .75), frames[0]),
        ('Deploying', 28, 8, (0, 1.15, .15, 2.55), frames[1]),
        ('Aiming upward', 28, 8, (0, 1.3, .15, 2.55), frames[4]),
    ]
    font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 20)
    stats = []
    thumbs = []
    try:
        for index, (label, yaw, pitch, view, frame) in enumerate(views):
            images = []
            for path in (before, after):
                renderer.load = lambda asset, lod=0, path=path: provider(path, lod) if asset == 'siege' else saved_load(asset, lod)
                images.append(renderer.render('siege', (900, 700), yaw, pitch=pitch, view=view, frame=frame, transparent=True))
            old, new = [np.array(image) for image in images]
            old_mask, new_mask = old[:, :, 3] > 127, new[:, :, 3] > 127
            union = old_mask | new_mask
            intersection = old_mask & new_mask
            iou = np.count_nonzero(intersection) / np.count_nonzero(union)
            old_outer, new_outer = binary_fill_holes(old_mask), binary_fill_holes(new_mask)
            outer_iou = np.count_nonzero(old_outer & new_outer) / np.count_nonzero(old_outer | new_outer)
            old_distance = distance_transform_edt(~new_mask)[old_mask]
            new_distance = distance_transform_edt(~old_mask)[new_mask]
            delta = np.abs(old[:, :, :3].astype(float) - new[:, :, :3].astype(float))
            error = float(delta[intersection].mean())
            stats.append(dict(view=label, silhouetteIoU=float(iou), meanRGBError=error,
                              externalSilhouetteIoU=float(outer_iou),
                              originalCoverageWithin2Pixels=float(np.mean(old_distance <= 2)),
                              optimizedCoverageWithin2Pixels=float(np.mean(new_distance <= 2)),
                              pixelRGBError95=float(np.quantile(delta[intersection], .95))))
            for label_key, image in zip(('before', 'after'), images):
                image.save(output / f'{label_key}-{index:02d}.png')
            pair = Image.new('RGB', (1800, 750), '#0b111b')
            for column, image in enumerate(images):
                background = Image.new('RGBA', image.size, '#0b111b')
                background.alpha_composite(image)
                pair.paste(background.convert('RGB'), (column * 900, 40))
            draw = ImageDraw.Draw(pair)
            draw.text((20, 9), label + f' — original {before_count:,} triangles', font=font, fill='#e2eaf1')
            draw.text((920, 9), f'Optimized {after_count:,} triangles', font=font, fill='#9ad8f0')
            path = output / f'comparison-{index:02d}.png'
            pair.save(path)
            thumbs.append(pair.resize((900, 375), Image.Resampling.LANCZOS))
            print(f'{label}: silhouette {iou:.5%}; mean RGB error {error:.3f}/255; {path.name}', flush=True)
    finally:
        renderer.load = saved_load
    sheet = Image.new('RGB', (1800, 1500), '#0b111b')
    for index, thumb in enumerate(thumbs):
        sheet.paste(thumb, (index % 2 * 900, index // 2 * 375))
    sheet.save(output / 'comparison-sheet.png')
    report = dict(scope='Offline rendered comparisons, not Unity shader equivalence or measured GPU frame time',
                  before=str(before), after=str(after), rigHeaderIdentical=True,
                  deformation=deformation, visuals=stats)
    (output / 'quality-report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    # Exact pixel-mask overlap over-penalizes subpixel shifts on the many thin
    # rims. Retain that raw measure, and also bound bidirectional displacement
    # at the actual 900x700 comparison size. Missing rims fail these guards.
    assert all(item['silhouetteIoU'] > .94 for item in stats), 'Inspect missing geometry before accepting'
    assert all(item['externalSilhouetteIoU'] > .95 for item in stats), 'Inspect outer contour before accepting'
    assert all(min(item['originalCoverageWithin2Pixels'], item['optimizedCoverageWithin2Pixels']) > .99 for item in stats), 'Inspect visible contour displacement'
    assert all(item['meanRGBError'] < 4 for item in stats), 'Inspect shading/texture loss before accepting'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before', type=Path, default=ROOT / '.build/siege-140/before/assets/siege.meshbin')
    parser.add_argument('--after', type=Path, default=ROOT / '.build/siege-140/candidate.meshbin')
    parser.add_argument('--output', type=Path, default=ROOT / '.build/siege-140/visual-review')
    arguments = parser.parse_args()
    review(arguments.before, arguments.after, arguments.output)
