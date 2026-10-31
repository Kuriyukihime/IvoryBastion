"""Original seamless blue-plasma volume atlas for Seraph's mobile/siege engines.

RGBA is straight alpha. Atlas order is left-to-right, top-to-bottom in the PNG;
each tile's nozzle is at its bottom (Unity local v=0), tip at its top (v=1).
No shocks/diamonds, axial beads, smoke columns, or external source imagery.
"""
from pathlib import Path
import json
import math
import numpy as np
from scipy.ndimage import gaussian_filter, map_coordinates
from scipy.special import erf
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
WIDTH, HEIGHT, DEPTH, FRAMES = 256, 512, 40, 32
COLS, ROWS, NOISE_SIZE = 8, 4, 64
FONT = "C:/Windows/Fonts/segoeui.ttf"


def smooth(a, b, x):
    v = np.clip((x - a) / (b - a), 0, 1)
    return v * v * (3 - 2 * v)


def volume_noise(seed, bands):
    rng = np.random.default_rng(seed)
    field = np.zeros((NOISE_SIZE,) * 3, np.float32)
    for blur, weight in bands:
        layer = gaussian_filter(rng.standard_normal(field.shape, dtype=np.float32), blur, mode="wrap")
        field += layer * (weight / max(float(layer.std()), 1e-6))
    return field / field.std()


