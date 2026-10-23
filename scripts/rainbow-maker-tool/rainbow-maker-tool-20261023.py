#!/usr/bin/env python3
"""rainbow-maker-tool (Blender half): give a group of surfaces the Rainbow Maker's cut-crystal glass, and render it.

Claude Fable 5.1, 2026-10-23. Astra: "a tool that creates the glass texture thing for a group of surfaces like the
crystal." It changes only what the surfaces are made of; geometry is another tool's job (Astra's planned
geographic-statue-converter turns something into geometry; the two together make a crystal wolf). The Unity half,
scripts/unity-editor/rainbow-maker-tool-20261023.cs, does the same inside a Unity project.

    blender --background --python rainbow-maker-tool-20261023.py -- --mesh <file> [--mesh <file> ...] --out <dir>
            [--name crystal] [--tint "#ffd9ec"] [--ior 1.75] [--spread 0.12] [--render] [--video] [--still-frame N]
    blender --background <scene.blend> --python rainbow-maker-tool-20261023.py -- --selected --out <dir> [--render]

--mesh brings files in (OBJ, FBX, glTF/GLB, STL, PLY); --selected works on the objects selected in a .blend you open.
Every mesh in the group gets the crystal: three glasses added together, red, green and blue, each at a slightly
different IOR (--ior ± --spread), because Blender 4.5's glass has no dispersion; white light fans into colour at
every edge and a facet that catches the sun throws rainbow fire. Flat-shaded faces stay flat, smooth ones smooth.
Exports <out>/<name>-<date>.fbx (the group, Y up, metres, triangles; Unity gives it the shader there) and .blend.
--render: a check still on a dark stage with a small bright sun and a ring of eight sparkle lights so facets flash;
--video: a 6 s turntable. Cycles on the HIP GPU when it is there, 90 s cap per frame.
"""
import bpy, bmesh, math, sys, os, json, datetime, argparse
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser(prog="rainbow-maker-tool")
ap.add_argument("--mesh", action="append", default=[], help="a mesh file to bring in (repeatable)")
ap.add_argument("--selected", action="store_true", help="use the selected objects of the open .blend instead")
ap.add_argument("--out", required=True)
ap.add_argument("--name", default="crystal", help="name for the exported files")
ap.add_argument("--tint", default="#ffffff", help="glass tint, hex; clear by default")
ap.add_argument("--ior", type=float, default=1.75, help="refraction (1.5 window glass, 1.55 lead crystal, 2.4 diamond)")
ap.add_argument("--spread", type=float, default=0.06, help="how far red and blue sit from --ior: the colour split")
ap.add_argument("--render", action="store_true")
ap.add_argument("--video", action="store_true")
ap.add_argument("--still-frame", type=int, default=20)
A = ap.parse_args(argv)
STAMP = datetime.datetime.now().astimezone().strftime("%Y%m%d")   # Astra's PC runs on America/Denver
slug = A.name.lower().replace(" ", "-")
os.makedirs(A.out, exist_ok=True)

def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))

# ---------- the group of surfaces ----------
scn = bpy.context.scene
if A.selected:
    group = [o for o in bpy.context.selected_objects if o.type == "MESH"]
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn = bpy.context.scene
    for path in A.mesh:
        ext = os.path.splitext(path)[1].lower()
        {".obj": lambda p: bpy.ops.wm.obj_import(filepath=p),
         ".fbx": lambda p: bpy.ops.import_scene.fbx(filepath=p),
         ".glb": lambda p: bpy.ops.import_scene.gltf(filepath=p),
         ".gltf": lambda p: bpy.ops.import_scene.gltf(filepath=p),
         ".stl": lambda p: bpy.ops.wm.stl_import(filepath=p),
         ".ply": lambda p: bpy.ops.wm.ply_import(filepath=p)}[ext](path)
    group = [o for o in scn.objects if o.type == "MESH"]
if not group:
    sys.exit("rainbow-maker-tool: no mesh to work on (give --mesh files or --selected objects)")

# ---------- the crystal glass ----------
tint = hex_rgb(A.tint)
m = bpy.data.materials.new("rainbow_maker_glass")
m.use_nodes = True
nt = m.node_tree
for n in list(nt.nodes):
    if n.type != "OUTPUT_MATERIAL":
        nt.nodes.remove(n)
out = nt.nodes["Material Output"]
glasses = []
for rgb, ior in (((1, 0, 0, 1), A.ior - A.spread), ((0, 1, 0, 1), A.ior), ((0, 0, 1, 1), A.ior + A.spread * 1.4)):
    g = nt.nodes.new("ShaderNodeBsdfGlass")
    g.distribution = "GGX"
    g.inputs["Color"].default_value = (rgb[0] * tint[0], rgb[1] * tint[1], rgb[2] * tint[2], 1)
    g.inputs["Roughness"].default_value = 0.0
    g.inputs["IOR"].default_value = ior
    glasses.append(g)
add1, add2 = nt.nodes.new("ShaderNodeAddShader"), nt.nodes.new("ShaderNodeAddShader")
nt.links.new(glasses[0].outputs[0], add1.inputs[0]); nt.links.new(glasses[1].outputs[0], add1.inputs[1])
nt.links.new(add1.outputs[0], add2.inputs[0]); nt.links.new(glasses[2].outputs[0], add2.inputs[1])
nt.links.new(add2.outputs[0], out.inputs["Surface"])
for ob in group:
    ob.data.materials.clear()
    ob.data.materials.append(m)
