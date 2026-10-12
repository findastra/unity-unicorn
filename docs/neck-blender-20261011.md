# Neck blender

The goal: no visible line where an avatar's head meets its body.

## Why the line shows

Many bases ship the head and the body as two meshes. The body's neck pokes out through the head's lower surface at an
angle, and each mesh has its own texture and material. Two things make a line there:

1. **Colour.** The body texture's skin at the neck doesn't match the face texture's skin under the jaw. Poppy's was
   about RGB 213/141/141 against the face's 255/185/176.
2. **Lighting.** Matcap and rim lighting read each surface's normals, and the two surfaces meet at an angle, so they
   light differently right at the line even when the texels match. After the colour fix, Poppy's neck still rendered
   about 8% brighter than her jaw from below (252/184/170 against 234/170/158 in Unity), which showed as a hard edge.

Fae's base has neither problem: her head and body are one welded mesh with smooth normals, and her body texture is
painted peach around the neck to match her face.

## What the tool does

`scripts/neck-blender-20261011.py`, from the real geometry (Blender, headless, via the `bpy` module):

1. Finds the body vertices under the head surface and the line where the neck comes out of the head.
2. **Colour:** paints the face texture's colour along that line into a copy of the body texture: full strength under
   the head and 4 mm below the line, back to the body's own skin by 4.5 cm. Other UV islands are left alone.
3. **Lighting:** writes `Body Neck Mask.png` and `Face Neck Mask.png`. They fade matcap and rim to 0 at the line and
   back to full strength 3 cm away on each side, so both sides render as their matching texture at the line.
4. With `--unity-mat` and `--unity-face-mat`: writes `.meta` files (new GUIDs, masks linear), points the body's
   `_MainTex` at the new texture and both materials' `_MatcapMask` and `_RimMask` at their masks, and backs up every
   file it changes first. The original textures are never changed.

Locked Poiyomi materials compile mask sampling out if no mask was set when they were locked. The script adds
`#define PROP_MATCAPMASK` and `#define PROP_RIMMASK` beside `#define PROP_MATCAP` in each locked shader, which is
what a relock would write. Unlocking and relocking later keeps the masks.

Pass the body material's `_Color` as `--body-tint` so a tinted body still lands on the face colour.

## Use it

1. Find the FBX the prefab actually uses (its GUID is in the prefab). Poppy's prefabs use `Poppy 2.fbx`, not
   `Poppy 1.fbx`.
2. Note the body and head object names, the head skin material name, and the body and face base textures.
3. Run the command from the tool card. Keep the backup folder outside `Assets`.
4. Switch to Unity so it imports everything, then check the neck from below the chin (the angle that shows it most),
   the front and the back.

## Limits

- Quest materials made by VRCQuestTools are separate; rebuild them to pick up the change.
- It doesn't join or weld the meshes. That needs an FBX re-export and risks the face shapekeys and prefab links.
- Avatars whose head and body are already one mesh (like Fae) don't need it; the script stops if it finds no body
  under the head.

## Status (2026-10-11)

Poppy: colour pass, then the lighting masks after Astra saw the line was still there in Unity. Checked in Unity
from below the chin: the hard edge is gone. Backups are beside Poppy's assets in `neck-blend-backup-2026-10-11`.
