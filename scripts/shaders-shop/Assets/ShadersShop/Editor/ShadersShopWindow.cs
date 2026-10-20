// Claude, 2026-10-18. shaders-shop window (Unity Stable). Menu: Unity Unicorn > Shaders Shop > Open shop window.
// Shop: every shader we stock, why, its licence, what it's good for, and where it's already used. Install shows the
// exact files, sources and size first. This world: what the world's scenes actually use, saved with the world.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn.ShadersShop
{
    public class ShadersShopWindow : EditorWindow
    {
        Catalog cat; WorldRecord here; List<WorldRecord> others; Vector2 scroll; int tab; string filter = ""; string last = "";

        [MenuItem("Unity Unicorn/Shaders Shop/Open shop window")]
        public static void Open() => GetWindow<ShadersShopWindow>("Shaders Shop").Show();

        void OnEnable() { Reload(); EditorApplication.update += Tick; }
        void OnDisable() => EditorApplication.update -= Tick;
        void Tick() { if (ShadersInstaller.Busy) Repaint(); }
        void Reload() { cat = Catalog.Load(); here = WorldShaders.LoadThisWorld(); others = WorldShaders.LoadOtherWorlds(); }

        void OnGUI()
        {
            EditorGUILayout.LabelField("shaders-shop v1.0.0 (Unity Stable) · catalog " + cat.updated, EditorStyles.miniLabel);
            tab = GUILayout.Toolbar(tab, new[] { "Shop", "This world" });
            if (ShadersInstaller.Busy) EditorGUILayout.HelpBox(ShadersInstaller.Progress, MessageType.Info);
            if (!string.IsNullOrEmpty(last)) EditorGUILayout.HelpBox(last, MessageType.None);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) Shop(); else World();
            EditorGUILayout.EndScrollView();
        }

        void Shop()
        {
            filter = EditorGUILayout.TextField("Find (name or feature)", filter);
            foreach (var e in cat.shaders)
            {
                if (filter.Length > 0 && !(e.name + " " + string.Join(" ", e.features) + " " + e.kind).ToLowerInvariant().Contains(filter.ToLowerInvariant())) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(e.name + (string.IsNullOrEmpty(e.version) ? "" : "  " + e.version), EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        var c = GUI.color; GUI.color = e.Installed ? new Color(.5f, .9f, .55f) : Color.gray;
                        GUILayout.Label(e.install == "builtin" ? "BUILT IN" : e.Installed ? "IN THIS WORLD" : e.install.ToUpperInvariant(), EditorStyles.miniBoldLabel); GUI.color = c;
                    }
                    EditorGUILayout.LabelField("by " + e.author + " · licence " + e.license + " · " + e.kind + (e.Size != "" ? " · " + e.Size : ""), EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("Good for " + string.Join(", ", e.good_for) + " on " + string.Join(", ", e.platforms) + ". Features: " + string.Join(", ", e.features), EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(e.why, EditorStyles.wordWrappedLabel);
                    var usedHere = here?.shaders.Where(u => u.catalog_id == e.id).Sum(u => u.materials.Count) ?? 0;
                    var usedElsewhere = others.Where(w => w.shaders.Any(u => u.catalog_id == e.id)).Select(w => w.world).ToList();
                    if (usedHere > 0 || usedElsewhere.Count > 0)
                        EditorGUILayout.LabelField("Used in: " + string.Join(", ", (usedHere > 0 ? new[] { "this world (" + usedHere + " materials)" } : new string[0]).Concat(usedElsewhere)), EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (!string.IsNullOrEmpty(e.homepage) && GUILayout.Button("Open page", GUILayout.Width(90))) Application.OpenURL(e.homepage);
                        using (new EditorGUI.DisabledScope(ShadersInstaller.Busy || !e.Downloads))
                            if (e.Downloads && GUILayout.Button(e.Installed ? "Reinstall " + e.version : "Install " + e.version, GUILayout.Width(140)))
                                if (EditorUtility.DisplayDialog("Install " + e.name + "?", ShadersInstaller.Describe(e) + "\n\nThis downloads the files above into this world.", "Install", "Cancel"))
                                    ShadersInstaller.Install(e, "Unity editor window (confirmed in the dialog)", (ok, msg) => { last = (ok ? "Done: " : "Not done: ") + msg; Reload(); Repaint(); });
                    }
                }
            }
        }

        void World()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan this world")) { here = WorldShaders.Scan(cat); last = "Scanned " + here.scenes.Count + " scenes: " + here.shaders.Count + " shaders (not saved yet)"; }
                using (new EditorGUI.DisabledScope(here == null))
                    if (GUILayout.Button("Save world record")) { WorldShaders.Save(here); last = "Saved " + WorldShaders.RecordPath; }
            }
            if (here == null) { EditorGUILayout.HelpBox("No scan yet. Press 'Scan this world'.", MessageType.Info); return; }
            EditorGUILayout.LabelField(here.world + " · scanned " + here.scanned_at + " · " + here.scenes.Count + " scenes", EditorStyles.miniLabel);
            foreach (var u in here.shaders)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(u.shader, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(u.catalog_name + " · licence " + u.license + " · " + u.source + (string.IsNullOrEmpty(u.version) ? "" : " " + u.version) + " · " + u.materials.Count + " materials", EditorStyles.wordWrappedMiniLabel);
                }
        }
    }
}
