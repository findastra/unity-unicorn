# vine-spawn-with-trumpet-flowers

Astra asked (2026-10-19): "make another vine-spawn-with-trumpt-flowers and add the option to change the flower colors, like
give up to 10 options in color squares where you can just pick the form the color pallat, then you click the tool, and it
opens up in unity, maybe you have to select the world to send the tool too, there are three so far - Piranesi House, Astra's
Infinite Pole, and Liminal Doors."

It is built on [vine-bloomer](vine-bloomer-20261011.md). vine-bloomer drapes a Blender ivy mantle over whole buildings in
Piranesi House. This tool grows single vines in Unity over the objects you pick, with leaves, buds and trumpet flowers.

## On the page: unity-unicorn-vine-spawn-tool-panel

It's on the **World tools** side of Unity Unicorn's page.

- **Flower colours**: up to 10 colour squares. Click a square in the 30-colour palette to add it; click a chosen square to take
  it out. When 10 are chosen, the rest of the palette greys out.
- **Send to**: Piranesi House, Astra's Infinite Pole or Liminal Doors.
- **Open in Unity**: saves the job file (below) to Downloads.
- **More** shows the settings: vines per object, vine length, climb up, hang down or both, flowers and leaves per metre, flower
  size, leaf colour, flower glow, leaf glow and the seed. They stay hidden until you click it. Flower glow starts at 0.4 and
  Leaf glow at 0 (see [after the first real use](#after-the-first-real-use-2026-10-19)).
- For Astra's Infinite Pole, while Leaf glow is 0, a line under the buttons says its leaves would read black there and offers
  **Use Flower glow 1.5 and Leaf glow 0.4**.
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
by itself. `leaf_glow` (0 to 1) is optional, and the schema is still v1: a job saved before Leaf glow existed gets 0, the
look vines had then.

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

Where it's installed (2026-10-19, with Astra's OK in chat):

- **Astra's Infinite Pole:** findastra/astras-infinite-pole PR #5, with vines on three spots Astra picked.
- **Piranesi House:** findastra/piranesi-house PR #2. It compiles; nothing has been grown there yet.
- **Liminal Doors:** findastra/liminal-doors PR #2 (commit a72cad8), installed by the session building that world. Its
  Doorspace 09, the Upside-Down Conservatory, has 72 hanging vine lines (see below).

## In Unity: Tools › Unity Unicorn › vine-spawn-with-trumpet-flowers

- The same 30-colour palette as squares, and the 10 chosen colours. Click to add or take out, or add any colour with the
  colour field.
- **This project is**: the world, found automatically or chosen by you.
- A new window opens in the middle of Unity's main window, 600 by 720. The squares and notes wrap to the window's width, down
  to its smallest (340 wide). In Astra's Infinite Pole, while Leaf glow is 0, it offers the Pole's glows, as the page does.
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

- **Leaves and stems read nearly black in the Pole**, whatever their colour. The Pole is lit mostly by its own glowing
  materials, and the shader lit leaves only with scene light. In Liminal Doors, under a sun, the same leaves read mid-green.
  - So there's now an optional **Leaf glow**, a light of their own for stems and leaves. With no scene light at all, they keep
    that share of their colour. It runs from 0 to 1, with the same limits on the page, in the job (`leaf_glow`) and in the
    window.
  - It starts at **0**, which is the look vines always had, so worlds with a sun don't change.
  - For the Pole, the page and the window offer **Use Flower glow 1.5 and Leaf glow 0.4** while Leaf glow is 0. The Pole is
    listed in the page's `GLOW_WORLDS` and the tool's `VineWorlds20261019.LitByGlow`.
- **Flower glow starts at 0.4**, not 0.2, which read dark violet in the Pole. A Liminal Doors Doorspace chose 0.35 under its
  sun. If this browser saved the old 0.2 before Leaf glow existed, the page moves it to 0.4. A glow you chose yourself stays.
- **The window opened too narrow**, at its 420-wide minimum and off to one side, so the palette and the job note were cut off
  on the right. It now opens in the middle of Unity, 600 by 720, and its squares and notes wrap to its width.
- **On a phone kiosk, vines climbed the inside glass.** A kiosk's lowest point is its floor, inside, so climbing vines started
  there. Now vines start and grow only where the spot is open to the world:
  - Of 13 directions out of the face, at least 7 must meet none of the selected objects within 3 m.
  - A floor under a roof needs 10. An open kiosk's floor sees the world only through its doorway; a bench seat sees the sky.
  - A vine stops rather than turn a corner, or crawl across a floor, into a closed-in space.
  - If an object has no open spot left, a vine may still start inside, and the Grow's report says so.
  - Where every spot is open, vines grow exactly as before. Liminal Doors' Doorspace 09 call (by reflection, with
    `GetMethod("Grow")`) gives the same meshes and material names as the first version.
