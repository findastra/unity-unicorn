#!/usr/bin/env python3
"""glass-carver: turn a real animal scan into a little cut-crystal animal (a Unity Unicorn world tool).

Claude Fable 5.1, 2026-10-23, for Liminal Doors' glass animals (one hides behind every door). It follows marble-carver:
a real scan goes in (never an animal built from primitives), the tool only re-cuts and re-materials it.

    blender --background --python glass-carver-20261023.py -- --scan <file> --name Fox --out <dir>
            [--height 0.22] [--facets 420] [--style cut|smooth] [--tint "#ffd9ec"] [--render] [--video]
            [--source "<where the scan came from>"] [--licence "CC0"]

What it does, in order:
  1. import  the scan (OBJ, FBX, glTF/GLB, STL or PLY), join its parts, drop its textures, stand it on the ground and
             scale it to --height metres (default 22 cm: a hand-sized ornament, "one little glass animal").
  2. carve   make it one solid shape (voxel remesh), then cut it into flat facets like a lead-crystal figurine:
             --facets flat faces (collapse + planar decimation), hard edges, a hair of bevel on every edge.
             --style smooth keeps the scan's curves and only polishes it (blown glass instead of cut glass).
  3. glass   the material: for Blender renders three glasses added together (red, green, blue at different IORs), the
             same dispersion trick as The Rainbow Maker's crystals, so facet edges split into colour; a tint if given.
  4. export  <out>/<name>-<date>.fbx (Unity: Y up, metres, triangles, flat normals) and <name>-<date>.blend, plus
             <name>-<date>.json recording the scan's source and licence, the facet count and the height.
  5. render  (--render) a dark-background check still, lit by a small bright sun and a ring of sparkle lights so the
             facets flash; (--video) a 6 s turntable. Cycles on the HIP GPU when it is there, 90 s cap per frame.

Unity side: LiminalGlassAnimals.cs (Liminal Doors) places the FBX at a Doorspace's hiding spot with the
LiminalDoors/Crystal shader. Nothing here touches the Unity project.
"""
import bpy, bmesh, math, sys, os, json, datetime, argparse
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser(prog="glass-carver")
ap.add_argument("--scan", required=True, help="scan file (.obj .fbx .glb .gltf .stl .ply) or builtin:monkey for a pipeline test")
ap.add_argument("--name", required=True, help='the animal, e.g. "Fox"')
ap.add_argument("--out", required=True)
ap.add_argument("--height", type=float, default=None,
                help="finished height in metres (default: the animal's life size, from LIFE_SIZE below)")
ap.add_argument("--facets", type=int, default=420, help="how many flat faces the cut version has")
ap.add_argument("--style", choices=("cut", "smooth"), default="cut")
ap.add_argument("--tint", default="#ffffff", help="glass tint, hex")
ap.add_argument("--source", default="", help="where the scan came from (URL or museum)")
ap.add_argument("--licence", default="", help="the scan's licence, e.g. CC0 or CC BY 4.0 (author)")
ap.add_argument("--render", action="store_true")
ap.add_argument("--video", action="store_true")
ap.add_argument("--still-frame", type=int, default=20)
A = ap.parse_args(argv)
# Astra, 2026-10-23: the glass animals are life size, like the prismatic wolf she saw in VRChat. Adult standing
# height in metres, top of the head or back in a natural pose (the scan is scaled uniformly to it). The insects, the
# gecko and the firefly come out a few centimetres tall: that is their life size.
LIFE_SIZE = {
    "fox": 0.40, "hare": 0.35, "axolotl": 0.06, "raccoon": 0.30, "gecko": 0.03, "crane": 1.10, "moth": 0.05,
    "frog": 0.08, "hummingbird": 0.09, "ibex": 0.95, "cat": 0.25, "manta ray": 0.45, "hedgehog": 0.12,
    "lynx": 0.60, "seahorse": 0.20, "heron": 1.00, "dragonfly": 0.03, "snow leopard": 0.60, "raven": 0.30,
    "octopus": 0.40, "deer": 1.00, "firefly": 0.015, "kangaroo": 1.50, "scarab": 0.03, "praying mantis": 0.08,
    "flamingo": 1.30, "jellyfish": 0.40, "butterfly": 0.06, "red panda": 0.35, "swan": 1.00, "otter": 0.30,
    "sandpiper": 0.20, "dolphin": 0.60, "kingfisher": 0.17, "wolf": 0.80,
}
if A.height is None:
    key = A.name.lower()
    if key not in LIFE_SIZE:
        sys.exit("glass-carver: no life size on record for %r; pass --height <metres>" % A.name)
    A.height = LIFE_SIZE[key]
STAMP = datetime.datetime.now().astimezone().strftime("%Y%m%d")   # Astra's PC runs on America/Denver
slug = A.name.lower().replace(" ", "-")
os.makedirs(A.out, exist_ok=True)

def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))

# ---------- 1. import ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
if A.scan.startswith("builtin:"):
    # pipeline test only: a Blender primitive is never a finished animal (marble-carver's rule)
    bpy.ops.mesh.primitive_monkey_add(size=1.0)
    print("NOTE builtin test subject, not a real scan")
