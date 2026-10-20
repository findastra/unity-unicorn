# Avatar tools and world tools

Astra asked (2026-10-19): "I want a panel for world sparkle tools too. collect what sparkles we have already used in the
other two worlds we build. every single tool that we have built needs to go into a panel on unity unicorn. separate the page
by avatar tools vs world tools."

## The page

Below the L1 to L4 panels there are two tabs, **Avatar tools** and **World tools**, and only one side shows at a time. Every
panel on a side is closed until you click it, and the page remembers which tab and panels you had open
(`astra-pet-desk:unity-unicorn:sides:1`). A link can open one directly: `#avatar-tools`, `#world-tools`, or a panel's id
such as `#uu-vine` or `#uu-p-sparkles`. The L1 panel now says how many tools there are and has a button for each side.

Every tool is in exactly one panel. A tool that has its own working panel (matcap-palette, menu-map and
vine-spawn-with-trumpet-flowers) is that panel and has no row.

| Side | Panel | What is in it |
|---|---|---|
| Avatar | `unity-unicorn-avatar-build-panel` | face-transfer, neck-blender |
| Avatar | `unity-unicorn-matcap-panel` | matcap-palette (moved here, unchanged) |
| Avatar | `unity-unicorn-menu-map-panel` | menu-map (moved here, unchanged) |
| Avatar | `unity-unicorn-avatar-sparkles-panel` | sparkle-strapper, fan-fitter, sparkle-bender-strapper, star-effects-diagnostic, rgb-painter (PR #1, not merged) |
| Avatar | `unity-unicorn-avatar-effects-panel` | the other 11 effect and add-on tools of PR #1 |
| Avatar | `unity-unicorn-avatar-records-panel` | copyright-logger, copyright-catcher (PR #1) |
| World | `unity-unicorn-vine-spawn-panel` | vine-spawn-with-trumpet-flowers ([guide](vine-spawn-with-trumpet-flowers-20261019.md)) |
| World | `unity-unicorn-world-sparkles-panel` | 27 sparkles from the three worlds (below) |
| World | `unity-unicorn-every-world-panel` | tools for any world: world-spawn-tool, world-validator, desktop-world-test, the bakes, props, unity-asset-creator, unity-assets-library, shaders-shop, the two runners, asset-ledger |
| World | `unity-unicorn-piranesi-house-panel` | the house builder, masonry, open arches, tides, wall textures, vine-bloomer, marble-carver, and Review Shots from its repo |
| World | `unity-unicorn-infinite-pole-panel` | the seven cloud tools, and from its repo: Take World Photos and round 38's pickup audit and lift-out |
| World | `unity-unicorn-liminal-doors-panel` | the 22 tools still in Liminal Doors' repo (its ledger), and glass-animal-tool and bubble-chair-blower (PR #3) |

Counts on 2026-10-19: 22 avatar tools; 63 world tools (34 on `main` here, 3 not merged here, 26 still in the worlds' own
repos); 27 sparkles.

**Where the lists live:**

- In `pet.json` `intelligence_levels.L1`:
  - `sides` names every tool on `main` and every tool not merged, each on one side.
  - `tools_in_worlds` lists by world the tools that are still only in a world's repo.
- On the page, the main script's `TOOLS_MAIN`, `UNMERGED`, `IN_WORLDS` and `SPARKLES` carry the same lists, with the panel for
  each tool.
- `test/avatar-and-world-tools-test-20261019.mjs` keeps them equal and checks that no tool shows up twice.

**What the Liminal Doors panel lists:** its tools ledger (`docs/tools-by-floor-20261019.json` on branch
`claude/fresh-start-20261018`), less what moved here and what's in the sparkle panel.

- Moved here: asset-creator, shaders-shop (with the Unity Stable shaders) and world-spawn-tool.
- In the sparkle panel: sparkle-sky, glitter-paint and portal-shader.
- Left out: Codex's image generation, which is OpenAI's tool, not ours.

The ids are the ledger's, so the same id means the same tool across worlds (The Threshold's tracker relies on that).

## World sparkle tools

Every sparkle, glitter and mote used in the three worlds so far, with a link to its source. The same list is in
[world-sparkle-tools-20261019.json](world-sparkle-tools-20261019.json) (`unity-unicorn.world-sparkles/v1`) for other pages.

- **Liminal Doors** (branch `claude/fresh-start-20261018`, PR #2 not merged), 11 sparkles:
  - the Evening Sky sparkle layer (sparkles, big four-point glints and a nebula);
  - sky sparkles and field sparkles;
  - door motes and glitter halos (`LiminalDoorKit.Motes`, `GlitterHalo`);
  - Glitter Paint;
  - portal glitter (`_Glitter`);
  - pool bubbles and hall motes;
  - the swirling booth sparkles;
  - the Unity Stable/Sparkle shader they use.

  The frond grass shader has no glitter of its own: the glitter you see over the grass is the field sparkles.
- **Astra's Infinite Pole** (`main`), 13 sparkles:
  - the 13-effect glitter collection (Upgrade Glitter) and its in-world controls;
  - the Glitter Gradient and Soft Sparkle shaders;
  - stair star dust, round bokeh sparkles, cloud sparkle rain, Sparkle Clouds and lounge cloud sparkles;
  - the sparkle slurry round the orbs and eggs;
  - Celestial Sky stars;
  - the Glitter Cloud and Prismatic Stair shaders.
- **Piranesi House** (`master`), 3 sparkles:
  - mica sparkle in the stone;
  - the sparkle and mote particles;
  - the Colossus's glittering hand.

  Its Stormscape also uses copies of the Pole's Glitter Cloud and Prismatic Stair, which are listed once, under the Pole.

## Adding a tool

1. Put it in `pet.json` (`L1.tools` when it's on `main` here) and in `L1.sides`.
2. Put it in the page's `TOOLS_MAIN` with its panel: `avatar-build`, `avatar-sparkles`, `avatar-effects`, `avatar-records`,
   `every-world`, `piranesi-house`, `astras-infinite-pole`, `liminal-doors`, or `own-avatar`/`own-world` for a tool with its
   own panel.
3. Add its catalog file in `tools/`.
4. For a world's own tool, add it to `IN_WORLDS` and `L1.tools_in_worlds`. For a sparkle, add it to `SPARKLES` and the
   sparkle JSON.
5. Run the tests.

Made with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.
