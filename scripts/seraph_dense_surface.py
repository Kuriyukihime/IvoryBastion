"""Reference projection and padded UV baking for the reconstructed Seraph meshes.

The reference PNGs are immutable. This converts their colors into a mesh atlas.
"""
from pathlib import Path
import hashlib
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt
from scipy.spatial import cKDTree
from numba import njit
import xatlas

ROOT = Path(__file__).resolve().parents[1]


def reference(form, view):
    rgba = np.asarray(Image.open(ROOT / f"assets/source/seraph-{form}-{view}.png").convert("RGBA"))
    mask = rgba[:, :, 3] > 180
    yy, xx = np.nonzero(mask)
    _, nearest = distance_transform_edt(~mask, return_indices=True)
    return rgba[nearest[0], nearest[1], :3], (xx.min(), yy.min(), xx.max(), yy.max())


def sample(im, x, y):
    x = np.clip(x, 0, im.shape[1] - 1)
    y = np.clip(y, 0, im.shape[0] - 1)
    x0, y0 = x.astype(int), y.astype(int)
    x1, y1 = np.minimum(x0 + 1, im.shape[1] - 1), np.minimum(y0 + 1, im.shape[0] - 1)
    tx, ty = (x - x0)[:, None], (y - y0)[:, None]
    return (im[y0, x0] * (1 - tx) + im[y0, x1] * tx) * (1 - ty) + (im[y1, x0] * (1 - tx) + im[y1, x1] * tx) * ty


@njit(cache=True)
def raster_depth(vertices, faces, front, back):
    height, width = front.shape
    for ids in faces:
        a, b, c = vertices[ids[0]], vertices[ids[1]], vertices[ids[2]]
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-10:
            continue
        for y in range(max(0, int(min(a[1], b[1], c[1]))), min(height, int(max(a[1], b[1], c[1])) + 2)):
            for x in range(max(0, int(min(a[0], b[0], c[0]))), min(width, int(max(a[0], b[0], c[0])) + 2)):
                wa = ((b[1] - c[1]) * (x - c[0]) + (c[0] - b[0]) * (y - c[1])) / den
                wb = ((c[1] - a[1]) * (x - c[0]) + (a[0] - c[0]) * (y - c[1])) / den
                wc = 1 - wa - wb
                if min(wa, wb, wc) >= -.001:
                    depth = a[2] * wa + b[2] * wb + c[2] * wc
                    front[y, x] = max(front[y, x], depth)
                    back[y, x] = min(back[y, x], depth)


class Projector:
    def __init__(self, form, raw):
        self.form = form
        self.bounds = np.asarray(raw.bounds)
        self.sub = None
        if "reference_components" in raw.metadata:
            self.sub = [Projector(name, mesh) for name, mesh in raw.metadata["reference_components"]]
            self.tree = cKDTree(np.asarray(raw.vertices))
            self.owner = (raw.metadata["piece_ids"] > 0).astype(int)
            return
        self.refs = [reference(form, view) for view in ["front", "back", "side"]]
        # Choose the source view from actual surface depth, not tiny changes in
        # facet normals. Normal-based view blending painted alternating front/
        # back details across curved armor and produced the striped distortion.
        lo, hi = self.bounds
        vertices = np.asarray(raw.vertices).copy()
        vertices[:, 0] = (vertices[:, 0] - lo[0]) / (hi[0] - lo[0]) * 639
        vertices[:, 1] = (hi[1] - vertices[:, 1]) / (hi[1] - lo[1]) * 1023
        front = np.full((1024, 640), -np.inf, np.float32)
        back = np.full_like(front, np.inf)
        raster_depth(vertices, np.asarray(raw.faces), front, back)
        valid = np.isfinite(front) & np.isfinite(back)
        _, nearest = distance_transform_edt(~valid, return_indices=True)
        self.depth = np.stack([front[nearest[0], nearest[1]], back[nearest[0], nearest[1]]], axis=2)

    def pixels(self, vertices, view):
        lo, hi = self.bounds
        box = self.refs[view][1]
        x0, y0, x1, y1 = box
        if view < 2:
            u = (vertices[:, 0] - lo[0]) / (hi[0] - lo[0])
            if view == 1:
                u = 1 - u
        else:
            u = (hi[2] - vertices[:, 2]) / (hi[2] - lo[2])
        return x0 + u * (x1 - x0), y1 - (vertices[:, 1] - lo[1]) / (hi[1] - lo[1]) * (y1 - y0)

    def __call__(self, v, n):
        if self.sub is not None:
            _, nearest = self.tree.query(v)
            owner = self.owner[nearest]
            result = np.empty((len(v), 3))
            for i, sub in enumerate(self.sub):
                selected = owner == i
                result[selected] = sub(v[selected], n[selected])
            return result
        colors = [sample(im, *self.pixels(v, i)) / 255 for i, (im, _) in enumerate(self.refs)]
        lo, hi = self.bounds
        xy = (v[:, :2] - lo[:2]) / (hi[:2] - lo[:2])
        depth = sample(self.depth, xy[:, 0] * 639, (1 - xy[:, 1]) * 1023)
        thickness = np.maximum(.004, depth[:, 0] - depth[:, 1])
        front = np.clip(((v[:, 2] - depth[:, 1]) / thickness - .2) / .6, 0, 1)
        front = front * front * (3 - 2 * front)
        base = colors[1] * (1 - front[:, None]) + colors[0] * front[:, None]
        side = np.clip((np.abs(n[:, 0]) - .48) / .42, 0, 1)
        side = side * side * (3 - 2 * side)
        # A side reference exposes only the outer wing surface. Keep recesses
        # and front-facing details sourced from the matching front/rear cameras.
        side *= 0 if self.form in ["mobile", "body"] else .25
        out = base * (1 - side[:, None]) + colors[2] * side[:, None]
        if self.form == "siege":
            # A frontal photograph cannot describe the length of the cannon:
            # projecting it there repeats the circular muzzle down the barrel.
            barrel = (np.abs(v[:, 0]) < .46) & (v[:, 2] > 1.02) & (v[:, 1] > .77) & (v[:, 1] < 1.38)
            if barrel.any():
                q = v[barrel]
                im = self.refs[2][0]
                px = 66 + (self.bounds[1, 2] - q[:, 2]) / (self.bounds[1, 2] - 1.02) * 610
                py = 630 - (q[:, 1] - 1.025) * 385
                rail = sample(im, px, py) / 255
                cap = np.clip((q[:, 2] - self.bounds[1, 2] + .08) / .06, 0, 1) * np.clip(n[barrel, 2], 0, 1)
                out[barrel] = rail * (1 - cap[:, None]) + colors[0][barrel] * cap[:, None]
        return np.clip(out, 0, 1)


