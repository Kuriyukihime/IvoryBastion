"""Fit the Seraph rig, bake reference colors, and export three four-weight LODs."""
from pathlib import Path
import argparse
import json
import struct
import numpy as np
from scipy.sparse import coo_matrix, diags
from scipy.sparse.csgraph import dijkstra, connected_components
from scipy.spatial import cKDTree
import trimesh
import pymeshlab
from seraph_dense_surface import ROOT, Projector, bake, reference


def load_raw(form):
    if form == "mobile" and (ROOT / "assets/source/seraph-body-dense.glb").exists():
        from seraph_components import mobile_components
        return mobile_components()
    raw = trimesh.load(ROOT / f"assets/source/seraph-{form}-dense.glb", force="mesh", process=False)
    pieces = [m for m in raw.split(only_watertight=False) if len(m.faces) >= 32]
    raw = trimesh.util.concatenate(pieces)
    raw.remove_unreferenced_vertices()
    lo, hi = raw.bounds
    height = 1.80 if form == "mobile" else 2.55
    raw.vertices = (raw.vertices - [(lo[0] + hi[0]) * .5, lo[1], 0]) * height / (hi[1] - lo[1])
    v = np.asarray(raw.vertices)
    if form == "mobile":
        waist = v[(np.abs(v[:, 0]) < .095) & (v[:, 1] > .98) & (v[:, 1] < 1.18)]
        raw.vertices[:, 2] -= np.quantile(waist[:, 2], .5)
    else:
        # Center the circular chassis, not the long forward barrel.
        base = v[(v[:, 1] > .40) & (v[:, 1] < .66)]
        raw.vertices[:, 2] -= (np.quantile(base[:, 2], .05) + np.quantile(base[:, 2], .95)) * .5
    return raw


def simplify(mesh, count):
    if len(mesh.faces) <= count:
        return mesh.copy()
    ms = pymeshlab.MeshSet()
    ms.add_mesh(pymeshlab.Mesh(vertex_matrix=np.asarray(mesh.vertices), face_matrix=np.asarray(mesh.faces)))
    ms.apply_filter("meshing_decimation_quadric_edge_collapse", targetfacenum=count, preservenormal=True,
                    preserveboundary=True, optimalplacement=True, planarquadric=True)
    result = ms.current_mesh()
    return trimesh.Trimesh(result.vertex_matrix(), result.face_matrix(), process=False)


def unit_normals(vertices, normals):
    """Repair undefined normals on the reconstruction's tiny degenerate seams."""
    result = np.asarray(normals, dtype=np.float64).copy()
    lengths = np.linalg.norm(result, axis=1)
    valid = np.isfinite(result).all(axis=1) & (lengths > 1e-8)
    if not valid.all():
        assert valid.any(), "A reconstruction must contain defined surface normals."
        _, nearest = cKDTree(vertices[valid]).query(vertices[~valid])
        result[~valid] = result[valid][nearest]
        lengths = np.linalg.norm(result, axis=1)
    return result / lengths[:, None]


