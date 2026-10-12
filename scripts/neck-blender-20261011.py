"""neck-blender: hide the seam where an avatar's head meets its body.

Many VRChat bases ship the head and the body as separate meshes. The body's neck pokes out through the head's
lower surface, and the two textures are painted separately, so a hard colour line shows under the jaw and around
the back of the neck. The fix that works (it is how Fae's body texture is painted) is to paint the body texture
around the neck so that, right at the line where the body leaves the head, it is exactly the head's skin colour,
then fade back to the body's own skin a few centimetres down.

This script does that from the real geometry, not by eye:

1. Import the avatar FBX (Blender, headless). Find the body vertices that sit under the head's surface, and the
   line where the body's neck comes out of the head.
2. For each point on that line, read the head texture's colour where the head surface is (barycentric UV).
3. Give every body vertex a weight: 1 under the head and within --near of the line, smooth fade to 0 at --fade.
4. Rasterise the body triangles in UV space and blend the head colours into a copy of the body texture.
   Texels no body triangle uses are filled too (up to 48 px) so mipmaps don't bleed the old colour. Texels other
   islands use are left alone apart from a 3 px margin at the neck islands' own edges.

5. Colour alone isn't enough when the head and body meet at an angle: matcap and rim lighting read the two
   surfaces' normals, so one side renders brighter than the other right at the line (Poppy's neck rendered about
   8% brighter than her jaw even with matching texels). So the script also writes two masks, one per material, that
   fade matcap and rim to 0 at the line and back to full strength by --light-fade. Both sides then render as their
   (matching) texture at the line.

It never overwrites the original textures. With --unity-mat and --unity-face-mat it writes .meta files (new GUIDs;
masks are linear), points the body _MainTex at the new texture and both materials' _MatcapMask and _RimMask at the
masks, after backing everything up. Locked Poiyomi materials compile mask sampling out when no mask was set at lock
time, so it also adds `#define PROP_MATCAPMASK` and `#define PROP_RIMMASK` next to `#define PROP_MATCAP` in each
material's locked shader (the same lines a relock would add). Unlocking and relocking later keeps the masks.

Run with Python that has the `bpy` module (pip install bpy), numpy, Pillow and scipy:

    python scripts/neck-blender-20261011.py --fbx "<avatar>.fbx" --body-tex "<body base>.png" \
        --head-tex "<face base>.png" --out "<body base> - neck blend.png" \
        [--body Body --head Face --head-material Head] [--body-tint 1,0.985,0.969] \
        [--near 0.004 --fade 0.045 --light-fade 0.03] \
        [--unity-mat "<Body>.mat" --unity-face-mat "<Face>.mat" --backup-dir "<outside Assets>"]

--body-tint is the body material's _Color; the head colour is divided by it so the tinted result matches.
"""
import argparse, json, os, re, shutil, sys, uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage


def parse_args(argv):
    p = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    p.add_argument('--fbx', required=True)
    p.add_argument('--body-tex', required=True)
    p.add_argument('--head-tex', required=True)
    p.add_argument('--out', required=True)
    p.add_argument('--body', default='Body', help='body mesh object name')
    p.add_argument('--head', default='Face', help='head mesh object name')
    p.add_argument('--head-material', default='Head', help='head skin material name prefix')
    p.add_argument('--body-tint', default='1,1,1', help='body material _Color r,g,b')
    p.add_argument('--near', type=float, default=0.004, help='metres of full head colour below the line')
    p.add_argument('--fade', type=float, default=0.045, help='metres from the line where the blend ends')
    p.add_argument('--light-fade', type=float, default=0.03, help='metres over which matcap and rim fade back in')
    p.add_argument('--mask-size', type=int, default=1024)
    p.add_argument('--unity-mat', help='body .mat: _MainTex -> new texture, matcap/rim masks -> body mask')
    p.add_argument('--unity-face-mat', help='face .mat: matcap/rim masks -> face mask')
    p.add_argument('--backup-dir', help='where to back up the .mat (keep it outside Assets)')
    p.add_argument('--weights-json', help='optional: also write the per-triangle weights here')
    return p.parse_args(argv)