class Plume:
    def __init__(self):
        self.coarse = volume_noise(61837, [(5.3, .65), (2.5, .34), (1.2, .12)])
        self.fine = volume_noise(91821, [(2.1, .62), (.8, .25)])
        self.x = np.linspace(-1, 1, WIDTH, dtype=np.float32)[None, :]
        self.v = (1 - (np.arange(HEIGHT, dtype=np.float32) + .5) / HEIGHT)[:, None]
        self.nozzle_gate = smooth(.005, .026, self.v)
        self.tip_gate = 1 - smooth(.78, .985, self.v)
        self.turbulence = smooth(.025, .38, self.v)
        self.radius = (.285 + .17 * (1 - np.exp(-self.v * 8))) * (1 - .74 * self.v ** 2)
        self.core_radius = .128 * (1 - .76 * self.v)
        self.depth = np.linspace(-.72, .72, DEPTH, dtype=np.float32)

    def sample(self, noise, x, v, z, time, detail=False):
        # Integral toroidal translations guarantee that t=1 and t=0 sample
        # exactly the same field. Positive v motion is downstream, away from
        # the nozzle. Small circular crossflow rolls only the exterior gas.
        n = NOISE_SIZE
        phase = time * math.tau
        transverse = 55 if detail else 27
        downstream = 96 if detail else 88
        circuits = 1
        coords = np.broadcast_arrays(
            z * transverse + math.sin(phase) * 2.7,
            v * downstream - time * n * circuits,
            x * transverse + math.cos(phase) * 2.7,
        )
        return map_coordinates(noise, [c % n for c in coords], order=1, mode="grid-wrap")

    def render(self, time):
        rgb = np.zeros((HEIGHT, WIDTH, 3), np.float32)
        alpha = np.zeros((HEIGHT, WIDTH), np.float32)
        transmission = np.ones((HEIGHT, WIDTH), np.float32)
        dx = float(self.depth[1] - self.depth[0])
        x, v = self.x, self.v
        gate = self.nozzle_gate * self.tip_gate
        shear_gate = smooth(.09, .26, v) * (1 - smooth(.72, .94, v))
        # Eight slender 3D shear strands ride irregular, correlated flow
        # paths. These are long axial filaments rather than evenly spaced
        # shock cells. Their random bends advect with the surrounding gas.
        ribbons = []
        for i in range(8):
            a = self.sample(self.coarse, x[:, :1] * 0 + .31 + i * .37, v, -.14 - i * .23, time)
            b = self.sample(self.fine, x[:, :1] * 0 + .17 + i * .19, v, .26 + i * .29, time, True)
            angle = i * 2.399963 + .28 * a + .10 * b + .10 * math.sin(time * math.tau)
            radial = self.radius * np.clip(.45 + (i % 5) * .075 + .065 * a + .025 * b, .25, .92)
            cx, cz = np.cos(angle) * radial, np.sin(angle) * radial
            width = .012 + .009 * v + .003 * (i % 3)
            lateral = np.exp(-np.square((x - cx) / width) * 1.4)
            strength = shear_gate * np.clip(.87 + .10 * b, .45, 1.15) * (3.8 + (i % 3) * .35)
            ribbons.append((lateral, cz, width, strength))
        for z in self.depth:
            coarse = self.sample(self.coarse, x, v, z, time)
            fine = self.sample(self.fine, x, v, z, time, True)
            radius = np.sqrt(x * x + z * z)
            # A continuous axis, not a displaced chain of bright spots. Only
            # the soft outer shear layer has visibly rolling volume detail.
            outer_r = radius + self.turbulence * (.036 * coarse + .016 * fine)
            outer_q = np.maximum(0, outer_r) / self.radius
            mantle = .25 * np.exp(-np.power(outer_q * 1.15, 2.8)) + .75 * (1 - smooth(.65, 1.10, outer_q))
            mantle *= np.clip(.76 + .16 * coarse + .14 * fine, .17, 1.25)
            mantle *= 1.25 * np.power(1 - v, .24) * gate
            mantle *= .35 + .65 * smooth(.055, .34, v)
            # Narrow contour sheets reveal small rolling curls in the outer
            # shear. Their sharper center sits inside a soft volume edge.
            filaments = np.exp(-np.square((outer_q - .82 - .055 * coarse) / .105))
            filaments *= np.exp(-np.square((fine * .6 + coarse * .12) / .36)) * shear_gate * 1.8
            ribbon_density = np.zeros_like(mantle)
            for lateral, cz, width, strength in ribbons:
                # Exact depth-slab integration prevents a thin strand from
                # flashing as it moves between adjacent ray samples.
                a = math.sqrt(1.4) * (z - dx * .5 - cz) / width
                b = math.sqrt(1.4) * (z + dx * .5 - cz) / width
                depth_weight = (erf(b) - erf(a)) * width * math.sqrt(math.pi) / (2 * math.sqrt(1.4) * dx)
                ribbon_density += lateral * depth_weight * strength
            filaments += ribbon_density
            core = np.exp(-np.square(radius / self.core_radius) * 1.8)
            core *= 15 * (1 - smooth(.45, .84, v)) * self.nozzle_gate
            core *= 1 + .025 * self.turbulence * coarse
            density = mantle + filaments + core
            outer_heat = np.clip(.24 + .15 * (1 - outer_q) + .075 * fine + .12 * (1 - v), .07, .63)
            core_heat = np.clip(1.10 - smooth(.14, .70, v) * .50, .35, 1)
            shear_heat = np.clip(.75 + .045 * fine, .66, .84)
            heat = (outer_heat * mantle + shear_heat * filaments + core_heat * core) / np.maximum(density, 1e-6)
            knots = [0, .18, .38, .58, .78, .93, 1]
            palette = np.array([[.008, .025, .12], [.014, .075, .40], [.025, .24, .88],
                                [.055, .48, 1], [.25, .78, 1], [.76, .96, 1], [.98, 1, 1]], np.float32)
            colour = np.stack([np.interp(heat, knots, palette[:, c]) for c in range(3)], -1)
            # Forward emission dominates; a small depth gradient gives the
            # turbulent body shape rather than a flat colored streak.
            colour *= (.97 + .03 * np.clip(-z / .72, -1, 1))
            opacity = 1 - np.exp(-density * dx)
            weight = opacity * transmission
            rgb += colour * weight[..., None]
            alpha += weight
            transmission *= 1 - opacity
        rgb /= np.maximum(alpha[..., None], 1e-6)
        # Preserve a quiet, steady tip and prevent texture bleed between tiles.
        border = np.minimum(np.minimum(np.arange(WIDTH), np.arange(WIDTH)[::-1])[None, :],
                            np.minimum(np.arange(HEIGHT), np.arange(HEIGHT)[::-1])[:, None])
        alpha *= smooth(1, 4, border)
        rgba = np.uint8(np.clip(np.dstack([rgb, alpha]), 0, 1) * 255 + .5)
        rgba[rgba[..., 3] == 0, :3] = 0
        return Image.fromarray(rgba)


def composite(frame, text="BLUE PLASMA / CONTINUOUS VOLUME FLOW"):
    canvas = Image.new("RGB", (744, 610), "#0a111e")
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((376, 48, 731, 580), fill=(67, 73, 82))
    draw.rectangle((12, 48, 367, 580), fill=(8, 14, 25))
    canvas.paste(frame, (62, 57), frame)
    canvas.paste(frame, (426, 57), frame)
    draw.text((18, 16), text, fill="#d2e9ff", font=ImageFont.truetype(FONT, 19))
    draw.text((24, 585), "Original 32-frame volume / alpha composite / no in-game capture", fill="#8faecc", font=ImageFont.truetype(FONT, 15))
    return canvas