def fit_bones(form, raw):
    modular = "reference_components" in raw.metadata
    if modular:
        body = raw.metadata["reference_components"][0][1]
        lo, hi = body.bounds
        _, (x0, y0, x1, y1) = reference("body", "front")
    else:
        lo, hi = raw.bounds
        _, (x0, y0, x1, y1) = reference(form, "front")

    def front(px, py, z=0):
        return np.array([lo[0] + (px - x0) / (x1 - x0) * (hi[0] - lo[0]),
                         hi[1] - (py - y0) / (y1 - y0) * (hi[1] - lo[1]), z])

    if form == "mobile":
        if modular:
            bones = [("root", np.zeros(3)), ("torso", front(512, 525)), ("head", front(512, 242))]
            for side, sign in [("l", -1), ("r", 1)]:
                def p(x, y, z=0):
                    return front(512 + sign * (512 - x), y, z)
                bones += [
                    ("arm_" + side, p(381, 310)),
                    ("fore_" + side, p(317, 488, .015)),
                    ("hand_" + side, p(240, 665, .035)),
                    ("leg_" + side, p(437, 600)),
                    ("boot_" + side, p(417, 911, .012)),
                    ("wing_" + side, np.array([sign * .175, 1.39, -.035])),
                    ("vane_" + side, np.array([sign * .175, 1.245, -.035]))]
            for side, sign in [("l", -1), ("r", 1)]:
                identifier = 1 if sign < 0 else 3
                wing = np.asarray(raw.vertices)[raw.metadata["piece_ids"] == identifier]
                vane = np.asarray(raw.vertices)[raw.metadata["piece_ids"] == identifier + 1]
                bones += [
                    ("muzzle_" + side, front(512 + sign * (512 - 138), 878, .035)),
                    ("tip_wing_" + side, wing[np.argmax(wing[:, 1])]),
                    ("tip_vane_" + side, vane[np.argmax(vane[:, 0] * sign)])]
            return bones
        bones = [("root", np.zeros(3)), ("torso", front(512, 594)), ("head", front(512, 328))]
        for side, sign in [("l", -1), ("r", 1)]:
            def p(x, y, z=0):
                return front(512 + sign * (512 - x), y, z)
            bones += [
                ("arm_" + side, p(389, 390)),
                ("fore_" + side, p(320, 550, .075)),
                ("hand_" + side, p(244, 753, .18)),
                ("leg_" + side, p(438, 696)),
                ("boot_" + side, p(417, 969, .012)),
                ("wing_" + side, p(368, 342, .005)),
                ("vane_" + side, p(344, 463, -.015))]
        for side, sign in [("l", -1), ("r", 1)]:
            def p(x, y, z=0):
                return front(512 + sign * (512 - x), y, z)
            bones += [
                ("muzzle_" + side, p(148, 966, .295)),
                ("tip_wing_" + side, p(145, 17, .015)),
                ("tip_vane_" + side, p(60, 745, .005))]
        return bones

    bones = [("root", np.zeros(3)), ("torso", front(768, 445, -.27)),
             ("head", front(768, 378, -.27)), ("cannon", np.array([0, 1.025, 1.02])),
             ("halo", front(768, 366, -.48)), ("radar", front(768, 365, -.48))]
    for name, sign, rear in [("rack_l", -1, False), ("rack_r", 1, False),
                             ("shield_l", -1, True), ("shield_r", 1, True)]:
        bones.append((name, np.array([sign * 1.10, .74, -1.05 if rear else .90])))
    for i in range(6):
        a = (i + .5) * np.pi / 3
        bones.append(("anchor_" + str(i), np.array([np.sin(a) * .78, .43, np.cos(a) * .78])))
    for side, sign in [("l", -1), ("r", 1)]:
        bones += [("arm_" + side, front(768 + sign * 48, 404, -.27)),
                  ("fore_" + side, front(768 + sign * 65, 439, -.19)),
                  ("hand_" + side, front(768 + sign * 83, 449, -.08))]
    for i in range(4):
        a = (i + .5) * np.pi / 2
        bones.append(("vent_" + str(i), bones[3][1] + np.array([np.cos(a) * .23, np.sin(a) * .23, 0])))
    for rack in range(4):
        sign, rear = (-1 if rack % 2 == 0 else 1), rack >= 2
        for row in range(6):
            for col in range(3):
                py = 455 - row * 53
                px = 768 + sign * (275 + col * 48 + row * 9)
                point = front(px, py)
                vertices = np.asarray(raw.vertices)
                allowed = (vertices[:, 0] * sign > .4) & (vertices[:, 1] > .9) & ((vertices[:, 2] < -.5) if rear else (vertices[:, 2] > .4))
                candidates = vertices[allowed]
                distance = np.sum((candidates[:, :2] - point[:2]) ** 2, axis=1)
                nearby = candidates[np.argpartition(distance, 24)[:24]]
                point = nearby[np.argmax(nearby[:, 2])] + [0, 0, .015]
                bones.append((f"hatch_{rack}_{row}_{col}", point))
    v = np.asarray(raw.vertices)
    barrel = v[(np.abs(v[:, 0]) < .25) & (v[:, 1] > .75) & (v[:, 1] < 1.4)]
    tip = barrel[barrel[:, 2] > np.quantile(barrel[:, 2], .997)].mean(axis=0)
    bones.append(("cannon_muzzle", tip + [0, 0, .018]))
    return bones


