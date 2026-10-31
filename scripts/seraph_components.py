"""Keep reconstructed mobile armor and back-mounted wing mechanisms separate."""
from pathlib import Path
import numpy as np
import trimesh

ROOT = Path(__file__).resolve().parents[1]


def source(form, height):
    mesh = trimesh.load(ROOT / f"assets/source/seraph-{form}-dense.glb", force="mesh", process=False)
    mesh = trimesh.util.concatenate([m for m in mesh.split(only_watertight=False) if len(m.faces) >= 32])
    lo, hi = mesh.bounds
    mesh.vertices = (mesh.vertices - [(lo[0] + hi[0]) / 2, lo[1], 0]) * height / (hi[1] - lo[1])
    v = np.asarray(mesh.vertices)
    waist = v[(np.abs(v[:, 0]) < .095) & (v[:, 1] > .98) & (v[:, 1] < 1.18)]
    mesh.vertices[:, 2] -= np.quantile(waist[:, 2], .5)
    return mesh


def mobile_components():
    body = source("body", 1.67)
    old = source("mobile", 1.80)
    v = np.asarray(old.vertices)
    x, y, z = v.T
    pieces, piece_ids = [body], [np.zeros(len(body.vertices), np.int32)]
    for side, sign in enumerate([-1, 1]):
        for upper in [True, False]:
            if upper:
                region = (x * sign > .14) & (y > 1.39)
            else:
                region = (x * sign > .265) & (y > .875) & (y < 1.335) & (z < .075)
            selected = np.flatnonzero(region[old.faces].all(axis=1))
            wing = old.submesh([selected], append=True, repair=False)
            # The lower-wing crop also contains a disconnected section of the
            # original gauntlet. Keep the blade component, not that spare arm.
            wing = max(wing.split(only_watertight=False), key=lambda mesh: mesh.area)
            # Small mechanical couplers occupy the seam left by the component cut.
            start = np.array([sign * .175, 1.39 if upper else 1.245, -.035])
            end = np.array([sign * .245, 1.425 if upper else 1.22, .005])
            axis = end - start
            coupler = trimesh.creation.cylinder(radius=.025, height=np.linalg.norm(axis), sections=24,
                                                 transform=trimesh.geometry.align_vectors([0, 0, 1], axis))
            coupler.apply_translation((start + end) / 2)
            wing = trimesh.util.concatenate([wing, coupler])
            identifier = 1 + side * 2 + (0 if upper else 1)
            pieces.append(wing)
            piece_ids.append(np.full(len(wing.vertices), identifier, np.int32))
    combined = trimesh.util.concatenate(pieces)
    combined.metadata["piece_ids"] = np.concatenate(piece_ids)
    combined.metadata["reference_components"] = [("body", body), ("mobile", old)]
    return combined
