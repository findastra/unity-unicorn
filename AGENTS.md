# Assistant rules for unity-unicorn

Public credit is always Astra.

## Scope

Working offline draft: purpose-specific records, editing, status changes, search, local browser storage, JSON and Markdown exports. External integrations are planned.

The tool library is a local catalog and report viewer, not a Unity execution bridge. Keep private tool paths, project names and run history in imported catalogs/browser storage or ignored local sidecars outside Git. A last-used date requires execution evidence; never substitute a file modification time, catalog import time or page-load time. Preserve `astra-pet-desk:unity-unicorn:1` when changing the separate tool-library storage.

## Rules

- Keep the browser interface self-contained and dependency-free. Its dated entry is `unity-unicorn-20261008.html`.
- Treat user text as untrusted. Render it with textContent, validate links, and never insert record contents as HTML.
- Keep private records, health or finance entries, API secrets, credentials, environment files, logs and machine metadata outside Git. Browser exports are private user data.
- Label planned connections truthfully; do not imply an account, device, editor or service is connected without verification.
- Preserve the approved nine mood frames and the shared hub character. Metadata is in `pet.json`; the frames live in findastra/astras-pet-os and their addresses are in `sprite-20261008.json`.
- New descriptive files use lowercase names with the owner's America/Denver date immediately before the extension. Required contract files keep their standard names.
- Keep release tags immutable. Publish fixes as a new commit and tag; never force-push or retag an existing release.
- Update README limits and `docs/publications-20261008.md` when implementation or publication status changes.

## Validation

Check the actual browser script syntax and meaningful user flows after behavior changes. Keep data local, and do not launch scanners against the owner's files merely to test an interface.

Hub: https://github.com/findastra/astras-pet-os. Repository topic: `pet-app`.

## Pictures and avatars

- One art source (Flash Audit A58, Astra 2026-10-18): every picture of Unity Unicorn lives only in [findastra/astras-pet-os](https://github.com/findastra/astras-pet-os) (`art/frames/unity-unicorn/`); this repo links to them and keeps no copy, so change a picture there, never here.
- Every avatar asset folder keeps an `Edit Log - <avatar>-<date>.txt`, so create one from the template when it's missing. Refresh the avatar tracker and plans (`docs/avatar-tracker-20261009.md`, `docs/avatar-plans-20261009.md`) on each routine scan.
- When an avatar's licence may not allow uploads to more than one account, upload to findノastra only.
- Poppy's thumbnail is always `art/avatars/poppy-20261009.jpg` (the original is kept beside Poppy's assets); use it for every Poppy upload and tracker row. Fae's thumbnail is always `art/avatars/fae-20261009.jpg` (the original is kept beside Fae's assets).
- After avatar or world sessions, add learned skills to the categories of the skill book `docs/skills-20261009.md` and append to its session log, without account details, blueprint IDs or machine paths.

## Tools, tricks and tool panels

- Tool panels (Astra, 2026-10-19: "I do want all of it to say tool panel instead of just panel"; astras-pet-os#129): give a new tool panel a `-tool-panel` label from the start. Lights saved on the old `-panel` and `-module` labels carry over (the page's `RENAMED` list). Code names stay (`uu-panels`, `uu-panel`, the chat panel), and so do the Unity scripts' own words.
- A module is a file of code; never call a page box a module again (Astra's decision of 2026-10-19).
- Every tool is in exactly one tool panel (`test/avatar-and-world-tools-test-20261019.mjs`); the vine tool panel's palette, limits and job file must match the Unity tool's.
- vine-spawn-with-trumpet-flowers: a new job setting stays optional: a job without it gets the default, and the schema stays v1. A new default must not change vines that already look right in a world. Liminal Doors calls `VineBuilder20261019.Grow` by name through reflection: keep exactly one `Grow` and the `VineSettings20261019` field names and types. Vine tests set `VineInbox20261019.OnlyProjectInbox`, so they never take a real job from Downloads.
- The 48 palette matcaps are drawn from math in the page's `uu-matcap-recipes` script; never commit matcaps that came with avatars or shaders.
- The sample menu map uses made-up names; never commit a menu map, icon or parameter list exported from Astra's avatars (they hold paid assets' names and icons).
- When you make or export an asset, add its row to `docs/assets-we-created-20261019.json`, with every world and avatar it is used in.
- A new tool gets a made-by row in `docs/made-by-20261019.json`: who made it, when, and where it was first made.
- Change unity-asset-creator, unity-assets-library and shaders-shop (`scripts/unity-asset-creator/`, `scripts/shaders-shop/`, their catalog files and guides) here first, then in the worlds that use them.
- No item may be both a tool and a trick, so the unity-asset-creator trick (`skills/unity-asset-creator/`) is listed as `unity-asset-creator-trick` in `pet.json` and on the page. The trick's step table and `AssetChecklist.cs` must list the same steps in the same order (`test/asset-tools-test-20261019.mjs`).
- vcc-package-manager and vrchat-avatar-upload are L2 tricks to write, not L1 tools (astras-pet-os PR #114, Astra's decision of 2026-10-19); their catalog files stay, marked `Trick to write`.
- The tool library's local machine seed and user exports must remain outside Git.

Built with Codex (GPT-6), 2026-10-08. Record each change in its own new file in `changes/` (`changes/<topic>-YYYYMMDD.md`, the hub's rule of 2026-10-21), never as a new line here. The lines that stood here before are in [changes/history-20261008-20261019.md](changes/history-20261008-20261019.md).
