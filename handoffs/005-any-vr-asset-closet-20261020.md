status: open (waiting on Astra: Dev's yes to put her closet code in Unity Unicorn, then OK to merge)
for: any
from: Claude Opus 5.5 (claude-opus-5-5), 2026-10-20
needs: this card; README.md "VR asset closet (0.5.2-20261019)"; AGENTS.md (the VR asset closet line); hub PET-PROGRAM-FRAMEWORK.md section 3 (pet programs are control panels)
---
## Task

Bring the VR asset closet into Unity Stable once Dev says yes. The closet is her code. Her repo,
XxdevilishxX/Vr-Asset-Closet, is public with no licence, so it waits on her yes.

## Ownership and state

State: READY_FOR_REVIEW (held until Dev's yes)
Implementer: Claude Opus 5.5 (session "Unity Unicorn friend's code + closet", 2026-10-19 to 2026-10-20); writable files: `vr-asset-closet-20261019.html`, `unity-unicorn-20261008.html` (the closet tool panel, the THIRD and PROGRAMS rows, the RENAMED list, the version), `pet.json`, `README.md`, `AGENTS.md`, `test/vr-asset-closet-test-20261019.mjs`, this card; ownership: released
Reviewer: unassigned; next action: once Dev says yes, review this branch, bring it up to date with `main`, and ask Astra to merge

## What I did

- Branch `claude/vr-asset-closet-20261019`, commit `5497e7e`, built on `main` at `1b436d7`.
  - Pushed 2026-10-20 at Astra's request ("close chat and push"); the repo is private.
  - A draft pull request links to it. Nothing is merged.
- **Her code, ported:** `vr-asset-closet-20261019.html` is Dev's closet (her `index.html` of 2026-10-19), restyled for
  Unity Stable.
  - Kept from her closet: the rails and hangers, favorites, outfits, search, rails you add, photos and her twinkling stars.
  - Added: backups (export and import; import also reads her desktop app's `closet.json` and drops its `D:` paths),
    and the 14 `art/avatars/` thumbnails on the Avatars rail the first time.
  - Left out: her desktop-only parts (folder scan, Show in folder) and her theme picker.
- **Its own page, plus a tool panel (Astra's rule of 2026-10-19):** pet programs are control panels, and anything too
  big for a tool panel is its own website (hub #140, merged as `13511f4`).
  - Unity Stable keeps `unity-unicorn-vr-asset-closet-tool-panel` at the end of the Avatar tools side.
  - That panel shows how many items, photos, favorites and outfits the closet has saved, plus **Open the closet**.
  - Both read the browser store `astra-pet-desk:unity-unicorn:closet:1`.
- **Records:**
  - `pet.json`: version 0.5.2; "VR asset closet" in `L1.native`; "Dev's VR Closet" in `L1.third_party`.
  - The page: THIRD and PROGRAMS rows, and `vr-asset-closet` added to RENAMED so its lights behave like every tool panel's.
  - README section and an AGENTS.md line.
- **Tests:** `node --test test/*.mjs` ran 70 tests: 69 pass, 0 fail, 1 skipped (the local-seed test, skipped as always).
  - The new test, `test/vr-asset-closet-test-20261019.mjs`, runs the closet's data rules and the panel's counting.
- **Browser check (2026-10-19):** served both pages locally and checked them.
  - The panel says "not opened yet" on a fresh browser.
  - Open the closet loads the page with the 14 avatars.
  - After hearting one avatar and saving an outfit, the back link lands on the panel, which showed 14 items on 1 of 12 rails, 14 with photos, 1 favorite, 1 outfit.
  - At 375 px wide there is no sideways scrolling.
  - The only console error is the existing 404 for `unity-unicorn-local-tools-20261011.js`, which lives in the hub folder.
- **Elsewhere, same work:**
  - XxdevilishxX/Vr-Asset-Closet PR #1 (open, for Dev to merge herself; never merge it for her): `vr-closet-explained.html`.
    It is the same closet as a teaching copy, with a plain-words comment above every line of code and a blank line after each.
    Astra's rule: anything given to Dev is annotated that way.
  - findastra/findastra #9 (The Hayloft label for unity-assets-library) is merged.
- **Not done:**
  - Dev has not said yes.
  - The hub's System Map and registry do not list the closet yet; add it as an `own-program` of Unity Stable when this merges.

## Done when

- Dev has said yes (in chat with Astra) to her closet code being in Unity Unicorn.
- This branch is up to date with `main`, `node --test test/*.mjs` passes, and the pages are checked again in a browser.
- Astra has OK'd the merge in chat, with Dev credited in the merge commit (`Co-authored-by: Dev <344171313+XxdevilishxX@users.noreply.github.com>`).
- The hub's `system-map.json` has the closet as an `own-program` of Unity Stable, via its own hub PR.

## Return

Record receipt and review findings under "Receipt and review" below, on this branch. Then set `status: done` with the merge commit.

## Receipt and review

Pending.
