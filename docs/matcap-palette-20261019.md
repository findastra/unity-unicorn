# matcap-palette

Astra asked (2026-10-19) for "a panel on Unity Unicorn that is matcap palettes, as they would show up on a sphere, so we can
look through them like a makeup palette when we are choosing them", with a bunch sourced and ready. A matcap ("material
capture") is a picture of a lit ball; a matcap shader paints it onto a model by which way each point faces you.

## On the page: unity-unicorn-matcap-panel

- A graphite compact of round pans, one per matcap, with neon edges. Families: Metal, Candy, Holo, Glass, Toon, Neon,
  Soft, Sparkle, plus ★ Favourites and Yours.
- Pick a pan: it shows big on a sphere and on a ring you can turn (drag it, or the Turn slider), so you can see how it
  wraps a shape that is not a ball.
- **Hue** and **Tint** work like Poiyomi's matcap hue shift and colour, so you can see a tuned version before you make it.
- **Save PNG for Unity** writes a 512 px square PNG of the chosen matcap, with your hue and tint.
- **Load my matcaps** shows your own matcap files as pans too. They stay in that browser tab; nothing is uploaded.
- Favourites are saved in this browser (`astra-pet-desk:unity-unicorn:matcaps:1`).

The 48 matcaps are drawn from math by the page (`<script id="uu-matcap-recipes">`), not copied from anywhere, so they are
Astra's to use and share. A new matcap is one line in `MC_RECIPES`.

## PNGs for Unity

```
node scripts/matcap-palette/make-matcaps-20261019.mjs <out-folder> --size 512
```

It reads the recipes from the page itself, so the PNGs always match the panel, and writes
`matcap-<id>-20261019.png` plus `matcap-palette-index-20261019.json`. Node built-ins only.

## In Unity: Tools › Unity Unicorn › matcap-palette

`scripts/unity-editor/matcap-palette-20261019.cs` (copy it into `Assets/UnityUnicorn/Editor/`). It finds every texture in
the project whose path says matcap, shows each once as a round pan (the best of .png, .tga, .psd, .jpg and .bmp copies),
grouped by where it came from, with the Unity Unicorn palette first. Choose a material (or select an object), pick a
matcap slot (Poiyomi `_Matcap`, `_Matcap2` ...; lilToon `_MatCapTex`, `_MatCap2ndTex`), and press **Put … on …**:

- the texture goes in the slot with Undo;
- an off slot is switched on, with the keyword read from the Poiyomi shader source (`[ThryToggle(POI_MATCAP0)]_MatcapEnable`);
- a locked Poiyomi material keeps a switched-off slot off and the window says to unlock it first;
- a usage receipt goes in `UnityUnicornReports/matcap-palette-<time>.json` (`unity-unicorn.tool-usage/v1`).

## Checked

- `node --test test/matcap-palette-test-20261019.mjs`: the panel's label and controls, 48 recipes with unique ids in
  known families, every recipe's colours inside 0 to 1 and nothing outside the circle, a valid PNG, and no HTML writing.
- In Poppy's project (2026-10-19): the window listed 105 matcaps after removing duplicate formats, and Apply on a
  throwaway Poiyomi material set `_Matcap`, switched on `_MatcapEnable` with `POI_MATCAP0` and wrote a receipt. The
  material was deleted afterwards.

Made with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.
