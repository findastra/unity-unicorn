---
name: vrchat-unity-world
description: Work on the owner's Unity/VRChat worlds (Astra's Infinite Pole) without breaking her editor session. Covers driving the already-open Unity through the agent queue, compile/shader checks, scene backup and restore-then-apply, walkable cloud/platform colliders (thin, inside the visible outline), renders for review, and the local desktop test. Use for any scene, mesh, shader, collider or build task in a VRChat world project.
---

# VRChat world work in Unity (Claude)

Pair this with the `blender-unity-assets` skill for Blender meshes (its master is in this repo, `skills/blender-unity-assets/`).

## 0. Before touching anything
- Open the caretaker pet's panels on the right (skill `show-pet-panel`): Unity Stable for Unity Unicorn's tools and assets, Infinite Studio for Astra's Infinite Pole when it has a page.
- Follow Astra's global rules (`%USERPROFILE%\.claude\CLAUDE.md`):
  - Run `git fetch` and compare with `origin`.
  - Read the newest handoff: the hub's open cards in `astras-pet-os\handoffs\`, the repo's `docs/handoff-*.md` and recent commits. A newer handoff overrides your own session's older request; say so when it does.
  - If someone else's work is uncommitted, don't touch it.
- **Where things live (2026-10-18):**
  - Unity world and avatar projects live on the external drive, `D:\personal-files\unity\VRChatProjects\<repo>`, backed up on GitHub (Astra, 2026-10-25; not under Astra's PetOS). Astra's Infinite Pole is `D:\personal-files\unity\VRChatProjects\astras-infinite-pole` (moved 2026-10-25 from `Documents\ChatGPT\Mommy's Discord\Astra-Infinite-Pole`).
  - D: is exFAT, so git needs a `safe.directory` entry per repo there. Change nothing on D: without Astra's OK for that change; never move a project folder yourself.
  - On D: that includes agent-queue `.cmd` files, Unity saves and `Review/` reports (Astra, 2026-10-25: "dont change anything in the external drive"): ask for that task first. Reading, hashing and copying from D: is fine.
  - The world's caretaker pet is **Pole Puff** (program Infinite Studio). Reusable Unity/Blender tools belong to **Unity Unicorn**.
  - Find the project yourself (Everything, her folders, past chats, skill `find-things`) and open it yourself; never ask her where it is or to open it (Astra, 2026-10-19).
- **A new world starts fresh (Astra, 2026-10-18):** VRChat Creator Companion's World template (`%LOCALAPPDATA%\VRChatCreatorCompanion\VRCTemplates\World`) plus the SDK from VCC's `Repos` cache. Never copy another world's Packages, ProjectSettings, shaders, scenes or scripts; take only the pieces she names, and ask before bringing their materials, shaders or scripts. The agent queue and desktop test may be reused. Its first steps, in order, are a public findastra repo and a link with a cute one-line summary on her profile README, both done by Unity Unicorn's `world-spawn-tool` (`--check`, `--create-repo`, `--profile-pr`, then `--apply`).
- Read the workspace `AGENTS.md` and the newest `ACTIVITY.md` entries (`Documents\ChatGPT\Mommy's Discord\`). Then read the project's `AGENTS.md`, `CLAUDE.md` and `docs/agent-coordination.md`, and claim exact files on that board.
- **Push before you stop (Astra, 2026-10-12):**
  - Commit to `claude/<topic>-YYYYMMDD` and push it, even if unfinished. Work that is only on the PC counts as lost.
  - `main`, releases and uploads change only with her OK.
  - The world repo is public. Before pushing, scan the files for keys and tokens, and for machine paths with her Windows user name.
- **Tool history:** after you create or use a Unity/Blender tool, it belongs in Unity Unicorn's tool library. The last-use date comes from a completed run report (with a timestamp), never from a file date; missing evidence stays Unknown. Blender commands: `--python-exit-code 1` before `--python`.
- Git in this repo needs `git -c safe.directory=* ...`, because the repo is owned by another Windows account. Don't change the global git config.
- The scene and `Assets/SerializedUdonPrograms` are often already dirty from other agents. Preserve that work. Back up the scene and any assets you will change to `Review/Backups/<task>/` first.

## 1. Unity: never relaunch it
- Never close, kill or batchmode-launch Unity on an open project, because the VRChat SDK sign-in is lost each time. If Unity isn't running (`tasklist | grep Unity.exe`), open it once, the way she would (2026-10-19):
  - Write a `.lnk` in your scratchpad (WScript.Shell) with target `C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe` and arguments `-projectPath "<project>" -logFile "<project>\Logs\<name>.log"`, then run `explorer.exe "<lnk>"`.
  - Explorer starts Unity outside the Claude app, so Unity reads her real settings, where the VRChat SDK sign-in lives. `Start-Process` from a Claude shell runs Unity inside the app's private copy of AppData and the sign-in is missing.
- Then wait for a new timestamp in `Review/agent-queue/_listener.txt`. The SDK restores its sign-in after about 30–60 s; check with `status`.
- A project that was closed for a while re-saves its generated Udon assets when it opens (line endings only). Say so in the handoff and don't commit them.
- Editors can close on their own. If a `.cmd` sits unanswered, check that Unity is still running before you wait longer.
- Drive the editor by writing `Review/agent-queue/<name>.cmd` and reading `<name>.result.txt`. Commands: `status`, `refresh`, `save`, `menu Astra/...` (it saves the scene first), `desktop-test`, `build`.
  - `upload` only after the owner says yes in chat.
- After each `refresh`, wait about 45 s, then grep the new lines of the project's own log (`-logFile`, above) for `error CS` and `Shader error`. `%LOCALAPPDATA%\Unity\Editor\Editor.log` is shared by every open editor, so it can't say which project failed.
- Put tools in `Assets/Astra/Editor/Claude*.cs` as `[MenuItem("Astra/Claude/..")]`, so the queue can run them. Each tool writes a report to `Review/<task>/`.

## 2. Editor-script gotchas seen here
- A Unity "fake null" breaks `??`. Write `var c = x.GetComponent<T>(); if (c == null) c = x.gameObject.AddComponent<T>();`
- A constant and a nested class can't share a name (CS0102). The material property is `enableInstancing`.
- Filter report lists for destroyed objects (`b != null`) after `Undo.DestroyObjectImmediate`.
- **An apply step that edits colliders isn't idempotent.** Ship a "restore scene from backup" menu (copy the backup over the scene, then `EditorSceneManager.OpenScene`). Re-apply only after restoring, and confirm with `cmp` that the restore is byte-identical.

## 3. Walkable cloud / platform colliders (the owner's rule)
"Thin, and only below the cloud, nothing outside it seen from above." Do it like this:
- **What counts as support:** non-trigger BoxColliders on spiral `Cloud N` transforms, outer clouds, jump platforms, lift-pad walk colliders, swing anchors, crystal-vine clouds, DJ stage/step, duck platform and theatre floors.
- **Never touch:** seat, back and table colliders, VRCStations, triggers (voice and interaction), pickups, the deck console, booth floors, or the main "Invisible platform".
- **Building the slabs:**
  - Rasterise the visible cloud meshes from above in the collider's yaw frame. Record per cell whether it's occupied and its highest surface.
  - Keep cells inside the original footprint (slabs only shrink) and at least 0.1 m in from the outline.
  - Cover them with the largest rectangles as **0.06 m world-thickness** slabs. Put each slab 2 cm below the lowest visible top over that rectangle *and the cells around it*.
  - Measure world thickness as `size × lossyScale`, so scaled objects can't slip past.
- **Keep the slabs on the SAME GameObject.** Reuse the first BoxCollider and add extra BoxComponents next to it. `AstraCloudOrbit` carries a standing player only when `hit.collider.transform.name` starts with `"Cloud "`, and it overwrites per-cloud yaw every frame.
- If a collider floats above its visible top, lower it to just under the top and report how far.
- **Validate:**
  - thickness and outline: 0% outside, top no more than 2 cm above the visible surface;
  - live raycasts: stand-on, underside, and horizontal rays 0.3 m above and below the slab must hit nothing (no invisible walls);
  - orbit: every carry box sits on its `Cloud N` transform.
- Reference implementation: `Assets/Astra/Editor/ClaudeCloudRedo.cs` (menu 36c–36k).

- After you swap a cloud's mesh, re-slab only the changed groups (`ClaudeCloudRedo.Reslab(filter)`), then validate. A slab with no visible cloud above it any more is an invisible floor: switch it off and report it.

## 3b. Pickups (eggs, glow orbs; round 38)
- "Grabbable" means all of these: VRCPickup enabled and pickupable, a collider (SphereCollider), the Pickup layer, a Rigidbody (kinematic, like the orbs) and VRCObjectSync. Copy the components from a working glow orb with `EditorUtility.CopySerialized`. Find SDK types by name across all loaded assemblies; `Type.GetType` with a guessed assembly name returns null.
- Its centre must not be inside any solid collider. Desktop grabbing uses a raycast, so a box in front blocks it. Lift it out, or for a tall sofa or chair back, slide it off the back's thin side.
- **Reach:**
  - Look for the nearest walkable slab within 3–6 m at about the item's own height. Leave out orbiting `Cloud N` clouds, which move away at runtime.
  - Don't trust a straight-down ray. In the lounge it slips through the gaps between slabs to the main floor far below.
  - Place the item 0.2–1.7 m above the slab you found.
- **Sparkle:** a world-space "Sparkle slurry" particle child with orbital velocity, light noise and HDR colours (×2.4, so the bloom picks them up). The world's particle ceiling in `AstraWorldBuilder.Verify` is a summed maxParticles cap; raise it on purpose and comment why.
- Reference: `Assets/Astra/Editor/ClaudeRound38.cs`. The "fuse a sphere-stack mesh" tool is `Tools/Blender/fuse_sea.py`.

## 3c. Realistic props and downloads (2026-10-18)
- To make a prop look real, follow Unity Unicorn's `docs/realistic-props-20261018.md`:
  - Start from a real CC0 model or scan (Poly Haven API), never primitives.
  - Decompose it (`prop-decomposer`) and restyle by mask (`realistic-prop-restyle`).
  - Paint brand marks out of every map (albedo, normal, occlusion, gloss).
  - Mount it at true size, on a Standard material.
  - The same steps as a trick an AI can follow: Unity Unicorn's `skills/make-a-prop-look-real/SKILL.md`. Tricks are owned at L2 and followed at L3; this file is a harness skill (the hub's TOOLS-AND-TRICKS.md).
- **Every download** (asset or tool):
  - Ask Astra first, stating the file, source and size.
  - Add a row to Unity Unicorn's download ledger (`docs/download-ledger-*.json`): one copy, shared by grant.
  - List it in AI Doghouse's `library/ai-tools-*.md`.
- Windows path limit: Blender and Python fail inside the long session scratchpad, so put private downloads in a short `%TEMP%\<name>` folder.

## 4. Looks and review
- Before you replace a mesh asset, search the scene for every renderer that uses it.
- Put new shader looks behind a material keyword, so other materials sharing the shader stay unchanged.
- Render before and after the same way: fix the orbit pose (t = 35 s) and restore every transform you posed afterwards. Shoot close, about 15 m and bird's-eye, plus orthographic top and side collider overlays (`Hidden/Internal-Colored`, ZTest Always). Then look at them. The bloom-heavy pink world hides faint texture and exaggerates dark blotches.
- Pictures of the Unity editor itself: never `InternalEditorUtility.ReadScreenPixel`, which reads the screen and once caught her private chat lying over the window (2026-10-19). Use `PrintWindow` from outside, or `GUIView.GrabPixels` from inside Unity, and crop off folder lines that show her user name.
- A reviewing agent checks the actual files and renders. Report exact paths, counts and what you didn't verify.

## 5. Desktop test
- Queue `desktop-test`. Then confirm the test really ran:
  - a new `.vrcw` in `%USERPROFILE%\AppData\LocalLow\VRChat\VRChat\Worlds\` with a fresh timestamp;
  - in the newest `output_log_*.txt`: the same bundle name in the `--url=` argument, then `Finished entering world`, and no Udon exceptions.
- Editor.log printing "Build Finished, Result: Failure" during the SDK bundle step is a known quirk. Trust the fresh `.vrcw` and the client log.
- The `.vrcw` can disappear from `Worlds\` after the client quits. The `--url=` argument plus "Finished entering world" in the client log is enough proof.
- In `run()` helpers, delete `<name>.result.txt` before queueing, so you never read a stale result from an earlier job with the same name.
- Computer use can't grant the VRChat client (it isn't a Start-menu app). Say so and leave the in-client look to the owner or Astra.
- Never upload, publish or accept rights/terms without the owner's yes in chat.

## 6. Finish
- Write `Review/<task>/result.md`: changed files, validation evidence, limitations and desktop-test status.
- Set the board state to READY_FOR_REVIEW and append to `ACTIVITY.md`.
- Commit your work to a `claude/<topic>-YYYYMMDD` branch and push it, with a dated `docs/handoff-claude-YYYYMMDD.md` that lists what you did and what's open. If someone else's uncommitted change is in the folder, rescue it only in its own clearly labelled commit, and only with Astra's OK.
- Never merge to `main`, tag a release or upload without her yes.
- Note the tools you used for Unity Unicorn's tool history: tool id, completed-at time and report path.