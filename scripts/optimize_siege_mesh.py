"""Reduce only the shipped siege mesh; retain its atlas, bones and rigid parts.

Run with the presentation-requirements environment. Writes a candidate by
default, so visual/deformation review precedes replacing the runtime asset.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

import numpy as np
import pymeshlab
from scipy.spatial import cKDTree
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
DTYPE = np.dtype([('v', '<f4', 3), ('n', '<f4', 3), ('uv', '<f4', 2),
                  ('b', '<i4', 4), ('w', '<f4', 4)])


def textured_mesh(vertices, normals, faces, uv):
    # MeshLab's ndarray constructor leaves wedge texture IDs invalid in this
    # release. Its OBJ importer correctly binds every corner to material zero.
    work = ROOT / '.build/siege-140/mesh-work'
    work.mkdir(parents=True, exist_ok=True)
    Image.new('RGB', (8, 8), 'white').save(work / 'qem.png')
    (work / 'qem.mtl').write_text('newmtl qem\nmap_Kd qem.png\n', encoding='utf-8')
    path = work / 'qem.obj'
    with path.open('w', encoding='utf-8', newline='\n') as stream:
        stream.write('mtllib qem.mtl\n')
        np.savetxt(stream, vertices, fmt='v %.9g %.9g %.9g')
        np.savetxt(stream, normals, fmt='vn %.9g %.9g %.9g')
        np.savetxt(stream, uv, fmt='vt %.9g %.9g')
        stream.write('usemtl qem\n')
        corners = np.empty((len(faces), 3, 3), dtype=np.int64)
        corners[:, :, 0] = faces + 1
        corners[:, :, 1] = np.arange(len(faces) * 3).reshape(-1, 3) + 1
        corners[:, :, 2] = faces + 1
        np.savetxt(stream, corners.reshape(-1, 9), fmt='f %d/%d/%d %d/%d/%d %d/%d/%d')
    working = pymeshlab.MeshSet()
    working.load_new_mesh(str(path))
    return working


def read_mesh(path):
    with Path(path).open('rb') as stream:
        def string():
            length = stream.read(1)[0]
            assert length < 128
            return stream.read(length).decode('utf-8')
        def integer():
            return struct.unpack('<i', stream.read(4))[0]
        assert stream.read(5) == b'BAST3'
        bones = []
        for _ in range(integer()):
            bones.append((string(), struct.unpack('<3fi', stream.read(16))))
        prefix_end = stream.tell()
        parts = []
        for _ in range(integer()):
            name = string()
            material = struct.unpack('<5f', stream.read(20))
            vertices, indices = integer(), integer()
            data = np.frombuffer(stream.read(vertices * DTYPE.itemsize), DTYPE).copy()
            faces = np.frombuffer(stream.read(indices * 4), '<i4').reshape(-1, 3).copy()
            parts.append((name, material, data, faces))
        assert not stream.read(1)
    return Path(path).read_bytes()[:prefix_end], bones, parts


def compact(data, faces):
    used, remap = np.unique(faces, return_inverse=True)
    return data[used], remap.reshape(-1, 3).astype('<i4')


def reduce_part(data, faces, ratio, bones, lod, fixed_vertices=False):
    assert np.all(data['w'][:, 0] == 1) and np.all(data['w'][:, 1:] == 0), 'Keep blended rigs out of rigid optimization'
    face_bones = data['b'][faces, 0]
    assert np.all(face_bones == face_bones[:, :1]), 'Never collapse across an articulated seam'
    output, output_faces, stats = [], [], []
    offset = 0
    for bone in np.unique(face_bones[:, 0]):
        original, triangles = compact(data, faces[face_bones[:, 0] == bone])
        count = len(triangles)
        bone_name = bones[int(bone)][0]
        # Spend more of the close-range budget on the recognizable circular
        # cannon/halo and seated pilot. Chassis subdivisions remain cheaper.
        hero = bone_name in ('cannon', 'halo', 'radar', 'head', 'torso') or bone_name.startswith(('arm_', 'fore_', 'hand_', 'leg_', 'boot_'))
        local_ratio = max(ratio, (.60, .55, .50)[lod]) if hero else ratio
        if count <= 192:
            reduced, new_faces = original, triangles
        else:
            # Restore geometric adjacency across UV/normal seams so closed
            # shells stay closed. Original hard corner normals are restored
            # after decimation; genuine open boundaries stay pinned.
            key = np.round(original['v'], 7)
            _, unique, mapping = np.unique(key, axis=0, return_index=True, return_inverse=True)
            working = textured_mesh(original['v'][unique], original['n'][unique],
                                    mapping[triangles], original['uv'][triangles].reshape(-1, 2))
            working.apply_filter('meshing_decimation_quadric_edge_collapse_with_texture',
                                 targetfacenum=max(96, int(count * local_ratio)), qualitythr=.3,
                                 extratcoordw=1.0, preserveboundary=True, boundaryweight=10,
                                 preservenormal=True, optimalplacement=not fixed_vertices, planarquadric=True)
            mesh = working.current_mesh()
            corner_positions = mesh.vertex_matrix()[mesh.face_matrix()].reshape(-1, 3)
            corner_uv = mesh.wedge_tex_coord_matrix()
            assert corner_uv.shape == (len(corner_positions), 2)
            reduced = np.zeros(len(corner_positions), dtype=DTYPE)
            reduced['v'] = corner_positions
            reduced['uv'] = np.clip(corner_uv, 0, 1)
            # Retain the authored hard/satin normals instead of smoothing every
            # bevel together. Atlas coordinates disambiguate coincident seams.
            tree = cKDTree(np.column_stack((original['v'], original['uv'] * 2)))
            _, nearest = tree.query(np.column_stack((corner_positions, corner_uv * 2)))
            reduced['n'] = original['n'][nearest]
            reduced['b'][:, 0] = bone
            reduced['w'][:, 0] = 1
            # Keep a single indexed vertex for identical full attributes.
            packed = np.ascontiguousarray(reduced).view(np.dtype((np.void, DTYPE.itemsize))).reshape(-1)
            _, unique, mapping = np.unique(packed, return_index=True, return_inverse=True)
            reduced, new_faces = reduced[unique], mapping.reshape(-1, 3).astype('<i4')
            if len(new_faces) >= count:
                reduced, new_faces = original, triangles
        output.append(reduced)
        output_faces.append(new_faces + offset)
        offset += len(reduced)
        stats.append({'bone': int(bone), 'name': bone_name, 'before': count, 'after': len(new_faces)})
    return np.concatenate(output), np.concatenate(output_faces), stats


def optimize(source, destination, ratios, fixed_vertices=False):
    prefix, bones, parts = read_mesh(source)
    assert len(parts) == len(ratios) == 3
    result, report = [], []
    for index, ((name, material, data, faces), ratio) in enumerate(zip(parts, ratios)):
        reduced, triangles, groups = reduce_part(data, faces, ratio, bones, index, fixed_vertices)
        assert len(triangles) < len(faces)
        assert np.isfinite(reduced['v']).all() and np.isfinite(reduced['n']).all()
        result.append((name, material, reduced, triangles))
        report.append({'lod': index, 'beforeVertices': len(data), 'vertices': len(reduced),
                       'beforeTriangles': len(faces), 'triangles': len(triangles), 'groups': groups})
        print(f'Siege LOD{index}: {len(faces):,} -> {len(triangles):,} triangles; {len(data):,} -> {len(reduced):,} vertices', flush=True)
    destination = Path(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with destination.open('wb') as stream:
        stream.write(prefix)
        stream.write(struct.pack('<i', len(result)))
        for name, material, data, faces in result:
            encoded = name.encode('utf-8')
            stream.write(bytes([len(encoded)]))
            stream.write(encoded)
            stream.write(struct.pack('<5f2i', *material, len(data), faces.size))
            stream.write(data.tobytes())
            stream.write(np.asarray(faces, '<i4').tobytes())
    output_prefix, output_bones, _ = read_mesh(destination)
    assert output_prefix == prefix and output_bones == bones
    evidence = {'source': str(source), 'candidate': str(destination), 'ratios': ratios,
                'fixedVertices': fixed_vertices, 'bonesUnchanged': True,
                'texturesChanged': False, 'sha256': hashlib.sha256(destination.read_bytes()).hexdigest(), 'lods': report}
    destination.with_suffix('.json').write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=ROOT / '.build/siege-140/before/assets/siege.meshbin')
    parser.add_argument('--output', type=Path, default=ROOT / '.build/siege-140/candidate.meshbin')
    parser.add_argument('--ratios', type=float, nargs=3, default=[.40, .45, .45])
    parser.add_argument('--fixed-vertices', action='store_true')
    args = parser.parse_args()
    assert all(0 < ratio < 1 for ratio in args.ratios)
    optimize(args.source, args.output, args.ratios, args.fixed_vertices)
