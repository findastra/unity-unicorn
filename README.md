# Unity Unicorn

*An Intelligent App, or Pet, by Astra.*

## Unity Unicorn

Keep scene ideas, build tasks and release checks in one place.

## How to run

Open [unity-unicorn-20261008.html](unity-unicorn-20261008.html) directly in a modern browser. No install, server, account, or network connection is needed.

## What works

- Add and edit task records with purpose-specific fields.
- Search, filter by status, advance a record, and delete with undo.
- Keep entries in this browser's local storage.
- Export a private JSON backup or Markdown worklist.
- Choose among all nine pet moods.
- Search a library of Unity, cloud-generation, inspection and build tools.
- Import a private catalog with local paths and dated execution evidence.
- Import masonry-audit reports and usage receipts; inspect findings, metrics and limitations.
- Track the last completed use in America/Denver, with **Unknown** when evidence is missing.
- Copy command templates and export a private tool-library backup.

An optional ignored local sidecar can supply the installed desktop app with private inventory and execution dates. Run tools locally and import their reports. The browser does not connect to Unity or execute commands. See [the tool-library contracts and workflow](docs/tool-library-20261011.md).

## Skill book

What Unity Unicorn has learned about avatar and world work, sorted by category, is in [docs/skills-20261009.md](docs/skills-20261009.md). Add new skills there and log each session.

## Avatar tracker and plans

- [docs/avatar-tracker-20261009.md](docs/avatar-tracker-20261009.md): which avatars are uploaded to which VRChat account, with PC, Quest, impostor, fallback and opti coverage, plus the upload to-do list.
- [docs/avatar-plans-20261009.md](docs/avatar-plans-20261009.md): planned edits gathered from the edit log in each avatar's folder.

The tracker is updated by hand or by a routine scan. The app does not log in to VRChat itself.

## Asset ledger (design, 2026-10-14)

How Unity Unicorn acquires, audits, handles and credits every asset in Astra's worlds and avatars: a row for every
asset before it ships, paid assets never in a repo, research-only and unclear assets kept as private placeholders, and
credits generated per world. Design: [docs/asset-ledger-20261014.md](docs/asset-ledger-20261014.md); worked examples for
Astra's Infinite Pole (all cleared) and Piranesi House (blocked for public upload until its stand-in scans are replaced):
[docs/asset-ledger-example-20261014.json](docs/asset-ledger-example-20261014.json). Not built into the web app yet.

## Data and limits

Records are stored only in the browser where you enter them. Clearing site data can remove them; export backups. Exported records can contain personal information and must stay outside this source repository. The page does not send entries to a server. The pet's mood is a display choice. Loading the app, copying a command or importing a catalog never creates a new use timestamp. Imported report times are execution evidence supplied by the tool; the browser does not independently verify them. Existing planning-desk data is preserved in its original storage key.


## Source version

