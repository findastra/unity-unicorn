# Marble carver

Statues in the House must look like real carved marble. Real carving comes from real stone: build every figure from a
3D scan of an actual statue, then adapt it by hand in Blender. Never assemble a human or animal figure from primitives,
SDFs or procedural shapes — the user rejected that result as fake and anatomically wrong.

## 1. Find a real scan (licence first)

Search in this order and record the licence of every scan used in `Blender/Scans/Acknowledgement.txt`:

1. **Three D Scans** (Oliver Laric) — no copyright, any use. Mirrored on GitHub at `keijiro/ThreeDScans`
   (`Assets/ThreeDScans/<Name>/<Name>.fbx` + `_normals.png`, `_occlusion.png`); raw GitHub downloads work from the
   cloud workspace even when threedscans.com is blocked. Clone with `git clone --depth 1 --filter=blob:none`.
   Available there: Athena, Hermes, Nymph, Penelope (seated), Plato (bust), ShepherdBoy (seated youth), DeChalon
   (écorché, arm raised), MarblePlayer (crouching), BootlegEve, Gutenberg, Column03. Also `keijiro/CodameScans`.
2. **Smithsonian Open Access 3D** (CC0) and **Poly Haven** (CC0) — animals, busts, horse. These sites are usually blocked
   from the cloud workspace: download on the user's PC (built-in browser or ask the user) into `the Downloads folder`
   and stage from there.
