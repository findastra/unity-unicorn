status: open
for: codex
from: Claude Opus 5.5 (claude-opus-5-5), 2026-10-19
needs: PR #1 branch codex/avatar-effects-20261012 (UnicornCore.cs, bending-and-star-tools-20261012.cs, sparks-and-fans-20261012.cs)
---
## What I did
Astra asked (2026-10-19, overnight) to see Poppy's fans, sparkles and menu, to get sparkles on hands and feet, every fan
right, a new **Sparkle Bending**, and the star shooter she never got working. I worked in Poppy's own project (copies of
your PR #1 tools are installed there) and did not touch your branch. Three things I found belong in your tools:

1. **UnicornCore category order.** `Sparkles` (`phawksea|spark`) came before `Sparkle Bending`, so "Sparkle Bending (left)"
   was learned as Sparkles, and Star Gun and Star Burst were learned as Sparkle Bending (`star ?gun|star ?burst`). In
   Poppy's copy I put `Sparkle Bending` (`sparkle ?bend`) first and gave CooGee's star kits their own `Stars` category
   (`star ?burst|star ?gun|star ?comet|stardust|star ?footstep`), and moved the two star templates to `Templates/Stars`.
   With that, bending-adder's `IsUnconfirmedSparkle` guard is no longer needed for the stars.
2. **RGB fan speed.** rgb-painter's hue speed 0.15 is `0.15 * _Time.x` in Poiyomi: one rainbow every ~133 s, which still
   reads as one colour. Poppy's two RGB fan materials now use 2.0 (one rainbow every 10 s). Please make 2.0 the default.
3. **Star kits.** In Poppy's scene the CooGee kits had lost their MA Merge Animator and Menu Installer, and their MA Bone
   Proxies were swapped for VRCFury links on the parent and on child particles, so the firing animations never merged.
   The MA remake works when each `*_Gimmick.prefab` is dropped on the avatar root untouched. I re-added all four that way
   and put their menus under Flowtoys › Stars with MA Menu Install Targets (MA runs before VRCFury, so VRCFury adds the
   fans and bendings into the same Flowtoys submenu). star-effects-diagnostic should flag a kit whose MA components are
   missing instead of suggesting rewiring.

## Task
1. Bring fixes 1 and 2 into PR #1 (UnicornCore.cs categories; rgb-painter default speed). Keep learned templates' folders
   in step with their categories.
2. Teach star-effects-diagnostic to check for the MA Merge Animator, Parameters, Bone Proxy and Menu Installer on each kit.
3. Resolve PR #1's conflicts with `main` as card 002 says, then mark it ready. Astra names each PR she wants merged.

## Done when
A fresh learn on Poppy files Sparkle Bending (left/right) under Sparkle Bending and the star kits under Stars; rgb-painter
writes speed 2.0; the diagnostic reports a stripped kit; PR #1 merges cleanly with tests passing.

## Return
Push your branch, link it here, set `status: done`, and record your model and version.
