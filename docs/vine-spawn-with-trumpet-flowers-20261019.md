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
  size, leaf colour, flower glow and the seed. They stay hidden until you click it.
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
by itself.

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
- **Grow vines** grows on the selected objects in one Undo step:
  - Vines start low on the sides to climb, or high on walls and edges to hang. Each one feels its way along the surface
    with hidden temporary colliders on a world-space copy of each mesh, so the objects need no colliders of their own.
  - Vines turn up walls at inside corners and wrap round outside edges. Where there's nothing to hold, they hang for a little,
    then stop. LOD 1 and lower copies are skipped.
  - Each vine is a woody stem that turns green toward the tip. It has side shoots, oval leaves with a midrib in alternating
    pairs, and trumpets along its outer three quarters. The trumpets are a narrow tube that flares into five rounded lobes,
    on a short stalk with a green calyx, with a darker throat. Each one takes one of your colours at random.
  - About 28% of the flowers are buds. Each tip ends in a cluster of 2 to 4 flowers and buds.
  - There's one mesh per selected object, saved to `Assets/UnityUnicorn/Generated/Vines/`, with light probes, up to 150,000
    triangles per Grow.
  - Shader **Unity Stable/Vine Bloom**: vertex colours with Standard lighting, both sides lit, and shadows. Petals can glow
    (**Flower glow**), and leaf and flower tips sway a little. There's one material per glow value, so a new glow doesn't
    change vines you grew before.
- Each Grow writes a usage receipt to `UnityUnicornReports/vine-spawn-with-trumpet-flowers-<time>.json`
  (`unity-unicorn.tool-usage/v1`) for the tool library.
- **Look for jobs now** takes a job of any age. **Also watch a folder…** is for a browser that saves downloads somewhere else.

## Checked

- `node --test test/avatar-and-world-tools-test-20261019.mjs` checks these things:
  - the page's palette, worlds and defaults equal the Unity tool's;
  - colour squares stop at 10;
  - the job file and its name are what the tool reads, with the same limits;
  - bad worlds, colours and counts are refused;
  - the panel's scripts write text only.
- In a throwaway Unity 2022.3.22f1 project (batch mode, not a world), `test/unity/vine-spawn-smoke-test-20261019.cs`, 35
  checks, all pass:
  - a wall, an arch and a column with no colliders each got vines. That's 29,822 to 43,910 triangles each, every vertex
    within 0.35 m of its object, finite normals, and petals in all five picked colours;
  - the shader compiled with no errors;
  - hanging vines grew;
  - one Undo took the last Grow away and kept the first;
  - the inbox took the job for its world and left another world's job and an 11-colour job alone;
  - a receipt was written.
- The page-to-Unity hand-off, checked with `RunPageJob` (6 checks, all pass):
  - the job file the page's **Open in Unity** made, unchanged, was taken from the inbox;
  - its 5 colours, "both" and glow 1.2 carried over;
  - vines grew on a test doorway.
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
