"""Offline native-plane layout study using the shipped gauge meshes.

The shear represents the native HUD's spring/perspective tilt. This is a layout
preview, not an in-game capture; no model or gameplay changes are made.
"""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
DATA = json.loads((ROOT / '.build/seraph-defense-preview.json').read_text(encoding='utf-8'))
VERSION = json.loads((ROOT / 'package/manifest.json').read_text(encoding='utf-8'))['version_number']
SCALE = 3
im = Image.new('RGB', (760 * SCALE, 835 * SCALE), (13, 23, 34))


def font(size, bold=False):
    filename = 'segoeuib.ttf' if bold else 'segoeui.ttf'
    return ImageFont.truetype('C:/Windows/Fonts/' + filename, round(size * SCALE))


def text(canvas, x, y, value, color=(224, 235, 247), size=12, bold=False):
    ImageDraw.Draw(canvas).text((round(x * SCALE), round(y * SCALE)), value,
                               font=font(size, bold), fill=color)


def rect(canvas, bounds, color):
    ImageDraw.Draw(canvas).rectangle(tuple(round(v * SCALE) for v in bounds), fill=color)


def mesh(canvas, value, x, y):
    d = ImageDraw.Draw(canvas)
    verts, colors, triangles = value['vertices'], value['colors'], value['triangles']
    for k in range(0, len(triangles), 3):
        ids = triangles[k:k + 3]
        d.polygon([(round((x + verts[i][0]) * SCALE),
                    round((y + value['height'] - verts[i][1]) * SCALE)) for i in ids],
                  fill=tuple(colors[ids[0]][:3]))


text(im, 34, 18, 'IVORY BASTION / DEFENSE GAUGES', size=19, bold=True)
text(im, 34, 50, 'Offline layout preview: shared native HUD tilt and dedicated space',
     size=11, color=(144, 167, 189))

for row, (caption, shield, aegis) in enumerate([
    ('BOTH DEFENSE LAYERS ACTIVE', True, True),
    ('SHIELD EMPTY / AEGIS STILL ACTIVE', False, True),
    ('AEGIS DEPLETED / SHIELD STILL READABLE', True, False),
]):
    y = 95 + row * 236
    text(im, 34, y, caption, size=11, bold=True, color=(149, 177, 202))
    plane = Image.new('RGBA', (640 * SCALE, 212 * SCALE))
    # Native buff row, then level + XP. Only the native row spacing increases.
    for index, color in enumerate([(251, 208, 55), (91, 171, 255)]):
        x, cy, r = 47 + index * 52, 59, 22
        d = ImageDraw.Draw(plane)
        d.regular_polygon((x * SCALE, cy * SCALE, r * SCALE), 6, rotation=30,
                          fill=(*color, 255), outline=(240, 246, 255, 255), width=SCALE)
        text(plane, x - 7, cy - 9, '+', (17, 36, 55), 14, True)
    text(plane, 32, 99, 'Lv.  18', size=14, bold=True)
    rect(plane, (120, 112, 608, 123), (35, 53, 67))
    rect(plane, (120, 112, 480, 123), (155, 212, 218))
    # Root bottom is 6px above health. Labels stay inside its 30px height.
    text(plane, 32, 132, 'SERAPH  ' + ('340 / 500' if shield else '0 / 500'), size=10, bold=True)
    text(plane, 508, 132, 'AEGIS  ' + ('320' if aegis else '—'),
         (255, 212, 85) if aegis else (145, 132, 96), size=10, bold=True)
    rect(plane, (32, 146, 608, 151), (43, 33, 14))
    rect(plane, (32, 153, 608, 162), (20, 43, 69))
    if aegis:
        mesh(plane, DATA['aegis'], 32.75, 146.75)
    if shield:
        mesh(plane, DATA['shield'], 32.75, 153.75)
    rect(plane, (32, 168, 608, 202), (105, 175, 50))
    text(plane, 279, 170, '1000 / 1000', (250, 253, 246), size=16, bold=True)
    # Transform the complete native plane, including labels, instead of only
    # flattening or rotating defense meshes independently of their health bar.
    tilted = plane.transform(plane.size, Image.Transform.AFFINE,
                             (1, 0, 0, .047, 1, 0), Image.Resampling.BICUBIC)
    im.paste(tilted, (36 * SCALE, (y + 25) * SCALE), tilted)

text(im, 34, 805, 'White / blue: Seraph Shield     Yellow / black: Aegis Barrier',
     size=11, color=(151, 175, 196))
out = ROOT / f'dist/IvoryBastion-{VERSION}-defense-ui-preview.png'
im.resize((760, 835), Image.Resampling.LANCZOS).save(out)
print(out)
