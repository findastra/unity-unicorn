# Neck blender

The goal: no visible line where an avatar's head meets its body.

## Why the line shows

Many bases ship the head and the body as two meshes. The body's neck pokes out through the head's lower surface, and
each mesh has its own texture. If the body texture's skin at the neck is darker or pinker than the head texture's skin
under the jaw, you get a hard colour edge around the neck.

Fae's base doesn't show it for two reasons: her head and body are one welded mesh, and her body texture is painted
peach around the neck so it matches her face. Poppy's base had separate meshes and plain body skin at the neck
(about RGB 213/141/141 against the face's 255/185/176), so the line showed.

## What the tool does

`scripts/neck-blender-20261011.py` copies Fae's fix onto any separate-head avatar, working from the real geometry:

1. Imports the FBX in Blender (headless, via the `bpy` module) and finds the body vertices under the head surface and
   the line where the neck comes out of the head.
2. Reads the head texture's colour along that line.
3. Paints that colour into a copy of the body texture: full strength under the head and for 4 mm below the line,
   fading to the body's own skin by 4.5 cm. Unused texels near the neck are filled so mipmaps don't bring the old
   colour back. Other UV islands are left alone.
4. With `--unity-mat` it writes a `.meta` for the new texture (same import settings, new GUID), backs up the body
   material, and points its `_MainTex` at the new texture. The original texture is never changed.

Pass the body material's `_Color` as `--body-tint` so a tinted body still lands on the face colour.

## Use it

1. Find the FBX the avatar's prefab actually uses (its GUID is in the prefab). Poppy's prefabs use `Poppy 2.fbx`,
   not `Poppy 1.fbx`.
2. Note the body and head object names, the head skin material name, and the body and face base textures from the
   materials.
3. Run the command from the tool card. Keep the backup folder outside `Assets`.
4. Switch to Unity so it imports the new texture, then check the neck from the front, side and back.

## Limits

- Colour only. If the body and face materials have different rim light or matcap strength, a faint line can still
  show under those effects. Match them in Poiyomi (unlock the material first if it's locked).
- Quest materials made by VRCQuestTools are separate; rebuild them to pick up the new texture.
- It doesn't join or weld the meshes. That needs an FBX re-export and risks the face shapekeys and prefab links.
- Avatars whose head and body are already one mesh (like Fae) don't need it; the script stops if it finds no body
  under the head.

## Status (2026-10-11)

Poppy is done: `Model/Tex/Base Color1 - neck blend.png`, with `Body.mat` repointed and its old copy backed up beside
Poppy's assets. Checked in Blender with flat lighting from the front, side and back.