def neck_weights(fbx, body_name, head_name, head_mat, near, fade, light_fade):
    """Blender part: per-triangle body UVs, blend weights and head UVs to sample."""
    import bpy, bmesh
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    from mathutils.kdtree import KDTree
    from mathutils.geometry import barycentric_transform

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    B = bpy.data.objects[body_name]
    F = bpy.data.objects[head_name]
    slots = [i for i, s in enumerate(F.material_slots) if s.material and s.material.name.startswith(head_mat)]
    if not slots:
        raise SystemExit(f'no material starting with {head_mat!r} on {head_name!r}: '
                         f'{[s.material.name for s in F.material_slots if s.material]}')
    hi = slots[0]
    # Work in world space so object transforms can't throw the match off.
    Bw, Fw = B.matrix_world, F.matrix_world
    Bn = Bw.to_3x3().inverted().transposed()

    bmf = bmesh.new(); bmf.from_mesh(F.data); bmf.transform(Fw)
    bmf.verts.ensure_lookup_table(); bmf.faces.ensure_lookup_table()
    uvf = bmf.loops.layers.uv.active
    hfaces = [f for f in bmf.faces if f.material_index == hi]
    bvh = BVHTree.FromPolygons([v.co.copy() for v in bmf.verts], [[v.index for v in f.verts] for f in hfaces])

    def head_uv(co):
        loc, _n, idx, _d = bvh.find_nearest(co)
        ls = hfaces[idx].loops; best = None
        for k in range(1, len(ls) - 1):
            tri = (ls[0], ls[k], ls[k + 1])
            a, b, c = [l.vert.co for l in tri]
            ua, ub, uc = [Vector((*l[uvf].uv, 0)) for l in tri]
            bc = barycentric_transform(loc, a, b, c, Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
            score = min(bc)
            if best is None or score > best[0]:
                p = barycentric_transform(loc, a, b, c, ua, ub, uc); best = (score, (p.x, p.y))
        return best[1]

    m = B.data
    co = {v.index: Bw @ v.co for v in m.vertices}
    nrm = {v.index: (Bn @ v.normal).normalized() for v in m.vertices}
    # Only the upper part of the body can touch the head.
    head_low = min(v.co.z for f in hfaces for v in f.verts)
    zmin = head_low - fade * 2
    covered = set()
    for i, c in co.items():
        if c.z < zmin: continue
        hit = bvh.ray_cast(c + nrm[i] * 0.0003, nrm[i], 0.03)
        if hit[0] is not None: covered.add(i)
    if not covered:
        raise SystemExit('no body vertices under the head; check --body/--head names (are they one mesh already?)')
    nb = {v.index: set() for v in m.vertices}
    for e in m.edges:
        a, b = e.vertices; nb[a].add(b); nb[b].add(a)
    pts = []
    for v in covered:
        for n in nb[v]:
            if n not in covered:
                mid = (co[v] + co[n]) / 2; pts.append((mid, head_uv(mid)))
    kd = KDTree(len(pts))
    for i, (c, _) in enumerate(pts): kd.insert(c, i)
    kd.balance()
    vw, vc = {}, {}
    for i, c in co.items():
        if c.z < zmin: continue
        _c, j, d = kd.find(c)
        if i in covered or d <= near: w = 1.0
        elif d < fade:
            t = (d - near) / (fade - near); w = 1 - t * t * (3 - 2 * t)
        else: continue
        vw[i] = w; vc[i] = pts[j][1]
    uvl = m.uv_layers.active.data
    tris, all_tris = [], []
    for p in m.polygons:
        ls = list(p.loop_indices)
        hit = any(m.loops[li].vertex_index in vw for li in ls)
        for k in range(1, len(ls) - 1):
            tri = (ls[0], ls[k], ls[k + 1])
            all_tris.append([list(uvl[li].uv) for li in tri])
            if hit:
                tris.append([{'uv': list(uvl[li].uv), 'w': vw.get(m.loops[li].vertex_index, 0.0),
                              'fuv': list(vc.get(m.loops[li].vertex_index, (0, 0))),
                              'has': m.loops[li].vertex_index in vw} for li in tri])
    def lm(d):
        if d >= light_fade: return 1.0
        t = d / light_fade; return t * t * (3 - 2 * t)
    bmask = {i: (0.0 if i in covered else lm(kd.find(c)[2])) for i, c in co.items() if c.z >= zmin}
    btris = []
    for p in m.polygons:
        ls = list(p.loop_indices)
        if not any(bmask.get(m.loops[li].vertex_index, 1.0) < 1 for li in ls): continue
        for k in range(1, len(ls) - 1):
            btris.append([{'uv': list(uvl[li].uv), 'm': bmask.get(m.loops[li].vertex_index, 1.0)} for li in (ls[0], ls[k], ls[k + 1])])
    fm = F.data; fuv = fm.uv_layers.active.data; fmask = {}
    for v in fm.vertices: fmask[v.index] = lm(kd.find(Fw @ v.co)[2])
    ftris, fall = [], []
    for p in fm.polygons:
        if p.material_index != hi: continue
        ls = list(p.loop_indices); near_line = any(fmask[fm.loops[li].vertex_index] < 1 for li in ls)
        for k in range(1, len(ls) - 1):
            tri = (ls[0], ls[k], ls[k + 1])
            fall.append([list(fuv[li].uv) for li in tri])
            if near_line: ftris.append([{'uv': list(fuv[li].uv), 'm': fmask[fm.loops[li].vertex_index]} for li in tri])
    print(f'neck line points {len(pts)}, body verts under head {len(covered)}, blended verts {len(vw)}')
    return tris, all_tris, (btris, all_tris), (ftris, fall)


def paint(body_tex, head_tex, tint, tris, all_tris, out):
    B = np.asarray(Image.open(body_tex).convert('RGBA')).astype(np.float32) / 255
    Fc = np.asarray(Image.open(head_tex).convert('RGB').filter(ImageFilter.GaussianBlur(3))).astype(np.float32) / 255
    H, W = B.shape[:2]; FH, FW = Fc.shape[:2]

    def fs(uv):
        return Fc[min(FH - 1, max(0, int((1 - uv[1] % 1) * FH))), min(FW - 1, max(0, int(uv[0] % 1 * FW)))]

    wmap = np.zeros((H, W), np.float32); cmap = np.zeros((H, W, 3), np.float32); hit = np.zeros((H, W), bool)
    for tri in tris:
        cols = [fs(v['fuv']) if v['has'] else None for v in tri]
        if all(c is None for c in cols): continue
        fb = next(c for c in cols if c is not None)
        cols = np.array([c if c is not None else fb for c in cols]) / tint
        P = np.array([[v['uv'][0] * W, (1 - v['uv'][1]) * H] for v in tri]); ws = np.array([v['w'] for v in tri])
        x0, y0 = np.floor(P.min(0)).astype(int) - 1; x1, y1 = np.ceil(P.max(0)).astype(int) + 1
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, W - 1), min(y1, H - 1)
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = P
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-9: continue
        l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den
        l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
        l3 = 1 - l1 - l2
        inside = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        w = l1 * ws[0] + l2 * ws[1] + l3 * ws[2]
        c = l1[..., None] * cols[0] + l2[..., None] * cols[1] + l3[..., None] * cols[2]
        sl = (slice(y0, y1 + 1), slice(x0, x1 + 1))
        upd = inside & (w > wmap[sl])
        wmap[sl][upd] = w[upd]; cmap[sl][upd] = c[upd]; hit[sl] |= inside
    cov = Image.new('L', (W, H), 0); dr = ImageDraw.Draw(cov)
    for t in all_tris: dr.polygon([(u * W, (1 - v) * H) for u, v in t], fill=255)
    used = np.asarray(cov) > 0
    dist, idx = ndimage.distance_transform_edt(~hit, return_indices=True)
    grow = hit | (dist <= 3) | ((dist <= 48) & ~used)
    wmap = np.where(grow, wmap[idx[0], idx[1]], 0); cmap = cmap[idx[0], idx[1]]
    wmap = ndimage.gaussian_filter(wmap, 2.0) * grow
    res = B.copy(); res[..., :3] = B[..., :3] * (1 - wmap[..., None]) + np.clip(cmap, 0, 1) * wmap[..., None]
    Image.fromarray((np.clip(res, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA').save(out)
    changed = np.abs(res - B).max(2) > 2 / 255
    print(f'wrote {out}: {int(changed.sum())} texels changed')


def light_mask(tris, all_tris, size, out):
    """1 = full matcap/rim, 0 = none. Written to RGB (R is the channel Poiyomi reads); alpha 0 = no rim bias."""
    W = H = size; val = np.ones((H, W), np.float32); hit = np.zeros((H, W), bool)
    for tri in tris:
        P = np.array([[v['uv'][0] * W, (1 - v['uv'][1]) * H] for v in tri]); ms = np.array([v['m'] for v in tri])
        x0, y0 = np.floor(P.min(0)).astype(int) - 1; x1, y1 = np.ceil(P.max(0)).astype(int) + 1
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, W - 1), min(y1, H - 1)
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        (ax, ay), (bx, by), (cx, cy) = P; den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-9: continue
        l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den; l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
        l3 = 1 - l1 - l2; ins = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02); v = l1 * ms[0] + l2 * ms[1] + l3 * ms[2]
        sl = (slice(y0, y1 + 1), slice(x0, x1 + 1)); upd = ins & (v < val[sl]); val[sl][upd] = v[upd]; hit[sl] |= ins
    cov = Image.new('L', (W, H), 0); dr = ImageDraw.Draw(cov)
    for t in all_tris: dr.polygon([(u * W, (1 - v) * H) for u, v in t], fill=255)
    used = np.asarray(cov) > 0
    dist, idx = ndimage.distance_transform_edt(~hit, return_indices=True)
    grow = hit | (dist <= 2) | ((dist <= 24) & ~used)
    val = np.where(grow, val[idx[0], idx[1]], 1.0)
    val = np.minimum(val, np.where(grow, ndimage.gaussian_filter(val, 1.0), 1.0))
    img = (np.clip(val, 0, 1) * 255 + .5).astype(np.uint8)
    Image.fromarray(np.dstack([img, img, img, np.zeros_like(img)]), 'RGBA').save(out)
    print(f'wrote {out}')


def _meta(src_meta, dst, linear=False, max_size=None):
    gid = uuid.uuid4().hex
    m = re.sub(r'^guid: \w+', f'guid: {gid}', open(src_meta).read(), count=1, flags=re.M)
    if linear: m = m.replace('sRGBTexture: 1', 'sRGBTexture: 0')
    if max_size: m = re.sub(r'maxTextureSize: \d+', f'maxTextureSize: {max_size}', m)
    with open(dst + '.meta', 'w', newline='') as f: f.write(m)
    return gid


def _backup(path, backup_dir, name=None):
    os.makedirs(backup_dir, exist_ok=True)
    dst = os.path.join(backup_dir, name or os.path.basename(path))
    if not os.path.exists(dst): shutil.copy2(path, dst)  # keep the first (original) copy


def set_masks(mat, mask_guid, backup_dir, tag):
    text = open(mat).read(); _backup(mat, backup_dir)
    for prop in ('_MatcapMask', '_RimMask'):
        pat = re.compile(r'(- ' + prop + r':\n\s+m_Texture: )\{[^}]*\}')
        if not pat.search(text): raise SystemExit(f'{prop} not found in {mat}')
        text = pat.sub(r'\g<1>{fileID: 2800000, guid: ' + mask_guid + ', type: 3}', text, count=1)
    with open(mat, 'w', newline='') as f: f.write(text)
    sg = re.search(r'm_Shader: \{fileID: \d+, guid: (\w+)', text).group(1)
    if 'OriginalShader:' not in text: return  # unlocked: nothing compiled out
    root = mat
    while root and os.path.basename(root) != 'Assets': root = os.path.dirname(root)
    shader = None
    for dp, _d, fs in os.walk(root or os.path.dirname(mat)):
        for fn in fs:
            if fn.endswith('.shader.meta') and f'guid: {sg}' in open(os.path.join(dp, fn)).read():
                shader = os.path.join(dp, fn[:-5])
    if not shader: raise SystemExit(f'locked shader for {mat} not found; unlock and relock it in Poiyomi instead')
    s = open(shader, newline='').read(); _backup(shader, backup_dir, f'{tag}-{os.path.basename(shader)}')
    nl = '\r\n' if '\r\n' in s else '\n'; key = '#define PROP_MATCAP ' + nl
    if '#define PROP_MATCAPMASK' not in s:
        s = s.replace(key, key + '#define PROP_MATCAPMASK ' + nl + '#define PROP_RIMMASK ' + nl)
        with open(shader, 'w', newline='') as f: f.write(s)
    print(f'{os.path.basename(mat)}: matcap/rim masks set; locked shader updated')


def repoint_unity(body_tex, out, mat, backup_dir):
    src_meta = body_tex + '.meta'
    old = re.search(r'^guid: (\w+)', open(src_meta).read(), re.M).group(1)
    new = uuid.uuid4().hex
    with open(out + '.meta', 'w', newline='') as f:
        f.write(re.sub(r'^guid: \w+', f'guid: {new}', open(src_meta).read(), count=1, flags=re.M))
    text = open(mat).read()
    pat = re.compile(r'(- _MainTex:\n\s+m_Texture: \{fileID: 2800000, guid: )' + old)
    if not pat.search(text):
        raise SystemExit(f'{mat} does not use {os.path.basename(body_tex)} as _MainTex; left it alone')
    if backup_dir: _backup(mat, backup_dir)
    with open(mat, 'w', newline='') as f: f.write(pat.sub(r'\g<1>' + new, text, count=1))
    print(f'{os.path.basename(mat)} _MainTex -> {os.path.basename(out)} (guid {new})')


def main(argv=None):
    a = parse_args(sys.argv[1:] if argv is None else argv)
    if (a.unity_mat or a.unity_face_mat) and not a.backup_dir:
        raise SystemExit('--unity-mat/--unity-face-mat need --backup-dir (outside Assets) so the old files can be restored')
    tint = np.array([float(x) for x in a.body_tint.split(',')][:3], np.float32)
    tris, all_tris, bl, fl = neck_weights(a.fbx, a.body, a.head, a.head_material, a.near, a.fade, a.light_fade)
    if a.weights_json: json.dump({'tris': tris}, open(a.weights_json, 'w'))
    paint(a.body_tex, a.head_tex, tint, tris, all_tris, a.out)
    d = os.path.dirname(os.path.abspath(a.out))
    body_mask, face_mask = os.path.join(d, 'Body Neck Mask.png'), os.path.join(d, 'Face Neck Mask.png')
    light_mask(*bl, a.mask_size, body_mask); light_mask(*fl, a.mask_size, face_mask)
    if a.unity_mat:
        repoint_unity(a.body_tex, a.out, a.unity_mat, a.backup_dir)
        set_masks(a.unity_mat, _meta(a.body_tex + '.meta', body_mask, True, a.mask_size), a.backup_dir, 'body')
    if a.unity_face_mat:
        set_masks(a.unity_face_mat, _meta(a.body_tex + '.meta', face_mask, True, a.mask_size), a.backup_dir, 'face')


if __name__ == '__main__':
    main()