Version `0.2.0-20261011`, source tag [`v0.2.0-20261011`](https://github.com/findastra/unity-unicorn/tree/v0.2.0-20261011). Source publication and release verification are recorded separately from local feature checks in [the dated publication record](docs/publications-20261011.md). Previous revisions are recorded in docs/publications-20261009.md.

## Credits

Made by Astra. Interface prepared with Codex (GPT-6), 2026-10-08. The pet artwork is copied from Astra's PetOS.

Tool library and report imports prepared with Codex (GPT-6), 2026-10-11. The square pet-well layout is preserved from the Cage's 2026-10-09 revision.

Desktop hub sync, 2026-10-11: the Unity tool-library interface now accepts local catalog/verification imports, preserves original execution dates, and exports private backups. Local seed files are not published.

## Open arches and dated tools (0.3.0-20261011)

The library includes 31 dated tool definitions with short summaries below each name, including the no-windows auditor and its Unity scene-refresh workflow. Known tool descriptions update without changing recorded use dates. The naming rule is documented as `my-preferred-nomenclature` in the skill book.

See [publication and validation](docs/open-arches-publication-20261011.md). Source: [v0.3.0-20261011](https://github.com/findastra/unity-unicorn/tree/v0.3.0-20261011). The pet remains a local catalog and report viewer; it does not launch Unity commands itself.

## Marble carver and vine bloomer (2026-10-11)

Two world-building tools for the Piranesi House, with step scripts under `scripts/`:

- **marble-carver**: statues that look like real carved marble, starting from scans of real museum statues. They're adapted and weathered in Blender, baked, and brought into Unity. Guide: [docs/marble-carver-20261011.md](docs/marble-carver-20261011.md).
- **vine-bloomer**: ivy over every roof and most of every outer wall, using a Blender-rendered ivy mantle from scanned leaves plus 3D leaf curtains at the edges. Guide: [docs/vine-bloomer-20261011.md](docs/vine-bloomer-20261011.md).

Added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-11.

## Neck blender (2026-10-11)

- **neck-blender**: hides the line where a separate head mesh meets the body. It paints the face's skin colour into a copy of the body texture around the neck and fades matcap and rim out at the line on both materials, all weighted from the real geometry in Blender, with backups. Guide: [docs/neck-blender-20261011.md](docs/neck-blender-20261011.md).
- **face-transfer**: keeps a private log of each avatar's face tracking build (which VRCFT OSC standard its parameters use, which module drives them, which blendshapes the face FBX has) and moves a face tracking module to another avatar with its dependencies, after checking the target has every blendshape it animates. Guide: [docs/face-transfer-20261011.md](docs/face-transfer-20261011.md).

Added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-11.

## Framework panels (0.4.0-20261018)

The top of the page now follows [the Pet Program Framework](https://github.com/findastra/astras-pet-os/blob/main/PET-PROGRAM-FRAMEWORK.md), section 3 (Astra's sketch of 2026-10-18): the cyberhome, the **Unity Stable** title with its version, the quick summary and status line, the four stacked L1 to L4 panels, session notes that save to the backlog, the nine emotions, clicker training, the unified footer and the chat button. Every panel collapses on a phone, and every item has its two paired lights (ON/OFF and maintenance).

The L1 panel lists all 32 tools on `main` and the 21 that are not merged yet (PR #1's avatar effect tools, including **sparkle-strapper** and **fan-fitter**, PR #3, and the asset ledger), each marked "not merged". **Added tonight** lets you add a tool or program as you make it; it stays in this browser and goes into the backlog.

Limits: the lights, the backlog and added items are saved in this browser only, because the Farm's core switch is not built. The seat, the chat bot, keys, grants and the weekly backlog run are shown as planned. Added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.

## world-spawn-tool (2026-10-18)

- **world-spawn-tool**: spawns a new VRChat world. First a public GitHub repo, then a link with a cute one-line summary in the Findastra profile README (by pull request), then the Unity project skeleton. It started in Liminal Doors (draft findastra/liminal-doors#1) and moved here, its own pet program, on 2026-10-18. Guide: [docs/world-spawn-tool-20261018.md](docs/world-spawn-tool-20261018.md); code and tests in `scripts/world-spawn-tool/`.
- Limit: its skeleton step still copies from a reference world. Astra's rule since 2026-10-18 is that new worlds start fresh from VCC's World template, so change that step before the next world.

`pet.json` now has the framework's `intelligence_levels` block: every tool in L1 (on `main`, and not merged with its pull request), tricks in L2, harness skills in L3, and no item in two panels. Added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.

## Labels as in Astra's sketch (0.4.1-20261018)

Every panel's label now sits on its top border, the way Astra drew it: the cyberhome, the quick summary, the L1 to L4 panels, session notes, emotions, clicker training, the unified footer and the chat. The cyberhome has its own label (`unity-unicorn-cyberhome`) and the title has none. The quick summary uses the sketch's words: a dashboard for your world and avatar programs and the tools and tricks that manage them, made to preserve the authenticity, creativity and provenance of all artists' work. The moods show the sketch's names (awake, sleepy), and the chat button is the sketch's arrow in a circle. The L1 panel lists all 34 tools on `main` and the 21 not merged. Added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.

## Panels, not modules (0.4.2-20261019)

The bordered, collapsible boxes on the page are **panels** (Astra, 2026-10-19): to a developer a module is a file of code, and a box on a dashboard page is a panel ([PET-PROGRAM-FRAMEWORK.md](https://github.com/findastra/astras-pet-os/blob/main/PET-PROGRAM-FRAMEWORK.md), section 1). The four level panels are now labelled `unity-unicorn-L1-panel` to `unity-unicorn-L4-panel`, and the page's own names follow (`uu-panels`, `uu-panel`). Lights you saved on the old `-module` labels carry over to the new ones; the browser storage key keeps its name so nothing saved is lost. The face tracking modules, masonry mesh modules and Python modules elsewhere in this repo are real modules and keep the word. `test/panels-test-20261019.mjs` fails if a page box is called a module again. Changed with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.

## Matcap palette (0.4.3-20261019)

A new panel, **unity-unicorn-matcap-panel**: every matcap as a round pan in a graphite compact, like a makeup palette. Pick one to see it big on a sphere and on a ring you can turn, try a hue and tint, favourite it, or save it as a PNG for Unity. Its 48 matcaps (Metal, Candy, Holo, Glass, Toon, Neon, Soft, Sparkle) are drawn from math, so they are yours to use anywhere; **Load my matcaps** shows your own files too, in that tab only.

- **matcap-palette** (Unity): Tools › Unity Unicorn › matcap-palette shows every matcap in a project the same way and puts the one you pick on a Poiyomi or lilToon material's matcap slot, with Undo and a usage receipt. Code: `scripts/unity-editor/matcap-palette-20261019.cs`.
- PNGs: `node scripts/matcap-palette/make-matcaps-20261019.mjs <out-folder>` writes the 48 from the page's own recipes.
- Guide: [docs/matcap-palette-20261019.md](docs/matcap-palette-20261019.md). Made with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.

## Menu map (0.4.4-20261019)

A new panel, **unity-unicorn-menu-map-panel**: an avatar's real radial menu, the way VRChat shows it, after VRCFury and Modular Avatar have built it. Click a wedge to open a submenu or switch a toggle; the middle is Back. Beside it, the **parameter budget**: synced bits used out of 256, the total before VRCFury's compressor, and what each add-on costs (each top menu, the compressor's own slots, and parameters the menu does not show). **Load a menu map** keeps your file in that tab only; **Try the sample** shows a made-up avatar.

- **menu-map** (Unity): select the avatar and run Tools › Unity Unicorn › menu-map. It builds a disposable test copy (the SDK build step an upload runs), writes `UnityUnicornReports/menu-map-<avatar>-<time>.json` with every menu page, control, parameter and icon (PNG), deletes the copy and leaves a usage receipt. The scene is never saved. Code: `scripts/unity-editor/menu-map-20261019.cs`.
- Sample: [docs/menu-map-sample-20261019.json](docs/menu-map-sample-20261019.json). Guide: [docs/menu-map-20261019.md](docs/menu-map-20261019.md). Made with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.

## Asset creator, assets library and shaders shop (0.4.5-20261019)

Three Unity Stable tools Astra asked for on 2026-10-18 ("lets make this booth together and then publish those tools"). They were built and first used in Liminal Doors, and moved here unchanged on 2026-10-19. Each folder is laid out like a world project, so installing one is a single copy into the project's folder.

- **unity-asset-creator**: walks one world asset through a researched 12-step checklist (plan, provenance, scale and pivot, geometry, materials, textures, colliders, lighting, effects, prefab, review in the world, export). A failed check blocks its step, and steps 3, 7 and 11 need a person. It ends by exporting the version (package, previews, checksummed manifest) and opening a pull request to the library. Code: `scripts/unity-asset-creator/`. Guide: [docs/unity-asset-creator-20261019.md](docs/unity-asset-creator-20261019.md).
- **unity-assets-library**: the public database of finished assets and their versions, [findastra/unity-assets-library](https://github.com/findastra/unity-assets-library), indexed in `library.json`. Astra merges every version. Guide: [docs/unity-assets-library-20261019.md](docs/unity-assets-library-20261019.md).
- **shaders-shop**: installs shaders for worlds the way Creator Companion would, after showing the files, sources and licences, and records which shaders each world uses. It also stocks our own Unity Stable shaders. Code: `scripts/shaders-shop/`. Guide: [docs/shaders-shop-20261019.md](docs/shaders-shop-20261019.md).
- **The unity-asset-creator trick** ([skills/unity-asset-creator/SKILL.md](skills/unity-asset-creator/SKILL.md), L2): how an agent walks the checklist through the world's agent queue and publishes, and what it asks Astra first.

Limits: tested in one world (Liminal Doors) on two assets, the phone booth and stop 1's sky, both v1.0.0 in the library. The library has no licence yet. Liminal Doors keeps its incubating copy, so change the tools here first. Made with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.
