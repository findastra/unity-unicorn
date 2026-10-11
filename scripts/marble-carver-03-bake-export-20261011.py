# marble-carver step 3: bake the weathered marble onto the scan's UVs, export for the House
import bpy, os
NAME = "Colossus"
OUT = os.environ.get("PIRANESI_PROJECT", r"<project-path>") + r"\Blender\Out" + "\\" + NAME
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 16
try: sc.cycles.device = 'GPU'
except Exception: pass
ob = bpy.data.objects[NAME]
for o in bpy.context.view_layer.objects: o.select_set(False)
ob.select_set(True); bpy.context.view_layer.objects.active = ob
N = ob.data.materials[0].node_tree.nodes
def bake(name, kind, **kw):
    img = bpy.data.images.new(name, 2048, 2048, alpha=False)
    if kind != 'DIFFUSE': img.colorspace_settings.name = 'Non-Color'
    node = N.new("ShaderNodeTexImage"); node.image = img
    for n in N: n.select = False
    node.select = True; N.active = node
    bpy.ops.object.bake(type=kind, margin=8, **kw)
    img.filepath_raw = os.path.join(OUT, name + ".png"); img.file_format = 'PNG'; img.save()
    N.remove(node)
bake(NAME + "_Albedo", 'DIFFUSE', pass_filter={'COLOR'})
bake(NAME + "_Roughness", 'ROUGHNESS')
bpy.ops.wm.obj_export(filepath=os.path.join(OUT, NAME + ".obj"), export_selected_objects=True, export_uv=True,
                      export_normals=True, export_materials=False, forward_axis='Y', up_axis='Z')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + ".blend"))
print(NAME, "exported to", OUT)
