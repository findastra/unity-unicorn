status: open
for: any assistant (Claude or Codex) on Astra's PC, and the hub's chat for the registry
from: Claude Opus 5.5 (claude-opus-5-5), 2026-10-26
needs: findastra/unity-unicorn PR #32 (`claude/cung-list-udon-kits-20261026`), not merged
---
## What I did

- The Cung List, item 1, in findastra/unity-unicorn, PR #32, version 0.5.4-20261026:
  - `udon-kit-mover` (`scripts/udon-kits/`): eight Udon kits, each a pure C# core plus an UdonSharp wrapper;
    `udon-kits-manifest.json`; `Tests/` (29 core and parity tests, `dotnet run`); `Tools/install-udon-kits-20261026.ps1`.
  - `test/udon-kits-test-20261026.mjs` (manifest and UdonSharp limits gate) and `.github/workflows/udon-kits-20261026.yml`.
  - The trick `skills/compose-a-world-from-kits/SKILL.md`, the guide `docs/udon-kits-20261026.md`, and
    `changes/udon-kits-20261026.md`.
- Tests here:
  - the 29 kit tests pass;
  - the Node gate's 5 pass;
  - all Node tests: 69 pass, 1 is skipped and 11 fail (the same 11 as on `main`);
  - Python: 50 pass.
- Nothing on Astra's PC was touched.

## Task

1. **Astra merges PR #32** when she OKs it by number.
2. **On Astra's PC**, in one world (Liminal Doors suggested):
   - run the installer with `-RunTests`, then with `-Apply`;
   - open Unity and confirm UdonSharp compiles the 11 wrappers with 0 errors;
   - in ClientSim, try one kit of each family.
   - If a wrapper fails to compile against the real SDK, fix it in unity-unicorn with its core and parity test, not in the
     world.
3. **Hub registry:** add `udon-kit-mover` (tool) and `compose-a-world-from-kits` (trick) as nodes of Unity Stable in
   astras-pet-os `system-map.json`, so the registry table and the page frame's counts list them.

## Done when

The kits compile in a world, at least one kit per family was playtested in ClientSim (results written in the world's
plan), and the hub lists the tool and the trick.

## Return

Set `status: done`, link what you merged or ran, and record your model and version in `changes/`.
