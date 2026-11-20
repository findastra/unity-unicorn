# world-spawn-tool

Spawns a new VRChat world for Astra. It's a **Unity Unicorn tool**: it started in Liminal Doors (the first world
it was written from, draft PR findastra/liminal-doors#1) and moved here on 2026-10-18, when Astra asked for every tool to be
merged into its own pet program. Files: `scripts/world-spawn-tool/` (script, template, tests), this guide and
`tools/world-spawn-tool-20261018.json`.

Version 0.2.0-20261019, by Claude Opus 5.5 (`claude-opus-5-5`). 0.1.0 was made on 2026-10-18; on 2026-10-19 step 3
was changed to start from VCC's World template, and gh was made to work when Python runs the tool.

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

A new world **starts fresh** (Astra, 2026-10-18). It never copies another world's packages, settings, shaders, scenes
or scripts. A piece from another world comes in only when Astra names it.

3. **Skeleton** (`--apply`), built from VRChat Creator Companion's official **World template**
   (`%LOCALAPPDATA%\VRChatCreatorCompanion\VRCTemplates\World`, read only) and the SDK zips in VCC's package cache
   (`%LOCALAPPDATA%\VRChatCreatorCompanion\Repos`):
   - every template file except the template's own `package.json` and `README.md`: `Packages/manifest.json`,
     `Packages/.gitignore`, `ProjectSettings/`, `Assets/.gitkeep` and `.gitignore`;
   - `ProjectSettings/` with `productName` set to the world's title, a new `productGUID` (the template's GUID is the
     same in every VCC project), and empty build settings;
   - the VRChat SDK (`com.vrchat.base`, `com.vrchat.worlds`) unpacked from VCC's cache into `Packages/`. It is not
     committed. The tool picks the newest cached versions that match the template's `vpm-manifest.json` ranges and each
     other's `vpmDependencies` (pre-releases are skipped, as VCC does by default). `vpm-manifest.json` is then locked
     to them, the way VCC writes it;
   - `.gitignore` gets the rules it must have: `Review/` and the embedded SDK. `.gitattributes` keeps Unity's text
     files LF and pictures, sound and models binary;
   - `Assets/<Code>/Editor/<Code>AgentQueue.cs` and its asmdef. This is the Infinite Pole agent queue, which is agent
     tooling, not world content, so agents drive the open editor instead of relaunching it. The menu prefix is
     `<Code>/`, and `<Code>` is the title in PascalCase;
   - `README.md` starting with the summary;
   - a Unity Unicorn usage record (`unity-unicorn.tool-usage/v1`) in `Review/world-spawn/`. That is private run
     history, so it stays out of Git.

   The project goes in `<dest-root>/<repo>`. That folder may already exist if it is a fresh clone of the world's repo
   with no Unity project yet (Mommy's World has its seed there). Then nothing in it is overwritten: its README stays,
   and its `.gitignore` only gains the rules it lacks. The tool never writes into a folder that already has `Assets/`,
   `Packages/`, `ProjectSettings/` or `Library/`.
4. **Pieces Astra names** from another world, with their GUIDs, the way Liminal Doors' fresh start took only Infinite
   Pole's booth meshes. Only meshes, unless she says to bring materials, shaders or scripts too.
5. **First import.** Open the project once in a normal Unity window (from the Hub or VCC), so the SDK sign-in stays.
   Batch mode only while no editor has the project open. The SDK sets its defines after the first compile, so editor
   code that uses SDK types needs `#if UDON`.
6. **Git:** commit on `claude/<topic>-<date>` (`git init` and `origin` first if the folder wasn't a clone), push, and
   leave a dated handoff.

## Using it

Preview everything (changes nothing; the GitHub checks are read only):

```bash
python scripts/world-spawn-tool/world-spawn-tool-20261018.py --name "Liminal Doors" --summary "a VRChat world of doors ..." --dest-root "<folder that holds project folders>"
```

Then, in order, each with the same `--name` and `--summary`: `--create-repo`, `--profile-pr` (add
`--platform "PC VR / desktop"` and `--profile-after <repo>` to place the line), then `--apply`. Use `--check` for the
first-steps status only. `--template` and `--vcc-repos` point at another copy of VCC's template or cache.
`--reference` is gone: passing it stops the tool with the fresh-start rule.

Tests: `python scripts/world-spawn-tool/test-world-spawn-tool-20261018.py`. They need no network and no Unity: they
build a small stand-in for the template and the SDK cache in a temp folder.

## gh from Python

The shell's `gh` is signed in, but `gh` started by Python says "You are not logged into any GitHub hosts". The cause
(checked on 2026-10-19): Microsoft Store Python gets its own private copy of `%APPDATA%`. From inside it,
`%APPDATA%\GitHub CLI\hosts.yml`, the file that says who is signed in, doesn't exist. The token itself is in the Windows
keyring. `gh` inherits that view, so it finds no sign-in. `git push` from the same Python still works, because git has
its own credential helper.

So the tool splits the work:

- **Read-only checks** (`--check`, and the first-steps gate on `--apply`) use GitHub's public API when `gh` is signed
  out. That needs no sign-in, because world repos are public by rule. It can't see a private repo, so it says so.
- **Step 1** prints the exact `gh repo create` command to run from the shell (Git Bash).
- **Step 2** makes the README change and pushes the branch from Python with git, then prints the exact `gh pr create`
  command for the shell.
- The read-only `gh` checks are printed too, for anyone who wants to run them from the shell.

Never ask Astra for a token to get around it.

## Traps found while making it

- **Long paths.** The SDK's folders are deep (up to about 140 characters inside the package). The tool unpacks them
  through `\\?\` paths, but Unity can't import a file at 260 characters or more while Windows long paths are off. The
  preview warns when `--dest-root` is too deep. The session scratchpad is too deep; `Documents\Astra's PetOS` is fine.
- **The PetOS junction.** `--dest-root` is used as given, not resolved, so `Astra's PetOS` stays in the paths instead
  of turning into `Astra's Pet Apps`.
- **Git Bash paths.** Pass `--dest-root` through `cygpath -w` (or a relative path) from Git Bash. A `/c/...` path with an
  apostrophe can reach Python unconverted.
- **Camel-case titles.** The derived name is a guess: "Astra's PetOS" would give `astras-petos`, but the hub is
  `astras-pet-os`. Pass `--repo` when the real name differs.

## Before it moves to Unity Unicorn

- Fold steps 4–6 in as subcommands (pieces Astra names, by GUID; first import; git and push), using the Liminal Doors
  recipe in `docs/handoff-claude-20261018.md` there.
- Optionally tell the hub: the caretaker pet's `for` field in `pets.json` (Door Dormouse has `for: liminal-doors`).
- A catalog entry is ready: `world-spawn-tool-20261018.json` (`unity-unicorn.tool-catalog/v1`).

## Use history

- **Mommy's World, 2026-10-19: steps 1 and 2 only.** This was the first real use, with version 0.1.0. The tool
  derived the repo name (`mommys-world`) and the code name (`MommysWorld`), and its `insert_profile_line` placed the
  profile line right after Liminal Doors. `gh` couldn't sign in from Python, so the `gh` commands ran from the shell:
  [findastra/mommys-world](https://github.com/findastra/mommys-world) (public) and the profile pull request
  [findastra/findastra#10](https://github.com/findastra/findastra/pull/10), which is not merged. Step 3 was not run:
  Astra said no Unity yet, and the step still copied from a reference world then.
- Liminal Doors itself was spawned by hand on 2026-10-18; version 0.1.0 encoded that recipe.
- Test runs into scratch folders don't count as uses: 2026-10-18T09:54:53Z (0.1.0), and on 2026-10-19 one run of the
  0.2.0 skeleton step from the real VCC World template 1.5.0 and SDK 3.10.5 into a temp folder (all 1194 + 2601 SDK
  files unpacked, then deleted).
