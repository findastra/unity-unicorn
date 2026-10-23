# Glass carver

Little cut-crystal animals from real animal scans, for Liminal Doors (one hides behind every one of its 34 doors)
and for any world that wants a glass figure. It is marble-carver's sibling: a real scan goes in, the tool only
re-cuts it and changes what it is made of. **Never build an animal from primitives, SDFs or procedural shapes**
(the marble-carver rule; Astra rejected that result as fake and anatomically wrong).

Made with Claude Fable 5.1 (`claude-fable-5-1`), 2026-10-23, out of The Rainbow Maker's crystal work (Astra's
double-crystal Kikkerland sun catcher): steep facets so light bounces inside, three glasses added together (red,
green, blue at IOR 1.70 / 1.75 / 1.82) because Blender 4.5's glass has no dispersion, and sparkle lights behind the
camera so facets flash.

## 1. Find a real scan (licence first)

Same order as marble-carver, and record source and licence with `--source` and `--licence` (they go into the
animal's `.json` next to its FBX, and credit-only licences get their author in the VRChat world description):

1. **Smithsonian Open Access 3D** (CC0 where marked; some items are non-commercial, read each page) and
   **Sketchfab's public-domain and CC BY scans** (museum taxidermy, natural history collections).
2. **Poly Haven** (CC0) for the few animals it has.
3. **Astra's own photos** of a real animal or figurine, modelled from the photos (as the Rainbow Maker was), when
   nothing scanned exists.
4. Avoid non-commercial scans for public worlds.

Downloads happen on Astra's PC with her OK for each one; stage them in the Downloads folder.

## 2. Carve

```
blender --background --python scripts/glass-carver/glass-carver-20261023.py -- \
  --scan "<Downloads>/fox-scan.glb" --name Fox --out "<liminal-doors>/Assets/LiminalDoors/GlassAnimals" \
  --facets 420 --style cut --source "Sketchfab: <author>, <url>" --licence "CC BY 4.0" --render
```

- `--height` metres (default: the animal's life size from the script's `LIFE_SIZE` table). `--facets` flat faces for
  the cut style (default 420).
- `--style cut` flat facets, hard edges, a hair of bevel; `--style smooth` keeps the scan's curves, polished.
- `--tint "#ffd9ec"` colours the glass; clear is the default.
- `--render` writes `<animal>-<date>-check.png` on a dark stage with a sun and eight sparkle lights; `--video` a 6 s
  turntable. Cycles on the HIP GPU, 96 samples, 90 s cap per frame. `builtin:monkey` as `--scan` is a pipeline test
  only, never a finished animal.
- Outputs, all named `<animal>-<date>.*`: `.fbx` (Y up, metres, triangles, flat normals), `.blend`, `.json` (scan,
  source, licence, style, height, facets, triangles).

## 3. Into Liminal Doors

`Assets/LiminalDoors/Editor/LiminalGlassAnimals.cs`:

- **LiminalDoors > Glass animals > Place all**: for every `Doorspace NN · Place Animal` root, finds the
  `<Animal> hiding spot` group the Doorspace kit made, takes the newest `GlassAnimals/<animal>-*.fbx`, places it there
  facing the arrival, gives every renderer the `LiminalDoors/Crystal` material, and adds an Animator with one slow
  turn a minute (Animators are allowed in VRChat worlds; scripts are not). Animals without an FBX are listed.
- **Render checks**: one picture per placed animal in `Review/glass-animals/`.
- `Assets/LiminalDoors/Shaders/Crystal.shader` is the Rainbow Maker's crystal shader renamed: red, green and blue
  refract through steepened facets at their own IORs and read the nearest reflection probe, with sun glint and fire.

## Quality bar

- The animal is recognisable at arm's length: a fox reads as a fox before it reads as glass.
- Facets flash as it turns; colour fringes show at facet edges in the check render.
- Under 4000 triangles per animal (Quest budget); the default cut lands around 2000.
- Licence recorded; nothing placed in the world from an unknown source.

## Astra's answers (2026-10-23)

| # | Question | Answer |
|---|---|---|
| 1 | Where do the scans come from? | Museum and public scans (Smithsonian, Sketchfab CC0 / CC BY, Poly Haven); Astra OKs each download |
| 2 | Cut crystal or smooth glass? | Cut crystal |
| 3 | How big? | **Life size**, like the prismatic wolf she saw in VRChat (not the 22 cm ornament that was recommended). `LIFE_SIZE` in the script holds a height per animal; `--height` overrides it. Insects, the gecko and the firefly come out a few centimetres tall. |
| 4 | Which animal first? | The fox, Doorspace 01 |
| 5 | Clear or a colour? | Clear, with rainbow fire |

The command above therefore drops `--height 0.22`; the fox comes out 40 cm tall.