def segment(v, a, b):
    delta = b - a
    t = np.clip((v - a) @ delta / max(np.dot(delta, delta), 1e-9), 0, 1)
    return np.linalg.norm(v - (a + t[:, None] * delta), axis=1), t


def mobile_labels(v, bones):
    names = {name: i for i, (name, _) in enumerate(bones)}
    p = dict(bones)
    x, y, z = v.T
    labels = np.full(len(v), names["root"], int)
    labels[y > .98] = names["torso"]
    labels[(y > p["head"][1]) & (np.abs(x) < .145)] = names["head"]
    for side, sign in [("l", -1), ("r", 1)]:
        same = x * sign > 0
        thigh = same & (y < 1.0)
        labels[thigh] = names["leg_" + side]
        labels[thigh & (y < p["boot_" + side][1])] = names["boot_" + side]
        arm_dist, _ = segment(v, p["arm_" + side], p["fore_" + side])
        fore_dist, t = segment(v, p["fore_" + side], p["muzzle_" + side])
        arm = same & (np.abs(x) > .135) & (y > .65) & (y < 1.40) & (np.minimum(arm_dist, fore_dist) < .105)
        labels[arm] = names["arm_" + side]
        labels[arm & (fore_dist < arm_dist)] = names["fore_" + side]
        # Forearm-integrated guns stay rigid with forearm; hands articulate only
        # their small visible shell, avoiding the stretched weapon of a draft rig.
        hand_dist = np.linalg.norm(v - p["hand_" + side], axis=1)
        hand = arm & (hand_dist < .032) & (z > .015)
        labels[hand] = names["hand_" + side]
        upper, _ = segment(v, p["wing_" + side], p["tip_wing_" + side])
        lower, _ = segment(v, p["vane_" + side], p["tip_vane_" + side])
        wing = same & (np.abs(x) > .13) & (y > 1.34)
        vane = same & (np.abs(x) > .205) & (y > .88) & (y < 1.35) & (z < -.025) & (lower < .14)
        # Near the lower blade's broad outer edge there is no arm in front.
        vane |= same & (np.abs(x) > .40) & (y > .90) & (y < 1.3) & (lower < .16)
        labels[wing] = names["wing_" + side]
        labels[vane] = names["vane_" + side]
    return labels


def siege_labels(v, bones):
    names = {name: i for i, (name, _) in enumerate(bones)}
    p = dict(bones)
    x, y, z = v.T
    labels = np.full(len(v), names["root"], int)
    for side, sign in [("l", -1), ("r", 1)]:
        panel = (x * sign > .25) & (y > .78) & ((z > .42) | (z < -.62))
        labels[panel] = names["rack_" + side]
        labels[panel & (z < -.28)] = names["shield_" + side]
    cannon = (np.abs(x) < .43) & (y > .79) & (y < 1.36) & (z > 1.02)
    labels[cannon] = names["cannon"]
    center = p["halo"]
    ring_radius = np.sqrt(x * x + (y - center[1]) ** 2)
    halo = (ring_radius > .35) & (ring_radius < .60) & (np.abs(x) < .60) & (y > 1.25) & (z > -.62) & (z < .20)
    labels[halo] = names["halo"]
    pilot = (np.abs(x) < .26) & (y > 1.40) & (y < 1.82) & (z > -.42) & (z < .06)
    labels[pilot] = names["torso"]
    labels[pilot & (y > p["head"][1])] = names["head"]
    for side, sign in [("l", -1), ("r", 1)]:
        arm = pilot & (x * sign > .095) & (y < p["head"][1])
        labels[arm] = names["arm_" + side]
        labels[arm & (z > -.15)] = names["fore_" + side]
    foot = (y < .66) & (np.sqrt(x*x + z*z) > .95)
    angles = np.arctan2(x, z) % (np.pi*2)
    for i in range(6):
        labels[foot & (np.floor(angles / (np.pi/3)).astype(int) == i)] = names["anchor_" + str(i)]
    return labels


