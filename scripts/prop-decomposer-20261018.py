# prop-decomposer (Claude Opus 5.5, 2026-10-18) - Unity Unicorn tool.
# Reverse-engineers a realistic reference prop (CC0 scan or model) into its sub-components so a stylised or
# rebuilt version can copy what makes it read as real: proportions, part list, part sizes, bevel widths,
# material zones and how detail is split between geometry and texture.
#
# Run (Blender 4.x, headless):
#   blender -b --factory-startup --python-exit-code 1 --python prop-decomposer-20261018.py -- <model.gltf|.glb|.fbx|.obj> <out-dir> [min-part-tris]
# Writes <out-dir>/decomposition-<name>-YYYYMMDD.json (parts, sizes, bevels, material zones) and renders:
#   hero, front, side, exploded (parts pushed apart, numbered by size) and a part sheet.
import bpy, bmesh, sys, os, json, math, datetime
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = os.path.abspath(argv[0]), os.path.abspath(argv[1])
MIN_TRIS = int(argv[2]) if len(argv) > 2 else 12
os.makedirs(OUT, exist_ok=True)
NAME = os.path.splitext(os.path.basename(SRC))[0]
STAMP = datetime.date.today().strftime("%Y%m%d")

bpy.ops.wm.read_factory_settings(use_empty=True)
ext = SRC.lower().rsplit(".", 1)[-1]
if ext in ("gltf", "glb"): bpy.ops.import_scene.gltf(filepath=SRC)
elif ext == "fbx": bpy.ops.import_scene.fbx(filepath=SRC)
elif ext == "obj": bpy.ops.wm.obj_import(filepath=SRC)
else: raise SystemExit("unsupported format " + ext)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes: raise SystemExit("no meshes")
# world-space bounds of the whole prop
def wb(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))), Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
lo, hi = wb(meshes); size = hi - lo
report = {"source": os.path.basename(SRC), "date": STAMP, "objects": [], "parts": [], "materials": {},
          "overall_mm": [round(size.x * 1000), round(size.y * 1000), round(size.z * 1000)]}

# materials: what texture maps drive each zone (tells you whether detail is geometry or texture)
for m in bpy.data.materials:
    maps = []
    if m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image:
                outs = [l.to_socket.name for o in n.outputs for l in o.links]
                maps.append({"image": n.image.name, "feeds": outs, "px": list(n.image.size)})
        b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        vals = {}
        if b:
            for s in ("Metallic", "Roughness"):
                if not b.inputs[s].is_linked: vals[s] = round(b.inputs[s].default_value, 3)
        report["materials"][m.name] = {"maps": maps, "constants": vals}

# split every object into connected parts and measure them
parts = []
for o in meshes:
    report["objects"].append({"name": o.name, "tris": sum(len(p.vertices) - 2 for p in o.data.polygons), "materials": [s.material.name if s.material else None for s in o.material_slots]})
    bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    seen = set()
    for f0 in bm.faces:
        if f0.index in seen: continue
        stack, faces = [f0], []
        seen.add(f0.index)
        while stack:
            f = stack.pop(); faces.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in seen: seen.add(g.index); stack.append(g)
        tris = sum(len(f.verts) - 2 for f in faces)
        if tris < MIN_TRIS: continue
        vs = {v for f in faces for v in f.verts}
        plo = Vector((min(v.co.x for v in vs), min(v.co.y for v in vs), min(v.co.z for v in vs)))
        phi = Vector((max(v.co.x for v in vs), max(v.co.y for v in vs), max(v.co.z for v in vs)))
        area = sum(f.calc_area() for f in faces)
        # bevel estimate: narrow faces that bridge a big normal change (chamfer strips on hard edges)
        bevels = []
        for f in faces:
            for e in f.edges:
                if len(e.link_faces) == 2:
                    a = e.link_faces[0].normal.angle(e.link_faces[1].normal, 0)
                    if 0.2 < a < 1.2: bevels.append(e.calc_length())
        bevels.sort()
        mat = o.material_slots[faces[0].material_index].material.name if o.material_slots and o.material_slots[faces[0].material_index].material else None
        parts.append({"object": o.name, "tris": tris, "material": mat, "area_cm2": round(area * 1e4, 1),
                      "min": [round(c * 1000) for c in plo], "max": [round(c * 1000) for c in phi],
                      "size_mm": [round((phi - plo)[i] * 1000) for i in range(3)],
                      "center": list((plo + phi) / 2), "verts": [v.index for v in vs],
                      "median_soft_edge_mm": round(bevels[len(bevels) // 2] * 1000, 1) if bevels else None})
    bm.free()

parts.sort(key=lambda p: -p["area_cm2"])
for i, p in enumerate(parts): p["id"] = i + 1
report["parts"] = [{k: v for k, v in p.items() if k not in ("verts", "center")} for p in parts]
report["part_count"] = len(parts)
report["tris_total"] = sum(o["tris"] for o in report["objects"])

# ---------------------------------------------------------------- renders
sc = bpy.context.scene
try: sc.render.engine = 'BLENDER_EEVEE_NEXT'
except Exception: sc.render.engine = 'BLENDER_EEVEE'
sc.render.resolution_x, sc.render.resolution_y = 1400, 1400
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (.42, .42, .45, 1)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN')); sc.collection.objects.link(sun); sun.rotation_euler = (math.radians(55), 0, math.radians(35)); sun.data.energy = 3
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cen = (lo + hi) / 2; R = max(size) * 1.6
def shot(name, direction, ortho=None):
    d = Vector(direction).normalized(); cam.location = cen + d * R * 1.6
    cam.rotation_euler = (cen - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO' if ortho else 'PERSP'
    if ortho: cam.data.ortho_scale = ortho
    else: cam.data.lens = 50
    sc.render.filepath = os.path.join(OUT, "%s-%s-%s.png" % (NAME, name, STAMP)); bpy.ops.render.render(write_still=True)
shot("hero", (1, -1.3, .6))
shot("front", (0, -1, 0), max(size) * 1.25)
shot("side", (1, 0, 0), max(size) * 1.25)

# exploded view: move each part away from the centre along its own offset, number it
for o in meshes:
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); inv = o.matrix_world.inverted()
    for p in parts:
        if p["object"] != o.name: continue
        off = (Vector(p["center"]) - cen); off = off * 1.2 + off.normalized() * max(size) * .08 if off.length > 1e-5 else Vector((0, 0, 0))
        loc = inv.to_3x3() @ off
        for vi in p["verts"]: bm.verts[vi].co += loc
    bm.to_mesh(o.data); bm.free()
for p in parts[:40]:
    c = Vector(p["center"]); off = (c - cen); c2 = c + (off * 1.2 + off.normalized() * max(size) * .08 if off.length > 1e-5 else Vector())
    cu = bpy.data.curves.new("t%d" % p["id"], 'FONT'); cu.body = str(p["id"]); cu.size = max(size) * .05
    t = bpy.data.objects.new("label%d" % p["id"], cu); sc.collection.objects.link(t); t.location = c2 + Vector((0, -max(size) * .3, 0)); t.rotation_euler = (math.radians(90), 0, 0)
    mt = bpy.data.materials.new("lab"); mt.diffuse_color = (1, .2, .6, 1); t.data.materials.append(mt)
R *= 1.5
shot("exploded", (1, -1.3, .5))
shot("exploded-front", (0, -1, 0), max(size) * 2.4)

json.dump(report, open(os.path.join(OUT, "decomposition-%s-%s.json" % (NAME, STAMP)), "w"), indent=1)
print("PROP_DECOMPOSER_DONE parts", len(parts), "tris", report["tris_total"])
