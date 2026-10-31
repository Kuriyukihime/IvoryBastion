"""Rasterize exported production UI triangles at native sample density.

No image smoothing is applied: the right column's coverage comes entirely
from the same transparent edge vertices used by the runtime canvas.
"""
import base64
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]


def font(size):
    for name in ('C:/Windows/Fonts/segoeui.ttf', 'C:/Windows/Fonts/arial.ttf'):
        if Path(name).exists():
            return ImageFont.truetype(name, size)
    return ImageFont.load_default()


def triangles(canvas, element, scale, angle, shift):
    p = np.asarray(element['vertices'], dtype=np.float64)
    p += np.asarray([element['x'], element['y']])
    p -= [190, 75]
    r = math.radians(angle)
    rotation = np.array([[math.cos(r), -math.sin(r)], [math.sin(r), math.cos(r)]])
    p = (p @ rotation.T) * scale
    p[:, 1] *= -1
    p += shift
    colors = np.asarray([list(base64.b64decode(c)) if isinstance(c, str) else c
                         for c in element['colors']], dtype=np.float32) / 255
    indices = np.asarray(element['triangles']).reshape(-1, 3)
    h, w = canvas.shape[:2]
    for tri in indices:
        a, b, c = p[tri]
        lo = np.maximum(0, np.floor(np.minimum(np.minimum(a, b), c)).astype(int))
        hi = np.minimum([w - 1, h - 1], np.ceil(np.maximum(np.maximum(a, b), c)).astype(int))
        if np.any(hi < lo):
            continue
        denominator = (b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(denominator) < 1e-8:
            continue
        xx, yy = np.meshgrid(np.arange(lo[0], hi[0]+1)+.5, np.arange(lo[1], hi[1]+1)+.5)
        u = ((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/denominator
        v = ((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/denominator
        z = 1-u-v
        inside = (u >= 0) & (v >= 0) & (z >= 0)
        rgba = (u[..., None]*colors[tri[0]]+v[..., None]*colors[tri[1]]+z[..., None]*colors[tri[2]])
        alpha = np.clip(rgba[..., 3:4], 0, 1)*inside[..., None]
        region = canvas[lo[1]:hi[1]+1, lo[0]:hi[0]+1]
        region[:] = rgba[..., :3]*alpha+region*(1-alpha)


def render(case, angle):
    h, w = 360, 660
    yy, xx = np.meshgrid(np.arange(h), np.arange(w), indexing='ij')
    gradient = .85 + .13*np.sin(xx/180) + .08*yy/h
    canvas = np.asarray([.105, .145, .178], dtype=np.float32)[None, None, :]*gradient[..., None]
    for element in case['elements']:
        triangles(canvas, element, case['scale'], angle, [w/2, 186])
    return Image.fromarray(np.uint8(np.clip(canvas, 0, 1)*255))


def main():
    cases = json.loads((ROOT/'.build/hud-aa-geometry.json').read_text())
    out = Image.new('RGB', (1360, 1540), '#101923')
    d = ImageDraw.Draw(out)
    d.text((28, 18), 'IVORY BASTION  /  HUD EDGE COVERAGE', font=font(26), fill='#e2eff9')
    d.text((28, 54), 'Actual runtime geometry. Same slant, palette and shapes; no full-panel blur.', font=font(17), fill='#9fb7ca')
    d.text((30, 91), 'BEFORE  /  HARD TRIANGLE EDGES', font=font(19), fill='#c7d2dd')
    d.text((710, 91), 'REVIEW  /  ONE-PIXEL EDGE COVERAGE', font=font(19), fill='#a5d9f8')
    images = []
    for i, scale in enumerate((.75, 1, 1.5)):
        angle = (6, 9, 12)[i]
        row = 122+i*372
        for column, aa in enumerate((False, True)):
            case = next(c for c in cases if c['scale'] == scale and c['aa'] == aa)
            im = render(case, angle)
            out.paste(im, (20+column*680, row))
            ImageDraw.Draw(out).text((34+column*680, row+9), f'{scale*100:.0f}% HUD scale  /  {angle} degrees', font=font(16), fill='#d2dfe8')
            if scale == 1:
                images.append(im)
    d = ImageDraw.Draw(out)
    d.text((28, 1254), '4x pixel inspection  /  untouched raster samples, enlarged with nearest-neighbor', font=font(18), fill='#a9bdce')
    for i, im in enumerate(images):
        # The upper/right component includes slanted frame, fine cells and a meter.
        crop = im.crop((350, 102, 508, 158)).resize((632, 224), Image.Resampling.NEAREST)
        out.paste(crop, (30+i*680, 1284))
    path = ROOT/'dist/IvoryBastion-0.33.0-hud-edges.png'
    out.save(path)
    print(path)


if __name__ == '__main__':
    main()