def mobile_geodesic_labels(v, tri, bones):
    """Follow the surface to each blade/gun instead of crossing nearby air gaps."""
    names, p = {n: i for i, (n, _) in enumerate(bones)}, dict(bones)
    tree = cKDTree(v)
    seeds = {}

    def points(name, values):
        for point in values:
            _, index = tree.query(point)
            seeds[int(index)] = names[name]

    def line(name, a, b, steps=6):
        a, b = np.asarray(a), np.asarray(b)
        points(name, [a * (1 - t) + b * t for t in np.linspace(.18, .95, steps)])

    points("root", [[0, .94, .05], [0, .94, -.05], [-.07, .97, .02], [.07, .97, .02]])
    for z in [-.075, .075]:
        points("torso", [[0, 1.12, z], [0, 1.27, z], [-.075, 1.22, z], [.075, 1.22, z]])
        points("head", [p["head"] + [0, .10, z], p["head"] + [0, .18, z]])
    for side, sign in [("l", -1), ("r", 1)]:
        line("arm_" + side, p["arm_" + side], p["fore_" + side])
        line("fore_" + side, p["fore_" + side], p["muzzle_" + side] + [sign * .025, -.065, 0], 10)
        # The whole glove and integrated accelerator form a rigid gauntlet.
        line("leg_" + side, p["leg_" + side], p["boot_" + side], 8)
        line("boot_" + side, p["boot_" + side], [sign * .15, .055, .025], 10)
        line("wing_" + side, p["wing_" + side], p["tip_wing_" + side], 12)
        line("vane_" + side, p["vane_" + side], p["tip_vane_" + side], 12)
    edges = np.concatenate([tri[:, [0, 1]], tri[:, [1, 2]], tri[:, [2, 0]]])
    edges = np.unique(np.sort(edges, axis=1), axis=0)
    length = np.maximum(1e-8, np.linalg.norm(v[edges[:, 0]] - v[edges[:, 1]], axis=1))
    edges = np.concatenate([edges, edges[:, ::-1]])
    graph = coo_matrix((np.tile(length, 2), (edges[:, 0], edges[:, 1])), shape=(len(v), len(v))).tocsr()
    distances, _, sources = dijkstra(graph, directed=False, indices=np.array(list(seeds)), min_only=True, return_predecessors=True)
    labels = mobile_labels(v, bones)
    lookup = np.zeros(len(v), int)
    for index, label in seeds.items():
        lookup[index] = label
    valid = np.isfinite(distances)
    labels[valid] = lookup[sources[valid]]
    x, y, z = v.T
    for side, sign in [("l", -1), ("r", 1)]:
        # Reconstruction places the wing skins behind the gauntlet, but their
        # stems touch the shoulder. Keep the complete rigid blade on its hinge.
        upper = (x * sign > .13) & (y > 1.385)
        lower = (x * sign > .215) & (y > .875) & (y < 1.335) & (z < .060)
        labels[upper] = names["wing_" + side]
        labels[lower] = names["vane_" + side]
    return labels


