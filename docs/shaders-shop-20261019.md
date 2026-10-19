# shaders-shop

Downloads shaders for VRChat worlds and tracks which shaders each world uses. It's a **Unity Unicorn tool** (Unity
Stable, `pet-program-5`). Astra asked for it on 2026-10-18: a shaders-shop module in Unity Stable that downloads
shaders for worlds and tracks which shaders each world uses. ("Module" here is the code; on the page it is a tool box.)

It was built and first used in Liminal Doors (findastra/liminal-doors, branch `claude/fresh-start-20261018`, last
changed in `db27ce1`) and copied here unchanged on 2026-10-19. Version 1.0.0, by Claude Opus 5.5 (`claude-opus-5-5`).

## Files

`scripts/shaders-shop/` is laid out like a world project:

| File | What it is |
|---|---|
| `Assets/ShadersShop/shaders-catalog.json` | The shelf (`unity-unicorn.shaders-catalog/v1`): every shader we stock, why, its licence, what it's good for and the exact files to install. |
| `Assets/ShadersShop/Editor/ShadersShopWindow.cs` | The window: **Shop** (every shader, and which worlds already use it) and **This world**. |
| `Assets/ShadersShop/Editor/ShadersInstaller.cs` | Installs the way VRChat Creator Companion would: VPM or zip packages with their dependencies into `Packages/` (and `vpm-manifest.json`), `.unitypackage` imports, loose files; store and manual shaders only open their page. |
| `Assets/ShadersShop/Editor/WorldShaders.cs` | Traces every material the world's scenes use (prefabs and the skybox too) to its shader and catalog entry, and saves `docs/shaders-in-world.json` with the world. |
| `Assets/ShadersShop/Editor/ShadersShopAgent.cs` | The same from an agent queue: scan, install from a request file, resume after a reload, report shader errors. |
| `Assets/ShadersShop/UnityStable/*.shader` | Our own Unity Stable shaders, own work for Astra: Candy Paint, Cloud Puff, Cloud Sea, Handmade PBR, Pink Glass, Sparkle, Sunset Sky. |
| `Tools/shaders-shop/refresh-catalog-20261018.py` | Rebuilds the catalog from its sources (VPM listings, GitHub releases, raw files). Read-only requests. |
| `Tools/shaders-shop/scan-world-shaders-20261018.py` | The world scan from files, without opening Unity, for worlds that aren't open. |
| `Tools/shaders-shop/worlds/*.json` | The records so far: Astra's Infinite Pole, Piranesi House and Liminal Doors (2026-10-18). |

The shelf (2026-10-18): Poiyomi Toon, lilToon, ORL Shaders, Mochie's Unity Shaders (free), UnlitWF, Lit (z3y),
SkyboxPlus, our own Unity Stable shaders, Unity built-in and the VRChat SDK shaders. Two store skies (Sample Sky Shader,
Sorairo Sky) are listed so the scan can name them; they are never downloaded or committed.

## Use

1. Copy everything inside `scripts/shaders-shop/` into the world project's folder. Keep `Review/` out of Git.
2. **Unity Unicorn › Shaders Shop › Open shop window.** Install shows the files, sources and size first and asks.
3. **This world**: **Scan this world**, then **Save world record**, writes `docs/shaders-in-world.json` (the agent
   menu **Scan this world** does both). Commit it with the world.

For agents, `Review/shaders-shop/install.json` holds the request:
`{"ids": ["liltoon"], "approved_by": "Astra in chat, 2026-10-18"}`. Only with Astra's OK for exactly those shaders.

## Limits

- Versions are pinned at the 2026-10-18 refresh until `refresh-catalog-20261018.py` runs again.
- The shader error report also looks in `Assets/LiminalDoors`, the world it was written in; in other worlds that
  folder simply isn't there.
- Install history (`Review/shaders-shop/installs.jsonl`) is private and stays on the PC.
- Liminal Doors still has its incubating copy. Change the tool here first.
