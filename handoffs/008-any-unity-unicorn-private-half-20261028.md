status: open
for: any assistant (Claude or Codex), and Astra for the merges
from: Claude Opus 5.5 (claude-opus-5-5), 2026-10-28
needs: findastra/unity-unicorn-private PR #2 (README); this repo's #33 and #32 merged before the name changes
---
## What I did

Astra, 2026-10-28: she doesn't want `unity-assets-library` as a repo of its own. It is Unity Unicorn's, because it
loads items into her worlds and avatars, and it should be its private part. Her picture of it: one library like
Poppy's Closet, two kinds (clothes and world assets), each card saying made by you or someone else, tagged private or
public, shown in two sections.

She answered the deck "Unity Library Move" with "all recommended":

1. **Unity Unicorn gets a private half.** This repo stays public, and GitHub can't make one folder private, so the
   library repo became the private half. Public items go in this repo (packages on its Releases, so the code stays light).
2. **Its name is `unity-unicorn-private`.** Renamed on GitHub 2026-10-28; the old address forwards (checked with
   `gh api` and `git ls-remote`). Description, with her OK: "Unity Unicorn’s private half: Astra’s private clothes and
   world assets."
3. **Someone else's items are cards only:** picture, who made it, where it came from, where the file is on D:. Their
   packages are never uploaded.

Done: the rename, the description, and `unity-unicorn-private` PR #2 (README says all of the above; waits on her merge).

## What is left

1. **The new name here and in the hub,** after #33 and #32 merge (they change the same files). Files that say
   `unity-assets-library`: `pet.json`, `README.md`, `AGENTS.md`, `unity-unicorn-20261008.html`,
   `tools/unity-assets-library-20261019.json`, `docs/` (asset-ledger, assets-we-created, avatar-and-world-tools,
   made-by, tool-categories, unity-asset-creator, unity-assets-library), `scripts/unity-asset-creator/` (3 .cs files,
   the publisher and its checklist), `skills/unity-asset-creator/SKILL.md`, `test/asset-tools-test-20261019.mjs`. Several
   still call the library public; it has been private since 2026-10-20. In the hub: `system-map.json`,
   `TOOLS-AND-TRICKS.md`, `docs/unity-unicorn-page-modules-20261018.md` and the generated pages. The hub's `pets.json`
   also lists this repo as private, but GitHub says public and Astra kept it public: flip it to public there.
2. **Later, the library page:** its own page with a tool panel on Unity Stable (control-panel rule); private and
   public sections, Clothes / World and Made by you / Someone else's filters, and Make public on her own items (a pull
   request that moves the item into this repo). The page code can be public; it reads the private list from her PC's
   copy of `unity-unicorn-private`.
3. **Liminal Doors on D:** still names the old repo in `Assets/UnityAssetCreator` and
   `Tools/unity-asset-creator/publish-asset-20261018.py`. Nothing breaks (the old address forwards); change it there
   only with Astra's OK, since D: is hands off.
