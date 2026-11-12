---
name: blender-unity-assets
description: Model, sculpt-style shape, and export 3D meshes with Blender (headless, scripted) for the owner's Unity/VRChat worlds, such as Astra's Infinite Pole. Use when a world item needs a real modeled shape (clouds, props, platforms) instead of primitives built in C#.
---

# Blender for Unity/VRChat world assets

Blender is driven by Python scripts in the background, so no clicking is needed and every result can be rerun.

## 0. Before you start
- Read the latest entries in `%USERPROFILE%\Documents\ChatGPT\Mommy's Discord\ACTIVITY.md` and the workspace `AGENTS.md`. Log what you did there afterward.
- Find Blender: the verified install is `%LOCALAPPDATA%\Programs\Blender\blender-4.5.9-windows-x64\blender.exe` (4.5.9 LTS). Otherwise try `blender --version`, then `C:\Program Files\Blender Foundation\Blender *\blender.exe`. If it is missing, **stop and ask the owner**. Blender is free from blender.org, but installing software needs her approval, and there is no spending, ever.
- Put scripts in `<project>\Tools\Blender\` (outside `Assets`, so Unity doesn't import them), and put the exported meshes in `Assets\Astra\Meshes\`.

## 1. Run Blender headless
```
blender -b --factory-startup --python Tools\Blender\make_clouds.py -- --out "Assets\Astra\Meshes\Clouds" --count 8 --seed 3141
```
Read the arguments after `--` from `sys.argv`. Start from an empty scene (`bpy.ops.wm.read_factory_settings(use_empty=True)`), so no default cube is left behind.

## 2. Modeling patterns that work well headless
- **Soft organic shapes (clouds, puffs):** avoid metaballs. Run headless, they left thin necks, holes and stray bits in the 2026-10-06 cloud redo. What worked (`D:\personal-files\unity\VRChatProjects\astras-infinite-pole\Tools\Blender\make_cloud_redo.py`; the project moved there from `Documents\ChatGPT\Mommy's Discord\Astra-Infinite-Pole` on 2026-10-25):
  - Grow overlapping, tilted ellipsoid UV spheres *out of each other*, each new lobe centred about 0.75 × its parent's radius away, so the body is always connected.
  - Put them all in one bmesh, then use a voxel Remesh, which unions the lobes.
  - Keep only the largest island. Do it again after the remesh and after decimating.
  - Use a light Laplacian smooth (3 iterations), then two-scale noise relief along the normals.
  - Squash anything above the walkable height softly with tanh. Never clip it.
  - Asymmetry: a bent main chain, side billows mostly on one side, belly lobes and small crown billows.
- **Seeded variety:** use `random.seed(seed + i)` for each variant, so reruns reproduce the same shapes.
- **Budget for a VRChat PC world:** aim for 300 to 1,500 triangles for something repeated many times. Use a Decimate modifier (`ratio`) and check `len(mesh.polygons)`.
- Apply all modifiers and transforms before export. Use smooth shading with auto-smooth off, so it doesn't look like bricks.
- Optional: store a vertex color (for example, 0 on the underside and 1 on top, or a random value per puff). Shaders can use it for gradients.

## 3. Export for Unity
**Simplest path (used for the cloud redo):** skip FBX.
- Write a JSON file of vertices, normals, colours and triangles, already in Unity space, to `Tools\Blender\out\`.
- Build `.asset` meshes from it in an editor script. No importer settings are needed.
- Converting Blender to Unity: `(x, y, z)_unity = (x, z, y)_blender`, and **reverse the triangle winding**.

FBX path:
```python
bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    bake_space_transform=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
    add_leaf_bones=False, bake_anim=False)
```
In Unity, on the model importer: Read/Write off, Generate Colliders off (colliders are built by the world scripts), and Import Materials None, because the world uses its own shaders.

## 4. Check before handing off
- Blender resolves a relative render path against its own folder (one preview landed in `C:\Review\`). Always pass `os.path.abspath(...)`.
- Render a preview PNG with Eevee (`bpy.context.scene.render.engine = 'BLENDER_EEVEE_NEXT'`, or `'BLENDER_EEVEE'` on older versions). Save it to `<project>\Review\`, then look at it.
- Report the triangle count per mesh and the bounds (min and max for x, y and z) in metres.
- Keep to the contract of the thing you are replacing, such as pivot, walkable top height and footprint. Otherwise colliders and gameplay break.

## 5. Rules
- Smallest complete change. Don't overwrite a mesh unless you keep a backup (`Review\Backups\`).
- Never spend money, sign up for paid add-ons, or download from untrusted sites. Built-in Blender only.
- Add a dated entry to ACTIVITY.md with the files changed, the checks you ran, and what's still open.

## 6. Lessons from the cloud redo (Claude, 2026-10-07)
- **Check who else uses a mesh before replacing it.** `Glitter Cloud 4.asset` also builds 2,133 lounge-furniture puffs. Make new assets and reassign only the renderers you mean to change.
- **Shared shaders:** put a new look behind a `shader_feature_local` keyword (`[Toggle(KEYWORD)]` property) and enable it per material. Everything else on that shader (DJ deck, booths, chairs) keeps the old path and pays nothing.
- **Match texture size to object size.** A pattern that suits a 3 m cloud reads as dark mottling on a 10 m one. Give big objects their own material with a larger texture scale, and fade fold darkening with camera distance.
- **Look at renders up close, at about 15 m and from a bird's-eye view**, not just the Blender preview. Bloom washes out subtle texture up close and exaggerates blotches far away.
- For colliders on walkable meshes, see the `vrchat-unity-world` skill (Unity Stable, `skills/vrchat-unity-world/SKILL.md`; installed for Claude and Codex).
