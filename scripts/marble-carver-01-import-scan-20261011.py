# marble-carver step 1: bring in a real statue scan (run in Blender's Text Editor with Alt+P)
import bpy, os
SCAN = "Athena"                      # scan folder / file name under SCANS
NAME = "Colossus"                    # the House figure it becomes
SCANS = os.environ.get("PIRANESI_PROJECT", r"<project-path>") + r"\Blender\Scans"
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=SCANS + "\\" + SCAN + "\\" + SCAN + ".fbx")
ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
ob.name = NAME
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
zs = [v.co.z for v in ob.data.vertices]
s = 2.0 / (max(zs) - min(zs))
ob.scale = (s, s, s); bpy.ops.object.transform_apply(scale=True)
bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
ob.location = (0, 0, 0); bpy.context.view_layer.update()
ob.location.z -= min((ob.matrix_world @ v.co).z for v in ob.data.vertices)
bpy.ops.object.transform_apply(location=True)
m = bpy.data.materials.new("Marble_" + NAME); m.use_nodes = True
nt = m.node_tree; b = nt.nodes["Principled BSDF"]
b.inputs["Base Color"].default_value = (0.86, 0.84, 0.80, 1); b.inputs["Roughness"].default_value = 0.42
b.inputs["Subsurface Weight"].default_value = 0.15; b.inputs["Subsurface Radius"].default_value = (0.02, 0.016, 0.012)
tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(SCANS + "\\" + SCAN + "\\" + SCAN + "_normals.png"); tn.image.colorspace_settings.name = "Non-Color"
nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tn.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = bpy.data.images.load(SCANS + "\\" + SCAN + "\\" + SCAN + "_occlusion.png"); ta.image.colorspace_settings.name = "Non-Color"
mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = "RGBA"; mx.blend_type = "MULTIPLY"; mx.inputs[0].default_value = 1
mx.inputs[6].default_value = (0.86, 0.84, 0.80, 1); nt.links.new(ta.outputs[0], mx.inputs[7]); nt.links.new(mx.outputs[2], b.inputs["Base Color"])
ob.data.materials.clear(); ob.data.materials.append(m)
for a in bpy.context.screen.areas:
    if a.type == 'VIEW_3D':
        a.spaces[0].shading.type = 'MATERIAL'
        with bpy.context.temp_override(area=a, region=[r for r in a.regions if r.type == 'WINDOW'][0]):
            bpy.ops.view3d.view_selected()
print(SCAN, "in as", NAME, len(ob.data.vertices), "verts")
