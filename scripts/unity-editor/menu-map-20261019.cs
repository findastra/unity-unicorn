// Unity Unicorn menu-map (Claude Opus 5.5, 2026-10-19).
// Tools > Unity Unicorn > menu-map builds a disposable test copy of the selected avatar (the same SDK build step an upload
// runs, so VRCFury and Modular Avatar add their menus, parameters and compressor), reads the built expression menu and
// parameters, and deletes the copy. It writes UnityUnicornReports/menu-map-<avatar>-<time>.json for the menu-map panel on
// Unity Unicorn's page: every menu page with names, control types, parameters and icons (PNG), and every synced parameter
// with its bit cost and the total after VRCFury's compressor. A usage receipt goes beside it.
// Nothing on the avatar changes and the scene is never saved while the copy exists.
// Install: copy this file into Assets/UnityUnicorn/Editor/ (or any Editor folder). The VRChat SDK is reached by reflection,
// so it compiles in an assembly without SDK references.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityUnicorn
{
    public static class MenuMap20261019
    {
        const int IconSize = 64;
        static readonly Dictionary<int, string> ControlTypes = new Dictionary<int, string>
        {
            { 101, "Button" }, { 102, "Toggle" }, { 103, "SubMenu" },
            { 201, "TwoAxisPuppet" }, { 202, "FourAxisPuppet" }, { 203, "RadialPuppet" },
        };
        static readonly string[] ValueTypes = { "Int", "Float", "Bool" };

        static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);
        static Type DescriptorType => TypeNamed("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");

        [MenuItem("Tools/Unity Unicorn/menu-map", false, 46)]
        public static void FromMenu()
        {
            var avatar = SelectedAvatar();
            if (!avatar)
            {
                EditorUtility.DisplayDialog("menu-map", "Select an avatar (the object with the VRC Avatar Descriptor) in the Hierarchy first.", "OK");
                return;
            }
            try
            {
                var path = Export(avatar);
                EditorUtility.RevealInFinder(path);
                Debug.Log("menu-map wrote " + path + ". Load it in Unity Unicorn's menu-map panel.");
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                EditorUtility.DisplayDialog("menu-map", "The test build failed: " + inner.Message + "\n\nThe avatar was not changed. The Console has the details.", "OK");
                Debug.LogException(inner);
            }
        }

        [MenuItem("Tools/Unity Unicorn/menu-map", true)]
        static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode && DescriptorType != null;

        static GameObject SelectedAvatar()
        {
            var type = DescriptorType;
            var go = Selection.activeGameObject;
            if (type == null || !go) return null;
            var d = go.GetComponentInParent(type, true);
            return d ? d.gameObject : null;
        }

        // For scripts and agent queues: export the scene avatar with this name; returns the JSON path.
        public static string ExportAvatar(string avatarName)
        {
            var type = DescriptorType ?? throw new InvalidOperationException("The VRChat Avatars SDK is not in this project.");
            var avatar = Object.FindObjectsOfType(type, true).Cast<Component>().Select(c => c.gameObject)
                .FirstOrDefault(g => g.scene.IsValid() && g.name == avatarName);
            if (!avatar) throw new InvalidOperationException("No avatar named " + avatarName + " in the open scenes.");
            return Export(avatar);
        }

        public static string Export(GameObject source)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode first.");
            var descriptorType = DescriptorType ?? throw new InvalidOperationException("The VRChat Avatars SDK is not in this project.");
            var callbacks = TypeNamed("VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks")
                ?? throw new InvalidOperationException("The SDK build callbacks were not found.");
            var preprocess = callbacks.GetMethod("OnPreprocessAvatar", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject) }, null)
                ?? throw new InvalidOperationException("The SDK's OnPreprocessAvatar was not found.");

            var sourceParams = Ref(source.GetComponent(descriptorType), "expressionParameters");
            int beforeBuild = sourceParams ? Cost(sourceParams) : 0;

            GameObject clone = null;
            try
            {
                EditorUtility.DisplayProgressBar("menu-map", "Building a test copy of " + source.name + "…", 0.3f);
                clone = Object.Instantiate(source);
                clone.name = source.name + "(Clone)";
                clone.SetActive(true);
                // VRCFury skips avatars that still hold prefab instances, as in its own play-mode build.
                foreach (var t in clone.GetComponentsInChildren<Transform>(true))
                {
                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                    if (root && PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                if (!(bool)preprocess.Invoke(null, new object[] { clone }))
                    throw new InvalidOperationException("The SDK build step returned false; the Console names the component that failed.");

                EditorUtility.DisplayProgressBar("menu-map", "Reading the built menu…", 0.8f);
                var descriptor = clone.GetComponent(descriptorType);
                if (!descriptor) throw new InvalidOperationException("The built copy has no Avatar Descriptor.");
                var menu = Ref(descriptor, "expressionsMenu");
                var parameters = Ref(descriptor, "expressionParameters");
                if (!parameters) throw new InvalidOperationException("The built copy has no expression parameters.");
                var limitField = parameters.GetType().GetField("MAX_PARAMETER_COST", BindingFlags.Public | BindingFlags.Static);
                int limit = limitField != null ? (int)limitField.GetValue(null) : 256;
                int? beforeCompressor = CompressorOldTotal(clone);

                var icons = new Dictionary<Texture, string>();
                var paths = new Dictionary<string, string>();
                var sb = new StringBuilder();
                var stamp = DateTime.UtcNow;
                sb.Append("{\n  \"schema\": \"unity-unicorn.menu-map/v1\",\n  \"tool_id\": \"menu-map\",\n");
                sb.Append("  \"avatar\": {\"name\": ").Append(Str(source.name)).Append("},\n");
                sb.Append("  \"built_at\": ").Append(Str(stamp.ToString("yyyy-MM-ddTHH:mm:ssZ"))).Append(",\n");
                sb.Append("  \"unity\": ").Append(Str(Application.unityVersion)).Append(",\n");
                sb.Append("  \"menu\": ");
                WriteMenu(sb, menu, source.name, "", new HashSet<Object>(), icons, paths, 1);
                sb.Append(",\n  \"parameters\": [");
                int used = Cost(parameters);
                var rows = ParameterRows(parameters);
                for (int i = 0; i < rows.Count; i++)
                {
                    var p = rows[i];
                    paths.TryGetValue(p.name, out var where);
                    sb.Append(i == 0 ? "\n" : ",\n").Append("    {\"name\": ").Append(Str(p.name))
                      .Append(", \"type\": ").Append(Str(p.type))
                      .Append(", \"synced\": ").Append(p.synced ? "true" : "false")
                      .Append(", \"saved\": ").Append(p.saved ? "true" : "false")
                      .Append(", \"default\": ").Append(p.defaultValue.ToString("0.###", CultureInfo.InvariantCulture))
                      .Append(", \"bits\": ").Append(p.synced ? (p.type == "Bool" ? 1 : 8) : 0)
                      .Append(", \"menu_path\": ").Append(where == null ? "null" : Str(where)).Append("}");
                }
                sb.Append("\n  ],\n  \"budget\": {\"limit\": ").Append(limit).Append(", \"used\": ").Append(used)
                  .Append(", \"before_compressor\": ").Append(beforeCompressor.HasValue ? beforeCompressor.Value.ToString() : "null")
                  .Append(", \"before_build\": ").Append(beforeBuild).Append("},\n  \"icons\": {");
                int n = 0;
                foreach (var kv in icons)
                {
                    var png = IconPng(kv.Key);
                    if (png == null) continue;
                    sb.Append(n++ == 0 ? "\n" : ",\n").Append("    ").Append(Str(kv.Value)).Append(": ").Append(Str("data:image/png;base64," + png));
                }
                sb.Append("\n  }\n}\n");

                Directory.CreateDirectory("UnityUnicornReports");
                var file = Path.Combine("UnityUnicornReports", "menu-map-" + Safe(source.name) + "-" + stamp.ToString("yyyyMMdd-HHmmss") + ".json");
                File.WriteAllText(file, sb.ToString());
                WriteReceipt(stamp, new List<string>
                {
                    source.name + ": " + used + " of " + limit + " bits after the build" + (beforeCompressor.HasValue ? " (" + beforeCompressor + " before VRCFury's compressor)" : ""),
                    rows.Count + " parameters, " + paths.Count + " used by menu controls, " + icons.Count + " icons",
                    "menu map written to " + file,
                });
                return Path.GetFullPath(file);
            }
            finally
            {
                if (clone) Object.DestroyImmediate(clone);
                EditorUtility.ClearProgressBar();
            }
        }

        static Object Ref(Object owner, string field) => owner ? new SerializedObject(owner).FindProperty(field)?.objectReferenceValue : null;
        static int Cost(Object parameters) => (int)parameters.GetType().GetMethod("CalcTotalCost").Invoke(parameters, null);

        // VRCFury leaves a debug component on the built avatar: "Parameter Compressor … Old Total: 412 bits".
        static int? CompressorOldTotal(GameObject built)
        {
            foreach (var c in built.GetComponentsInChildren<Component>(true))
            {
                if (!c || c.GetType().Name != "VRCFuryDebugInfo") continue;
                var t = c.GetType();
                var title = t.GetField("title")?.GetValue(c) as string;
                var info = t.GetField("debugInfo")?.GetValue(c) as string;
                if (title != "Parameter Compressor" || info == null) continue;
                var m = Regex.Match(info, @"Old Total:\s*(\d+)");
                if (m.Success) return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            return null;
        }

        class Param { public string name, type; public bool synced, saved; public float defaultValue; }

        static List<Param> ParameterRows(Object parameters)
        {
            var list = new SerializedObject(parameters).FindProperty("parameters");
            var rows = new List<Param>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var p = list.GetArrayElementAtIndex(i);
                var name = p.FindPropertyRelative("name").stringValue;
                if (string.IsNullOrEmpty(name)) continue;
                int vt = p.FindPropertyRelative("valueType").intValue;
                var synced = p.FindPropertyRelative("networkSynced");
                rows.Add(new Param
                {
                    name = name,
                    type = vt >= 0 && vt < ValueTypes.Length ? ValueTypes[vt] : "Int",
                    synced = synced == null || synced.boolValue,
                    saved = p.FindPropertyRelative("saved").boolValue,
                    defaultValue = p.FindPropertyRelative("defaultValue").floatValue,
                });
            }
            return rows;
        }

        // One menu page and its submenus. A page already on the path is written as a link, so loops end.
        static void WriteMenu(StringBuilder sb, Object menu, string name, string path, HashSet<Object> onPath,
                              Dictionary<Texture, string> icons, Dictionary<string, string> paths, int depth)
        {
            string pad = new string(' ', depth * 2);
            sb.Append("{\"name\": ").Append(Str(name)).Append(", \"controls\": [");
            if (!menu || !onPath.Add(menu)) { sb.Append("]").Append(menu ? ", \"loop\": true}" : "}"); return; }
            var controls = new SerializedObject(menu).FindProperty("controls");
            for (int i = 0; i < controls.arraySize; i++)
            {
                var c = controls.GetArrayElementAtIndex(i);
                var cname = c.FindPropertyRelative("name").stringValue;
                ControlTypes.TryGetValue(c.FindPropertyRelative("type").intValue, out var type);
                var param = c.FindPropertyRelative("parameter.name")?.stringValue ?? "";
                var here = path.Length == 0 ? cname : path + " › " + cname;
                if (param.Length > 0 && !paths.ContainsKey(param)) paths[param] = here;
                sb.Append(i == 0 ? "\n" : ",\n").Append(pad).Append("  {\"name\": ").Append(Str(cname))
                  .Append(", \"type\": ").Append(Str(type ?? "Unknown"))
                  .Append(", \"parameter\": ").Append(param.Length > 0 ? Str(param) : "null")
                  .Append(", \"value\": ").Append(c.FindPropertyRelative("value").floatValue.ToString("0.###", CultureInfo.InvariantCulture))
                  .Append(", \"icon\": ").Append(IconId(c.FindPropertyRelative("icon").objectReferenceValue as Texture, icons));
                var subs = c.FindPropertyRelative("subParameters");
                var subNames = new List<string>();
                for (int s = 0; subs != null && s < subs.arraySize; s++)
                {
                    var sn = subs.GetArrayElementAtIndex(s).FindPropertyRelative("name").stringValue;
                    if (string.IsNullOrEmpty(sn)) continue;
                    subNames.Add(sn);
                    if (!paths.ContainsKey(sn)) paths[sn] = here;
                }
                if (type != "SubMenu" && subNames.Count > 0) sb.Append(", \"sub_parameters\": [").Append(string.Join(", ", subNames.Select(Str))).Append("]");
                var labels = c.FindPropertyRelative("labels");
                if (type != "SubMenu" && type != "Button" && type != "Toggle" && labels != null && labels.arraySize > 0)
                {
                    sb.Append(", \"labels\": [");
                    for (int l = 0; l < labels.arraySize; l++)
                    {
                        var lab = labels.GetArrayElementAtIndex(l);
                        sb.Append(l == 0 ? "" : ", ").Append("{\"name\": ").Append(Str(lab.FindPropertyRelative("name").stringValue))
                          .Append(", \"icon\": ").Append(IconId(lab.FindPropertyRelative("icon").objectReferenceValue as Texture, icons)).Append("}");
                    }
                    sb.Append("]");
                }
                if (type == "SubMenu")
                {
                    sb.Append(", \"submenu\": ");
                    WriteMenu(sb, c.FindPropertyRelative("subMenu").objectReferenceValue, cname, here, onPath, icons, paths, depth + 1);
                }
                sb.Append("}");
            }
            sb.Append(controls.arraySize > 0 ? "\n" + pad : "").Append("]}");
            onPath.Remove(menu);
        }

        static string IconId(Texture t, Dictionary<Texture, string> icons)
        {
            if (!t) return "null";
            if (!icons.TryGetValue(t, out var id)) icons[t] = id = "i" + icons.Count;
            return Str(id);
        }

        // Icons are often compressed and unreadable, so draw each through a RenderTexture at 64 px.
        static string IconPng(Texture t)
        {
            if (!t) return null;
            var rt = RenderTexture.GetTemporary(IconSize, IconSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var read = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(t, rt);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
                read.Apply();
                return Convert.ToBase64String(read.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(read);
            }
        }

        static string Safe(string s) => Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

        static string Str(string s)
        {
            var b = new StringBuilder("\"");
            foreach (var ch in s ?? "")
            {
                switch (ch)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (ch < 0x20) b.Append("\\u").Append(((int)ch).ToString("x4"));
                        else b.Append(ch);
                        break;
                }
            }
            return b.Append('"').ToString();
        }

        static void WriteReceipt(DateTime stamp, List<string> findings)
        {
            var rows = string.Join(",\n", findings.Select(f => "        {\"severity\": \"info\", \"message\": " + Str(f.Replace("\\", "/")) + "}"));
            var json = "{\n    \"schema\": \"unity-unicorn.tool-usage/v1\",\n    \"tool_id\": \"menu-map\",\n    \"project\": {\"name\": " + Str(Path.GetFileName(Directory.GetCurrentDirectory())) + "},\n" +
                       "    \"completed_at\": \"" + stamp.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\",\n    \"status\": \"completed\",\n    \"evidence\": \"Menu map exported from a disposable test build\",\n" +
                       "    \"findings\": [\n" + rows + "\n    ]\n}\n";
            File.WriteAllText(Path.Combine("UnityUnicornReports", "menu-map-receipt-" + stamp.ToString("yyyyMMdd-HHmmss") + ".json"), json);
        }
    }
}
