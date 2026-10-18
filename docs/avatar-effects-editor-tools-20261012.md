# Avatar effects editor tools

A Unity Unicorn tool set by Astra. Prepared with OpenAI Codex (GPT-6), 2026-10-12.

This source preview brings the fan, sparkle and RGB helpers into the tool library and adds a unified bending chooser and a read-only star diagnostic. It contains editor code only. Acquire the intended avatar, effects and shader packages from their creators; no avatar scene, learned template, third-party asset, machine inventory or private acceptance runner is included.

## Install or update

Keep a recoverable copy of the intended Unity project before installing code or applying effects. Use Unity 2022.3 with the project's matching VRChat SDK, VRCFury and shader dependencies. Modular Avatar is needed for effects authored with its components. The tool library is a catalog: it does not install packages or launch Unity.

All eight files in [scripts/unity-editor](../scripts/unity-editor/) compile together. For a new installation, copy those dated source filenames into the project's `Assets/UnityUnicorn/Editor` folder and let Unity compile them. Unity's `Editor` folder is required so this code stays out of the uploaded avatar.

For a project that already has these classes, replace each existing file's contents from the matching source instead of leaving duplicate class definitions. Keep existing filenames and `.meta` files. This table identifies the legacy files; it does not request renaming them.

| Existing file | Published source |
|---|---|
| `UnicornCore.cs` | `unicorn-core-20261012.cs` |
| `SparksAndFans.cs` | `sparks-and-fans-20261012.cs` |
| `PhysAndPaint.cs` | `phys-and-paint-20261012.cs` |
| `Strappers.cs` | `strappers-20261012.cs` |
| `Copyright.cs` | `copyright-tools-20261012.cs` |
| New extension, if its classes are absent | `bending-and-star-tools-20261012.cs` |
| New surface/weight extension | `gold-and-dangle-tools-20261012.cs` |
| New loadout companion preview | `gunsmith-loadout-tools-20261012.cs` |

Do not copy generated DLLs, project reports, private templates, materials or vendor assets from another project. This is source packaging, not a `.unitypackage` containing asset dependencies.

## Tools and boundaries

| Menu under Tools / Unity Unicorn | Behavior | Check after use |
|---|---|---|
| `fan-fitter` | Discovers supplied prefab variants sharing the learned fan meshes, preserves distinct stock variants, copies learned left/right setup and offers exclusive toggles per hand. | Verify every intended stock choice, bone placement, matching materials and saved prefab overrides after reopening the scene. |
| `rgb-painter` | Copies supported Poiyomi materials, enables active color layers to cycle, and gives neutral reflective layers color so a hue cycle can be visible. Particle renderers are skipped. | Inspect the actual fan over a full cycle. Material properties alone do not prove the rendered appearance. |
| `sparkle-strapper` | Offers independent feet controls and a continuous RGB menu option alongside the supplied manual hue control. It copies the controller/menu/parameters before edits and checks for incomplete previous installation. | Test hand and foot controls separately, the full RGB loop and returning to manual hue. Verify animation bindings and saved references after a scene reload. |
| `bending-adder` | Checks learned blood, water and sparkle-bending templates before adding missing effects with Undo. Existing add-ons remain unchanged. | Supply a verified separate sparkle-bending source; star gun/burst templates are not treated as proof of that source. Check placement, controls and Undo. |
| `star-effects-diagnostic (read only)` | Inspects missing references, local controller integration, repeated attachment targets, firing conditions and unbuilt animation paths. Export is optional. | Compare the creator's source prefab, then test the final built controller and visible firing behavior in an emulator. The command does not repair or install a gun. |

`golden-painter` clones explicitly selected material slots and preserves texture references and alpha. Review shader tint/finish support and actual appearance. It does not paint texture pixels. `chain-dangle-weights` diagnoses indexed mesh islands and maps selected islands to existing bones on a mesh copy. It leaves vertices, indices and unmapped weights unchanged. UV seams can split physical pieces; more than 256 islands is diagnostic-only. Neither tool guesses a rig, creates physics, saves the scene or proves a real shoe repair.

`gunsmith-loadouts` imports a planned v1 manifest and requires explicit mappings to local prefab roots and parent transforms under the selected scene avatar. It previews changes and revalidates them before applying with Undo. Local EditorPrefs retain persistent ownership IDs and tombstones; only matching tool-created instances qualify for removal, and added objects/components or nondefault overrides block removal. A saved scene and the Newtonsoft JSON package are required. This preview attaches prefabs only: follow the creator's setup for dependencies, controllers and menus. It has not yet passed real scene Undo/save-reopen tests.

The existing learning, separate bending strappers, PhysBone and provenance helper classes are included because the source set shares them. Their inclusion is not a claim that every workflow was newly validated. The provenance ledger reflects supplied records and is not a legal clearance service.

Select the exact scene avatar root. Learn placement templates only from a verified source avatar and review the chosen add-ons before using them elsewhere. New templates use lowercase dated prefab and pose filenames. Legacy pose sidecars remain readable; when several learned versions have the same name, the latest recorded learning timestamp is selected. A learned pose is not runtime verification.

The generic core recognizes explicit sparkle-bending names before ordinary sparkles. It does not invent the missing effect. The golden painter and chain-weight tools are source previews. The intended gold avatar and shoe rig still require validation.

## Private data and actual use

Runtime reports and the provenance ledger are written to the project's private `UnityUnicornReports` folder outside `Assets`. Exclude that folder, private learned templates and generated avatar materials from public source control. The provenance helper reads a legacy ledger when present and writes future saves to a dated private ledger. Templates and generated effect assets remain local to the Unity project that needs them.

File dates and report times use America/Denver with daylight-saving handling. New reports get dated, timed filenames that avoid overwriting a same-second record. These helper changes apply to this published source preview; existing installed projects keep their old helper behavior until updated.

Import actual completed execution reports using the [tool-library workflow](tool-library-20261011.md). An audit may complete with findings. Standalone compilation, synthetic tests, opening a window and importing a catalog do not establish completed use of a tool on an avatar. New catalog definitions therefore have no last-use date.

## Validation status

The corrected fan/sparkle changes received independent source review for controller isolation, partial installation handling, prefab override persistence, material copies and rerun behavior. The editor source preview is checked with Unity 2022.3.22f1's standalone Roslyn compiler and the existing browser catalog regression suite. These are source checks, not a Unity import or live VRChat test of the published package.

After installation, wait for Unity compilation. Use a review copy, exercise the real menus, save and reopen the scene, verify persisted references and repeat the operation to check duplicates. Confirm the visible result in Unity and the relevant avatar emulator before recording a successful feature result. Never reuse an old package's usage receipt as evidence for this source revision.

The golden material and chain-weight source passed 30 synthetic checks inside Unity 2022.3.22f1, including actual asset creation, persistent texture references, selective weights and Undo. Standard shader fixtures were used; Poiyomi unlocking, visible gold, real shoe topology and rig movement remain unverified. The Gunsmith companion passed 42 pure-data checks and independent ownership-source review; this does not establish live scene behavior. Neither result creates a real-avatar usage receipt.
