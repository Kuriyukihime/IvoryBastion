"""Use the same cached multiview reconstruction pipeline as Nightbound.

Only the resulting geometry is shipped. References and raw GLBs stay in source.
"""
from pathlib import Path
import argparse
import json
import os
import sys

ROOT = Path(__file__).resolve().parents[1]
TOOLING = ROOT.parent / "Nightbound" / ".build"
sys.path.insert(0, str(TOOLING / "shape-code"))
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["TRANSFORMERS_OFFLINE"] = "1"
os.environ["HF_XET_NUM_CONCURRENT_RANGE_GETS"] = "8"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("form", choices=["mobile", "siege", "body"])
    parser.add_argument("--resolution", type=int, default=512)
    parser.add_argument("--views", nargs="+", default=["front", "back"])
    args = parser.parse_args()
    import torch
    from PIL import Image
    from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline

    torch.set_num_threads(8)
    print("Loading cached Hunyuan3D-2mv on " + torch.cuda.get_device_name(0), flush=True)
    pipeline = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
        str(TOOLING / "shape-model"), subfolder="hunyuan3d-dit-v2-mv",
        use_safetensors=True, variant="fp16", device="cuda")
    images = {
        view: Image.open(ROOT / f"assets/source/seraph-{args.form}-{view}.png").convert("RGBA")
        for view in args.views
    }
    print("Reconstructing Seraph " + args.form, flush=True)
    mesh = pipeline(
        image=images, num_inference_steps=40, guidance_scale=5.5,
        octree_resolution=args.resolution, num_chunks=6000,
        generator=torch.Generator(device="cuda").manual_seed(3117))[0]
    target = ROOT / f"assets/source/seraph-{args.form}-dense.glb"
    mesh.export(target)
    meta = dict(
        vertices=len(mesh.vertices), triangles=len(mesh.faces), bounds=mesh.bounds.tolist(),
        engine="Hunyuan3D-2mv", resolution=args.resolution, steps=40, seed=3117,
        source_images=[f"seraph-{args.form}-{view}.png" for view in args.views])
    target.with_suffix(".json").write_text(json.dumps(meta, indent=2))
    print(json.dumps(meta), flush=True)


if __name__ == "__main__":
    main()
