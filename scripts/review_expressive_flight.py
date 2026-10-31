"""Exact shipped mesh, skin weights and compiled flight poses; not game captures."""
import argparse
import json
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import presentation_mesh as m

ROOT = m.ROOT
POSES = [
    ("Hover / relaxed tuck", dict(Time=1)),
    ("Cruise / trailing legs", dict(Time=1, Forward=1)),
    ("Boost / full extension", dict(Time=1, Forward=1, Boost=1)),
    ("Boost / rear silhouette", dict(Time=1, Forward=1, Boost=1)),
    ("Bank left / tucked knee", dict(Time=1, Forward=.7, Strafe=-.85, Turn=-.65, Boost=1)),
    ("Bank right / tucked knee", dict(Time=1, Forward=.7, Strafe=.85, Turn=.65, Boost=1)),
    ("Climb / ascent", dict(Time=1, Forward=.3, Vertical=1, Pitch=-65, Boost=1)),
    ("Dive / descent", dict(Time=1, Forward=.65, Vertical=-1, Pitch=65, Boost=1)),
    ("Air brake / open wings", dict(Time=1, Forward=.15, Brake=1)),
    ("Reverse / side slip", dict(Time=1, Forward=-1, Strafe=.6, Turn=.6)),
    ("Fire / banking upward", dict(Time=1, Forward=1, Strafe=1, Turn=1, Boost=1, Aim=1, Pitch=-65, LeftKick=1)),
    ("Fire / banking downward", dict(Time=1, Forward=-1, Strafe=-1, Turn=-1, Brake=1, Aim=1, Pitch=65, RightKick=1)),
    ("Blue Shift / gather", dict(Time=1, PowerAge=.22, Overdrive=1)),
    ("Blue Shift / release", dict(Time=1, PowerAge=.46, Overdrive=1)),
    ("Blue Shift / aimed boost", dict(Time=1, Forward=1, Strafe=-1, Boost=1, PowerAge=.46, Overdrive=1, Aim=1, Pitch=-55)),
    ("Deploy / folding pilot", dict(Time=1, Unfold=.70, Forward=.2, Brake=1)),
]
EXTREMES = [
    ("Compound boost climb " + side, dict(Time=.4, Forward=1, Strafe=s, Turn=s, Vertical=1, Boost=1, Aim=1, Pitch=-65, LeftKick=1, RightKick=1))
    for side, s in [("left", -1), ("right", 1)]
] + [
    ("Compound boost dive " + side, dict(Time=.9, Forward=1, Strafe=s, Turn=s, Vertical=-1, Boost=1, Aim=1, Pitch=65, LeftKick=1, RightKick=1))
    for side, s in [("left", -1), ("right", 1)]
] + [
    ("Reverse climb brake " + side, dict(Time=1.8, Forward=-1, Strafe=s, Turn=s, Vertical=1, Boost=1, Brake=1, PowerAge=.46))
    for side, s in [("left", -1), ("right", 1)]
] + [
    ("Release during diving boost " + side, dict(Time=2.6, Forward=1, Strafe=s, Turn=s, Vertical=-1, Boost=1, PowerAge=.46))
    for side, s in [("left", -1), ("right", 1)]
]


def inspect(frames, poses):
    report = []
    for lod in range(3):
        bones, parts = m.load("mobile", lod)
        for (label, _), frame in zip(poses, frames):
            ratios, rigid_ratios, vertices = [], [], []
            for part, v, normals in m.pose(bones, parts, frame):
                assert np.isfinite(v).all() and np.isfinite(normals).all(), label
                # Renderer bounds are in model-local space, before the visual
                # form transform. Undo that transform for a true culling check.
                local = ((v - m.vec(frame["Offset"])) / frame["Scale"]) @ m.euler(m.vec(frame["Rotation"]))
                vertices.append(local)
                edge = np.concatenate([part["t"][:, [0, 1]], part["t"][:, [1, 2]], part["t"][:, [2, 0]]])
                before = np.linalg.norm(part["v"][edge[:, 0]] - part["v"][edge[:, 1]], axis=1)
                after = np.linalg.norm(local[edge[:, 0]] - local[edge[:, 1]], axis=1)
                valid = before > 1e-5
                ratios.extend((after[valid] / before[valid]).tolist())
                rigid = part["w"][:, 0] > .99999
                dominant = part["b"][:, 0]
                same = valid & rigid[edge].all(axis=1) & (dominant[edge[:, 0]] == dominant[edge[:, 1]])
                rigid_ratios.extend((after[same] / before[same]).tolist())
            vertices = np.concatenate(vertices)
            rest = np.concatenate([p["v"] for p in parts])
            extra = np.maximum(rest.min(0) - vertices.min(0), vertices.max(0) - rest.max(0))
            entry = dict(lod=lod, pose=label, max_edge_stretch=float(max(ratios)),
                         edge_stretch_99=float(np.quantile(ratios, .99)),
                         edge_stretch_999=float(np.quantile(ratios, .999)),
                         rigid_max_edge_stretch=float(max(rigid_ratios)),
                         bounds_growth=extra.tolist())
            report.append(entry)
            assert entry["max_edge_stretch"] < 3, entry
            assert entry["edge_stretch_99"] < 1.45, entry
            assert entry["edge_stretch_999"] < 1.9, entry
            assert entry["rigid_max_edge_stretch"] < 1.005, entry
            assert np.max(extra) < .5, ("Exceeds the renderer's .5m margin", entry)
    (ROOT / ".build/expressive-flight-review.json").write_text(json.dumps(report, indent=2))
    print("PASS", len(report), "flight/extreme-pose/LOD deformation and culling reviews", flush=True)