else:
    ext = os.path.splitext(A.scan)[1].lower()
    importer = {".obj": lambda p: bpy.ops.wm.obj_import(filepath=p),
                ".fbx": lambda p: bpy.ops.import_scene.fbx(filepath=p),
                ".glb": lambda p: bpy.ops.import_scene.gltf(filepath=p),
                ".gltf": lambda p: bpy.ops.import_scene.gltf(filepath=p),
                ".stl": lambda p: bpy.ops.wm.stl_import(filepath=p),
                ".ply": lambda p: bpy.ops.wm.ply_import(filepath=p)}[ext]
    importer(A.scan)
meshes = [o for o in scn.objects if o.type == "MESH"]
if not meshes:
    sys.exit("glass-carver: no mesh in " + A.scan)
for o in scn.objects:
    o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
ob.name = A.name
for o in [o for o in scn.objects if o is not ob]:
    bpy.data.objects.remove(o)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
ob.data.materials.clear()
# stand on the ground, at the origin, --height tall
zs = [v.co.z for v in ob.data.vertices]
s = A.height / (max(zs) - min(zs))
ob.scale = (s, s, s)
bpy.ops.object.transform_apply(scale=True)
bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
ob.location = (0, 0, 0)
bpy.context.view_layer.update()
ob.location.z -= min((ob.matrix_world @ v.co).z for v in ob.data.vertices)
bpy.ops.object.transform_apply(location=True)
source_tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)

# ---------- 2. carve ----------
def apply_all():
    for md in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=md.name)

# one watertight solid first: scans arrive with holes, loose shells and a million tiny faces
rm = ob.modifiers.new("solid", "REMESH")
rm.mode = "VOXEL"
rm.voxel_size = A.height / 90
rm.use_smooth_shade = True
apply_all()
if A.style == "cut":
    # fewer, flatter faces: collapse to the facet budget, then merge faces that are nearly coplanar
    dc = ob.modifiers.new("facets", "DECIMATE")
    dc.decimate_type = "COLLAPSE"
    dc.ratio = max(0.001, A.facets * 1.3 / max(1, len(ob.data.polygons)))
    apply_all()
    pl = ob.modifiers.new("planar", "DECIMATE")
    pl.decimate_type = "DISSOLVE"
    pl.angle_limit = math.radians(9)
    apply_all()
    flat_faces = len(ob.data.polygons)          # the facets you see; triangulation below only splits them for Unity
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], ngon_method="BEAUTY")   # Unity wants triangles; beauty keeps facets clean
    bm.to_mesh(ob.data); bm.free()
    for p in ob.data.polygons:
        p.use_smooth = False
    bv = ob.modifiers.new("edge polish", "BEVEL")
    bv.width = A.height / 400
    bv.segments = 1
    bv.limit_method = "ANGLE"
    bv.angle_limit = math.radians(20)
    apply_all()
else:
    sm = ob.modifiers.new("polish", "SMOOTH")
    sm.iterations, sm.factor = 6, 0.6
    dc = ob.modifiers.new("lighter", "DECIMATE")
    dc.ratio = max(0.01, 6000 / max(1, len(ob.data.polygons)))
    apply_all()
    for p in ob.data.polygons:
        p.use_smooth = True
    flat_faces = 0
facets = flat_faces
triangles = len(ob.data.polygons)

# ---------- 3. glass ----------
m = bpy.data.materials.new("glass_" + slug)
m.use_nodes = True
nt = m.node_tree
for n in list(nt.nodes):
    if n.type != "OUTPUT_MATERIAL":
        nt.nodes.remove(n)
out = nt.nodes["Material Output"]
tint = hex_rgb(A.tint)
glasses = []
for rgb, ior in (((1, 0, 0, 1), 1.70), ((0, 1, 0, 1), 1.75), ((0, 0, 1, 1), 1.82)):
    g = nt.nodes.new("ShaderNodeBsdfGlass")
    g.distribution = "GGX"
    g.inputs["Color"].default_value = (rgb[0] * tint[0], rgb[1] * tint[1], rgb[2] * tint[2], 1)
    g.inputs["Roughness"].default_value = 0.0 if A.style == "cut" else 0.03
    g.inputs["IOR"].default_value = ior
    glasses.append(g)
add1, add2 = nt.nodes.new("ShaderNodeAddShader"), nt.nodes.new("ShaderNodeAddShader")
nt.links.new(glasses[0].outputs[0], add1.inputs[0]); nt.links.new(glasses[1].outputs[0], add1.inputs[1])
nt.links.new(add1.outputs[0], add2.inputs[0]); nt.links.new(glasses[2].outputs[0], add2.inputs[1])
nt.links.new(add2.outputs[0], out.inputs["Surface"])
ob.data.materials.append(m)

