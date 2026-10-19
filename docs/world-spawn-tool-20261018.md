# world-spawn-tool

Spawns a new VRChat world for Astra. It's a **Unity Unicorn tool**: it started in Liminal Doors (the first world
it was written from, draft PR findastra/liminal-doors#1) and moved here on 2026-10-18, when Astra asked for every tool to be
merged into its own pet program. Files: `scripts/world-spawn-tool/` (script, template, tests), this guide and
`tools/world-spawn-tool-20261018.json`.

> **Before the next use:** step 3 (`--apply`) still copies the skeleton from a working reference world. Since
> 2026-10-18 Astra's rule is that a new world starts fresh from VRChat Creator Companion's World template, never a copy
> of another world. Change `--apply` to start from that template before spawning another world.

Version 0.1.0-20261018, by Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-18.

## First things to do (every new world)

Astra, 2026-10-18: these two come first, before any Unity work.

1. **Make the world's GitHub repo, public.** The name is the world's title in lowercase words joined by hyphens
   (Liminal Doors → `liminal-doors`), the same way as Astra's other repos. Names come from GitHub and the hub, so
   never coin one.
2. **Link it in the Findastra profile README.** Add a line to the **VR & VRChat** list in
   [findastra/findastra](https://github.com/findastra/findastra) with a link to the repo and a cute one-line summary.
   Do it on a `claude/<repo>-link-<date>` branch with a pull request. Merging is Astra's call.

The tool does both when asked (`--create-repo`, `--profile-pr`), and `--apply` refuses to build the Unity project
until both are done: the repo is public, and the profile links it or a PR adding the link is open.

Liminal Doors got these on 2026-10-18: [findastra/liminal-doors](https://github.com/findastra/liminal-doors) and
[findastra/findastra#4](https://github.com/findastra/findastra/pull/4).

## Then the Unity project

3. **Skeleton** (`--apply`), copied from a reference world that already works, read only:
   - `Packages/` manifest, lock and VPM files, plus the embedded VRChat SDK (`com.vrchat.*`; not committed);
   - `ProjectSettings/` with `productName` set to the world's title, a new `productGUID`, and empty build settings;
   - `.gitignore` (made sure to ignore `Review/` and the embedded SDK), `.gitattributes`, `.vsconfig`;
   - `Assets/<Code>/Editor/<Code>AgentQueue.cs` and its asmdef. This is the Infinite Pole agent queue, so agents drive
     the open editor instead of relaunching it. The menu prefix is `<Code>/`, and `<Code>` is the title in PascalCase;
   - `README.md` starting with the summary;
   - a Unity Unicorn usage record (`unity-unicorn.tool-usage/v1`) in `Review/world-spawn/`. That is private run
     history, so it stays out of Git.
4. **Reuse assets** from the reference world with their GUIDs: `Tools/infinite-pole-export/deps.py` (dependency list)
   and `extract_prefab.py` (a scene subtree as a prefab). These are still separate scripts.
5. **First import and render.** Batch mode only while no editor has the project open; then open it normally once.
6. **Git:** `git init`, commit on `claude/<topic>-<date>`, add `origin`, push, and leave a dated handoff.

## Using it

Preview everything (changes nothing; the GitHub checks are read only):

```bash
python scripts/world-spawn-tool/world-spawn-tool-20261018.py --name "Liminal Doors" --summary "a VRChat world of doors ..." --reference "<working world project>" --dest-root "<folder that holds project folders>"
```

Then, in order, each with the same `--name` and `--summary`: `--create-repo`, `--profile-pr` (add
`--platform "PC VR / desktop"` and `--profile-after <repo>` to place the line), then `--apply`. Use `--check` for the
first-steps status only. Tests: `python scripts/world-spawn-tool/test-world-spawn-tool-20261018.py` (no network, no Unity).

## Traps found while making it

- **gh signed out from Python.** The Microsoft Store Python runs sandboxed and may not see the sign-in that `gh`
  has in a normal shell. The tool then says so instead of reporting "missing". Run it with
  `GH_TOKEN=$(gh auth token)` for that one command.
- **Long paths.** The SDK's folders are deep. Copies go through `\\?\` paths, so a long destination doesn't hit
  Windows' 260-character limit.
- **Camel-case titles.** The derived name is a guess: "Astra's PetOS" would give `astras-petos`, but the hub is
  `astras-pet-os`. Pass `--repo` when the real name differs.

## Before it moves to Unity Unicorn

- Fold steps 4–6 in as subcommands (asset reuse by GUID, first import and render, git and push), using the
  Liminal Doors recipe in `docs/handoff-claude-20261018.md`.
- Optionally tell the hub: the caretaker pet's `for` field in `pets.json` (Door Dormouse has `for: liminal-doors`).
- A catalog entry is ready: `world-spawn-tool-20261018.json` (`unity-unicorn.tool-catalog/v1`).

## Use history

No real spawn yet. Liminal Doors itself was spawned by hand on 2026-10-18, and this tool encodes that recipe. Its
only run so far was a test into a scratch folder (2026-10-18T09:54:53Z, completed, output deleted), which doesn't
count as a use.