- **A world with the first version:** copy the new file over the old one. The next Grow replaces the shader.
  - Vines grown before then keep their look: their materials have no Leaf glow, and the shader's default is 0.
  - With no Leaf glow, a material keeps its first name (`Vine Bloom glow 0.4.mat`). With Leaf glow it is
    `Vine Bloom glow 1.5 leaf 0.40.mat`.
  - Vines that already grew inside a kiosk stay there: take them away with Undo or delete them, then grow again.

| Test kiosk (10 vines, seed 5) | Before: vertices inside | After: vertices inside |
| --- | --- | --- |
| Closed | 12,000 of 28,300 | 0 of 42,522 |
| Open door | 9,515 of 30,928 | 0 of 20,146 |

Over 16 seeds on each kiosk, 27 of the 32 runs had nothing inside. The other five had 1 to 7 vertices: petal or leaf tips
poking through the 4 cm glass from outside.

Pictures: `docs/screenshots-20261019/unity-vines-kiosks.png`, `unity-vines-no-light-leaf-glow-0.4.png` (and `-0.png`, the look
with no Leaf glow), `unity-vine-window-open.png` and `unity-vine-window-smallest.png`.

## Checked

- `node --test test/avatar-and-world-tools-test-20261019.mjs` checks these things:
  - the page's palette, worlds and defaults equal the Unity tool's, and so do the shader's own defaults;
  - every setting has the same limits on the page, in the job reader and on the window's sliders;
  - colour squares stop at 10;
  - the job file and its name are what the tool reads, with the same limits;
  - a job without `leaf_glow` gets 0, and 0.4 carries over;
  - an old saved Flower glow of 0.2 moves to 0.4, and a chosen one stays;
  - the Pole, and only the Pole, gets the offer of its glows, and one click sets and keeps both;
  - bad worlds, colours and counts are refused;
  - the panel's scripts write text only.
- In a throwaway Unity 2022.3.22f1 project (batch mode, not a world), `test/unity/vine-spawn-smoke-test-20261019.cs`, 60
  checks, all pass:
  - a wall, an arch and a column with no colliders each got vines. That's 33,117 to 47,720 triangles each, every vertex
    within 0.35 m of its object, finite normals, and petals in all five picked colours;
  - the shader compiled with no errors, and a first-version shader in the project was replaced;
  - a material made with the first shader kept its look (Leaf glow 0, Flower glow 1.5);
  - with no Leaf glow, the material kept its first name;
  - with no scene light, leaves were green with Leaf glow 0.4 (20,566 green pixels) and black with 0 (none);
  - jobs without `leaf_glow` got 0; 0, 0.65 and 7 (held to 1) carried over;
  - nothing grew inside the closed kiosk or the open one;
  - on a bench, vines crossed the seat and climbed the backrest;
  - Liminal Doors' reflection call found one `Grow`, grew on all 4 test lines, and kept the old material name;
  - hanging vines grew;
  - one Undo took the last Grow away and kept the first;
  - the inbox took the job for its world and left another world's job and an 11-colour job alone. In tests it looks only in
    the project's own `UnityUnicornInbox/`, never in Downloads, where a world's open editor may be waiting for a job;
  - a receipt was written.
