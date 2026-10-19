// Unity Unicorn matcap-palette (Claude Opus 5.5, 2026-10-19).
// Tools > Unity Unicorn > matcap-palette shows every matcap texture in the project as a round pan, like a makeup palette,
// grouped by where it came from. Pick one, pick a material and a matcap slot, and apply it (with Undo). Works with
// Poiyomi (_Matcap, _Matcap2, ...) and lilToon (_MatCapTex, _MatCap2ndTex); any texture slot named like a matcap is offered.
// Each apply writes a usage receipt to UnityUnicornReports/ so the tool library can show real last-use dates.
// Install: copy this file into Assets/UnityUnicorn/Editor/ (or any Editor folder).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn
{
    public sealed class MatcapPalette20261019 : EditorWindow
    {
        class Entry { public Texture2D tex; public string path, group, name; }

        readonly List<Entry> entries = new List<Entry>();
        Entry chosen;
        Material target;
        string search = "", group = "All", note = "";
        float pan = 72;
        int slot;
        Vector2 scroll;
        Texture2D ring;
        static readonly Color Graphite = new Color(0.149f, 0.153f, 0.169f);
        static readonly Color Pink = new Color(1f, 0.235f, 0.78f);

        [MenuItem("Tools/Unity Unicorn/matcap-palette", false, 45)]
        public static void Open()
        {
            var w = GetWindow<MatcapPalette20261019>("Matcap palette");
            w.minSize = new Vector2(520, 520);
            w.Reload();
            w.PickTargetFromSelection();
        }

        void OnEnable() { if (entries.Count == 0) Reload(); }
        void OnSelectionChange() { PickTargetFromSelection(); Repaint(); }

        void PickTargetFromSelection()
        {
            if (Selection.activeObject is Material m) { target = m; return; }
            var go = Selection.activeGameObject;
            var r = go ? go.GetComponentInChildren<Renderer>() : null;
            if (r && r.sharedMaterial) target = r.sharedMaterial;
        }

        void Reload()
        {
            entries.Clear();
            var seen = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Regex.IsMatch(path, "mat ?cap", RegexOptions.IgnoreCase) || !seen.Add(path)) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                entries.Add(new Entry { tex = tex, path = path, name = Path.GetFileNameWithoutExtension(path), group = GroupOf(path) });
            }
            // The same matcap saved as .png, .jpg and .bmp shows once: the best format wins.
            int Rank(string p) { var x = Path.GetExtension(p).ToLowerInvariant(); return x == ".png" ? 0 : x == ".tga" ? 1 : x == ".psd" ? 2 : x == ".jpg" || x == ".jpeg" ? 3 : 4; }
            var kept = entries.GroupBy(e => e.group + "/" + e.name).Select(g => g.OrderBy(e => Rank(e.path)).First()).ToList();
            entries.Clear(); entries.AddRange(kept);
            int Order(string g) => g == "Unity Unicorn palette" ? 0 : g == "Poiyomi" || g == "lilToon" ? 2 : 1;
            entries.Sort((a, b) => Order(a.group) != Order(b.group) ? Order(a.group) - Order(b.group) : string.CompareOrdinal(a.group + "/" + a.name, b.group + "/" + b.name));
            if (chosen == null || !entries.Contains(chosen)) chosen = entries.FirstOrDefault();
            ring = MakeRing(128);
        }

        static string GroupOf(string path)
        {
            if (path.Contains("_PoiyomiShaders")) return "Poiyomi";
            if (path.Contains("Unity Unicorn Palette") || path.Contains("UnityUnicorn/Matcaps")) return "Unity Unicorn palette";
            if (path.ToLowerInvariant().Contains("liltoon")) return "lilToon";
            var parts = path.Split('/');
            return parts.Length > 2 ? parts[1].Trim('!', ' ', '[', ']') : "Project";
        }

        // A graphite square with a round hole and a thin rim, drawn over each texture so it reads as a pan.
        static Texture2D MakeRing(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            float c = (n - 1) / 2f, r = n / 2f - 3;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float inside = Mathf.Clamp01(r - d + 0.5f);
                    float rim = Mathf.Clamp01(1.5f - Mathf.Abs(d - r - 1.5f));
                    var col = Color.Lerp(Graphite, new Color(0.05f, 0.05f, 0.06f), rim);
                    col.a = 1 - inside;
                    t.SetPixel(x, y, col);
                }
            t.Apply();
            return t;
        }

        void DrawPan(Rect r, Texture2D tex, bool selected)
        {
            if (tex) GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
            GUI.DrawTexture(r, ring, ScaleMode.StretchToFill, true);
            if (selected) Handles.DrawSolidRectangleWithOutline(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Color.clear, Pink);
        }

        List<string> Slots()
        {
            var list = new List<string>();
            if (!target) return list;
            var sh = target.shader;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
                if (sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture && Regex.IsMatch(sh.GetPropertyName(i), "^_?mat ?cap\\d*(tex|2ndtex)?$", RegexOptions.IgnoreCase))
                    list.Add(sh.GetPropertyName(i));
            return list;
        }

        void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), Graphite);
            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, normal = { textColor = Color.white } };
            var muted = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = new Color(0.72f, 0.75f, 0.8f) } };
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                var big = GUILayoutUtility.GetRect(150, 150, GUILayout.Width(150), GUILayout.Height(150));
                DrawPan(big, chosen?.tex, false);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(chosen != null ? chosen.name : "No matcaps found", title);
                    if (chosen != null) GUILayout.Label(chosen.group + "\n" + chosen.path, muted);
                    target = (Material)EditorGUILayout.ObjectField("Material", target, typeof(Material), false);
                    var slots = Slots();
                    if (target && slots.Count == 0) EditorGUILayout.HelpBox("This material's shader has no matcap slot.", MessageType.Info);
                    if (slots.Count > 0)
                    {
                        slot = Mathf.Clamp(slot, 0, slots.Count - 1);
                        slot = EditorGUILayout.Popup("Slot", slot, slots.ToArray());
                        using (new EditorGUI.DisabledScope(chosen == null))
                            if (GUILayout.Button("Put " + (chosen != null ? chosen.name : "it") + " on " + target.name + "." + slots[slot])) Apply(slots[slot]);
                    }
                    if (!string.IsNullOrEmpty(note)) GUILayout.Label(note, muted);
                }
            }
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                var groups = new[] { "All" }.Concat(entries.Select(e => e.group).Distinct()).ToArray();
                group = groups[Mathf.Max(0, EditorGUILayout.Popup(Array.IndexOf(groups, group), groups, GUILayout.Width(170)))];
                pan = GUILayout.HorizontalSlider(pan, 48, 128, GUILayout.Width(90));
                if (GUILayout.Button("Reload", GUILayout.Width(60))) Reload();
            }
            var shown = entries.Where(e => (group == "All" || e.group == group) && (search.Length == 0 || e.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            GUILayout.Label(shown.Count + " of " + entries.Count + " matcaps", muted);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            int cols = Mathf.Max(1, (int)((position.width - 24) / (pan + 14)));
            string lastGroup = null; int col = 0;
            for (int i = 0; i < shown.Count; i++)
            {
                var e = shown[i];
                if (group == "All" && e.group != lastGroup)
                {
                    if (col != 0) { GUILayout.EndHorizontal(); col = 0; }
                    GUILayout.Label(e.group, title); lastGroup = e.group;
                }
                if (col == 0) GUILayout.BeginHorizontal();
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(pan + 10)))
                {
                    var r = GUILayoutUtility.GetRect(pan, pan, GUILayout.Width(pan), GUILayout.Height(pan));
                    DrawPan(r, e.tex, e == chosen);
                    if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) { chosen = e; note = ""; Event.current.Use(); Repaint(); }
                    GUILayout.Label(e.name, new GUIStyle(muted) { alignment = TextAnchor.UpperCenter, fixedWidth = pan + 8 });
                }
                if (++col == cols) { GUILayout.EndHorizontal(); col = 0; }
            }
            if (col != 0) GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        // The toggle that switches a slot on, read from Poiyomi's shader source ([ThryToggle(KEYWORD)]_Matcap2Enable ...).
        static (string prop, string keyword) EnableFor(Material m, string slot)
        {
            var guess = slot.StartsWith("_MatCap", StringComparison.Ordinal) ? (slot == "_MatCapTex" ? "_UseMatCap" : "_UseMatCap2nd") : slot + "Enable";
            if (!m.HasProperty(guess)) return (null, null);
            string keyword = null;
            var path = AssetDatabase.GetAssetPath(m.shader);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var hit = Regex.Match(File.ReadAllText(path), @"\[ThryToggle\((\w+)\)\]\s*" + Regex.Escape(guess) + @"\b");
                if (hit.Success) keyword = hit.Groups[1].Value;
            }
            return (guess, keyword);
        }

        void Apply(string slotName)
        {
            if (chosen == null || !target) return;
            Undo.RecordObject(target, "Unity Unicorn matcap-palette");
            target.SetTexture(slotName, chosen.tex);
            var (prop, keyword) = EnableFor(target, slotName);
            bool locked = target.shader.name.StartsWith("Hidden/Locked/", StringComparison.Ordinal);
            var findings = new List<string> { chosen.path + " -> " + target.name + "." + slotName };
            if (prop != null && target.GetFloat(prop) < 0.5f)
            {
                if (locked) findings.Add(prop + " is off and the material is locked: unlock it in Poiyomi to switch the slot on.");
                else { target.SetFloat(prop, 1); if (keyword != null) target.EnableKeyword(keyword); findings.Add("switched on " + prop + (keyword != null ? " (" + keyword + ")" : "")); }
            }
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            note = string.Join("\n", findings);
            WriteReceipt(findings);
        }

        static void WriteReceipt(List<string> findings)
        {
            Directory.CreateDirectory("UnityUnicornReports");
            var stamp = DateTime.UtcNow;
            var rows = string.Join(",\n", findings.Select(f => "        {\"severity\": \"info\", \"message\": \"" + f.Replace("\\", "/").Replace("\"", "'") + "\"}"));
            var json = "{\n    \"schema\": \"unity-unicorn.tool-usage/v1\",\n    \"tool_id\": \"matcap-palette\",\n    \"project\": {\"name\": \"" + Path.GetFileName(Directory.GetCurrentDirectory()) + "\"},\n" +
                       "    \"completed_at\": \"" + stamp.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\",\n    \"status\": \"completed\",\n    \"evidence\": \"Matcap applied from the palette window\",\n" +
                       "    \"findings\": [\n" + rows + "\n    ]\n}\n";
            File.WriteAllText(Path.Combine("UnityUnicornReports", "matcap-palette-" + stamp.ToString("yyyyMMdd-HHmmss") + ".json"), json);
        }
    }
}
