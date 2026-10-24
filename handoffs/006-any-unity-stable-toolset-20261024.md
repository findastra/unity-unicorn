status: open
for: any assistant (Claude or Codex), and Astra for the merge and the open questions
from: Claude Opus 5.5 (claude-opus-5-5), 2026-10-24
needs: findastra/unity-unicorn PR #31 (`claude/tool-categories-20261024`), not merged
---
## What I did

Astra's day on Unity Stable (2026-10-24), all on PR #31, version 0.5.3-20261024:

- **Tool names and categories.** Name = what it works on + category word (`vine-spawn`, `crystal-reflection-painter`).
  Ten categories in rainbow order, red to pink: spawner and painter are Astra's; carver, strapper, builder, mover,
  checker, viewer, keeper and runner are proposed. `docs/tool-categories-20261024.json` holds every tool's category,
  extra jobs (`also`), related group, and the old-to-new `renames` (70 renamed, 15 unchanged; the table is in the
  README). Nothing is renamed in code or in the worlds yet.
- **The page is the hub's pet program frame** (the generated `apps/pet-programs/unity-unicorn-20261018.html` from
  astras-pet-os, last night's Wonderland Launchpad layout), in Unity Stable's own grays. The hub's stylesheet and
  scripts are copied beside the page; copy them again when the hub changes them.
- **Each tool once,** in `unity-unicorn-world-building-toolset` inside the L1 control panel: colour chips, an
  All / Avatar / World filter, and a card per tool with who made it, its run record from the tool library, what it was
  made for, where it lives and Send to world / avatar (a job file to Downloads; only vine-spawn reads jobs today).
- L1 tools and toolsets are rounded tool boxes; standard panels are dotted; `unity-unicorn-tool-maker` (was Added
  tonight) sits under the toolset.

## Still open

1. **Astra's answers.** First deck (https://claude.ai/artifact/9nStRu4oQemTnfXuSZe4Vp): the eight proposed categories,
   marble-carver, world-spawn-tool, renaming (she later said "I want to rename the tools", so rename is a yes; the
   others are open). Second deck (https://claude.ai/artifact/Dt6bdzfRDR2VGtN3RmMevk): order inside a colour is answered
   ("all recommended": related side by side, A to Z); spawner as a kind, L1, and an avatar-building toolset are open.
2. **Merge PR #31** only with Astra's OK for it by number.
3. **The renames themselves** (files, Unity menus, world repos) wait on her go-ahead after the merge.
4. **Clean-up:** the old page script still runs for the standard panels the hub frame replaced (its look-ups find
   nothing); lights saved on the old page show as unset under the hub frame's storage.
5. **Tests:** 51 pass; 11 fail, the same 11 that fail on main (they check the old page's structure:
   panels-test, assets panel, avatar-and-world-tools, matcap and menu-map sections, levels-match line endings).
6. **Next:** the worlds and avatars project panels Astra described.