- The page-to-Unity hand-off, checked with `RunPageJob` (7 checks, all pass):
  - a job made by the page's own `VINE.job` (what **Open in Unity** saves) was taken from the inbox;
  - its 5 colours, "both", glow 1.2 and Leaf glow 0.6 carried over;
  - vines grew on a test doorway.
- The window, in a visible editor (`RunWindowPicture`):
  - It opened in the middle of Unity at 600 by 720, with 10 colours, a long job file name and the Pole's offer.
  - Nothing was cut off there, or at its smallest, 340 wide.
  - The pictures are Unity's own pixels of the window, never the screen.
- The page's glow settings, through a local server, starting from an empty browser:
  - the Leaf glow slider runs 0 to 1 in steps of 0.05, and moving it is saved;
  - the Pole's offer showed for the Pole, not for Liminal Doors;
  - clicking it set Flower glow 1.5 and Leaf glow 0.4, kept them and hid the offer;
  - **Open in Unity** then made a job with `glow` 1.5 and `leaf_glow` 0.4. The download was caught in the page.
- The page in the browser:
  - the squares, the 10-colour limit (the 11th is disabled), taking colours out, the world picker and the job file's contents.
  - The download was caught in the page, so nothing was saved to Downloads.

Pictures of the test renders are in `docs/screenshots-20261019/`.

## In a real world (Astra's Infinite Pole, 2026-10-19)

- **The hand-off, for real:**
  - On this page (`main`), I picked 5 colours and Astra's Infinite Pole, and pressed **Open in Unity**.
  - The job reached Downloads at 21:48:30.1.
  - The world's open editor moved it to `UnityUnicornReports/jobs/` at 21:48:32.5, 2.4 s later.
  - It opened this window with those 5 colours.
- **Grow:** one Grow on the banner phone booth, the DJ booth and the lounge bench made 185 trumpet flowers, 66 buds and
  62,409 triangles. One Undo took it all away, and Redo put it back. The usage receipt was completed at 2026-10-20T04:18:08Z.
- **Found:**
  - With the page's default Flower glow 0.2, the petals read dark violet in a world lit mostly by its own glow. 1.5 shows
    the colours.
  - Leaves and stems stay nearly black there whatever their colour, because the shader lights them only with scene light.
  - On a phone kiosk, vines can climb inside, up the inner glass.
  - The window opens too narrow: the palette and the job note are cut off on the right.
- **Not checked:** a browser whose downloads ask where to save, and the VRChat client.

## In Liminal Doors (Doorspace 09, 2026-10-19)

- **Used by its build script.** Doorspace 09's builder calls the tool by reflection with these settings:
  - Hang, 3 vines per object, length 5 m (and 1.7 m), 4.5 flowers and 22 leaves per metre;
  - Flower glow 0.35, flower size 1.2, up to 300,000 triangles;
  - colours #ff6fae #e040fb #9d6bff #fff4fb #ffb38a.
- **How it was grown:**
  - A hanging vine only dangles a little in the air, so each vine trails down a glass strip.
  - True-to-life leaves read as wisps across a 16 m room, so the vines were grown at 1/1.7 size and scaled up 1.7x about
    each strip's top.
- **Result:** 72 meshes and 254,241 triangles. The build report completed at 2026-10-19 22:27:06 -06:00
  (2026-10-20T04:27:06Z). That is the tool's `last_used`, the later of the two runs.
- **Found:** with a directional sun in the scene, the leaves render mid-green, not black. So the dark leaves in the Pole
  come from its lighting. With glow 0.35, the petals read pink, magenta and lilac.

Made with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-19.
