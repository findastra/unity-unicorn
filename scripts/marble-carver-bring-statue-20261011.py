"""marble-carver step 4 (headless Blender Python): turn a carved statue (OBJ + baked PNGs from 03_bake_export.py) into
House files - PMS2 mesh + LOD1 with UVs, textures, and a Materials.json entry. Self-contained (needs only bpy, numpy,
Pillow), so it runs in the cloud workspace or in Blender's own Python.

    python3 bring_statue.py NAME SRC_DIR SCAN_NORMAL.png SCAN_OCCLUSION.png HEIGHT_M OUT_ROOT [lod_tris]

OUT_ROOT is the folder holding Meshes/, Imported/ and Data/ (the repo's out/ or the Unity project's Assets/Piranesi).
Writes OUT_ROOT/Meshes/NAME.bytes, NAME_LOD1.bytes, OUT_ROOT/Imported/NAME/*.png and merges OUT_ROOT/Data/Materials.json.
Prints the statue's top points (Unity space, base at the origin) for seats and footholds.
"""
import os, sys, json, struct
import numpy as np
from PIL import Image
import bpy

def export_pms2(ob, path, sub_names):
    """PMS2: Unity space (Blender x, z, y; winding flipped), per-corner normals and UVs deduplicated."""
    dg = bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(dg).to_mesh(); me.calc_loop_triangles()
    nl = len(me.loops); nt = len(me.loop_triangles)
    co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    lv = np.empty(nl, np.int32); me.loops.foreach_get('vertex_index', lv)
    cn = np.empty(nl * 3, np.float32); me.corner_normals.foreach_get('vector', cn); cn = cn.reshape(-1, 3)
    uv = np.zeros((nl, 2), np.float32)
    if me.uv_layers.active:
        b = np.empty(nl * 2, np.float32); me.uv_layers.active.data.foreach_get('uv', b); uv = b.reshape(-1, 2)
    tl = np.empty(nt * 3, np.int32); me.loop_triangles.foreach_get('loops', tl); tl = tl.reshape(-1, 3)
    tm = np.empty(nt, np.int32); me.loop_triangles.foreach_get('material_index', tm)
    key = np.concatenate([lv[:, None].astype(np.int64), np.round(cn * 500).astype(np.int64), np.round(uv * 8192).astype(np.int64)], 1)
    uniq, inv = np.unique(key, axis=0, return_inverse=True); inv = inv.ravel()
    first = np.zeros(len(uniq), np.int64); first[inv[::-1]] = np.arange(nl)[::-1]
    P = co[lv[first]][:, [0, 2, 1]]; N = cn[first][:, [0, 2, 1]]; U = uv[first]
    C = np.full((len(P), 4), 255, np.uint8)
    tris = inv[tl][:, [0, 2, 1]]
    subs = [(nm, tris[tm == mi].ravel().astype(np.int32)) for mi, nm in enumerate(sub_names) if (tm == mi).any()]
    with open(path, 'wb') as f:
        f.write(b'PMS2'); f.write(struct.pack('<iii', len(P), len(subs), 1))
        f.write(P.astype('<f4').tobytes()); f.write(N.astype('<f4').tobytes()); f.write(C.tobytes()); f.write(U.astype('<f4').tobytes())
        for nm, idx in subs:
            bn = nm.encode(); f.write(struct.pack('<i', len(bn))); f.write(bn)
            f.write(struct.pack('<i', len(idx))); f.write(idx.astype('<i4').tobytes())
    ob.evaluated_get(dg).to_mesh_clear()
    return P

def decimate(ob, target):
    ob.data.calc_loop_triangles(); n = len(ob.data.loop_triangles)
    if n <= target: return n
    mod = ob.modifiers.new('dec', 'DECIMATE'); mod.ratio = target / n; mod.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    ob.modifiers.clear(); old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
    me.calc_loop_triangles(); return len(me.loop_triangles)

def main(name, src, normal_png, occl_png, height, out_root, lod_tris=7000):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.obj_import(filepath=os.path.join(src, name + '.obj'), forward_axis='Y', up_axis='Z')
    ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    V = np.array([v.co for v in ob.data.vertices])
    z0 = V[:, 2].min(); H = V[:, 2].max() - z0
    feet = V[V[:, 2] < z0 + 0.08 * H]
    cx, cy = (feet[:, 0].min() + feet[:, 0].max()) / 2, (feet[:, 1].min() + feet[:, 1].max()) / 2
    s = height / H
    for v in ob.data.vertices:                     # base centre to origin, scale, turn 180 deg -> faces +Z in Unity
        x, y, z = v.co.x - cx, v.co.y - cy, v.co.z - z0
        v.co = (-x * s, -y * s, z * s)
    ob.data.update()
    ob.data.materials.clear(); ob.data.materials.append(bpy.data.materials.new('Marble'))
    sub = f'{name}_Marble'
    os.makedirs(os.path.join(out_root, 'Meshes'), exist_ok=True)
    Pu = export_pms2(ob, os.path.join(out_root, 'Meshes', name + '.bytes'), [sub])
    n = decimate(ob, lod_tris)
    export_pms2(ob, os.path.join(out_root, 'Meshes', name + '_LOD1.bytes'), [sub])
    d = os.path.join(out_root, 'Imported', name); os.makedirs(d, exist_ok=True)
    Image.open(os.path.join(src, f'{name}_Albedo.png')).convert('RGB').save(os.path.join(d, f'{name}_Albedo.png'))
    Image.open(normal_png).convert('RGB').resize((2048, 2048), Image.LANCZOS).save(os.path.join(d, f'{name}_Normal.png'))
    rough = np.asarray(Image.open(os.path.join(src, f'{name}_Roughness.png')).convert('L').resize((2048, 2048)), np.float32) / 255
    occ = np.asarray(Image.open(occl_png).convert('L').resize((2048, 2048)), np.float32) / 255
    mask = np.stack([1 - rough, 0.35 + 0.65 * occ, np.zeros_like(occ)], -1)
    Image.fromarray((np.clip(mask, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(d, f'{name}_Mask.png'))
    entry = dict(sub=sub, shader='prop', albedo=f'{name}/{name}_Albedo.png', normal=f'{name}/{name}_Normal.png',
                 mask=f'{name}/{name}_Mask.png', alpha='OPAQUE', smooth=1.0, metal=0.0, doubleSided=False)
    mp = os.path.join(out_root, 'Data', 'Materials.json')
    man = json.load(open(mp)) if os.path.exists(mp) else {}
    man[name] = [entry]; man[name + '_LOD1'] = [entry]
    os.makedirs(os.path.dirname(mp), exist_ok=True); json.dump(man, open(mp, 'w'), indent=1)
    top = Pu[np.argsort(-Pu[:, 1])[:5]].round(3).tolist()
    print(f'{name}: {len(Pu)} verts, height {height} m, LOD1 {n} tris; highest points {top}')

if __name__ == '__main__':
    a = sys.argv[1:]
    main(a[0], a[1], a[2], a[3], float(a[4]), a[5], int(a[6]) if len(a) > 6 else 7000)
