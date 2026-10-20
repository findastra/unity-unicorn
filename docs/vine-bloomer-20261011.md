# Vine bloomer

The target set by the user: **every roof covered in vines, and every outer wall majority vines**. Strand-by-strand ivy
(the old `ivy3.py` pieces, ~150k triangles for one partly covered wall) cannot reach that coverage within a VRChat budget,
so coverage comes from an **ivy mantle** and the 3D leaves only dress the edges.

## Layers

1. **Mantle texture** (`scripts/vine-bloomer-ivy-texture-20261011.py`, Blender/Cycles headless): ~1,150 scanned English-ivy leaves (ambientCG
   LeafSet017 + LeafSet029, CC0 — already packed as `Imported/Ivy/Ivy_Albedo.png`) laid in overlapping layers over a
   1.5 m tile, tips hanging down, old woody stems beneath, wrapped so the tile repeats seamlessly. Orthographic
   emission renders write albedo + alpha, leaf normals, contact occlusion, and a per-leaf **rank** (top leaves high)
   into `Imported/IvyMat/IvyMat_{Albedo,Normal,Mask}.png` (mask: R smoothness, G occlusion, B rank).
2. **Mantle shells** (one mesh per building piece, instanced wherever that piece stands): take the piece's real
   exterior triangles (roof terraces, domes, parapets, cornice tops, outer wall faces), subdivide to ≤0.8 m edges,
   offset 4–5 cm along the interpolated source normals, and give each vertex a **coverage** value in vertex colour A.
   A second, sparser shell at ~9 cm gives depth when seen at a glance. UVs: planar XZ on up-facing faces, along the
   wall on vertical faces (v = world up so the leaves hang), cylindrical on domes and drums.
3. **Leaf curtains** (`gen/ivy3.py` `grow`/`hang`, lighter than before): falling over cornices and balustrades, round
   corners and window heads, so silhouettes and edges read as real 3D ivy. Flowers (`FlowerSet001`) sparsely.

## Coverage rules

- Roofs, terraces, domes, drums, parapets: mean coverage ≈ 0.85; small clear patches of stone so it reads as grown,
  thinning near the oculus lantern.
- Outer walls: mean ≈ 0.7, thicker from the waterline up the corners and pilasters and cascading from the cornice;
  window and door openings stay clear (no triangles there) with a 0.3 m fade.
- Keep clear: roof water channels and aqueduct beds (water must stay visible), slide carvings, walkable path centres
  on the roof way, the Colossus, plaques and gold lettering.
- Interiors are not part of this pass (they have their own ivy pieces).

## Shader

Unity shader `Piranesi/IvyMat` (manifest shader `ivymat`): alpha-clipped foliage lighting (translucency, wet under the
tide, caustics), plus thinning: `clip(alb.a - 0.5)` and `clip(rank - (1 - coverage) * 0.85 - 0.1)` so the mantle
thins leaf by leaf toward its edges instead of ending on a straight triangle edge. The builder needs a `case "ivymat"`
in `MakeManifestMat` and `manifest_u.py` needs the shader in `normalise`.

## Placement and budget

- Pieces to mantle: `Vest_Dome` (roof + dome), `VestExt_{Window,Window_CutL,Window_CutR,Blind,Gallery,Gate,Tower,Cascade}`
  (outer skins), `ArcadeExt`, `NaveExt` (gallery walls + roofs), `SeaHall`, `IsleHall`, `Tower3`, `Wing_1..3`,
  `BirdTower`, `Castellum`, `Aqueduct`.
- 3 variants per piece (different coverage seeds); in `world7.py` follow EVERY placement of the piece (by object name)
  and pick the variant by index, so neighbours differ.
- Budget per hall (roof + four outer walls): mantle ≤ 20k triangles, curtains ≤ 25k; LOD1 at ~25% with the generic
  `_LOD1` LODGroup. Check the total with the layout object counts before shipping.

## Verify

Render each piece in Blender with the mantle (daylight, oblique and edge-on views) and send the images; then rebuild in
Unity and take review shots from the roof way and from the sea. The user judges coverage from a distance — if a roof
still shows mostly stone, raise coverage, don't add strands.

Status (2026-10-11): the first pass is in the Piranesi House. Every hall roof, dome and drum is covered, and most outer
walls of the halls, galleries, naves, sea halls and isle halls. That's 36 mantle meshes in 597 placements, each with a
LOD. The shell script is `scripts/vine-bloomer-mantle-20261011.py`.

Lessons from the first pass:
- Wind every source face so its normal points out before building the shells. Faces with flipped winding put the shell
  inside the wall, where back-face culling hides it.
- Decimate only small ornament faces. Quadric decimation of the whole surface collapses big, simple faces such as a
  dome drum. Pass big faces through unchanged, and give richly ornamented drums a synthetic cylinder.
- Split triangles by longest-edge bisection, not uniform subdivision. Long cornice slivers otherwise explode into
  thousands of triangles.
- Measure "near an opening" from evenly sampled surface area, not from vertex density, which is low on big flat faces.
- The outer wall field of a hall belongs to the inner side piece (`Vest_Side*`), not only the exterior skin, so mantle both.

See also (2026-10-19): [vine-spawn-with-trumpet-flowers](vine-spawn-with-trumpet-flowers-20261019.md) grows single vines with
trumpet flowers in Unity over objects you pick, in up to 10 colours, for any of the three worlds.