def skin(v, tri, bones, form, raw=None):
    modular = raw is not None and "piece_ids" in raw.metadata
    edges = np.concatenate([tri[:, [0, 1]], tri[:, [1, 2]], tri[:, [2, 0]]])
    if modular:
        names, p = {n: i for i, (n, _) in enumerate(bones)}, dict(bones)
        x, y, z = v.T
        _, nearest = cKDTree(np.asarray(raw.vertices)).query(v)
        owner = raw.metadata["piece_ids"][nearest].copy()
        graph = coo_matrix((np.ones(len(edges)), (edges[:, 0], edges[:, 1])), shape=(len(v), len(v)))
        count, connected = connected_components(graph, directed=False)
        # A nearby wing skin must not claim individual vertices of the body.
        for component in range(count):
            selection = connected == component
            owner[selection] = np.bincount(owner[selection], minlength=5).argmax()
        labels = np.full(len(v), names["root"], int)
        labels[y > 1.03] = names["torso"]
        labels[(y > p["head"][1]) & (np.abs(x) < .15)] = names["head"]
        for side, sign in [("l", -1), ("r", 1)]:
            leg = (x * sign > 0) & (y < 1.03)
            labels[leg] = names["leg_" + side]
            labels[leg & (y < p["boot_" + side][1])] = names["boot_" + side]
            a, _ = segment(v, p["arm_" + side], p["fore_" + side])
            b, _ = segment(v, p["fore_" + side], p["muzzle_" + side])
            # Follow the A-pose arm corridor as it widens toward the hands.
            # A fixed X threshold incorrectly assigned lateral hip plates to the
            # forearm, pulling the thighs outward during the Blue Shift flourish.
            inner_edge = .145 + np.maximum(0, 1.35 - y) * .39
            arm = (x * sign > inner_edge) & (y > .64) & (y < 1.43) & (np.minimum(a, b) < .14)
            labels[arm] = names["arm_" + side]
            labels[arm & (b < a)] = names["fore_" + side]
            # Follow the connected forearm down to the muzzle. This includes the
            # full depth of the gun while excluding nearby, separate hip armor.
            roi = (owner == 0) & (y > .60) & (y < 1.24) & (x * sign > .10)
            region_edges = edges[roi[edges].all(axis=1)]
            region_graph = coo_matrix((np.ones(len(region_edges)), (region_edges[:, 0], region_edges[:, 1])), shape=(len(v), len(v)))
            _, regions = connected_components(region_graph, directed=False)
            candidates = np.flatnonzero(roi)
            seed = candidates[np.argmin(np.linalg.norm(v[candidates] - p["muzzle_" + side], axis=1))]
            limb = roi & (regions == regions[seed])
            labels[limb] = names["fore_" + side]
            labels[limb & (y > p["fore_" + side][1])] = names["arm_" + side]
        for identifier, name in enumerate(["wing_l", "vane_l", "wing_r", "vane_r"], 1):
            labels[owner == identifier] = names[name]
    else:
        labels = mobile_geodesic_labels(v, tri, bones) if form == "mobile" else siege_labels(v, bones)
    w = np.zeros((len(v), len(bones)), np.float32)
    w[np.arange(len(v)), labels] = 1
    edges = np.unique(np.sort(edges, axis=1), axis=0)
    edges = edges[edges[:, 0] != edges[:, 1]]
    edges = np.concatenate([edges, edges[:, ::-1]])
    # Reconstruction includes tiny sliver triangles beside much larger ones.
    # Equal neighbor weights produce visible discontinuities over those very
    # short edges. Weight diffusion by physical edge length instead.
    edge_weights = (1 / np.maximum(1e-6, np.linalg.norm(v[edges[:, 0]] - v[edges[:, 1]], axis=1))).astype(np.float32) if modular else np.ones(len(edges), np.float32)
    adjacency = coo_matrix((edge_weights, (edges[:, 0], edges[:, 1])), shape=(len(v), len(v))).tocsr()
    adjacency = diags(1 / np.maximum(1, np.asarray(adjacency.sum(axis=1)).ravel())) @ adjacency
    # Surface diffusion only; a gap between a wing and an arm never blends them.
    iterations = max(24, round((330 if modular else 220) * np.sqrt(len(v) / 160000)))
    for _ in range(iterations):
        w = .5 * w + .5 * (adjacency @ w)
    indices = np.argsort(w, axis=1)[:, -4:][:, ::-1]
    weights = np.take_along_axis(w, indices, axis=1)
    weights /= np.maximum(weights.sum(axis=1)[:, None], 1e-9)
    return indices.astype(np.int32), weights