def auxiliaries():
    y, x = np.mgrid[-1:1:512j, -1:1:512j]
    r = np.sqrt(x * x + y * y)
    rng = np.random.default_rng(68012)
    noise = np.zeros_like(r)
    for sigma, weight in [(28, .6), (11, .28), (4, .10)]:
        n = gaussian_filter(rng.normal(size=r.shape), sigma)
        noise += n / n.std() * weight
    support = 1 - smooth(.70, .985, r)
    vapor = np.exp(-r * r * 3.4) * np.clip(.62 + .15 * noise, 0, 1) * support * .62
    white = np.ones_like(r)
    Image.fromarray(np.uint8(np.dstack([white, white, white, vapor]) * 255)).save(ROOT / "assets/thruster_vapor.png")
    glow = (.75 * np.exp(-r * r * 16) + .25 * np.exp(-r * r * 4.5)) * support
    heat = np.exp(-r * r * 9)
    Image.fromarray(np.uint8(np.clip(np.dstack([.38 + .62 * heat, .80 + .20 * heat, white, glow]), 0, 1) * 255)).save(ROOT / "assets/thruster_glow.png")


def main():
    plume = Plume()
    frames = []
    for i in range(FRAMES):
        frame = plume.render(i / FRAMES)
        frames.append(frame)
        if i == 0:
            composite(frame).save(ROOT / ".build/thruster-first-frame.png")
            print("FIRST FRAME READY .build/thruster-first-frame.png", flush=True)
        print(f"Rendered plasma frame {i + 1}/{FRAMES}", flush=True)
    atlas = Image.new("RGBA", (WIDTH * COLS, HEIGHT * ROWS))
    for i, frame in enumerate(frames):
        atlas.paste(frame, (i % COLS * WIDTH, i // COLS * HEIGHT))
    atlas.save(ROOT / "assets/thruster_plume.png")
    auxiliaries()
    previews = [composite(f) for f in frames]
    # GIF durations use 10ms ticks. Four 40ms frames + twenty-eight 30ms frames
    # give exactly one second without a long stutter at the loop boundary.
    durations = [40 if i % 8 == 7 else 30 for i in range(FRAMES)]
    previews[0].save(ROOT / "dist/IvoryBastion-0.9.0-thruster-plume.gif", save_all=True,
                     append_images=previews[1:], duration=durations, loop=0, optimize=False)
    sheet = Image.new("RGB", (1280, 1060), "#0a111e")
    draw = ImageDraw.Draw(sheet)
    for j, index in enumerate([0, 4, 8, 12, 16, 20, 24, 28]):
        at = (j % 4 * 320 + 32, j // 4 * 530 + 8)
        sheet.paste(frames[index], at, frames[index])
        draw.text((at[0], at[1] + 505), f"FLOW {index + 1:02d}", fill="#b8d8f2")
    sheet.save(ROOT / "dist/IvoryBastion-0.9.0-thruster-plume-frames.png")
    endpoint = np.asarray(plume.render(1)).astype(float)
    data = np.stack([np.asarray(f) for f in frames]).astype(float)
    delta = np.abs(np.roll(data[..., 3], -1, axis=0) - data[..., 3]).mean(axis=(1, 2))
    energies = data[..., 3].mean(axis=(1, 2))
    tips = [int(np.flatnonzero((a[..., 3] >= 8).any(axis=1))[0]) for a in data]
    with Image.open(ROOT / "dist/IvoryBastion-0.9.0-thruster-plume.gif") as preview:
        gif_frames = preview.n_frames
        gif_duration = sum((preview.seek(i) or preview.info.get("duration", 0)) for i in range(gif_frames))
    report = dict(atlas=[WIDTH * COLS, HEIGHT * ROWS], tile=[WIDTH, HEIGHT], frames=FRAMES,
                  origin="bottom center / local v=0", png_order="left-to-right, top-to-bottom",
                  endpoint_max_error=float(np.max(np.abs(endpoint - data[0]))),
                  alpha_energy_min=float(energies.min()), alpha_energy_max=float(energies.max()),
                  alpha_energy_variation=float((energies.max() - energies.min()) / energies.mean()),
                  mean_frame_delta=float(delta.mean()), wrap_frame_delta=float(delta[-1]),
                  tip_top_row_min=min(tips), tip_top_row_max=max(tips),
                  gif_frames=gif_frames, gif_duration_ms=gif_duration)
    assert report["endpoint_max_error"] <= 1, report
    assert report["wrap_frame_delta"] < report["mean_frame_delta"] * 1.8, report
    assert report["alpha_energy_variation"] < .18, report
    assert max(tips) - min(tips) < 20, report
    assert gif_frames == FRAMES and gif_duration == 1000, report
    for frame in frames:
        a = np.asarray(frame)[..., 3]
        assert not a[0].any() and not a[-1].any() and not a[:, 0].any() and not a[:, -1].any()
    (ROOT / ".build/thruster-texture-review.json").write_text(json.dumps(report, indent=2))
    print(json.dumps(report, indent=2), flush=True)


if __name__ == "__main__":
    main()
