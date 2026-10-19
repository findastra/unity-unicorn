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

## Framework modules (0.4.0-20261018)

The top of the page now follows [the Pet Program Framework](https://github.com/findastra/astras-pet-os/blob/main/PET-PROGRAM-FRAMEWORK.md), section 3 (Astra's sketch of 2026-10-18): the cyberhome, the **Unity Stable** title with its version, the quick summary and status line, the four stacked L1 to L4 modules, session notes that save to the backlog, the nine emotions, clicker training, the unified footer and the chat button. Every module collapses on a phone, and every item has its two paired lights (ON/OFF and maintenance).

The L1 module lists all 32 tools on `main` and the 21 that are not merged yet (PR #1's avatar effect tools, including **sparkle-strapper** and **fan-fitter**, PR #3, and the asset ledger), each marked "not merged". **Added tonight** lets you add a tool or program as you make it; it stays in this browser and goes into the backlog.

Limits: the lights, the backlog and added items are saved in this browser only, because the Farm's core switch is not built. The seat, the chat bot, keys, grants and the weekly backlog run are shown as planned. Added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.

## world-spawn-tool (2026-10-18)

- **world-spawn-tool**: spawns a new VRChat world. First a public GitHub repo, then a link with a cute one-line summary in the Findastra profile README (by pull request), then the Unity project skeleton. It started in Liminal Doors (draft findastra/liminal-doors#1) and moved here, its own pet program, on 2026-10-18. Guide: [docs/world-spawn-tool-20261018.md](docs/world-spawn-tool-20261018.md); code and tests in `scripts/world-spawn-tool/`.
- Limit: its skeleton step still copies from a reference world. Astra's rule since 2026-10-18 is that new worlds start fresh from VCC's World template, so change that step before the next world.

`pet.json` now has the framework's `intelligence_levels` block: every tool in L1 (on `main`, and not merged with its pull request), tricks in L2, harness skills in L3, and no item in two modules. Added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.
