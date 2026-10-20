# Assistant rules for unity-unicorn

Public credit is always Astra.

Browser interface prepared with Codex (GPT-6), 2026-10-08. Record the model and version when another assistant changes this repository.
Square pet frame (`v0.1.1-20261009`) fixed with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-09.
Skill book `docs/skills-20261009.md` started with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-09. After avatar or world sessions, add learned skills to its categories and append to its session log, without account details, blueprint IDs or machine paths.
Avatar tracker and plans (`docs/avatar-tracker-20261009.md`, `docs/avatar-plans-20261009.md`) started with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-09. Rules: every avatar asset folder keeps an `Edit Log - <avatar>-<date>.txt`, so create one from the template when it's missing. Refresh both files on each routine scan. When an avatar's licence may not allow uploads to more than one account, upload to findノastra only. Poppy's thumbnail is always `art/avatars/poppy-20261009.jpg` (the original is kept beside Poppy's assets); use it for every Poppy upload and tracker row. Fae's thumbnail is always `art/avatars/fae-20261009.jpg` (the original is kept beside Fae's assets).

Tool library and private report imports prepared with Codex (GPT-6), 2026-10-11.
marble-carver and vine-bloomer tools, scripts and guides added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-11.
neck-blender tool, script and guide added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-11.
face-transfer tool, script and guide added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-11.
Realistic-props guide, prop-decomposer and realistic-prop-restyle tools, and the asset ledger's Downloads section added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.
Framework modules on the page (L1 to L4, session notes, emotions, clicker training, footer, chat) added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.
world-spawn-tool moved in from findastra/liminal-doors, and pet.json `intelligence_levels` added, with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.
Module labels on each box's top border, the cyberhome's own label, the sketch's summary and mood names (0.4.1) with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.
Panels, not modules (0.4.2): the page boxes are panels (`unity-unicorn-L1-panel` to `-L4-panel`, `uu-panels`, `uu-panel`), saved lights carried over, with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19 (Astra's decision of the same day). A module is a file of code; never call a page box a module again.
Matcap palette panel (unity-unicorn-matcap-panel), the matcap-palette Unity window and the PNG maker added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19. The 48 palette matcaps are drawn from math in the page's `uu-matcap-recipes` script; never commit matcaps that came with avatars or shaders.
Menu map panel (unity-unicorn-menu-map-panel) and the menu-map Unity export added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19. The sample menu map uses made-up names; never commit a menu map, icon or parameter list exported from Astra's avatars (they hold paid assets' names and icons).
vcc-package-manager and vrchat-avatar-upload moved from L1 tools to L2 tricks to write (0.4.5; astras-pet-os PR #114, Astra's decision of 2026-10-19); their catalog files stay, marked `Trick to write`, with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.
unity-asset-creator, unity-assets-library and shaders-shop moved in from findastra/liminal-doors unchanged (`scripts/unity-asset-creator/`, `scripts/shaders-shop/`, their catalog files and guides), and the unity-asset-creator trick (`skills/unity-asset-creator/`), with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19 (0.4.6). The trick is listed as `unity-asset-creator-trick` in `pet.json` and on the page, because no item may be both a tool and a trick. Change these tools here first, then in the worlds that use them. The trick's step table and `AssetChecklist.cs` must list the same steps in the same order (`test/asset-tools-test-20261019.mjs`).

## Scope

Working offline draft: purpose-specific records, editing, status changes, search, local browser storage, JSON and Markdown exports. External integrations are planned.

The tool library is a local catalog and report viewer, not a Unity execution bridge. Keep private tool paths, project names and run history in imported catalogs/browser storage or ignored local sidecars outside Git. A last-used date requires execution evidence; never substitute a file modification time, catalog import time or page-load time. Preserve `astra-pet-desk:unity-unicorn:1` when changing the separate tool-library storage.

## Rules

- Keep the browser interface self-contained and dependency-free. Its dated entry is `unity-unicorn-20261008.html`.
- Treat user text as untrusted. Render it with textContent, validate links, and never insert record contents as HTML.
- Keep private records, health or finance entries, API secrets, credentials, environment files, logs and machine metadata outside Git. Browser exports are private user data.
- Label planned connections truthfully; do not imply an account, device, editor or service is connected without verification.
- Preserve the approved nine mood frames and the shared Cage character. Metadata is in `pet.json`; the frames live in findastra/astras-pet-os and their addresses are in `sprite-20261008.json`.
- New descriptive files use lowercase names with the owner's America/Denver date immediately before the extension. Required contract files keep their standard names.
- Keep release tags immutable. Publish fixes as a new commit and tag; never force-push or retag an existing release.
- Update README limits and `docs/publications-20261008.md` when implementation or publication status changes.

## Validation

Check the actual browser script syntax and meaningful user flows after behavior changes. Keep data local, and do not launch scanners against the owner's files merely to test an interface.

Cage: https://github.com/findastra/astras-pet-os. Repository topic: `pet-app`.

Tool-library interface sync: OpenAI Codex (GPT-6), 2026-10-11. Local machine seed and user exports must remain outside Git.

Pictures moved to the hub (one art source, Flash Audit A58, Astra 2026-10-18): every picture of Unity Unicorn lives only in [findastra/astras-pet-os](https://github.com/findastra/astras-pet-os) (`art/frames/unity-unicorn/`); this repo links to them and keeps no copy, so change a picture there, never here. By Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.
Avatar tools and world tools as two tabs of panels, the world sparkle tools panel (`docs/world-sparkle-tools-20261019.json`), the Piranesi House, Infinite Pole and Liminal Doors tool panels, `pet.json` `L1.sides` and `L1.tools_in_worlds`, and vine-spawn-with-trumpet-flowers (page panel, Unity window and its job inbox) added with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19 (0.5.0). Every tool is in exactly one panel (`test/avatar-and-world-tools-test-20261019.mjs`); the vine panel's palette, limits and job file must match the Unity tool's.
