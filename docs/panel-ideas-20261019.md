# Panel ideas for Unity Unicorn

Astra asked (2026-10-19): "suggest what other panels I might need to craft, like for either avatar or Unity. Think of the
main concepts for designing both avatars and worlds." These are suggestions, none built yet. Each one follows the matcap
palette's pattern: see the choices side by side the way they will really look, pick one, and send it to Unity with Undo
and a usage receipt. The first five are the ones I would build next, in this order.

## Build next

| # | Panel | Design concept | What it shows and does | Why now |
|---|---|---|---|---|
| 1 | **menu-map** (built: [guide](menu-map-20261019.md)) | Toggles and menus | Draws an avatar's real radial menu (after VRCFury and Modular Avatar build it), page by page, with icons, plus the synced-parameter meter (256 bits) and what each add-on costs. | Poppy is at 256/256 after VRCFury's compressor; every new effect now needs a plan. |
| 2 | **effects-rack** | Effects and props | Every effect on an avatar (fans, sparkles, bending, stars, wings) with its menu path, gesture trigger, hand or foot, and a picture of it firing. Adds one from a learned template. | Tonight's work: 18 fan colours, four bendings, four star kits, two sparkle sets. |
| 3 | **performance-rank** | Performance | VRChat's PC and Quest rank: polygons, materials, meshes, bones, PhysBones, particles, texture memory, download size, with the worst offenders first and a fix for each. | Effects add particles and materials fast. |
| 4 | **gesture-map** | Face and expressions | Each hand gesture (Fist, Open, Point, Peace, Rock'n'Roll, Gun, Thumbs up) with its face picture and what else fires on it (Star Gun fires on Gun, Star Burst on Open). Face tracking status. | Poppy's gestures now drive faces and effects at once; a map shows clashes. |
| 5 | **color-themes** | Colour | Colour palettes as swatch pans (hair, skin, eyes, outfit, emission, accent); try one on the avatar's Poiyomi hue and colour sliders, and save it as a menu preset. | Astra's taste is graphite with neon accents; themes make variants (Golden, Bloody) quick. |

## Avatar panels

- **closet** (with Devilish's Avatar Closet): outfits as saved sets of toggles, with a picture of each, one tap to wear.
- **proportions**: height chart, eye height and body sliders, side by side with other avatars.
- **physbones**: every PhysBone (hair, ears, tail, chains, fans) with presets (soft hair, bouncy ears, heavy chain) and its cost.
- **shader-looks**: rim light, emission, glitter, outline and AudioLink presets per material, on a sphere like the matcaps.
- **variants**: Poppy, Golden Poppy and Bloody Poppy as a family tree: what each changed, and which changes can flow across.
- **credits**: where each asset came from and its licence (copyright-logger and copyright-catcher's results).
- **quest-check**: what breaks on Android and the fallback for each.

## World panels

- **floor-plan**: spawn, paths, landmarks and sightlines from above, with walkable floors and colliders marked.
- **sky-palette**: skyboxes and times of day as round pans (the matcap idea for skies), tried on the world in one click.
- **lighting-mood**: baked or realtime, probes, post-processing and fog presets, each with a render.
- **material-shelf**: the world's materials and trim sheets as swatches, with where each is used.
- **interactions**: mirrors, seats, pickups, buttons and doors, each with its test result.
- **soundscape**: music, ambience zones and AudioLink, with volumes and ranges drawn on the floor plan.
- **social-spots**: where people gather (mirror, DJ booth, seats) and the player cap.
- **journey**: a world's stops in order (Liminal Doors' doors), with the concept plate, status and render of each.
- **world-performance**: draw calls, occlusion, LODs, lightmap size and upload size.

Written with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.
