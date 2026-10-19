# unity-asset-creator checklist: how to build a good VRChat world asset

The ordered steps the **unity-asset-creator** tool walks through, and the trick follows. Version 1.1.0, 2026-10-19
(1.0.0 was 2026-10-18), by Claude Opus 5.5 (`claude-opus-5-5`) for Astra. It belongs to Unity Unicorn (`pet-program-5`) and is incubating in
Liminal Doors until it moves to [findastra/unity-unicorn](https://github.com/findastra/unity-unicorn).

Each step says **what to do**, **why** (with its source), and **what the tool checks by itself**. Steps go in order:
a step can be marked done only when every step above it is done. A failed check blocks the step. A warning can pass
with a written note that says why it is acceptable here.

Numbers are for **one asset** in a world. VRChat's own budget is for the whole world (about 250,000 triangles and
100 MB on Android), so each asset gets a share of it. Set the budget in step 1.

---

## 1. Plan: name, version, budget

- Give it an id `<world-or-avatar>-<asset>` in lowercase words with hyphens (the asset-ledger rule). Give it a
  display name, a kind (model, prop, effect, sky, ...), a version and the platforms it is for (PC, Android).
- Versions are `MAJOR.MINOR.PATCH`. Start at `1.0.0`. Raise MAJOR when a world using it must change, MINOR for a
  look or feature, PATCH for a fix.
- Set the budget: triangles, materials, largest texture, particles. Defaults for a prop: 10,000 triangles, 4
  materials, 1024 px textures, 300 particles.
