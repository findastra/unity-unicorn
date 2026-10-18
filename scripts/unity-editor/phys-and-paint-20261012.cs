// Unity Unicorn: rgb-painter, hair-weighter and spring-joint-strapper. A pet app by Astra.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityUnicorn
{
    // ---------------- rgb-painter ----------------
    // Makes an RGB copy of the selected prop: duplicates its materials as "<name> RGB" and turns on Poiyomi hue
    // cycling for the main colour and every matcap, cubemap and emission slot that is on. The originals are
    // never changed. Locked Poiyomi materials are unlocked on the copy only (Poiyomi relocks them on upload).
    public class RgbPainter : EditorWindow
    {
        GameObject target; float speed = 0.15f; bool duplicateObject = true, tint = true; string result = "";

        [MenuItem("Tools/Unity Unicorn/rgb-painter", false, 40)]
        static void Open() { var w = GetWindow<RgbPainter>(true, "Unity Unicorn · rgb-painter"); w.target = Selection.activeGameObject; w.minSize = new Vector2(380, 220); }

        void OnGUI()
        {
            target = (GameObject)EditorGUILayout.ObjectField("Prop", target, typeof(GameObject), true);
            speed = EditorGUILayout.Slider("Cycle speed", speed, 0.02f, 1f);
            duplicateObject = EditorGUILayout.ToggleLeft("Make an RGB copy of the object too (keeps the original)", duplicateObject);
            tint = EditorGUILayout.ToggleLeft("Tint silver/white parts so the colour cycle shows (hue shift can't colour grey)", tint);
            GUI.enabled = target;
            if (GUILayout.Button("Paint RGB", GUILayout.Height(30))) result = Paint();
            GUI.enabled = true;
            if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        public static string PaintInPlace(GameObject go, float speed = 0.15f, bool tint = true)
        { var w = CreateInstance<RgbPainter>(); w.target = go; w.duplicateObject = false; w.speed = speed; w.tint = tint; var r = w.Paint(); DestroyImmediate(w); return r; }

        string Paint()
        {
            var notes = new List<string>();
            var obj = target;
            if (duplicateObject)
            {
                obj = Instantiate(target, target.transform.parent);
                obj.name = Regex.Replace(target.name, "^(Silver|Gold|Purple)( \\w+)?", "RGB").Trim();
                if (obj.name == target.name) obj.name = target.name + " RGB";
                Undo.RegisterCreatedObjectUndo(obj, "rgb-painter");
                EffectAssets.Changed(obj);
                notes.Add($"copied {target.name} as {obj.name}");
            }
            var done = new Dictionary<Material, Material>();
            var outputFolder = EffectAssets.Root + "/rgb-materials";
            var settingsKey = speed.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + tint;
            foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (!m) continue;
                    if (!done.TryGetValue(m, out var copy))
                    {
                        if (AssetDatabase.GetAssetPath(m).StartsWith(outputFolder + "/", StringComparison.Ordinal) && m.GetTag("UnityUnicornRgbV2", false, "") == settingsKey)
                        { done[m] = m; notes.Add(m.name + ": RGB settings already installed"); continue; }
                        copy = new Material(m) { name = m.name + " RGB" };
                        try
                        {
                            Unlock(copy, notes);
                            if (!copy.HasProperty("_MainHueShiftToggle")) throw new InvalidOperationException(copy.name + ": this shader does not expose Poiyomi hue cycling.");
                            Cycle(copy, speed, notes);
                            if (tint) TintActiveLayers(copy);
                            MaterialEditor.ApplyMaterialPropertyDrawers(copy);
                        }
                        catch { DestroyImmediate(copy); throw; }
                        copy.SetOverrideTag("UnityUnicornRgbV2", settingsKey);
                        var path = EffectAssets.NewPath(outputFolder, copy.name, "mat");
                        AssetDatabase.CreateAsset(copy, path);
                        done[m] = copy;
                    }
                    mats[i] = copy;
                }
                Undo.RecordObject(r, "rgb-painter"); r.sharedMaterials = mats; EffectAssets.Changed(r);
            }
            AssetDatabase.SaveAssets();
            notes.Add($"{done.Count} materials painted RGB");
            UnicornCore.Report("rgb-painter", done.Count > 0 ? "completed" : "failed", $"RGB on {obj.name}", notes);
            Selection.activeGameObject = obj;
            return string.Join("\n", notes);
        }

        static void Unlock(Material m, List<string> notes)
        {
            var original = m.GetTag("OriginalShader", false, "");
            if (string.IsNullOrEmpty(original)) return;
            var sh = Shader.Find(original);
            if (!sh)
            {
                var guid = m.GetTag("OriginalShaderGUID", false, "");
                if (!string.IsNullOrEmpty(guid)) sh = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            }
            if (!sh) throw new InvalidOperationException($"{m.name}: the original shader {original} is unavailable; no RGB material was assigned.");
            m.shader = sh;
            m.SetOverrideTag("OriginalShader", "");
            m.SetOverrideTag("OriginalShaderGUID", "");
            if (m.HasProperty("_ShaderOptimizerEnabled")) m.SetFloat("_ShaderOptimizerEnabled", 0);
            notes.Add($"{m.name}: unlocked copy (Poiyomi relocks it on upload)");
        }

        static void Cycle(Material m, float speed, List<string> notes)
        {
            void On(string toggle, string spd)
            {
                if (!m.HasProperty(toggle)) return;
                m.SetFloat(toggle, 1);
                if (m.HasProperty(spd)) m.SetFloat(spd, speed);
            }
            // Poiyomi compiles main hue adjustment behind this parent feature/keyword.
            if (m.HasProperty("_MainColorAdjustToggle")) m.SetFloat("_MainColorAdjustToggle", 1);
            On("_MainHueShiftToggle", "_MainHueShiftSpeed");
            foreach (var p in new[] { "", "2", "3", "4" })
                if (m.HasProperty($"_Matcap{p}Enable") && m.GetFloat($"_Matcap{p}Enable") > 0)
                    On($"_Matcap{p}HueShiftEnabled", $"_Matcap{p}HueShiftSpeed");
            if (m.HasProperty("_CubeMapEnabled") && m.GetFloat("_CubeMapEnabled") > 0) On("_CubeMapHueShiftEnabled", "_CubeMapHueShiftSpeed");
            foreach (var p in new[] { "", "1", "2", "3" }) On($"_EmissionHueShiftEnabled{p}", $"_EmissionHueShiftSpeed{p}");
            if (!m.HasProperty("_MainHueShiftToggle")) notes.Add($"{m.name}: not a Poiyomi material, hue cycling skipped");
        }

        static void TintActiveLayers(Material m)
        {
            void TintNeutral(string property)
            {
                if (!m.HasProperty(property)) return;
                var color = m.GetColor(property);
                Color.RGBToHSV(color, out _, out var saturation, out var value);
                if (saturation >= 0.08f) return;
                // Keep the authored brightness and alpha; add saturation so hue has something to rotate.
                value = Mathf.Max(value, 0.05f);
                m.SetColor(property, new Color(value, value * 0.15f, value * 0.15f, color.a));
            }
            TintNeutral("_Color");
            foreach (var suffix in new[] { "", "2", "3", "4" })
                if (m.HasProperty($"_Matcap{suffix}Enable") && m.GetFloat($"_Matcap{suffix}Enable") > 0) TintNeutral($"_Matcap{suffix}Color");
            if (m.HasProperty("_CubeMapEnabled") && m.GetFloat("_CubeMapEnabled") > 0) TintNeutral("_CubeMapColor");
            foreach (var suffix in new[] { "", "1", "2", "3" })
                if (m.HasProperty("_EnableEmission" + suffix) && m.GetFloat("_EnableEmission" + suffix) > 0) TintNeutral("_EmissionColor" + suffix);
        }
    }

    // ---------------- PhysBone helpers ----------------
    static class PB
    {
        public static List<Component> On(GameObject avatar)
        {
            var t = UnicornCore.PhysBone;
            return t == null ? new List<Component>() : avatar.GetComponentsInChildren(t, true).ToList();
        }

        public static Transform Root(Component pb) { var t = UnicornCore.GetField(pb, "rootTransform") as Transform; return t ? t : pb.transform; }

        public static readonly Dictionary<string, (float pull, float spring, float stiffness, float gravity, float falloff, float immobile, float maxAngle)> Presets =
            new Dictionary<string, (float, float, float, float, float, float, float)>
            {
                ["Flowy"] = (0.15f, 0.45f, 0.05f, 0.25f, 0.6f, 0.2f, 60f),
                ["Bouncy"] = (0.35f, 0.8f, 0.1f, 0.1f, 0.4f, 0.3f, 45f),
                ["Heavy / straight"] = (0.4f, 0.1f, 0.3f, 0.6f, 0.8f, 0.5f, 25f),
                ["Windswept"] = (0.1f, 0.6f, 0.0f, 0.05f, 0.2f, 0.0f, 90f),
                ["Ears"] = (0.3f, 0.6f, 0.4f, 0.0f, 0f, 0.6f, 30f),
                ["Tail"] = (0.2f, 0.5f, 0.2f, 0.2f, 0.5f, 0.3f, 70f),
                ["Skirt"] = (0.3f, 0.3f, 0.2f, 0.4f, 0.7f, 0.6f, 40f),
                ["Accessory"] = (0.25f, 0.5f, 0.15f, 0.3f, 0.5f, 0.4f, 50f),
            };

        public static void Apply(Component pb, (float pull, float spring, float stiffness, float gravity, float falloff, float immobile, float maxAngle) v)
        {
            Undo.RecordObject(pb, "Unity Unicorn PhysBone");
            UnicornCore.SetField(pb, "pull", v.pull);
            UnicornCore.SetField(pb, "spring", v.spring);
            UnicornCore.SetField(pb, "stiffness", v.stiffness);
            UnicornCore.SetField(pb, "gravity", v.gravity);
            UnicornCore.SetField(pb, "gravityFalloff", v.falloff);
            UnicornCore.SetField(pb, "immobile", v.immobile);
            UnicornCore.SetField(pb, "limitType", 1); // Angle
            UnicornCore.SetField(pb, "maxAngleX", v.maxAngle);
            EditorUtility.SetDirty(pb);
        }
    }

    // ---------------- hair-weighter ----------------
    // Changes how hair flows by retuning its PhysBones: presets or sliders, for every hair chain or just the
    // ones you tick. Writes the old values to the report so a change can be undone by hand later too.
    public class HairWeighter : EditorWindow
    {
        GameObject avatar; Vector2 scroll; string preset = "Flowy"; string result = "";
        float pull = 0.15f, spring = 0.45f, stiffness = 0.05f, gravity = 0.25f, falloff = 0.6f, immobile = 0.2f, maxAngle = 60f;
        readonly Dictionary<Component, bool> pick = new Dictionary<Component, bool>();
        static readonly Regex HairName = new Regex("hair|bang|fringe|ponytail|pigtail|braid|bun|strand|lock", RegexOptions.IgnoreCase);

        [MenuItem("Tools/Unity Unicorn/hair-weighter", false, 41)]
        static void Open() { var w = GetWindow<HairWeighter>(true, "Unity Unicorn · hair-weighter"); w.avatar = UnicornCore.SelectedAvatar(); w.minSize = new Vector2(420, 480); }

        void OnGUI()
        {
            avatar = (GameObject)EditorGUILayout.ObjectField("Avatar", avatar, typeof(GameObject), true);
            if (UnicornCore.PhysBone == null) { EditorGUILayout.HelpBox("The VRChat Avatars SDK isn't in this project, so there are no PhysBones to tune.", MessageType.Warning); return; }
            if (!avatar) return;
            var hair = PB.On(avatar).Where(p => HairName.IsMatch(PB.Root(p).name) || HairName.IsMatch(p.name)).ToList();
            EditorGUILayout.LabelField($"{hair.Count} hair PhysBones", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(150));
            foreach (var p in hair)
            {
                if (!pick.ContainsKey(p)) pick[p] = true;
                pick[p] = EditorGUILayout.ToggleLeft($"{PB.Root(p).name}  (pull {UnicornCore.GetField(p, "pull")}, gravity {UnicornCore.GetField(p, "gravity")})", pick[p]);
            }
            EditorGUILayout.EndScrollView();

            var names = PB.Presets.Keys.Take(4).ToArray();
            var i = EditorGUILayout.Popup("Preset", Mathf.Max(0, Array.IndexOf(names, preset)), names);
            if (names[i] != preset) { preset = names[i]; (pull, spring, stiffness, gravity, falloff, immobile, maxAngle) = PB.Presets[preset]; }
            pull = EditorGUILayout.Slider("Pull (snaps back)", pull, 0, 1);
            spring = EditorGUILayout.Slider("Momentum (swing)", spring, 0, 1);
            stiffness = EditorGUILayout.Slider("Stiffness", stiffness, 0, 1);
            gravity = EditorGUILayout.Slider("Gravity (falls down)", gravity, -1, 1);
            falloff = EditorGUILayout.Slider("Gravity falloff", falloff, 0, 1);
            immobile = EditorGUILayout.Slider("Immobile (ignores walking)", immobile, 0, 1);
            maxAngle = EditorGUILayout.Slider("Max angle", maxAngle, 0, 180);
            EditorGUILayout.HelpBox("Enter Play mode with Gesture Manager or Av3Emulator to watch the flow. Ctrl+Z undoes a change.", MessageType.None);
            if (GUILayout.Button("Apply to ticked hair", GUILayout.Height(30)))
            {
                var notes = new List<string>();
                foreach (var p in hair.Where(h => pick[h]))
                {
                    notes.Add($"{PB.Root(p).name}: was pull {UnicornCore.GetField(p, "pull")}, spring {UnicornCore.GetField(p, "spring")}, stiffness {UnicornCore.GetField(p, "stiffness")}, gravity {UnicornCore.GetField(p, "gravity")}");
                    PB.Apply(p, (pull, spring, stiffness, gravity, falloff, immobile, maxAngle));
                }
                notes.Add($"set {preset}-based values on {notes.Count} chains");
                result = UnicornCore.Report("hair-weighter", notes.Count > 1 ? "completed" : "failed", $"Hair flow on {avatar.name}", notes);
            }
            if (result.Length > 0) EditorGUILayout.HelpBox("Report: " + result, MessageType.Info);
        }
    }

    // ---------------- spring-joint-strapper ----------------
    // Adds a PhysBone chain to each selected bone that doesn't move yet (hair, ears, tail, skirt, accessory),
    // with a preset for its kind, and skips bones already covered by a PhysBone.
    public class SpringJointStrapper : EditorWindow
    {
        string kind = "Accessory"; float radius = 0.02f; bool endpoint = true; string result = "";

        [MenuItem("Tools/Unity Unicorn/spring-joint-strapper", false, 42)]
        static void Open() { var w = GetWindow<SpringJointStrapper>(true, "Unity Unicorn · spring-joint-strapper"); w.minSize = new Vector2(380, 240); }

        void OnGUI()
        {
            if (UnicornCore.PhysBone == null) { EditorGUILayout.HelpBox("The VRChat Avatars SDK isn't in this project.", MessageType.Warning); return; }
            EditorGUILayout.HelpBox("Select the first bone of each chain in the Hierarchy (the bone where the hair, ear, tail or charm starts).", MessageType.None);
            var kinds = PB.Presets.Keys.ToArray();
            kind = kinds[EditorGUILayout.Popup("Kind", Mathf.Max(0, Array.IndexOf(kinds, kind)), kinds)];
            radius = EditorGUILayout.Slider("Radius", radius, 0, 0.1f);
            endpoint = EditorGUILayout.ToggleLeft("Add a tip if the chain ends in a single bone", endpoint);
            EditorGUILayout.LabelField($"{Selection.transforms.Length} bones selected");
            GUI.enabled = Selection.transforms.Length > 0;
            if (GUILayout.Button("Add PhysBones", GUILayout.Height(30))) result = Run();
            GUI.enabled = true;
            if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        void OnSelectionChange() => Repaint();

        string Run()
        {
            var type = UnicornCore.PhysBone; var notes = new List<string>(); var made = 0;
            foreach (var bone in Selection.transforms)
            {
                var avatar = bone.root.gameObject;
                var covered = PB.On(avatar).Any(p => bone.IsChildOf(PB.Root(p)) && !(UnicornCore.GetField(p, "ignoreTransforms") is List<Transform> ig && ig.Any(bone.IsChildOf)));
                if (covered) { notes.Add($"{bone.name}: already moves with a PhysBone, skipped"); continue; }
                var pb = Undo.AddComponent(bone.gameObject, type);
                UnicornCore.SetField(pb, "rootTransform", bone);
                UnicornCore.SetField(pb, "radius", radius);
                if (endpoint && bone.childCount == 0) UnicornCore.SetField(pb, "endpointPosition", new Vector3(0, 0.05f, 0));
                PB.Apply(pb, PB.Presets[kind]);
                made++; notes.Add($"{bone.name}: {kind} PhysBone added");
            }
            var f = UnicornCore.Report("spring-joint-strapper", made > 0 ? "completed" : "failed", $"{made} chains", notes);
            return string.Join("\n", notes.Take(15)) + "\nReport: " + f;
        }
    }
}
