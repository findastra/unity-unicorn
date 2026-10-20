# vine-spawn-with-trumpet-flowers

Astra asked (2026-10-19): "make another vine-spawn-with-trumpt-flowers and add the option to change the flower colors, like
give up to 10 options in color squares where you can just pick the form the color pallat, then you click the tool, and it
opens up in unity, maybe you have to select the world to send the tool too, there are three so far - Piranesi House, Astra's
Infinite Pole, and Liminal Doors."

It is built on [vine-bloomer](vine-bloomer-20261011.md). vine-bloomer drapes a Blender ivy mantle over whole buildings in
Piranesi House. This tool grows single vines in Unity over the objects you pick, with leaves, buds and trumpet flowers.

## On the page: unity-unicorn-vine-spawn-panel

It's on the **World tools** side of Unity Unicorn's page.

- **Flower colours**: up to 10 colour squares. Click a square in the 30-colour palette to add it; click a chosen square to take
  it out. When 10 are chosen, the rest of the palette greys out.
- **Send to**: Piranesi House, Astra's Infinite Pole or Liminal Doors.
- **Open in Unity**: saves the job file (below) to Downloads.
- **More** shows the settings: vines per object, vine length, climb up, hang down or both, flowers and leaves per metre, flower
  size, leaf colour, flower glow, leaf glow and the seed. They stay hidden until you click it. Flower glow starts at 0.8 and
  Leaf glow at 0.4 (see [after the first real use](#after-the-first-real-use-2026-10-19)).
- **How it reaches Unity** explains the route in four short paragraphs.
- The picture redraws as you choose. Your colours, world and settings are kept in this browser
  (`astra-pet-desk:unity-unicorn:vines:1`).

## How the button reaches Unity, and what it can't do

A web page can't start Unity, open a project, bring Unity to the front or write into a project folder. Browsers block all of
that on purpose. What the page can do is save a file, so the route is:

1. **Open in Unity** saves `unity-unicorn-vine-spawn-<world>-<yyyymmdd-hhmmss>.json` (UTC) to Downloads. The file holds the
   colours, the world and the settings (schema `unity-unicorn.vine-spawn-job/v1`).
2. While a world is open in Unity with the tool installed, the tool checks three places every 2 seconds: Windows' real
   Downloads folder, `UnityUnicornInbox/` in the project, and one more folder you can pick.
3. It takes the newest job for **its own world** that is less than 2 hours old, and moves it to
   `UnityUnicornReports/jobs/`. Then it opens its window with the page's colours and settings, and flashes Unity on the
   taskbar.
4. Nothing grows yet. You select the walls, arches or railings, then press **Grow vines**.

The tool knows which world it is in from the project folder and product name (liminal, piranesi, infinite or pole). You can
also set it in the window. Jobs for another world stay in Downloads for that world's editor, and so does any file that isn't
a valid job. A job has 1 to 10 `#rrggbb` colours, one of the three worlds, and is under 64 KB. A job never grows anything
by itself. `leaf_glow` (0 to 1) is optional, and the schema is still v1: a job saved before Leaf glow existed gets 0.4.

**Why not the agent queues?** They differ from world to world. Liminal Doors' queue runs `LiminalDoors/` and `Unity Unicorn/`
menus, Astra's Infinite Pole's runs only `Astra/` menus, and Piranesi House has trigger files (`Review/do_build`,
`Review/do_shots`) instead of a queue. The tool's own inbox works the same way in all three. Agents can still use it: drop a
job into the project's `UnityUnicornInbox/`.

**Not used: a `unity-unicorn://` link.** A link like that could also start Unity when it's closed, but it needs a protocol
handler in the registry. Registry writes made from Claude or Codex land in the app's private copy, so Astra would have to
install it herself. It's a possible next step, not part of this tool.

## Install, once per world

Copy `scripts/unity-editor/vine-spawn-with-trumpet-flowers-20261019.cs` into the world project's `Assets/UnityUnicorn/Editor/`
(or any Editor folder). That one file is the whole tool. The first time you grow vines, it writes its shader to
`Assets/UnityUnicorn/Shaders/Unity Stable Vine Bloom.shader`.

Not installed in any world yet. Liminal Doors was being built in its open editor while this was made, and the other two
were not touched.

## In Unity: Tools › Unity Unicorn › vine-spawn-with-trumpet-flowers

- The same 30-colour palette as squares, and the 10 chosen colours. Click to add or take out, or add any colour with the
  colour field.
- **This project is**: the world, found automatically or chosen by you.
- A new window opens in the middle of Unity's main window, 600 by 720. The squares and notes wrap to the window's width, down
  to its smallest (340 wide).
- **Grow vines** grows on the selected objects in one Undo step:
  - Vines start low on the sides to climb, or high on walls and edges to hang, and only where the spot is open to the world.
    Each one feels its way along the surface with hidden temporary colliders on a world-space copy of each mesh, so the
    objects need no colliders of their own.
  - Vines turn up walls at inside corners and wrap round outside edges. Where there's nothing to hold, they hang for a little,
    then stop. LOD 1 and lower copies are skipped.
  - Each vine is a woody stem that turns green toward the tip. It has side shoots, oval leaves with a midrib in alternating
    pairs, and trumpets along its outer three quarters. The trumpets are a narrow tube that flares into five rounded lobes,
    on a short stalk with a green calyx, with a darker throat. Each one takes one of your colours at random.
  - About 28% of the flowers are buds. Each tip ends in a cluster of 2 to 4 flowers and buds.
  - There's one mesh per selected object, saved to `Assets/UnityUnicorn/Generated/Vines/`, with light probes, up to 150,000
    triangles per Grow.
  - Shader **Unity Stable/Vine Bloom**: vertex colours with Standard lighting, both sides lit, and shadows. Petals glow by
    **Flower glow**, stems and leaves by **Leaf glow**, and leaf and flower tips sway a little. There's one material per pair
    of glow values, so a new glow doesn't change vines you grew before.
- Each Grow writes a usage receipt to `UnityUnicornReports/vine-spawn-with-trumpet-flowers-<time>.json`
  (`unity-unicorn.tool-usage/v1`) for the tool library.
- **Look for jobs now** takes a job of any age. **Also watch a folder…** is for a browser that saves downloads somewhere else.

## After the first real use (2026-10-19)

The first real use, in Astra's Infinite Pole (findastra/astras-infinite-pole PR #5), found three problems. All three are
fixed in the same file:

- **Leaves and stems read nearly black** in a world lit mostly by glowing materials, whatever their colour. The shader lit
  them only with scene light. Now they have **Leaf glow**, a light of their own: with no scene light at all they keep that
  share of their colour. It runs from 0 to 1 (0 is the old look) and starts at 0.4. It's on the page, in the job
  (`leaf_glow`) and in the window, with the same limits everywhere.
- **Flower glow now starts at 0.8**, not 0.2, which read dark violet in the Pole. If this browser saved the old 0.2 before Leaf
  glow existed, the page moves it to 0.8. A glow you chose yourself stays.
- **The window opened too narrow**, at its 420-wide minimum and off to one side, so the palette and the job note were cut off
  on the right. It now opens in the middle of Unity, 600 by 720, and its squares and notes wrap to its width.
- **On a phone kiosk, vines climbed the inside glass.** A kiosk's lowest point is its floor, inside, so climbing vines started
  there. Now a vine starts only where the spot is open to the world:
  - Of 13 directions out of the face, at least 4 must meet none of the selected objects within 3 m.
  - Vines prefer the most open spots.
  - A vine that would turn a corner into a closed-in space stops there.
  - If an object has no open spot left, a vine may still start inside, and the Grow's report says so.
- **A world with the first version:** copy the new file over the old one. The next Grow replaces the shader. Vines grown
  before that get Leaf glow 0.4 too (their materials never had one) and keep their own Flower glow. Vines that already grew
  inside a kiosk stay there: take them away with Undo or delete them, then grow again.

| Test kiosk (10 vines, seed 5) | Before: vertices inside | After: vertices inside |
| --- | --- | --- |
| Closed | 12,000 of 28,300 | 0 of 33,105 |
| Open door | 9,515 of 30,928 | 45 of 24,181: leaves leaning into the doorway, none more than 15 cm in |

Pictures: `docs/screenshots-20261019/unity-vines-kiosks.png`, `unity-vines-no-light-leaf-glow-0.4.png` (and `-0.png` for the
old look), `unity-vine-window-open.png` and `unity-vine-window-smallest.png`.

## Checked

- `node --test test/avatar-and-world-tools-test-20261019.mjs` checks these things:
  - the page's palette, worlds and defaults equal the Unity tool's, and so do the shader's own defaults;
  - every setting has the same limits on the page, in the job reader and on the window's sliders;
  - colour squares stop at 10;
  - the job file and its name are what the tool reads, with the same limits;
  - a job without `leaf_glow` gets 0.4, and 0 stays 0;
  - an old saved Flower glow of 0.2 moves to 0.8, and a chosen one stays;
  - bad worlds, colours and counts are refused;
  - the panel's scripts write text only.
- In a throwaway Unity 2022.3.22f1 project (batch mode, not a world), `test/unity/vine-spawn-smoke-test-20261019.cs`, 56
  checks, all pass:
  - a wall, an arch and a column with no colliders each got vines. That's 29,206 to 57,342 triangles each, every vertex
    within 0.35 m of its object, finite normals, and petals in all five picked colours;
  - the shader compiled with no errors, and a first-version shader in the project was replaced;
  - a material made with the first shader then had Leaf glow 0.4 and kept its Flower glow 1.5;
  - with no scene light, leaves were green with Leaf glow 0.4 (94,825 green pixels) and black with 0 (none);
  - jobs without `leaf_glow` got 0.4; 0, 0.65 and 7 (held to 1) carried over;
  - nothing grew inside the closed kiosk, and nothing more than 15 cm inside the open one;
  - hanging vines grew;
  - one Undo took the last Grow away and kept the first;
  - the inbox took the job for its world and left another world's job and an 11-colour job alone. In tests it looks only in
    the project's own `UnityUnicornInbox/`, never in Downloads, where a world's open editor may be waiting for a job;
  - a receipt was written.
- The page-to-Unity hand-off, checked with `RunPageJob` (7 checks, all pass):
  - a job made by the page's own `VINE.job` (what **Open in Unity** saves) was taken from the inbox;
  - its 5 colours, "both", glow 1.2 and Leaf glow 0.6 carried over;
  - vines grew on a test doorway.
- The window, in a visible editor (`RunWindowPicture`): it opened in the middle of Unity at 600 by 720 with 10 colours and a
  long job file name, and nothing was cut off there or at its smallest, 340 wide. The pictures are Unity's own pixels of the
  window, never the screen.
- The page's Leaf glow slider (0 to 1, step 0.05) and its note, through a local server: moving it to 0.65 was saved, and
  **Open in Unity** made a job with `leaf_glow` 0.65 and `glow` 0.8. The download was caught in the page.
- The page in the browser:
  - the squares, the 10-colour limit (the 11th is disabled), taking colours out, the world picker and the job file's contents.
  - The download was caught in the page, so nothing was saved to Downloads.

Not checked: the tool installed in a real world, Unity's window opening in front of you from a real download, and a browser
whose downloads ask where to save. Pictures of the test renders are in `docs/screenshots-20261019/`.

Made with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.