- **Why:** VRChat recommends about 250,000 triangles for a whole Android world, and 1k textures at most
  ([VRChat: Quest content optimization](https://creators.vrchat.com/platforms/android/quest-content-optimization)).
  A budget per asset keeps the world inside it.
- **Tool checks:** the id pattern, a semantic version, a prefab assigned, budgets above zero.

## 2. Provenance: who made it, from where, under which licence

- Fill in the asset-ledger row: creator, source, licence (SPDX id, `own-work`, `store-personal`, ...),
  attribution text, and whether a **public** upload is allowed.
- **Why:** "No row, no ship." Paid assets never go in a repo, research-only and unclear ones are private placeholders
  ([Unity Unicorn asset-ledger](https://github.com/findastra/unity-unicorn/blob/main/docs/asset-ledger-20261014.md)).
- **Tool checks:** creator, source and licence filled in. Licence `unclear` or `research-only` fails a public asset.

## 3. Scale, pivot and facing

- 1 Unity unit = 1 metre, Y up. The front faces +Z. The pivot sits at the base centre, so placing it at a floor point
  stands it on the floor. The prefab root has scale 1 and no rotation.
- **Why:** assets that agree on scale and pivot drop into any world without fixing transforms, and static batching
  and lightmaps behave predictably.
- **Tool checks:** root scale 1 and rotation 0. The lowest point of the visible meshes is within 5 cm of the pivot.
  It reports the size in metres. You confirm the front faces +Z.

## 4. Geometry

- Stay within the triangle budget. Every mesh has normals. Separate objects that should hide separately stay
  separate meshes (don't merge a building and its ground).
- Static, baked assets need lightmap UVs (UV2).
- **Why:** triangle count is VRChat's headline world budget. Merging unrelated objects stops occlusion culling from
  hiding them ([VRChat](https://creators.vrchat.com/platforms/android/quest-content-optimization)).
- **Tool checks:** total triangles against the budget, meshes without normals, missing UV2 on static parts (warning).
- **Make it look made, not generated:** see [Craft](#craft-make-it-look-made-not-generated) below; it applies to
  steps 4, 5 and 6.

## 5. Materials and shaders

- Use as few materials as possible (share one material, or one atlas, per object). Turn on **GPU instancing** on
  every material. Avoid transparency where you can; when you can't, keep it to one material.
- PC: Standard or a light custom shader. Android: `VRChat/Mobile/*` shaders with baked light.
- Glow comes from HDR emission, which the world's bloom picks up, not from extra lights.
- **Why:** each material adds draw calls, and VRChat is CPU-bound, so material count is the biggest lever. VRChat asks
  for GPU instancing on all materials and advises avoiding transparency
  ([VRChat](https://creators.vrchat.com/platforms/android/quest-content-optimization),
  [VRChat wiki: atlasing](https://wiki.vrchat.com/wiki/How_to_Atlas_everything_in_your_World),
  [Vket: render cost](https://winter2021.vket.com/en/submission_tips/4)).
- **Tool checks:** material count against the budget, instancing on each, missing or error shaders, how many are
  transparent, and non-mobile shaders on an Android asset (warning).

## 6. Textures

- 1024 px or less (Android), power-of-two sizes, compressed, mipmaps on for 3D surfaces. Colour maps sRGB, data maps
  linear. Use vertex colours or flat colours where a texture adds nothing.
- **Why:** textures are the main memory cost. VRChat says 1k at most, and that Crunch only shrinks the download, not
  memory ([VRChat](https://creators.vrchat.com/platforms/android/quest-content-optimization)).
- **Tool checks:** each texture's size and import maximum, compression, mipmaps, power-of-two.

## 7. Colliders

- Simple colliders (boxes, spheres, capsules) only where players stand or touch. A walkable collider is **thin and
  under the visible surface, never above it or outside it seen from above** (Astra's rule from Infinite Pole).
  Triggers are marked as triggers.
- **Why:** physics is allowed in worlds but shouldn't be overused (VRChat). An invisible ledge above a surface feels
  like a bug.
- **Tool checks:** lists every collider. A solid collider whose top is more than 2 cm above the visible top fails.
  Non-convex mesh colliders are a warning. You confirm the colliders match where people stand.

## 8. Lighting

- A non-moving asset is **static** (batching, lightmaps and occlusion). No realtime lights inside an asset: use
  emission, and light probes for moving parts.
- **Why:** baking is essential and realtime lights are very expensive. Bake occlusion culling
  ([VRChat](https://creators.vrchat.com/platforms/android/quest-content-optimization)). Mark unmoving objects static
  ([Unity Learn: optimization](https://learn.unity.com/tutorial/3-2-optimization-1)).
- **Tool checks:** realtime lights in the asset (warning). Static flags on a non-moving asset's renderers (warning
  when missing). It lists the glowing materials.

## 9. Effects (particles)

- Cap the particles (the budget is the sum of every system's max particles). Small, soft particles keep overdraw low.
  No particle collision. Use one shared particle material with a cheap additive or mobile shader. Let systems cull
  when off screen. Mesh particles use GPU instancing.
- **Why:** stacked transparent particles cost GPU time (overdraw), and collisions add CPU work. Mesh particles render
  much faster instanced ([Unity: particle GPU instancing](https://docs.unity3d.com/Manual/PartSysInstancing.html)).
- **Tool checks:** the summed max particles against the budget, collision modules (warning), particle renderers
  without a material (fail), and culling mode.

## 10. Prefab

- One prefab, named after the asset, inside the asset's folder, holding everything the asset needs. No missing
  scripts. Children have real names (no "GameObject"). Dependencies stay inside the asset's folder, so the package is
  self-contained.
- **Tool checks:** missing scripts (fail), unnamed children (warning), dependencies outside the asset folder
  (warning, listed).

## 11. Review in the world

- Look at it in the world's own light: front, three-quarter and top renders, made by the tool against the open scene's
  sky. Then run the VRChat desktop test (Build & Test) and look at it in the client.
- **Why:** the editor isn't the client. Renders catch looks, and the desktop test catches what only VRChat shows.
- **Tool checks:** the preview renders exist for this version. You confirm you looked, and that the desktop test ran.

## 12. Export and publish

- The tool writes `<id>/<version>/` with the `.unitypackage` (the asset folder and its dependencies), the preview
  PNGs and `manifest.json`. The manifest holds the id, version, stats, the ledger row, the finished checklist with
  notes, checksums, and the Unity and VRChat SDK versions.
- Then `publish-asset-20261018.py` opens a pull request adding that version to
  [findastra/unity-assets-library](https://github.com/findastra/unity-assets-library). Astra merges it.
- **Tool checks:** every step above is done, the version isn't already exported, and the package and manifest were
  written with matching checksums.

---

## Craft: make it look made, not generated

Added 2026-10-19, after Astra said the first booth "still looks like rudimentary shapes" and asked for it to "look real
and handcrafted, not computer generated". These are the habits prop artists use, and how the Liminal Doors booth
(`Tools/Blender/make_phone_booth.py`) does each one. Model in Blender, headless and scripted, so every result can be
rerun (Codex trick `blender-unity-assets`).

1. **Work from a real reference.** Take proportions and the telling details from a real object, not from primitives.
   The booth follows Sir Giles Gilbert Scott's K6 kiosk (1935): about 2.5 m tall, a domed roof, eight rows of
   narrow-wide-narrow panes, crowns in the pediments
   ([Falkirk museum record](https://collections.falkirk.gov.uk/objects/55188/kiosk-telephone),
   [Kent listing](https://heritage.kent.gov.uk/Monument/MKE122847)). Scale may bend for VR (the booth keeps 1.3 m so
   avatars fit); the details may not.
2. **No razor edges.** Bevel every hard edge (a few millimetres, 1 to 3 segments) and use weighted normals, so edges
   catch light like cast or machined parts. Pre-subdivide round parts before bevelling, or the bevels look artificial
   and the baked maps suffer ([80.lv: radio receiver](https://80.lv/articles/prop-art-tips-creating-a-realistic-radio-receiver-in-blender-marmoset-toolbag/)).
3. **Bake the light in.** Ambient occlusion from the whole asset (parts shade their neighbours), saved as the
   occlusion map and used as the grime mask.
4. **Put wear where real wear goes.** A curvature (edge) mask drives chipped paint and polished metal on edges; the
   occlusion drives grime in corners; streaks run down vertical faces; tops fade in the sun. Keep it sparse: a few
   flakes, not a pattern ([80.lv: vintage drill](https://80.lv/articles/creating-a-vintage-drill-in-zbrush-substance-3d-painter-marmoset/)).
   Layer it in order: base, marks, wear, dirt, dust.
5. **Use real surface scans for the fine detail.** CC0 scans for roughness breakup, scratches and fingerprints
   ([ambientCG](https://ambientcg.com), CC0), box-projected so there are no seams. Download them only with the
   owner's OK, and keep the zips out of git; only the baked textures go in the project.
6. **Check each channel on its own.** Look at albedo, roughness and metallic separately before judging the whole: most
   "CG look" comes from flat roughness.
7. **One atlas per material group.** Join an atlas's parts into one mesh to unwrap and bake (separately unwrapped
   parts overlap and paint over each other), then split them again with their own pivots (the door keeps its hinge).
8. **Use a PBR shader that shows the bake.** Standard with albedo, metallic-smoothness, normal, occlusion and emission
   maps; the world's reflection probe does the rest.
