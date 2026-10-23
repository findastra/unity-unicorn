# Rainbow maker tool

Gives a group of surfaces the cut-crystal glass of The Rainbow Maker (Astra's double-crystal Kikkerland sun
catcher, rendered on 2026-10-23): light goes in through a facet and out through another, red, green and blue each
bent a little differently, so edges fan into colour and a facet that catches the sun throws rainbow fire.

Astra, 2026-10-23: "a tool that creates the glass texture thing for a group of surfaces like the crystal." It
changes only what the surfaces are **made of**. Shape is another tool's job: Astra's planned
**geographic-statue-converter** turns something into geometry. Run that first, then this, and a statue scan becomes
a crystal wolf like the prismatic wolf she saw in VRChat.

Made with Claude Fable 5.1 (`claude-fable-5-1`), 2026-10-23.

## In Unity (the half that goes into worlds)

Files: `scripts/unity-editor/rainbow-maker-tool-20261023.cs` (an Editor folder) and
`scripts/unity-editor/rainbow-maker-tool-20261023.shader` (anywhere under Assets outside Editor).

1. Select the objects in the Hierarchy: one thing, or a whole group.
2. **Tools > Unity Unicorn > rainbow-maker-tool > Make selection crystal.**
3. Every renderer under the selection now shares one material, `Assets/UnityUnicorn/RainbowMakerTool/rainbow-maker-glass-<date>.mat`,
   on the `UnityUnicorn/RainbowMakerGlass` shader. Meshes are untouched; Undo brings the old materials back.

The material's sliders, with the Rainbow Maker's values as defaults:

| Slider | What it does | Default |
|---|---|---|
| Tint | the glass's colour; clear by default | near white |
| Index of refraction | how much the glass bends the view (1.5 window, 1.55 lead crystal, 2.4 diamond) | 1.55 |
| Dispersion | how far red and blue are bent apart: the colour fringes | 0.12 |
| Body opacity | how solid the glass looks straight on | 0.4 |
| Facet steepness | exaggerates each facet's tilt so neighbours catch different parts of the room: the patchwork | 2.5 |
| Sun sparkle, Sparkle sharpness | the flash when a facet lines up with the scene's sun | 10, 40 |

What the glass shows is the nearest **reflection probe**; bake one in the room, or it reads the skybox. The shader
is built-in pipeline, transparent, no grab pass, stereo-instanced: VRChat-safe on PC. It reads as cut crystal on
flat-shaded meshes and as blown glass on smooth ones.

## In Blender (renders and exports)

`scripts/rainbow-maker-tool/rainbow-maker-tool-20261023.py`, headless:

```
blender --background --python scripts/rainbow-maker-tool/rainbow-maker-tool-20261023.py -- \
  --mesh "<file>" [--mesh "<file>" ...] --out "<folder>" --name crystal --render
blender --background "<scene.blend>" --python .../rainbow-maker-tool-20261023.py -- --selected --out "<folder>" --render
```

- `--mesh` brings files in (OBJ, FBX, glTF/GLB, STL, PLY); `--selected` works on the selected objects of an open .blend.
- The glass is three Glass BSDFs added together (red, green, blue) at `--ior` minus and plus `--spread`, because
  Blender 4.5's glass has no dispersion control. `--tint` colours it.
- Exports `<name>-<date>.fbx` for Unity (give it the shader there), `.blend`, and a `.json` record.
- `--render`: a check still on a dark stage with a small bright sun and a ring of eight sparkle lights behind the
  camera, the way jewellers light a stone, so facets flash. `--video`: a 6 s turntable. Cycles on the HIP GPU,
  96 samples, 90 s cap per frame.

## What it is not

- It does not make geometry: no remeshing, faceting or decimation. That is the geographic-statue-converter's job.
- It does not source scans or decide licences; whatever surfaces it is given, it leaves their provenance to the
  project that owns them.

## Where it came from

The Rainbow Maker world (findastra/the-rainbow-maker, `Art/rainbow-maker/build-rainbow-maker-20261022.py`, the
`real` render mode) worked out, on 2026-10-23: steep facets so light bounces inside; three glasses for dispersion;
a bright small sun plus sparkle lights visible only in reflections; and that a flat pendant with shallow facets
renders as plain glass. The Unity shader is the one on the Rainbow Maker's crystals.