@njit(cache=True)
def raster(v, n, uv, tri, positions, normals, mask):
    height, width = mask.shape
    for face in tri:
        a, b, c = face
        ax, ay = uv[a, 0] * (width - 1), (1 - uv[a, 1]) * (height - 1)
        bx, by = uv[b, 0] * (width - 1), (1 - uv[b, 1]) * (height - 1)
        cx, cy = uv[c, 0] * (width - 1), (1 - uv[c, 1]) * (height - 1)
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-8:
            continue
        for y in range(max(0, int(min(ay, by, cy))), min(height, int(max(ay, by, cy)) + 2)):
            for x in range(max(0, int(min(ax, bx, cx))), min(width, int(max(ax, bx, cx)) + 2)):
                wa = ((by - cy) * (x - cx) + (cx - bx) * (y - cy)) / den
                wb = ((cy - ay) * (x - cx) + (ax - cx) * (y - cy)) / den
                wc = 1 - wa - wb
                if min(wa, wb, wc) < -.001:
                    continue
                mask[y, x] = True
                for j in range(3):
                    positions[y, x, j] = v[a, j] * wa + v[b, j] * wb + v[c, j] * wc
                    normals[y, x, j] = n[a, j] * wa + n[b, j] * wb + n[c, j] * wc


def bake(form, meshes, raw):
    fingerprint = hashlib.sha256()
    for mesh in meshes:
        fingerprint.update(np.asarray(mesh.vertices, np.float32).tobytes())
        fingerprint.update(np.asarray(mesh.faces, np.int32).tobytes())
    cache = ROOT / f".build/seraph-{form}-uv-{fingerprint.hexdigest()[:14]}.npz"
    if cache.exists():
        data = np.load(cache)
        mappings, triangles, uvs = ([data[f"{prefix}{i}"] for i in range(3)] for prefix in ["m", "t", "u"])
        width, height = map(int, data["size"])
    else:
        atlas = xatlas.Atlas()
        for mesh in meshes:
            atlas.add_mesh(np.asarray(mesh.vertices, np.float32), np.asarray(mesh.faces, np.uint32), np.asarray(mesh.vertex_normals, np.float32))
        pack = xatlas.PackOptions()
        pack.resolution, pack.padding, pack.bilinear = 4096, 8, True
        chart = xatlas.ChartOptions()
        chart.max_iterations = 2
        print(form + ": unwrapping three detail levels", flush=True)
        atlas.generate(chart, pack)
        mappings, triangles, uvs, data = [], [], [], {}
        for i in range(3):
            m, t, u = atlas[i]
            mappings.append(m)
            triangles.append(t)
            uvs.append(u)
            data.update({f"m{i}": m, f"t{i}": t, f"u{i}": u})
        width, height = atlas.width, atlas.height
        data["size"] = np.array([width, height])
        np.savez_compressed(cache, **data)
    ratio = min(1, 4096 / max(width, height))
    width, height = round(width * ratio), round(height * ratio)
    print(f"{form}: baking {width} x {height} surface", flush=True)
    positions = np.zeros((height, width, 3), np.float32)
    normals = np.zeros_like(positions)
    mask = np.zeros((height, width), bool)
    for i, mesh in enumerate(meshes):
        raster(np.asarray(mesh.vertices, np.float32)[mappings[i]], np.asarray(mesh.vertex_normals, np.float32)[mappings[i]], uvs[i], triangles[i], positions, normals, mask)
    projector = Projector(form, raw)
    output = np.zeros((height, width, 3), np.uint8)
    for row in range(0, height, 96):
        active = mask[row:row + 96]
        output[row:row + 96][active] = np.uint8(projector(positions[row:row + 96][active], normals[row:row + 96][active]) * 255)
    _, nearest = distance_transform_edt(~mask, return_indices=True)
    output = output[nearest[0], nearest[1]]
    albedo = Image.fromarray(output)
    albedo.save(ROOT / f"assets/{form}_surface.png")
    rgb = output.astype(np.float32) / 255
    strength = np.clip((rgb[:, :, 2] - rgb[:, :, 0] * 1.15 - .08) * 3, 0, 1)
    emission = np.uint8(rgb * strength[:, :, None] * 255)
    Image.fromarray(emission).save(ROOT / f"assets/{form}_emission.png")
    return mappings, triangles, uvs, albedo, projector
