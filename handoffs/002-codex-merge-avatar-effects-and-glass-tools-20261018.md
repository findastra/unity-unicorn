status: open
for: codex
from: Claude Opus 5.5 (claude-opus-5-5), 2026-10-18
needs: pet.json (intelligence_levels), unity-unicorn-20261008.html (uu-modules section), test/tool-library-test-20261011.mjs
---
## What I did
Astra asked (2026-10-18) to "merge every single tool" into its own pet program, and then to "push it all". PR #8 (the
framework modules on the page) and PR #9 (world-spawn-tool moved in from liminal-doors; pet.json `intelligence_levels`)
are merged. Your two drafts are not: both conflict with `main`, and Claude does not push to `codex/` branches.

- **PR #1** `codex/avatar-effects-20261012`: conflicts in AGENTS.md, docs/avatar-effects-program-20261012.md,
  docs/tool-library-20261011.md and test/tool-library-test-20261011.mjs. The `.cs` and `tools/*.json` files merge
  cleanly. Poppy's project already has the menu installed. SparksAndFans and bending-and-star-tools match your branch;
  Strappers, UnicornCore and Copyright differ from it.
- **PR #3** `codex/liminal-glass-tools-20261015`: conflicts with `main`. You asked to keep it in draft until
  bubble-chair-blower molds around the seated avatar.

## Task
1. Bring `main` into both branches and resolve the conflicts. `main`'s built-in tool count in the test is now 33.
2. Each tool you merge goes in its own module, and in no other. In `pet.json` `intelligence_levels`, move it from
   `L1.tools_not_merged` to `L1.tools`. On the page, move it from the `UNMERGED` list to `TOOLS_MAIN` in the uu-modules
   script. Dubstep Gunsmith is a program (`L1.native`), not a tool.
3. The 10 menu-only tools in PR #1 need catalog files, so the page and the Goldfish can see them.
4. Mark PR #1 ready when it is. Keep PR #3 in draft until the chair works. Astra names each PR she wants merged.

## Done when
Both branches merge cleanly into `main`, tests pass, and no tool shows up in two modules.

## Return
Push your branches, link them here, set `status: done`, and record your model and version.
