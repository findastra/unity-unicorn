# Avatar effects tool program

Requested by Astra on 2026-10-12. Prepared with OpenAI Codex (GPT-6).

This program extends Unity Unicorn's reusable avatar tools and introduces Dubstep Gunsmith as a separate catalog of third-party creator gun assets, reference videos and planned avatar loadouts. It records requested work and validation requirements. It does not establish that an avatar has been edited, a tool has run, or a website is live.

Private avatar names, local project inventory, account details and execution reports belong in the private catalog or local project records. Follow the [tool-library contract](tool-library-20261011.md) when adding tools and recording actual use.

## Work order

1. Verify the current source avatar project, exact scene and prefab dependencies. Preserve a recoverable baseline before changing assets.
2. Complete and validate the original avatar's shared changes. Capture the resulting source version and evidence before using it as the base for a new variant.
3. Prepare a separate project from the existing gold variant's source. Add face tracking and repair its shoes and materials, then carry across the applicable validated changes from the original avatar.
4. Create the new blood variant from the secured original avatar only after the shared changes are complete. Keep it in a separate Unity project.

The current source projects, asset selections and completed-work evidence must be resolved privately. Historical snapshots alone do not prove the current project's contents.

## Shared avatar tools

| Tool area | Requested behavior | Required checks |
|---|---|---|
| Neck blending | Use and improve the existing [neck blender](neck-blender-20261011.md) as needed. Its guide records a previous texture and lighting correction; fresh validation is still required on the intended current avatar. | Confirm the prefab's real FBX and materials, keep backups, and inspect the neck from below the chin, the front and the back after Unity import. |
| Phawksea's Sparks | Locate the existing sparkle tool, add foot sparkles, and add an option that cycles continuously through RGB. Preserve the hand effects and their controls. | Identify the exact installed asset and existing tool before changing them. Check attachment, foot movement, trail behavior, toggle behavior and a complete continuous color cycle. |
| Fans | Locate the existing fan tool, add every remaining supplied stock fan color, repair the RGB fan that currently appears silver, and submit the resulting reusable tool to Unity Unicorn. | Inventory the current avatar and supplied stock colors before adding options. Diagnose shader, material, animation and menu behavior from the actual project. Verify each supplied choice and a complete RGB cycle in Unity. |
| Bending adder | Create or update one reusable workflow for blood, water and sparkle bending, including the requested sparkle bending addition. | Preserve separate effect choices and left/right controls. Check bone placement, effect toggles, animation references and dependency packaging on the intended avatar. |
| Star gun and star shooter | Investigate the existing sets that have not worked and begin a reusable diagnostic and setup tool. | Identify the exact prefabs and required packages. Check missing scripts, menu and parameter wiring, animator behavior, firing triggers and particle visibility before claiming a repair. |

The complete owner-supplied stock color selection and the current RGB fan implementation still need to be matched to the working project. Do not substitute a guessed palette or treat a stock package inventory as the avatar's current menu.

## Gold variant tools

Face tracking is the confirmed meaning of FT. Review the face transfer workflow currently under development before reusing it. Its current guide describes synthetic-project testing and no completed real-avatar run; that is not evidence that face tracking is installed or working on this variant. Link its guide here after that separate change is published.

- Create a golden paint tool and increase the selected gold treatment while preserving the intended texture detail and material boundaries.
- Repair the shoe dangles and create a reusable dangle repair tool. Preserve open chain links: compressing or collapsing the mesh until the chains seal is not an acceptable result. Inspect mesh geometry, bone weights, deformation and any physics separately, then validate their combined movement.
- Incorporate the applicable original-avatar edits after their source state is secured and validated.

## Blood variant

Create this project after the original avatar's shared changes are secured. Explore blood trails and blood eyes, and inspect the wings for incorrect weighting or missing movement. These are proposed edits until the intended assets, appearance and movement have been examined. Preserve the original avatar and the separate gold project throughout this work.

## Dubstep Gunsmith integration

Dubstep Gunsmith is the requested separate website for organizing third-party creator gun assets, their source listings and videos, planned avatar loadouts, and known or unknown performance costs. Unity Unicorn should expose it as an external tool while the catalog itself is hosted on that website. Weapon creation and automatic optimization are outside this catalog's scope.

[Dubstep Gunsmith](https://dubstep-gunsmith.audra-volk-1606.chatgpt.site) was published owner-private on 2026-10-12. It provides reference videos and stills, scoped credits, source links, catalog entries, separate avatar-plan memberships, known/unknown cost totals, JSON backups and planned-loadout manifests. Data stays in the current browser. Reference identification is marked unverified unless confirmed; researched shop listings are separate from exact video matches. The hosted catalog contains private user references and is not bundled in this public repository.

The planned workflow is to associate each collected asset with its creator, source listing, reference video, dependency notes, planned avatar loadouts and measured costs where available. Unknown performance costs remain unknown. The source-preview Unity companion can map locally acquired prefabs and preview managed additions/removals, but it still needs live scene validation. The website alone does not edit a Unity project. Measurements must retain their test conditions in private records. A catalog entry or video alone does not demonstrate that the asset works on a particular avatar.

## Editor source preview

The [editor tool guide](avatar-effects-editor-tools-20261012.md) describes the reusable source preview for supplied fan variants, RGB materials, hand/foot sparkles, bending templates and read-only star diagnostics. Golden material copying and explicit chain-island weighting are also included as source previews; the actual gold avatar and shoe repair remain unverified. The preview includes no third-party asset files or private avatar runner.

## Completion and integration evidence

Add reusable implementations and dated definitions to Unity Unicorn only when their status can be described accurately. Label planned workflows as planned and unfinished implementations as unfinished. Keep completed use dates Unknown until a real execution report or usage receipt exists, and keep failed attempts separate from completed runs.

For a finished avatar change, retain a recoverable baseline, a clear change record, Unity import or compile results and checks of the visible behavior. Runtime or VRChat validation should be stated separately from source inspection or synthetic tests. Update the private local catalog or sidecar after actual work without replacing existing custom entries or run history.