def sheet(frames, version):
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 17)
    image = Image.new("RGB", (1680, 2520), "#0a101a")
    for i, ((label, _), frame) in enumerate(zip(POSES, frames)):
        yaw = 155 if i == 3 else 35
        im = m.render("mobile", (420, 580), yaw=yaw, pitch=5, frame=frame, view=(0, 1.06, 0, 1.32))
        x, y = i % 4 * 420, i // 4 * 620
        image.paste(im.convert("RGB"), (x, y))
        ImageDraw.Draw(image).text((x + 12, y + 585), label, font=font, fill="#bdd9f3")
        print("POSE", label, flush=True)
    ImageDraw.Draw(image).text((16, 2490), "Runtime meshes + compiled choreography / offline pose study", font=font, fill="#829eb8")
    image.save(ROOT / f"dist/IvoryBastion-{version}-expressive-flight-poses.png")


def smooth(t, a, b):
    s = min(1, max(0, (t - a) / (b - a)))
    return s * s * (3 - 2 * s)


def motion(version):
    requests = []
    for i in range(140):
        t = i / 20
        travel = smooth(t, .2, 1.0) * (1 - smooth(t, 4.0, 4.5))
        bank = math.sin((t - 1) * math.tau / 2.6) * smooth(t, 1, 1.4) * (1 - smooth(t, 3.5, 4))
        p = dict(Unfold=0, Time=t, Forward=travel, Strafe=bank * .7, Turn=bank * .5,
                 Boost=smooth(t, 2, 2.4) * (1 - smooth(t, 3.8, 4.15)),
                 Brake=smooth(t, 3.9, 4.15) * (1 - smooth(t, 4.4, 4.9)),
                 Vertical=smooth(t, 2.5, 2.9) * (1 - smooth(t, 3.1, 3.7)) * .75,
                 PowerAge=t - 5.2)
        requests.append(dict(Form="mobile", Input=p))
    frames = m.export(requests)
    images = []
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 16)
    for i, frame in enumerate(frames):
        im = m.render("mobile", (600, 700), yaw=145, pitch=6, frame=frame, view=(0, 1.06, 0, 1.32))
        d = ImageDraw.Draw(im)
        d.text((16, 14), "SERAPH / FLIGHT CHOREOGRAPHY", font=font, fill="#c4def4")
        t = i / 20
        label = "HOVER" if t < .2 else "ACCELERATE" if t < 1.2 else "BANK" if t < 2 else "BOOST + CLIMB" if t < 3.9 else "AIR BRAKE" if t < 4.9 else "BLUE SHIFT" if t >= 5.2 and t < 6.45 else "SETTLE"
        d.text((16, 39), label, font=font, fill="#6caff4")
        d.text((16, 672), "Offline mesh motion / effects reviewed separately", font=font, fill="#829eb8")
        images.append(im.convert("RGB"))
        print("FLIGHT FRAME", i, flush=True)
    images[0].save(ROOT / f"dist/IvoryBastion-{version}-expressive-flight-motion.gif",
                   save_all=True, append_images=images[1:], duration=50, loop=0, optimize=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--checks-only", action="store_true")
    parser.add_argument("--motion", action="store_true")
    args = parser.parse_args()
    version = json.loads((ROOT / "package/manifest.json").read_text())["version_number"]
    poses = POSES + EXTREMES
    frames = m.export([dict(Form="mobile", Input={"Unfold": 0, **p}) for _, p in poses])
    inspect(frames, poses)
    if not args.checks_only:
        sheet(frames[:len(POSES)], version)
    if args.motion:
        motion(version)
