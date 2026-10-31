"""Render and inspect the shipped Seraph hybrid meshes and compiled poses."""
import argparse
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import presentation_mesh as m

ROOT = m.ROOT
POSES = {
    "mobile": [
        ("Hover", dict(Time=1)), ("Boost", dict(Time=1, Forward=1, Boost=1)),
        ("Needlefire", dict(Time=1, Aim=1, LeftKick=.7)),
        ("Blue Shift / release", dict(Time=1, PowerAge=.48)),
        ("Aim upward", dict(Time=1, Aim=1, Pitch=-55)),
        ("Flight / rear", dict(Time=1, Forward=.7, Strafe=-.3))],
    "siege": [
        ("Deployed", dict(Time=1, Unfold=1)),
        ("Deploying", dict(Time=1, Unfold=.58)),
        ("Ion Lance / recoil", dict(Time=1, Unfold=1, LaserKick=1)),
        ("Aegis / brace", dict(Time=1, Unfold=1, Defense=1, HaloPhase=8)),
        ("Lance upward", dict(Time=1, Unfold=1, Pitch=-40)),
        ("Fortress / rear", dict(Time=1, Unfold=1, Salvo=1))]
}


def review(form):
    requests = [dict(Form=form, Input={"Unfold": 0 if form == "mobile" else 1, **p}) for _, p in POSES[form]]
    frames = m.export(requests)
    report = []
    for lod in range(3):
        bones, parts = m.load(form, lod)
        for (label, _), frame in zip(POSES[form], frames):
            ratios = []
            rigid_ratios = []
            for part, v, normals in m.pose(bones, parts, frame):
                assert np.isfinite(v).all() and np.isfinite(normals).all()
                edge = np.concatenate([part["t"][:, [0, 1]], part["t"][:, [1, 2]], part["t"][:, [2, 0]]])
                before = np.linalg.norm(part["v"][edge[:, 0]] - part["v"][edge[:, 1]], axis=1)
                after = np.linalg.norm(v[edge[:, 0]] - v[edge[:, 1]], axis=1) / frame["Scale"]
                valid = before > 1e-5
                ratios.extend((after[valid] / before[valid]).tolist())
                # Same-bone rigid edges must never stretch, especially the
                # mobile guns/blades and the fortress's physical hatch doors.
                weights = part["w"]
                dominant = part["b"][:, 0]
                rigid = (weights[:, 0] > .99999)
                mechanical = valid & rigid[edge].all(axis=1) & (dominant[edge[:, 0]] == dominant[edge[:, 1]])
                rigid_ratios.extend((after[mechanical] / before[mechanical]).tolist())
            maximum = float(max(ratios))
            report.append(dict(lod=lod, pose=label, max_edge_stretch=maximum,
                               edge_stretch_99=float(np.quantile(ratios, .99)),
                                edge_stretch_999=float(np.quantile(ratios, .999)),
                                rigid_max_edge_stretch=float(max(rigid_ratios))))
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)
    sheet = Image.new("RGB", (1500, 1480), "#0b111b")
    for i, ((label, _), frame) in enumerate(zip(POSES[form], frames)):
        im = m.render(form, (500, 680), 155 if "rear" in label else 28, pitch=5, frame=frame)
        x, y = i % 3 * 500, i // 3 * 720
        sheet.paste(im, (x, y))
        ImageDraw.Draw(sheet).text((x + 18, y + 687), label, font=font, fill="#bfd5ee")
    ImageDraw.Draw(sheet).text((18, 1450), "Actual runtime mesh / compiled animation poses / offline preview", font=font, fill="#8aacc6")
    sheet.save(ROOT / f".build/seraph-{form}-articulated-poses.png")
    (ROOT / f".build/seraph-{form}-articulation-review.json").write_text(json.dumps(report, indent=2))
    # Mobile shoulder/elbow undersuits deform through the overhead aim pose.
    # Check their full distribution as well as the worst edge, while fortress
    # panels must remain rigid. These guards supplement the rendered review.
    for entry in report:
        assert entry["max_edge_stretch"] < (3 if form == "mobile" else 1.015), (form, entry)
        assert entry["edge_stretch_99"] < (1.45 if form == "mobile" else 1.015), (form, entry)
        assert entry["edge_stretch_999"] < (1.9 if form == "mobile" else 1.015), (form, entry)
        assert entry["rigid_max_edge_stretch"] < 1.005, (form, entry)
    print(form, "PASS 18 pose/LOD deformation reviews; rendered six poses.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--form", choices=["mobile", "siege"])
    args = parser.parse_args()
    for form in ["mobile", "siege"]:
        if not args.form or args.form == form:
            review(form)
