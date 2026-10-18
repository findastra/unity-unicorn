# Realistic props: what an AI needs to make Unity edits look real · 2026-10-18

Started by Claude Opus 5.5 (`claude-opus-5-5`) at Astra's request, after the payphones in Astra's Infinite Pole were
called out as flat and fake. This guide builds on the marble-carver lesson: **start from something real, never from
primitives**.

## Why props read as real
Eight skills. Each one is something an assistant has to check on purpose, because none happens by accident.

1. **Real reference and real size.**
   - Model from a real object: a CC0 scan or model, plus photos.
   - Keep its true dimensions. The payphone below is 401 × 301 × 740 mm.
   - Wrong proportions give a prop away before anything else.
2. **Parts that follow the mechanism.**
   - A real object is an assembly: housing, plates, buttons, hinges, screws, cords.
   - List the sub-components first (see the typewriter below). Then decide which parts need real geometry and which can be texture.
3. **Soft edges.**
   - Real edges are never perfectly sharp.
   - Bevels of a few millimetres catch a highlight, and that is what makes a prop look solid rather than paper-thin.
   - Use a Bevel modifier, weighted normals, or bevels baked into the normal map.
4. **Silhouette in geometry, detail in textures.**
   - Keep geometry for what changes the outline.
   - Put fine detail (keys, labels, scratches, seams) into a PBR texture set: albedo with no baked lighting, metallic, roughness, normal and ambient occlusion.
5. **Wear that follows physics.**
   - Exposed edges wear first.
   - Grime collects in cavities.
   - Hands polish and scuff what they touch: keypads, handles, the coin slot.
   - Uniform dirt looks fake.
6. **Consistent texel density.**
   - Detail per centimetre should suit how close the player gets, and match the props around it.
   - A sharp prop next to blurry ones looks pasted in.
7. **Labels and decals, with no real brands.**
   - Instruction cards, stickers and numbers sell realism.
   - Paint out company logos and brand names, in the albedo *and* the normal map, because embossed logos survive a colour change.
8. **Correct material response in the engine.**
   - In Unity, use the Standard shader (or the project's PBR shader) with the right importer settings:
     - normal maps as Normal Map type;
     - metallic, smoothness and occlusion maps with sRGB off.
   - Then judge it in the world's own light, close up and from a few metres.

## Workflow for an assistant
1. **Find a real base.** Use the Poly Haven API (`https://api.polyhaven.com/assets?t=models`; all CC0), Smithsonian Open Access, or Three D Scans.
   - Check the licence.
   - Ask Astra before downloading, stating the file, source and size.
   - Add a row to the asset ledger (`kind: model`), plus a download row (see "Downloads" in `asset-ledger-20261014.md`).
2. **Decompose it** with `prop-decomposer-20261018.py`. It reports the part list, sizes, soft-edge widths and material zones, and renders hero, front, side and exploded views.
3. **Choose:**
   - **restyle the real base** (preferred: it keeps the real proportions, wear and texture set), or
   - **rebuild** from its measured parts when the style must differ completely.
4. **Restyle textures by mask, not by painting over.**
   - Shift hue only where the mask says so (for example unsaturated metal).
   - Keep the original brightness, so scratches and grime survive.
   - Flatten brand marks in albedo, normal and occlusion.
5. **Budget.**
   - Decimate with UVs kept: the normal map carries what the decimation removes.
   - Keep repeated props near 15–20k triangles.
6. **Unity.**
   - Build the mesh with tangents (`RecalculateTangents`) and set the importer types.
   - Use one instanced material.
   - Mount the prop by its real-world contact (back plate flush with the wall, at a sensible height), at the same proportion to its surroundings as in reality.
7. **Review** close (0.7 m), mid (2 m) and in the world's lighting. Compare with the source render.
8. **Record** the run: tool history (completed-at time, report path) and the asset-ledger rows.

## Example: decomposing a typewriter
A manual typewriter, the kind an assistant might be asked to make. It's about 30 × 30 × 12 cm for a portable model.
Parts below come from patent descriptions and mechanism explainers (sources at the end):

| Part | What it is | Geometry or texture | Material and wear |
|---|---|---|---|
| Body shell | Pressed steel case around the mechanism | Geometry, 3–5 mm bevels | Gloss enamel; chips on the corners |
| Keyboard | 40–46 round keys on key levers, in staggered rows | Keys as geometry; legends as texture | Chrome ring, glass or plastic top; legends worn on common letters |
| Space bar | Long bar under the keys | Geometry | Polished where thumbs hit |
| Typebasket and typebars | Fan of thin bars that swing up to strike | Geometry (visible through the top) | Steel; dust in the gaps |
| Platen | Rubber roller the paper wraps round | Cylinder + knobs | Black rubber, hardened and shiny |
| Carriage | Moving frame carrying the platen, return lever, margin stops | Geometry | Chrome lever, worn grip |
| Ribbon spools and vibrator | Two spools at the sides, a guide in front of the platen | Geometry + ribbon strip | Red and black ribbon |
| Paper table and bail | Guide behind the platen, bar with small rollers | Thin plates | Painted metal, scratches |
| Feet | Rubber feet under the base | Small geometry | Rubber |

Before modelling, a free CC0 base would be searched for first. Poly Haven has no typewriter, but its cash register,
cassette player and stapler show the same construction: a pressed shell, chrome trim and worn keys.

## Worked example: the Infinite Pole payphone (2026-10-18)
- **Base:** Poly Haven *Korean Public Payphone 01* by UM JOORIN, CC0: 48,132 triangles, 401 × 301 × 740 mm, a PBR set up to 8K, worn stainless steel.
- **The decomposer's finding:** the realism is almost all in the **texture set on one welded shell**.
  - The connected parts are only the body, two back plates, a cord and two small fittings.
  - The keypad, screen, instruction card, slots, scratches and grime are texture and normal-map detail.
  - Soft edges measure 5–10 mm.
- **Restyle:**
  - The steel became rose-pink metal, with its brightness variation kept.
  - The orange card unit became hot pink.
  - The dark plastics, screen and handset stayed as they were.
  - The company logo and a branded transit card were painted out in albedo, normal and occlusion.
  - Decimated to 16,000 triangles, and given a Standard material with all four maps.
- **Lesson for the decomposer:** splitting by connectivity misses parts that live in the texture. The next version should also split by UV island and texture zone.

## Sources consulted (2026-10-18)
- Poly Haven API and asset page for `korean_public_payphone_01` (CC0).
- 80.lv: hard-surface bevels with weighted normals; surface damage and wear; prop workflow and texture sets.
- Polycount: baked bevels and wavy reflections at low texel density; when trim sheets help.
- "Bevels in video games" (radiator blog, 2017): bevels catch light and make worlds look expensive.
- Typewriter mechanism: Google Patents US972706A and US516300A; Firgelli Automations "Typewriting machine mechanism".
