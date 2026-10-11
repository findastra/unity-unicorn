# marble-carver step 2: the House's weathering - stone veins, grime in the folds, rain and salt streaks,
# the green-brown tide band rising from the feet, lichen on the faces that catch rain
import bpy
NAME = "Colossus"
TIDE = 0.62          # how far up (in the 2 m scan's metres) the tide stain reaches
ob = bpy.data.objects[NAME]
for md in list(ob.modifiers): ob.modifiers.remove(md)
m = ob.data.materials[0]; nt = m.node_tree; N = nt.nodes; L = nt.links
b = N["Principled BSDF"]
ao = [n for n in N if n.type == 'TEX_IMAGE' and 'occlusion' in n.image.name][0]
tc = N.new("ShaderNodeTexCoord"); geo = N.new("ShaderNodeNewGeometry")
sep = N.new("ShaderNodeSeparateXYZ"); L.new(tc.outputs["Object"], sep.inputs[0])
def ramp(stops, inp):
    r = N.new("ShaderNodeValToRGB"); el = r.color_ramp.elements
    el[0].position, el[0].color = stops[0]; el[1].position, el[1].color = stops[-1]
    for p, c in stops[1:-1]:
        e = el.new(p); e.color = c
    L.new(inp, r.inputs[0]); return r
def mix(a, c2, fac, mode='MIX'):
    x = N.new("ShaderNodeMix"); x.data_type = 'RGBA'; x.blend_type = mode
    if isinstance(fac, float): x.inputs[0].default_value = fac
    else: L.new(fac, x.inputs[0])
    if isinstance(a, tuple): x.inputs[6].default_value = a
    else: L.new(a, x.inputs[6])
    if isinstance(c2, tuple): x.inputs[7].default_value = c2
    else: L.new(c2, x.inputs[7])
    return x.outputs[2]
vn = N.new("ShaderNodeTexNoise"); vn.inputs["Scale"].default_value = 2.2; vn.inputs["Detail"].default_value = 12; vn.inputs["Distortion"].default_value = 3.0
L.new(tc.outputs["Object"], vn.inputs["Vector"])
vr = ramp([(0.0, (0.86, 0.84, 0.79, 1)), (0.47, (0.88, 0.86, 0.82, 1)), (0.5, (0.64, 0.64, 0.64, 1)), (0.53, (0.87, 0.85, 0.81, 1)), (1.0, (0.84, 0.82, 0.77, 1))], vn.outputs["Fac"])
col = vr.outputs[0]
gr = ramp([(0.0, (0.30, 0.27, 0.22, 1)), (0.75, (1, 1, 1, 1))], ao.outputs[0])
col = mix(col, gr.outputs[0], 1.0, 'MULTIPLY')
mp = N.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (26, 26, 1.2); L.new(tc.outputs["Object"], mp.inputs[0])
sn = N.new("ShaderNodeTexNoise"); sn.inputs["Scale"].default_value = 3.0; sn.inputs["Detail"].default_value = 6; L.new(mp.outputs[0], sn.inputs["Vector"])
sr = ramp([(0.0, (1, 1, 1, 1)), (0.55, (1, 1, 1, 1)), (0.75, (0.78, 0.74, 0.66, 1))], sn.outputs["Fac"])
col = mix(col, sr.outputs[0], 1.0, 'MULTIPLY')
tn = N.new("ShaderNodeTexNoise"); tn.inputs["Scale"].default_value = 7; tn.inputs["Detail"].default_value = 6; L.new(tc.outputs["Object"], tn.inputs["Vector"])
tw = N.new("ShaderNodeMath"); tw.operation = 'MULTIPLY_ADD'; L.new(tn.outputs["Fac"], tw.inputs[0]); tw.inputs[1].default_value = 0.25; L.new(sep.outputs["Z"], tw.inputs[2])
tb = ramp([(0.0, (0.26, 0.31, 0.21, 1)), (TIDE * 0.32, (0.42, 0.45, 0.34, 1)), (TIDE, (1, 1, 1, 1)), (1.0, (1, 1, 1, 1))], tw.outputs[0])
col = mix(col, tb.outputs[0], 1.0, 'MULTIPLY')
up = N.new("ShaderNodeSeparateXYZ"); L.new(geo.outputs["Normal"], up.inputs[0])
ln = N.new("ShaderNodeTexNoise"); ln.inputs["Scale"].default_value = 6; ln.inputs["Detail"].default_value = 8; L.new(tc.outputs["Object"], ln.inputs["Vector"])
lm = N.new("ShaderNodeMath"); lm.operation = 'MULTIPLY'; L.new(up.outputs["Z"], lm.inputs[0]); L.new(ln.outputs["Fac"], lm.inputs[1])
lr = ramp([(0.0, (0, 0, 0, 1)), (0.36, (0, 0, 0, 1)), (0.5, (1, 1, 1, 1)), (1.0, (1, 1, 1, 1))], lm.outputs[0])
col = mix(col, (0.47, 0.49, 0.30, 1), lr.outputs[0])
L.new(col, b.inputs["Base Color"])
rr = ramp([(0.0, (0.6, 0.6, 0.6, 1)), (1.0, (0.38, 0.38, 0.38, 1))], ao.outputs[0]); L.new(rr.outputs[0], b.inputs["Roughness"])
print("weathered marble ready on", NAME)
