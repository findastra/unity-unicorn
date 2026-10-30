// iris-tool (Astra, 2026-10-27: "Can we call this iris-tool so we can use it on other avis?"). Puts an iris pack on an
// avatar's eyes behind a menu toggle, keeping its pupils, eye tracking and RGB. Avatars draw irises two ways:
//  - Texture eyes (Poppy: a Poiyomi or lilToon material with an iris picture). The iris is cut out of the pack and
//    stretched onto the eye texture's iris oval; the star copy is the eye material with that picture.
//  - Love Eye eyes (Obsi: Iamres' procedural Love Eye Shader, locked per material). The iris is drawn by the shader,
//    with parallax that follows the camera and pupil shapes, so a copy of the avatar's own locked shader gets a picture
//    layer under its pupil. The paid shader's code is never shipped here: the tool patches the copy in the project.
// The toggle (VRCFury, saved, off by default) swaps the eye slots to the star copies; the avatar's own materials stay.
// Window: Tools > Unity Unicorn > iris-tool. Made with Claude Opus 5.5 (claude-opus-5-5), 2026-10-27, first used on Obsi.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IrisTool20261027
{
    public const string Version = "0.1.0";

    // New files are named <stem>-YYYYMMDD.<ext> (Astra's naming rule, her local date); running again reuses the file a
    // first run made, so a shader name is never defined twice.
    public static string Dated(string folder, string stem, string ext)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            var found = Directory.GetFiles(folder, stem + "-*." + ext).Select(f => f.Replace('\\', '/'))
                .FirstOrDefault(f => Regex.IsMatch(Path.GetFileName(f), "^" + Regex.Escape(stem) + @"-\d{8}\." + ext + "$"));
            if (found != null) return found;
        }
        return folder + "/" + stem + "-" + DateTime.Now.ToString("yyyyMMdd") + "." + ext;
    }

    static string Slug(string s) => Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    public static bool IsLoveEye(Material m) => m && m.shader && m.shader.name.StartsWith("Iamres/Love Eye Shader");

    // ---------------------------------------------------------------- texture eyes

    // A size x size picture: the pack's `from` rectangle (pixels, top-left origin, as an image editor shows it) drawn
    // stretched into `to`; the rest is black. Works on textures that are not marked readable.
    public static Texture2D CutIris(Texture2D pack, RectInt from, int size, RectInt to)
    {
        var rt = RenderTexture.GetTemporary(pack.width, pack.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(pack, rt); RenderTexture.active = rt;
        var src = new Texture2D(pack.width, pack.height, TextureFormat.RGBA32, false);
        src.ReadPixels(new Rect(0, 0, pack.width, pack.height), 0, 0); src.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        // `from` is in the picture file's own pixels; Unity may have imported it smaller (Max Size)
        int srcW = pack.width, srcH = pack.height;
        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(pack)) is TextureImporter ti) ti.GetSourceTextureWidthAndHeight(out srcW, out srcH);
        var px = new Color32[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 255);
        for (int y = 0; y < to.height; y++)
            for (int x = 0; x < to.width; x++)
            {
                int dx = to.x + x, dy = to.y + y; if (dx < 0 || dy < 0 || dx >= size || dy >= size) continue;
                float u = (from.x + (x + 0.5f) * from.width / to.width) / srcW;
                float v = 1f - (from.y + (y + 0.5f) * from.height / to.height) / srcH;
                px[(size - 1 - dy) * size + dx] = src.GetPixelBilinear(u, v);
            }
        UnityEngine.Object.DestroyImmediate(src);
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false); t.SetPixels32(px); t.Apply();
        return t;
    }

    public static Texture2D SavePng(Texture2D t, string assetPath)
    {
        File.WriteAllBytes(assetPath, t.EncodeToPNG());
        AssetDatabase.ImportAsset(assetPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    // ---------------------------------------------------------------- Love Eye eyes

    // Writes "Iris Tool/Love Eye Stars/<name>" next to `folder` from a locked Love Eye shader: the same eye, plus
    // _IrisTex under the pupil. The picture follows the parallax uv (the eye tracking) and turns with the material's
    // own hue settings (a plain hue rotation: Love Eye's HSV round trip is not neutral at 0).
    public static Shader MakeLoveEyeStars(Shader locked, string folder)
    {
        var srcPath = AssetDatabase.GetAssetPath(locked);
        var text = File.ReadAllText(srcPath);
        var name = locked.name.Substring(locked.name.LastIndexOf('/') + 1);
        var sfx = locked.name.Contains("Locked/") ? "_" + name : "";
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        var outp = new StringBuilder(); string[] block = null;
        foreach (var raw in lines)
        {
            var ln = raw; var t = ln.Trim();
            if (ln.StartsWith("Shader \"")) ln = "Shader \"Iris Tool/Love Eye Stars/" + name + "\" {";
            else if (t.StartsWith("Properties {"))
            {
                int k = ln.TrimEnd().LastIndexOf('}');
                ln = ln.Substring(0, k) + "_IrisTex(\"Iris Texture\", 2D) = \"black\" {} Iris_Blend(\"Iris Blend\", Range(0, 1)) = 1 "
                    + "Iris_Scale(\"Iris Scale\", Range(0.3, 3)) = 1 Iris_Bright(\"Iris Brightness\", Range(0, 4)) = 1 "
                    + "[Toggle] Iris_Follow_Hue(\"Iris Follows Hue\", Float) = 1 " + ln.TrimEnd().Substring(k);
            }
            else if (t.StartsWith("#include") && t.Contains("Iamres.cginc"))
            {
                var rel = Regex.Match(t, "\"([^\"]+)\"").Groups[1].Value;
                var inc = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(srcPath), rel)).Replace('\\', '/');
                var assets = Path.GetFullPath("Assets").Replace('\\', '/');
                ln = "        #include \"Assets" + inc.Substring(assets.Length) + "\"";
            }
            if (t.StartsWith("col.rgb += Col_2" + sfx)) { if (block == null) throw new Exception("Love Eye layout not recognised (no iris colour line)"); foreach (var b in block) outp.Append(b).Append('\n'); }
            outp.Append(ln).Append('\n');
            if (t.StartsWith("uniform float4 TileOffset;"))
                outp.Append("        sampler2D _IrisTex; uniform float Iris_Blend, Iris_Scale, Iris_Bright, Iris_Follow_Hue;\n");
            if (t.StartsWith("col = lerp(float4(Col" + sfx + ", 1.0) / rz"))
            {
                var body = ln.TrimEnd();
                if (!body.EndsWith("));")) throw new Exception("Love Eye layout not recognised (pupil line)");
                // the lerp's last argument is the pupil mask: from the smoothstep to the end, less the closing ");"
                var mask = body.Substring(body.IndexOf("smoothstep(0.05, -0.05, sdHeart("));
                mask = mask.Substring(0, mask.Length - 2);
                block = new[] {
                    "            // iris-tool: the iris picture under the pupil; the parallax uv keeps the eye tracking",
                    "            float irisPupil = " + mask + ";",
                    "            float3 irisCol = tex2D(_IrisTex, uv * (1.25 / Iris_Scale) + 0.5).rgb;",
                    "            if (Iris_Follow_Hue == 1) { float irisHue = (Main_Hue_Offset" + sfx + " + (USE_HUE" + sfx + " == 1 ? _Time.y * 0.1 * HUE_Speed_1" + sfx + " : 0.0)) * lerp(1.0, -1.0, saturate(Invert_Hue" + sfx + ")) * 6.2831853;",
                    "                float3 irisK = float3(0.57735, 0.57735, 0.57735); float irisC = cos(irisHue);",
                    "                irisCol = irisCol * irisC + cross(irisK, irisCol) * sin(irisHue) + irisK * dot(irisK, irisCol) * (1.0 - irisC); }",
                    "            col.rgb = lerp(col.rgb, irisCol * Iris_Bright / (1.0 + 35.0 * Bright), Iris_Blend * (1.0 - irisPupil));" };
            }
        }
        if (block == null) throw new Exception("Love Eye layout not recognised: " + srcPath);
        var outPath = Dated(folder, "love-eye-stars-" + Slug(name), "shader");
        // rewriting an unchanged shader would reimport it and wipe values just set on materials that use it
        var code = outp.ToString().TrimEnd('\n') + "\n";
        if (!File.Exists(outPath) || File.ReadAllText(outPath) != code)
        {
            File.WriteAllText(outPath, code);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<Shader>(outPath);
    }

    // ---------------------------------------------------------------- both

    // Every value of `from`'s shader properties onto `to`. Not CopyPropertiesFromMaterial: after it, Unity keeps no
    // value for a property `from` lacks (the iris ones), so the picture would not stay set.
    static void CopyValues(Material from, Material to)
    {
        var s = from.shader;
        for (int i = 0; i < s.GetPropertyCount(); i++)
        {
            var n = s.GetPropertyName(i); if (!to.HasProperty(n)) continue;
            switch (s.GetPropertyType(i))
            {
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    to.SetTexture(n, from.GetTexture(n)); to.SetTextureScale(n, from.GetTextureScale(n)); to.SetTextureOffset(n, from.GetTextureOffset(n)); break;
                case UnityEngine.Rendering.ShaderPropertyType.Color: to.SetColor(n, from.GetColor(n)); break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector: to.SetVector(n, from.GetVector(n)); break;
                case UnityEngine.Rendering.ShaderPropertyType.Int: to.SetInt(n, from.GetInt(n)); break;
                default: to.SetFloat(n, from.GetFloat(n)); break;
            }
        }
    }

    // The star copy of one eye material, saved in `folder`. Texture eyes get the iris as their main texture (and as the
    // emission map when they have one and `emission` is given); Love Eye eyes get the patched shader with every value
    // of their own material carried over.
    public static Material StarCopy(Material eye, Texture2D iris, Texture2D emission, string folder)
    {
        var path = Dated(folder, Slug(eye.name) + "-stars", "mat");
        // the shader first: making it may reimport it, which would undo values set on a material before
        var shader = IsLoveEye(eye) ? MakeLoveEyeStars(eye.shader, folder) : eye.shader;
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        if (IsLoveEye(eye))
        {
            m.shader = shader; CopyValues(eye, m); m.shaderKeywords = eye.shaderKeywords; m.renderQueue = eye.renderQueue;
            m.SetFloat("Iris_Blend", 1); m.SetFloat("Iris_Scale", 1); m.SetFloat("Iris_Bright", 1); m.SetFloat("Iris_Follow_Hue", 1);
            m.SetTexture("_IrisTex", iris);
        }
        else
        {
            m.shader = eye.shader; m.CopyPropertiesFromMaterial(eye);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", iris);
            if (emission && m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap")) m.SetTexture("_EmissionMap", emission);
        }
        m.name = eye.name + " Stars";
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        return m;
    }

    // A child "<label>" on the avatar with a VRCFury toggle at `menuPath` (saved, off by default) whose on state swaps
    // the given material slots of `renderer` to `stars`. Re-running replaces the toggle. Saves the scene.
    public static string AddToggle(GameObject avatar, Renderer renderer, int[] slots, Material[] stars, string menuPath, string folder)
    {
        var path = AnimationUtility.CalculateTransformPath(renderer.transform, avatar.transform);
        var clip = new AnimationClip();
        for (int i = 0; i < slots.Length; i++)
        {
            var b = EditorCurveBinding.PPtrCurve(path, renderer.GetType(), "m_Materials.Array.data[" + slots[i] + "]");
            AnimationUtility.SetObjectReferenceCurve(clip, b, new[] { new ObjectReferenceKeyframe { time = 0, value = stars[i] } });
        }
        var label = menuPath.Substring(menuPath.LastIndexOf('/') + 1);
        var clipPath = Dated(folder, Slug(label) + "-on", "anim");
        AssetDatabase.DeleteAsset(clipPath); AssetDatabase.CreateAsset(clip, clipPath);
        var old = avatar.transform.Find(label); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject(label); holder.transform.SetParent(avatar.transform, false);
        // VRCFury's public API (com.vrcfury.api) is not auto-referenced by Assembly-CSharp-Editor, so it is reached by reflection
        var api = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("com.vrcfury.api.FuryComponents")).FirstOrDefault(x => x != null);
        if (api == null) throw new Exception("VRCFury is not in this project");
        var t = api.GetMethod("CreateToggle").Invoke(null, new object[] { holder });
        t.GetType().GetMethod("SetMenuPath").Invoke(t, new object[] { menuPath });
        t.GetType().GetMethod("SetSaved").Invoke(t, null);
        var acts = t.GetType().GetMethod("GetActions").Invoke(t, null);
        acts.GetType().GetMethod("AddAnimationClip").Invoke(acts, new object[] { clip });
        EditorSceneManager.MarkSceneDirty(avatar.scene); EditorSceneManager.SaveScene(avatar.scene); AssetDatabase.SaveAssets();
        return menuPath + " on " + avatar.name + ": swaps " + renderer.name + " slots " + string.Join(", ", slots) + "; scene saved";
    }
}

