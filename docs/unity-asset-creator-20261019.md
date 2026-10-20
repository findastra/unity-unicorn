# unity-asset-creator

Builds one VRChat world asset through an ordered, researched checklist and exports each finished version to
[findastra/unity-assets-library](https://github.com/findastra/unity-assets-library). It's a **Unity Unicorn tool**
(Unity Stable, `pet-program-5`), with a trick of the same name that says how an agent walks it
([skills/unity-asset-creator/SKILL.md](../skills/unity-asset-creator/SKILL.md)).

Astra asked for it on 2026-10-18 ("lets make this booth together and then publish those tools"). It was built and
first used in Liminal Doors (findastra/liminal-doors, branch `claude/fresh-start-20261018`, last changed in `db27ce1`)
and copied here unchanged on 2026-10-19. Tool version 1.0.0; checklist 1.1.0 (the Craft section was added
2026-10-19). By Claude Opus 5.5 (`claude-opus-5-5`).

## Files

`scripts/unity-asset-creator/` is laid out like a world project, so installing it is one copy into the project's
folder:

| File | What it is |
|---|---|
| `Assets/UnityAssetCreator/Editor/AssetRecipe.cs` | One recipe per asset: the plan, the asset-ledger row, the changelog and how far it has come. A small asset file next to the asset, so its progress is kept in Git with it. |
| `Assets/UnityAssetCreator/Editor/AssetChecklist.cs` | The 12 steps and what the tool checks by itself at each one. |
| `Assets/UnityAssetCreator/Editor/AssetCreatorWindow.cs` | The checklist window. |
| `Assets/UnityAssetCreator/Editor/AssetCreatorAgent.cs` | The same checklist from an agent queue (menu names, no clicks). |
| `Assets/UnityAssetCreator/Editor/AssetExporter.cs` | Preview renders in the world's light, the `.unitypackage`, `manifest.json` (`unity-unicorn.asset/v1`) and a usage record. |
| `Assets/UnityAssetCreator/Editor/ShopShaders.cs` | Reads the shaders-shop's catalog file, so a shop shader is listed in `requires_shaders` instead of being packed. Works without the shop installed. |
| `Tools/unity-asset-creator/unity-asset-creator-checklist-20261018.md` | The researched checklist: each step's what, why (with sources) and what the tool checks; the Craft section. |
| `Tools/unity-asset-creator/publish-asset-20261018.py` | Step 12: checks each export and opens a pull request adding it to the library. |

The `.meta` files are kept so the scripts keep their GUIDs in every project.

## Use

1. Copy everything inside `scripts/unity-asset-creator/` into the world project's folder. Add `Review/` and
   `Exports/` to its `.gitignore`.
2. In Unity: **Unity Unicorn › Asset Creator › Open checklist window**. Select the asset's prefab and press **New for
   selected prefab**, or make a recipe with Create › Unity Unicorn › Asset Recipe.
3. Go down the steps in order: Check, read the result, write a note where it asks for one, Mark done. Render previews
   before step 11. Export at the end.
4. Publish: `python -I Tools/unity-asset-creator/publish-asset-20261018.py --library <clone of findastra/unity-assets-library>`.

Agents use the menus **Check every recipe**, **Apply confirmations file**, **Render previews for every recipe** and
**Export every ready recipe** through the world's agent queue (the trick has the details).

## Limits

- Steps 3, 7 and 11 need a person's confirmation note. The checks enforce budgets and rules; they don't judge looks.
- Tested in one world (Liminal Doors, Unity 2022.3.22f1, VRChat Worlds SDK 3.10.5) on two assets: the phone booth and
  stop 1's sky, both v1.0.0, merged into the library on 2026-10-19 (its PR #1).
- Liminal Doors still has its incubating copy. Change the tool here first, then bring the change to the worlds that
  use it.
