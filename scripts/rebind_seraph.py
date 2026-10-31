"""Rebind reconstructed assets without regenerating meshes or UV atlases."""
import argparse
import struct
import numpy as np
import presentation_mesh as presentation
from import_seraph_reconstruction import ROOT, skin, fit_bones, load_raw, articulate_armor, unit_normals


def rebind(form, normals_only=False):
    levels = [presentation.load(form, lod) for lod in range(3)]
    raw = None if normals_only else load_raw(form)
    bones = levels[0][0] if normals_only else fit_bones(form, raw)
    target = ROOT / f"assets/{form}.meshbin"
    temp = target.with_suffix(".meshbin.tmp")

    def string(f, s):
        b = s.encode()
        f.write(bytes([len(b)]))
        f.write(b)

    with temp.open("wb") as f:
        f.write(b"BAST3")
        f.write(struct.pack("<i", len(bones)))
        for name, anchor in bones:
            string(f, name)
            f.write(struct.pack("<3fi", *anchor, -1))
        f.write(struct.pack("<i", 3))
        for lod, (_, parts) in enumerate(levels):
            part = parts[0]
            # Restore surface adjacency across the independent UV charts.
            if not normals_only:
                v, _, inverse = np.unique(np.round(part["v"], 7), axis=0, return_index=True, return_inverse=True)
                indices, weights = skin(v, inverse[part["t"]], bones, form, raw)
            dtype = np.dtype([("v", "<f4", 3), ("n", "<f4", 3), ("uv", "<f4", 2), ("b", "<i4", 4), ("w", "<f4", 4)])
            data = np.empty(len(part["v"]), dtype=dtype)
            for key in ["v", "n", "uv"]:
                data[key] = part[key]
            data["n"] = unit_normals(part["v"], part["n"])
            data["b"], data["w"] = (part["b"], part["w"]) if normals_only else (indices[inverse], weights[inverse])
            data, tri = articulate_armor(data, part["t"], len(bones)) if form == "siege" and not normals_only else (data, part["t"])
            string(f, part["name"])
            f.write(struct.pack("<5f2i", *part["color"], part["smooth"], part["glow"], len(data), tri.size))
            f.write(data.tobytes())
            f.write(np.asarray(tri, "<i4").tobytes())
            print(form, "rebound LOD", lod, flush=True)
    temp.replace(target)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("form", choices=["mobile", "siege"])
    parser.add_argument("--normals-only", action="store_true")
    args = parser.parse_args()
    rebind(args.form, args.normals_only)
