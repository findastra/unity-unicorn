// rainbow-maker-tool (Unity half): give a group of surfaces the Rainbow Maker's cut-crystal glass.
// Claude Fable 5.1, 2026-10-23. Astra: "a tool that creates the glass texture thing for a group of surfaces like the
// crystal." It changes only what the surfaces are made of; geometry is another tool's job (Astra's planned
// geographic-statue-converter turns something into geometry; the two together make a crystal wolf).
//
// Select any objects in the Hierarchy, then  Tools > Unity Unicorn > rainbow-maker-tool > Make selection crystal.
// Every renderer under the selection gets one shared material on the UnityUnicorn/RainbowMakerGlass shader, saved as
// Assets/UnityUnicorn/RainbowMakerTool/rainbow-maker-glass-<date>.mat and reused on later runs (change its sliders
// there: tint, refraction, colour split, body opacity, facet steepness, sun sparkle). Meshes are not touched. Undo
// brings the old materials back. A reflection probe in the room is what the glass looks through; without one it
// reads the skybox. Flat-shaded meshes read as cut crystal; smooth ones as blown glass.
// Install: this file in an Editor folder, rainbow-maker-tool-20261023.shader anywhere under Assets outside Editor.
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RainbowMakerTool20261023
{
    const string Dir = "Assets/UnityUnicorn/RainbowMakerTool";
    const string MatPath = Dir + "/rainbow-maker-glass-20261023.mat";
    const string ShaderName = "UnityUnicorn/RainbowMakerGlass";

    [MenuItem("Tools/Unity Unicorn/rainbow-maker-tool/Make selection crystal")]
    public static void MakeCrystal()
    {
        if (Shader.Find(ShaderName) == null) { Debug.LogError("rainbow-maker-tool: install rainbow-maker-tool-20261023.shader under Assets first."); return; }
        var renderers = Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Distinct().ToList();
        if (renderers.Count == 0) { Debug.LogWarning("rainbow-maker-tool: nothing with a renderer in the selection."); return; }
        var mat = Material();
        Undo.SetCurrentGroupName("Make selection crystal");
        var group = Undo.GetCurrentGroup();
        foreach (var r in renderers)
        {
            Undo.RecordObject(r, "crystal glass");
            r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
        }
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        Debug.Log("rainbow-maker-tool: " + renderers.Count + " surface" + (renderers.Count == 1 ? "" : "s") + " made crystal. Material: " + MatPath);
    }

    [MenuItem("Tools/Unity Unicorn/rainbow-maker-tool/Make selection crystal", true)]
    static bool HasSelection() { return Selection.gameObjects.Length > 0; }

    static Material Material()
    {
        if (!AssetDatabase.IsValidFolder("Assets/UnityUnicorn")) AssetDatabase.CreateFolder("Assets", "UnityUnicorn");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/UnityUnicorn", "RainbowMakerTool");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m != null) return m;
        m = new Material(Shader.Find(ShaderName));
        // the Rainbow Maker's values: lead-crystal refraction, a strong colour split, steep facets, sun sparkle
        m.SetColor("_Tint", new Color(0.96f, 0.98f, 1f));
        m.SetFloat("_IOR", 1.55f); m.SetFloat("_Dispersion", 0.12f); m.SetFloat("_BodyAlpha", 0.4f);
        m.SetFloat("_Cut", 2.5f); m.SetFloat("_Sparkle", 10f); m.SetFloat("_SparkleSharpness", 40f);
        AssetDatabase.CreateAsset(m, MatPath);
        return m;
    }
}