faces = sum(len(o.data.polygons) for o in group)

# ---------- export ----------
base = os.path.join(A.out, "%s-%s" % (slug, STAMP))
for o in scn.objects:
    o.select_set(o in group)
bpy.ops.export_scene.fbx(filepath=base + ".fbx", use_selection=True, object_types={"MESH"},
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         use_mesh_modifiers=True, use_triangles=True, bake_anim=False, add_leaf_bones=False)
record = {"name": A.name, "surfaces": [o.name for o in group], "faces": faces, "tint": A.tint, "ior": A.ior,
          "spread": A.spread, "tool": "rainbow-maker-tool 0.1.0-" + STAMP, "made": STAMP}
with open(base + ".json", "w", encoding="utf-8") as f:
    json.dump(record, f, indent=2)
print("CRYSTAL", json.dumps(record))

# ---------- render ----------
if A.render or A.video:
    lo = Vector((min(min((o.matrix_world @ Vector(c)).x for c in o.bound_box) for o in group),
                 min(min((o.matrix_world @ Vector(c)).y for c in o.bound_box) for o in group),
                 min(min((o.matrix_world @ Vector(c)).z for c in o.bound_box) for o in group)))
    hi = Vector((max(max((o.matrix_world @ Vector(c)).x for c in o.bound_box) for o in group),
                 max(max((o.matrix_world @ Vector(c)).y for c in o.bound_box) for o in group),
                 max(max((o.matrix_world @ Vector(c)).z for c in o.bound_box) for o in group)))
    centre, H = (lo + hi) / 2, max((hi - lo).z, 0.01)
    pivot = bpy.data.objects.new("rainbow_maker_turntable", None)
    pivot.location = (centre.x, centre.y, lo.z)
    scn.collection.objects.link(pivot)
    for o in group:
        mw = o.matrix_world.copy()
        o.parent = pivot
        o.matrix_parent_inverse = pivot.matrix_world.inverted()
        o.matrix_world = mw

    # a dark stage so the glass reads: graphite floor, a small bright sun, a ring of sparkle lights the facets flash
    def emitter(name, rgb, strength, radius, loc):
        em = bpy.data.materials.new(name); em.use_nodes = True
        b = em.node_tree.nodes["Principled BSDF"]
        b.inputs["Emission Color"].default_value = (*rgb, 1); b.inputs["Emission Strength"].default_value = strength
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=radius)
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(em)
        o = bpy.data.objects.new(name, me); scn.collection.objects.link(o); o.location = loc
        o.visible_camera = False; o.visible_diffuse = False; o.visible_shadow = False
        return o
    floor_m = bpy.data.materials.new("stage"); floor_m.use_nodes = True
    fb = floor_m.node_tree.nodes["Principled BSDF"]
    fb.inputs["Base Color"].default_value = (0.09, 0.09, 0.1, 1); fb.inputs["Roughness"].default_value = 0.35
    bpy.ops.mesh.primitive_plane_add(size=H * 12, location=(centre.x, centre.y, lo.z - 0.001))
    bpy.context.active_object.data.materials.append(floor_m)
    emitter("sun", (1, 0.95, 0.85), 6000, H * 0.4, (centre.x + H * 3, centre.y - H * 2, lo.z + H * 5))
    for k in range(8):
        a = 2 * math.pi * k / 8
        emitter("sparkle_%d" % k, (1, 0.97, 0.92), 400, H * 0.08,
                (centre.x + H * 3 * math.cos(a), centre.y - H * 3 * math.sin(a) - H * 1.5, lo.z + H * (1.2 if k % 2 else 2.4)))
    world = bpy.data.worlds.new("bg"); scn.world = world; world.use_nodes = True
    wn = world.node_tree
    tc, sep, ramp = wn.nodes.new("ShaderNodeTexCoord"), wn.nodes.new("ShaderNodeSeparateXYZ"), wn.nodes.new("ShaderNodeValToRGB")
    wn.links.new(tc.outputs["Generated"], sep.inputs[0]); wn.links.new(sep.outputs["Z"], ramp.inputs[0])
    ramp.color_ramp.elements[0].color = (0.02, 0.02, 0.025, 1); ramp.color_ramp.elements[1].color = (0.25, 0.22, 0.3, 1)
    wn.links.new(ramp.outputs["Color"], wn.nodes["Background"].inputs[0])
    wn.nodes["Background"].inputs[1].default_value = 0.6
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam)
    cam.data.lens = 70
    cam.location = (centre.x + H * 2.6, centre.y - H * 3.6, lo.z + H * 1.6)
    cam.rotation_euler = (Vector((centre.x, centre.y, lo.z + H * 0.5)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scn.camera = cam
    pivot.rotation_euler = (0, 0, 0); pivot.keyframe_insert("rotation_euler", index=2, frame=1)
    pivot.rotation_euler = (0, 0, 2 * math.pi); pivot.keyframe_insert("rotation_euler", index=2, frame=181)
    for fc in getattr(pivot.animation_data.action, "fcurves", []):
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