public class IrisToolWindow20261027 : EditorWindow
{
    GameObject avatar; Renderer renderer; Texture2D pack, packEmission;
    bool[] pick = new bool[0];
    RectInt from = new RectInt(0, 0, 1024, 1024), to = new RectInt(0, 0, 1024, 1024);
    int size = 1024; string menuPath = "Eyes/Star Irises", folder = "Assets/iris-tool", note = "";
    Vector2 scroll;

    [MenuItem("Tools/Unity Unicorn/iris-tool")]
    static void Open() => GetWindow<IrisToolWindow20261027>("iris-tool").minSize = new Vector2(380, 420);

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Put an iris pack on an avatar's eyes behind a menu toggle. Pupils, eye tracking and RGB stay; the avatar's own materials are not changed.", MessageType.None);
        avatar = (GameObject)EditorGUILayout.ObjectField("Avatar", avatar, typeof(GameObject), true);
        renderer = (Renderer)EditorGUILayout.ObjectField("Mesh with the eyes", renderer, typeof(Renderer), true);
        if (renderer)
        {
            var mats = renderer.sharedMaterials;
            if (pick.Length != mats.Length) pick = mats.Select(m => IrisTool20261027.IsLoveEye(m) || (m && m.name.ToLowerInvariant().Contains("eye") && !m.name.ToLowerInvariant().Contains("eyeball") && !m.name.ToLowerInvariant().Contains("lash") && !m.name.ToLowerInvariant().Contains("brow"))).ToArray();
            EditorGUILayout.LabelField("Iris slots", EditorStyles.boldLabel);
            for (int i = 0; i < mats.Length; i++)
                pick[i] = EditorGUILayout.ToggleLeft(i + ": " + (mats[i] ? mats[i].name + (IrisTool20261027.IsLoveEye(mats[i]) ? "  (Love Eye)" : "") : "none"), pick[i]);
        }
        pack = (Texture2D)EditorGUILayout.ObjectField("Iris pack picture", pack, typeof(Texture2D), false);
        packEmission = (Texture2D)EditorGUILayout.ObjectField("Its emission map (optional)", packEmission, typeof(Texture2D), false);
        from = EditorGUILayout.RectIntField(new GUIContent("Cut from (pixels, top-left)", "The one iris in the pack picture, as an image editor shows it."), from);
        size = EditorGUILayout.IntPopup("Picture size", size, new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
        to = EditorGUILayout.RectIntField(new GUIContent("Place at (pixels, top-left)", "Texture eyes: the iris oval on the eye texture. Love Eye eyes: the whole square."), to);
        menuPath = EditorGUILayout.TextField("Menu toggle", menuPath);
        folder = EditorGUILayout.TextField("Save in", folder);
        using (new EditorGUI.DisabledScope(!avatar || !renderer || !pack || !pick.Any(p => p)))
            if (GUILayout.Button("Make star copies and the toggle")) Run();
        if (note.Length > 0) EditorGUILayout.HelpBox(note, MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    void Run()
    {
        try
        {
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            var stem = System.Text.RegularExpressions.Regex.Replace(pack.name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            var iris = IrisTool20261027.SavePng(IrisTool20261027.CutIris(pack, from, size, to), IrisTool20261027.Dated(folder, stem + "-iris", "png"));
            var em = packEmission ? IrisTool20261027.SavePng(IrisTool20261027.CutIris(packEmission, from, size, to), IrisTool20261027.Dated(folder, stem + "-iris-emission", "png")) : null;
            var mats = renderer.sharedMaterials;
            var slots = Enumerable.Range(0, mats.Length).Where(i => pick[i] && mats[i]).ToArray();
            var stars = slots.Select(i => IrisTool20261027.StarCopy(mats[i], iris, em, folder)).ToArray();
            note = IrisTool20261027.AddToggle(avatar, renderer, slots, stars, menuPath, folder);
        }
        catch (Exception e) { note = "Stopped: " + e.Message; Debug.LogException(e); }
    }
}