3. Avoid scans with non-commercial terms (e.g. Stanford's Lucy, Thai Statue, Dragon) for public worlds.

See the statue map at the end of this guide for which scan becomes which House figure and what each still needs.

## 2. Carve in Blender, live on the user's screen

The user wants to watch. When the session is linked to the PC, drive the user's Blender (app name `Blender 5.2.2.0`)
with computer use; otherwise run the same scripts headless in the cloud and show renders.

1. Copy the scan files onto the PC: `SendUserFile` (display `attach`) then `device_commit_files` into
   `<project-path>\Blender\Scans\<Scan>\`.
2. Open Blender, close the splash (click General), open the **Scripting** workspace, click **New** in the Text Editor.
3. For each step: write the script to the clipboard (`computer_write_clipboard`), click inside the Text Editor,
   `ctrl+a`, `ctrl+v`, `alt+p`, wait, screenshot the viewport and show the user. Scripts (edit the NAME/paths at the top):
   - `scripts/marble-carver-01-import-scan-20261011.py` — import the FBX, scale to 2 m, base on the floor, marble material with the scan's
     normal + occlusion maps, Material Preview, frame it.
   - `scripts/marble-carver-02-weather-marble-20261011.py` — the House weathering: stone veins, grime from the occlusion map, rain/salt
     streaks, the green-brown tide band rising from the feet, lichen on upward faces.
   - `scripts/marble-carver-03-bake-export-20261011.py` — bake albedo + roughness onto the scan's UVs (2048), export OBJ + .blend to
     `PiranesiHouse\Blender\Out\<Name>\`.
4. Adapting the pose or combining scans (raising an arm, a bull head on a body, wings): do it with an armature and
   automatic weights, or by joining parts then a Voxel Remesh at ≤3 mm (2 m scale) and Smooth/Clay strokes along the seam,
   then re-bake. Leave ancient breaks (missing forearms, chipped noses) — they are what makes it read as real.
5. Remote mouse sculpt strokes are unreliable (brush size and pressure don't register predictably); prefer scripted
   operations, and use brushes only where a visible stroke matters. Numpad keys can't be sent — use the View menu.

## 3. Bring it into the House

1. Stage the OBJ and baked PNGs to the cloud with `device_stage_files`.
2. Run `scripts/marble-carver-bring-statue-20261011.py` (Blender's Python, headless) — it centres the base, scales to the target height, turns
   the figure to face +Z in Unity (a rotation, never a mirror), writes `<Name>.bytes` (PMS2 with UVs) and
   `<Name>_LOD1.bytes`, the textures (`_Albedo`, scan `_Normal`, `_Mask` = R smoothness, G occlusion) and a
   `Materials.json` entry (shader `prop`). In the Piranesi repo the same job is `gen/real/scan_statue.py`.
3. Statues placed through the layout's `statues` list use their manifest materials automatically
   (`HouseBuilder.cs` statue loop: `IsManifestMesh` → `MakeRendererMulti`). Re-run `manifest_u.py` and `layout7.py`.
4. If the figure carries gameplay (the Colossus seat, footholds, the storm spiral), recompute those points from the new
   mesh — see `references/statue-map.md`.
5. Before overwriting any file on the PC, stage the PC copy and diff it: another agent (Codex) also edits the project.
   Merge, never clobber.
6. Copy to `<project-path>\Assets\Piranesi\{Meshes,Imported\<Name>,Data,Editor}`,
   rebuild (Review/do_build trigger or the Piranesi menu) and take close-up review shots (Review/do_props). Never enter
   Play mode unprompted.

## Quality bar

Check every statue at three distances: the silhouette from across the hall, the face and hands at 2 m, and the marble
surface up close. Anatomy, drapery and hands must come from the scan. If something still looks synthetic, the fix is
a better scan or a better bake — not more procedural detail.
## Which scan becomes which House figure

Status as of 2026-10-11. Heights are final Unity heights in metres.

| House figure (mesh) | Scan | Work still needed | Height |
|---|---|---|---|
| Colossus (`Colossus`) — centre hall | Athena (Three D Scans) | DONE: weathered, baked, in Unity. Her right forearm is an ancient break; the climb ends on her LEFT shoulder (seat ≈ (-1.15, 10.94, 0.0) statue-local), not a palm. | 13.5 |
| Seated giant (`Statue_SeatedSage`) | Penelope (seated, pensive) | weather, bake | 1.39 × layout scale |
| Drowned head / niche busts (`Statue_Head`) | Plato bust | weather, trim socle | 0.75 |
| Child (`Statue_Child`, Boy with Cymbals family) | ShepherdBoy (seated youth) | model cymbals into his hands, weather | 0.7 |
| Angel (`Statue_WingedFigure`) | Nymph or Athena body | wings: model by hand in Blender (feathers as layered cards → remesh), attach at the shoulder blades | 2.3 |
| Minotaur (`Statue_Minotaur`) | Hermes body | a real bull-head scan (Smithsonian/Three D Scans, download on PC) joined at the neck, remesh + smooth the seam | 2.45 |
| Queen (`Statue_Queen`) | BootlegEve or Athena head | crown | 0.85 |
| Elephant, Gorilla, Albatross | none yet | need real animal scans downloaded on the PC; Three D Scans has an eagle, lion, rhino, panther, goat — no elephant or gorilla | — |

### Gameplay tied to the Colossus

- `Kit2Extra.json` → `colossus` = {palm (seat point), palm_normal, height, pedestal 1.8, seat}. The layout's seat,
  oculus spotlights, glitter and storm spiral all read `palm`.
- `Kit3Extra.json` → `colossus_platforms` / `colossus_ledges` / `colossus_ledge_top`: generate with
  `gen/sculpt/colossus.py platforms(Vu, seat, normal)` from the new mesh's Unity-space vertices.
- Layout text: "Rest on the Colossus' shoulder".

### Paths

- Scans on the PC: `PiranesiHouse\Blender\Scans\<Scan>\`
- Carved results: `PiranesiHouse\Blender\Out\<Name>\` (`.obj`, `_Albedo.png`, `_Roughness.png`, `.blend`)
- Unity: `PiranesiHouse\Assets\Piranesi\Meshes`, `Imported\<Name>`, `Data`
- Cloud pipeline: `gen/real/scan_statue.py` (same job as `bring_statue.py`), then `gen/manifest_u.py`, `gen/layout7.py`
