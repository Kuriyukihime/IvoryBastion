"""Textured BAST3 model + exported runtime shield colors; not a Unity capture."""
from pathlib import Path
import json
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import presentation_mesh as presentation

ROOT = Path(__file__).resolve().parents[1]
VERSION = "0.8.0"
VIEW = np.array((.25, .12, .96)); VIEW /= np.linalg.norm(VIEW)
RIGHT = np.cross((0, 1, 0), VIEW); RIGHT /= np.linalg.norm(RIGHT)
BASIS = np.array([RIGHT, np.cross(VIEW, RIGHT), VIEW])


def model_panel(asset, size, world_half_height):
    """Use the maintained four-weight parser/texture renderer, never reparse a stride.

    Render the current authored neutral pose. No call to presentation.export is
    needed, so this cannot overwrite the animation review's shared pose files.
    """
    scale, world_center = (1.45, 2) if asset == 'siege' else (.78, .595)
    yaw = math.degrees(math.atan2(VIEW[0], VIEW[2]))
    pitch = math.degrees(math.asin(VIEW[1]))
    image = presentation.render(
        asset, size, yaw=yaw, pitch=pitch,
        view=(0, world_center/scale, 0, world_half_height/scale))
    return image.convert('RGB')


def surface_triangle(buffer, xy, colors):
    """Interpolate runtime vertex colors instead of introducing flat polygon edges."""
    lo = np.maximum(0, np.floor(xy.min(0)).astype(int))
    hi = np.minimum(np.array((buffer.shape[1], buffer.shape[0])), np.ceil(xy.max(0)).astype(int) + 1)
    if np.any(hi <= lo): return
    yy, xx = np.mgrid[lo[1]:hi[1], lo[0]:hi[0]]
    xx = xx + .5; yy = yy + .5
    a, b, c = xy
    den = (b[1]-c[1])*(a[0]-c[0]) + (c[0]-b[0])*(a[1]-c[1])
    if abs(den) < 1e-9: return
    u = ((b[1]-c[1])*(xx-c[0]) + (c[0]-b[0])*(yy-c[1]))/den
    v = ((c[1]-a[1])*(xx-c[0]) + (a[0]-c[0])*(yy-c[1]))/den
    w = 1-u-v
    inside = (u >= 0) & (v >= 0) & (w >= 0)
    rgba = u[..., None]*colors[0] + v[..., None]*colors[1] + w[..., None]*colors[2]
    alpha = rgba[..., 3:4] * inside[..., None]
    region = buffer[lo[1]:hi[1], lo[0]:hi[0]]
    region[:] = region*(1-alpha) + rgba[..., :3]*255*alpha


def shield_layer(image, vertices, normals, triangles, colors, world_half_height):
    vertices, normals, colors = (np.asarray(a, dtype=float) for a in (vertices, normals, colors))
    triangles = np.asarray(triangles, dtype=np.int32).reshape(-1, 3)
    assert np.isfinite(vertices).all() and np.isfinite(colors).all()
    assert triangles.min() >= 0 and triangles.max() < len(vertices)
    assert len(vertices) == len(normals) == len(colors)
    width, height = image.size
    projected = vertices @ BASIS.T
    xy = np.column_stack((width/2+projected[:, 0]*height/(world_half_height*2),
                          height/2-projected[:, 1]*height/(world_half_height*2)))
    visible = triangles[(normals[triangles].mean(1) @ VIEW) > 0]
    visible = visible[np.argsort(projected[visible, 2].mean(1))]
    buffer = np.array(image, dtype=np.float32)
    for triangle in visible:
        surface_triangle(buffer, xy[triangle], colors[triangle])
    return Image.fromarray(np.uint8(np.clip(buffer, 0, 255)))


def main():
    data = json.loads((ROOT / '.build/shield-geometry.json').read_text())
    normals = np.array(data['normals']); vertices = normals * 1.22
    triangles = np.array(data['triangles']).reshape(-1, 3)
    width, height = 1600, 1040
    canvas = Image.new('RGB', (width, height), (12, 19, 31))
    base = model_panel('mobile', (1500, 1500), 1.40)
    for panel, variant in enumerate(('calm', 'impact')):
        image = shield_layer(base.copy(),vertices,normals,triangles,data[variant],1.40)
        canvas.paste(image.resize((750,750),Image.Resampling.LANCZOS),(25+panel*800,140))
    draw = ImageDraw.Draw(canvas)
    font_path = Path('C:/Windows/Fonts/segoeui.ttf')
    font = ImageFont.truetype(str(font_path), 26)
    small = ImageFont.truetype(str(font_path), 19)
    draw.text((35, 26), 'IVORY BASTION 0.8 / MOBILE SHIELD', font=font, fill=(220, 236, 255))
    draw.text((35, 66), 'Current textured BAST3 model + actual runtime shield vertex colors. Offline neutral-pose study.', font=small, fill=(130, 160, 196))
    draw.text((155, 925), 'Charged / transparent center, separate blue sphere', font=small, fill=(184, 210, 242))
    draw.text((920, 925), 'Impact / white-blue crest and fading wake', font=small, fill=(184, 210, 242))
    draw.text((35, 995), 'No model outline or coating. Unity lighting, bloom, depth sorting and motion are not reproduced.', font=small, fill=(130, 160, 196))
    target = ROOT / f'dist/IvoryBastion-{VERSION}-mobile-shield-preview.png'
    canvas.save(target)
    print(target)


if __name__ == '__main__': main()