def articulate_armor(data, triangles, bone_count):
    """Split mechanical armor at articulation seams, never stretch a metal face.

    The reconstruction is continuous across several mechanical joints. A single
    face cannot be rubber-skinned between two independently rotating plates.
    Duplicate just those seam vertices, retaining exact positions and baked UVs.
    """
    dominant = data["b"][np.arange(len(data)), np.argmax(data["w"], axis=1)]
    labels = np.sort(dominant[triangles], axis=1)[:, 1]
    keys = triangles.astype(np.int64) * bone_count + labels[:, None]
    unique, inverse = np.unique(keys, return_inverse=True)
    source, bone = unique // bone_count, unique % bone_count
    result = data[source].copy()
    result["b"] = 0
    result["w"] = 0
    result["b"][:, 0] = bone
    result["w"][:, 0] = 1
    return result, inverse.reshape(-1, 3).astype(np.int32)


def export(form):
    raw = load_raw(form)
    bones = fit_bones(form, raw)
    print(f"{form}: {len(raw.faces):,} source triangles; fitting {len(bones)} bones", flush=True)
    meshes = [simplify(raw, count) for count in [320000, 85000, 18000]]
    mappings, triangles, uvs, albedo, projector = bake(form, meshes, raw)
    stats = []

    def string(f, s):
        data = s.encode()
        assert len(data) < 128
        f.write(bytes([len(data)]))
        f.write(data)

    target = ROOT / f"assets/{form}.meshbin"
    with target.with_suffix(".meshbin.tmp").open("wb") as f:
        f.write(b"BAST3")
        f.write(struct.pack("<i", len(bones)))
        for name, anchor in bones:
            string(f, name)
            f.write(struct.pack("<3fi", *anchor, -1))
        f.write(struct.pack("<i", 3))
        for lod, mesh in enumerate(meshes):
            v, tri = np.asarray(mesh.vertices), np.asarray(mesh.faces)
            n = unit_normals(v, np.asarray(mesh.vertex_normals))
            indices, weights = skin(v, tri, bones, form, raw)
            mapping, tri, uv = mappings[lod], triangles[lod], uvs[lod]
            dtype = np.dtype([("v", "<f4", 3), ("n", "<f4", 3), ("uv", "<f4", 2), ("b", "<i4", 4), ("w", "<f4", 4)])
            data = np.empty(len(mapping), dtype=dtype)
            data["v"], data["n"], data["uv"] = v[mapping], n[mapping], uv
            data["b"], data["w"] = indices[mapping], weights[mapping]
            if form == "siege":
                data, tri = articulate_armor(data, tri, len(bones))
            string(f, "Reconstructed_LOD" + str(lod))
            f.write(struct.pack("<5f2i", 1, 1, 1, .30, 1.35, len(data), tri.size))
            f.write(data.tobytes())
            f.write(np.asarray(tri, "<i4").tobytes())
            stats.append(dict(lod=lod, vertices=len(data), triangles=len(tri)))
            if lod == 0:
                preview = trimesh.Trimesh(data["v"], tri, vertex_normals=data["n"], process=False)
                preview.visual = trimesh.visual.TextureVisuals(uv=data["uv"], image=albedo)
                preview.export(ROOT / f"dist/IvoryBastion-Seraph-{form}-model.glb")
            print(f"{form}: exported LOD {lod}, {len(tri):,} triangles", flush=True)
    target.with_suffix(".meshbin.tmp").replace(target)
    info = dict(format="BAST3", authoring="scripts/import_seraph_reconstruction.py", source_triangles=len(raw.faces),
                bones=len(bones), lods=stats, bounds=raw.bounds.tolist(), texture=albedo.size,
                source="Seraph concept references reconstructed with Nightbound's cached Hunyuan3D-2mv pipeline")
    (ROOT / f"assets/{form}-info.json").write_text(json.dumps(info, indent=2))
    (ROOT / f".build/seraph-{form}-rig.json").write_text(json.dumps({n: a.tolist() for n, a in bones}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("form", choices=["mobile", "siege"])
    args = parser.parse_args()
    export(args.form)
