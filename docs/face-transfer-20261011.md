# Face transfer

The goal: know how each avatar's face tracking is built, and move a working face from one avatar to another.

## What a face tracking build is

Three parts have to agree:

1. **OSC parameters.** VRCFaceTracking sends values to the parameters in the avatar's VRCExpressionParameters
   asset. Their names show the standard:
   - `FT/v2/JawOpen`: VRCFT v5, Unified Expressions (current).
   - `v2/JawOpen`: VRCFT v4, Unified Expressions.
   - `JawOpen`, `EyesWiden`, `MouthApeShape` with no prefix: the legacy SRanipal set.
   - A trailing `1`, `2`, `4` or `Negative` is a binary-encoded parameter that saves sync bits; it counts as the
     same expression.
2. **The module.** A template package (Adjerry91's VRCFT templates, Jerry's templates and similar), a VRCFury or
   Modular Avatar prefab, or a hand-made FX layer. Its animation clips name blendshapes as `blendShape.<name>` on
   a renderer path such as `Body`.
3. **The face mesh.** Every blendshape the module animates has to exist on the renderer at that path. A missing
   one doesn't error; that expression just does nothing.

## What the tool does

`scripts/face-transfer-20261011.py`, standard library only, reads project files without Unity:

- **scan** writes a face profile: the parameters and their standard, the modules found (folders whose names look
  like face tracking, with their GUIDs and animated blendshapes), and each FBX's blendshapes with how many of the
  Unified Expressions, ARKit and SRanipal names it covers. `--builds-log` appends one line per scan to a private
  JSONL log; `builds` prints it as a table.
- **transfer** collects the module folders plus every in-project file they reference by GUID, checks the target
  FBX has every blendshape the clips animate, and reports GUID conflicts. It is a dry run unless `--apply` is
  given. Applying copies the files with their `.meta` files (so GUIDs and references survive) and backs up
  anything it overwrites to `--backup-dir`, which must be outside `Assets`. It refuses to apply while GUID
  conflicts exist.

## Use it

1. Read the avatars' edit logs and find the real Unity projects (the folders with `Assets/`, `Packages/` and
   `ProjectSettings/`).
2. Scan the source and target projects with `--builds-log` so both builds are on record.
3. Dry-run the transfer with `--report`. Fix what it finds first:
   - **Missing blendshapes:** the target base doesn't have them. Add them (or a Unified Expressions shape set for
     that base), or accept that those expressions won't move.
   - **Renderer path:** if the clips animate `Body` but the target's face mesh is called something else, rename
     the target's renderer or retarget the clips in Unity.
   - **Unresolved GUIDs:** those are package files (VRCFury, Modular Avatar, Poiyomi). Install the same packages
     in the target through VCC.
4. Run with `--apply`, switch to Unity, drop the module's prefab under the avatar root (or merge its FX layer and
   parameters), then test in play mode with VRCFaceTracking running and a scan again for the log.
5. Note the change in the avatar's edit log and the avatar plans.

## Limits

- It doesn't edit animator controllers, expression parameter assets or the avatar descriptor; wiring the module
  in is done in Unity.
- It reads blendshape names straight from FBX files. Shapes added in Unity by other tools, or meshes stored as
  `.asset`, aren't seen.
- Module detection is by folder name. Pass `--module` with the exact folder when a module is named differently.
- Test runs on synthetic projects are not tool usage. Builds logs, profiles and reports are private and stay
  outside Git.

## Status (2026-10-11)

Added and tested on synthetic projects. Not yet run on a real avatar.