# ---------- 4. export ----------
base = os.path.join(A.out, "%s-%s" % (slug, STAMP))
ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=base + ".fbx", use_selection=True, object_types={"MESH"},
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         use_mesh_modifiers=True, use_triangles=True, bake_anim=False, add_leaf_bones=False)
record = {"animal": A.name, "scan": os.path.basename(A.scan), "source": A.source, "licence": A.licence,
          "style": A.style, "height_m": A.height, "facets": facets, "triangles": triangles, "scan_triangles": source_tris,
          "tint": A.tint, "tool": "glass-carver 0.1.0-" + STAMP, "made": STAMP}
with open(base + ".json", "w", encoding="utf-8") as f:
    json.dump(record, f, indent=2)
print("CARVED", json.dumps(record))

# ---------- 5. render ----------
if A.render or A.video:
    # a dark stage so the glass reads: graphite floor, a small bright sun, a ring of sparkle lights the facets flash
    def emitter(name, rgb, strength, radius, loc, camera_visible=False):
        em = bpy.data.materials.new(name); em.use_nodes = True
        b = em.node_tree.nodes["Principled BSDF"]
        b.inputs["Emission Color"].default_value = (*rgb, 1); b.inputs["Emission Strength"].default_value = strength
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=radius)
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(em)
        o = bpy.data.objects.new(name, me); scn.collection.objects.link(o); o.location = loc
        o.visible_camera = camera_visible; o.visible_diffuse = False; o.visible_shadow = False
        return o
    H = A.height
    floor_m = bpy.data.materials.new("stage"); floor_m.use_nodes = True
    fb = floor_m.node_tree.nodes["Principled BSDF"]
    fb.inputs["Base Color"].default_value = (0.09, 0.09, 0.1, 1); fb.inputs["Roughness"].default_value = 0.35
    bpy.ops.mesh.primitive_plane_add(size=H * 12)
    floor = bpy.context.active_object; floor.data.materials.append(floor_m)
    emitter("sun", (1, 0.95, 0.85), 6000, H * 0.4, (H * 3, -H * 2, H * 5))
    for k in range(8):
        a = 2 * math.pi * k / 8
        emitter("sparkle_%d" % k, (1, 0.97, 0.92), 400, H * 0.08,
                (H * 3 * math.cos(a), -H * 3 * math.sin(a) - H * 1.5, H * (1.2 if k % 2 else 2.4)))
    world = bpy.data.worlds.new("bg"); scn.world = world; world.use_nodes = True
    wn = world.node_tree
    tc, sep, ramp = wn.nodes.new("ShaderNodeTexCoord"), wn.nodes.new("ShaderNodeSeparateXYZ"), wn.nodes.new("ShaderNodeValToRGB")
    wn.links.new(tc.outputs["Generated"], sep.inputs[0]); wn.links.new(sep.outputs["Z"], ramp.inputs[0])
    ramp.color_ramp.elements[0].color = (0.02, 0.02, 0.025, 1); ramp.color_ramp.elements[1].color = (0.25, 0.22, 0.3, 1)
    wn.links.new(ramp.outputs["Color"], wn.nodes["Background"].inputs[0])
    wn.nodes["Background"].inputs[1].default_value = 0.6
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam)
    cam.data.lens = 70
    cam.location = (H * 2.6, -H * 3.6, H * 1.6)
    cam.rotation_euler = (Vector((0, 0, H * 0.5)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scn.camera = cam
    # the animal turns once in the video
    ob.rotation_euler = (0, 0, 0); ob.keyframe_insert("rotation_euler", index=2, frame=1)
    ob.rotation_euler = (0, 0, 2 * math.pi); ob.keyframe_insert("rotation_euler", index=2, frame=181)
    for fc in ob.animation_data.action.fcurves if hasattr(ob.animation_data.action, "fcurves") else []:
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    scn.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "HIP"; prefs.get_devices()
        for d in prefs.devices:
            d.use = "RX" in d.name
        scn.cycles.device = "GPU"
    except Exception as e:
        print("GPU not available, CPU render:", e)
    scn.cycles.samples = 96; scn.cycles.use_denoising = True; scn.cycles.time_limit = 90
    scn.cycles.max_bounces = 16; scn.cycles.transmission_bounces = 16; scn.cycles.glossy_bounces = 8
    scn.cycles.sample_clamp_indirect = 12; scn.cycles.blur_glossy = 0.0
    scn.render.resolution_x = scn.render.resolution_y = 720
    scn.view_settings.view_transform = "AgX"
    scn.render.fps = 30
    if A.render:
        scn.frame_set(A.still_frame)
        scn.render.image_settings.file_format = "PNG"
        scn.render.filepath = base + "-check.png"
        bpy.ops.render.render(write_still=True)
        print("RENDERED", scn.render.filepath)
    if A.video:
        scn.frame_start, scn.frame_end = 1, 180
        scn.render.use_motion_blur = True
        scn.render.image_settings.file_format = "FFMPEG"
        scn.render.ffmpeg.format = "MPEG4"; scn.render.ffmpeg.codec = "H264"
        scn.render.filepath = base + "-turntable.mp4"
        bpy.ops.render.render(animation=True)
        print("RENDERED", scn.render.filepath)
bpy.ops.wm.save_as_mainfile(filepath=base + ".blend")
