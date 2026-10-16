# Liminal glass tools

Made by Astra. Prepared with Codex (GPT-6), 2026-10-15.

Two Unity Editor tools provide a reusable starting point for prismatic sculptures and bubble chairs. They run in Unity's Built-in Render Pipeline. Unity Unicorn lists their instructions; the browser catalog does not execute them.

## Install and open

Copy `scripts/liminal-glass-workshop-20261015.cs` into an `Editor` folder beneath your project's `Assets`. Copy `scripts/prismatic-glass-20261015.shader` into a folder beneath `Assets` outside `Editor`. Unity's required `Assets`, `Editor` and generated `.meta` names are standard Unity contracts; authored source and exported assets use dated names.

After compilation, open either:

- **Tools > Unity Unicorn > Glass Animal Tool**
- **Tools > Unity Unicorn > Bubble Chair Blower**

Both open the same workshop, with two tabs. Drag in an object, build the preview, adjust the controls, drag the preview to orbit, and export a new prefab into an existing folder within `Assets`. Export writes a fresh dated folder with mesh and material dependencies; it does not edit the input asset.

## Glass animal tool

The source field accepts a Mesh, imported model or prefab. It copies mesh geometry, bakes hierarchy transforms, normalizes the height and puts the pivot on the floor. The faceting option splits triangles for flat normals. Disabling it retains the supplied normals. Skinned renderers are baked into static sculptures; animation and source scripts are not carried over. Imported static meshes must have Read/Write enabled.

Controls include target height, tint, opacity, rainbow strength, facet contrast and reflection strength. The shader exposes spectrum frequency and edge highlights in the exported material inspector. Existing triangles determine facet size: use a suitably simplified source model for broad gemstone facets. The tool does not generate an anatomically correct animal from a species name.

The original shader combines transparency, reflection-probe sampling and a view-dependent spectrum approximation. It has no screen-space grab, physical refraction, dispersion tracing or realtime reflection renderer. Provide a baked reflection probe in the destination room. Transparent overlap and triangle sorting still require a visual check. Stereo-instancing macros are included, but headset validation remains open.

The visual direction came from observing a prismatic wolf in VRChat. No original world mesh, shader or cached asset bundle is included or extracted. This is a new approximation, not a verified exact copy.

## Bubble chair blower

Controls include diameter, front opening angle, filling thickness, glass tint, rim color and filling color. A custom filling Mesh/model can replace the generated rounded cushions; it becomes static geometry and is fitted to the available width. Inspect unusually shaped custom fillings for intersections.

The spherical shell has a real missing front cap, a rim, a base, rounded cushions, a non-convex static shell collider and a seat-support box collider. The `seat-anchor-add-vrcstation` transform marks a proposed seating position. Add a **VRCStation** using the installed VRChat SDK and test entry/exit and avatar scale. This tool does not install the SDK, create a working station or implement a chair-blowing gameplay interaction. It is an editor generator.

The chair is intended to be stationary. Do not add a non-kinematic Rigidbody to the non-convex shell collider. The basic geometry is original, and its exact appearance has not yet been compared with the requested reference chair.

## Validation

The integration entry `Astra.LiminalTools.GlassIntegration.Run` is in `test/liminal-glass-integration-20261015.cs`. Copy it into an Editor folder in a disposable Built-in project containing the two tools, then run:

```text
Unity -batchmode -quit -force-d3d11 -projectPath <scratch-project> -executeMethod Astra.LiminalTools.GlassIntegration.Run -liminalOutput <private-output-folder> -logFile <private-log>
```

The validation fixture expects the tool files at `Assets/liminal-tools/Editor/liminal-glass-workshop-20261015.cs` and `Assets/liminal-tools/prismatic-glass-20261015.shader` to export the package. It replaces the scratch scene, creates synthetic assets, renders a preview and writes a validation receipt. Do not run it in a working world project.

Checks cover source preservation, nested/mirrored transforms, height and pivot normalization, non-degenerate triangle data, multiple chair openings, custom filling, open-shell collision, prefab dependency persistence, shader compilation/support and a real Unity render. This does not certify a VRChat upload, headset performance, multiplayer interaction, original asset match or VRCStation behavior.

Unity 2022.3.22f1 with Direct3D 11 passed these checks on 2026-10-15. A visual cushion seam found after the first run was corrected and the render was repeated. Per-triangle assertions are geometry checks, not thousands of distinct user-flow tests. The catalog regression suite passed 12 checks, with one optional local-sidecar check skipped. In the actual editor UI, rebuilding the chair, changing its opening angle and dragging to orbit were verified visually. The preview uses Unity's preview-scene lighting and fits the complete chair in view. The animal model drag-and-drop flow and VRChat seating still require dedicated interactive validation.

## Sources

- [Unity: custom shaders and single-pass instancing](https://docs.unity3d.com/2022.3/Documentation/Manual/SinglePassInstancing.html)
- [Unity: SaveAsPrefabAsset](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.SaveAsPrefabAsset.html)
- [Unity: ShaderHasError](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ShaderUtil.ShaderHasError.html)

No third-party animal models, textures or recordings are shipped in these sources. Use assets you own or have permission to use as tool inputs, and keep their provenance with the destination project.
