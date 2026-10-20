// Claude, 2026-10-18. unity-asset-creator: the checklist window. Menu: Unity Unicorn > Asset Creator.
// Pick (or make) a recipe, fill in the plan and ledger row, then go down the steps in order: Check, read the result,
// write a note where it asks for one, Mark done. Render previews before step 11; Export at the end.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn.AssetCreator
{
    public class AssetCreatorWindow : EditorWindow
    {
        [SerializeField] AssetRecipe recipe;
        readonly Dictionary<string, Result> results = new Dictionary<string, Result>();
        readonly Dictionary<string, string> notes = new Dictionary<string, string>();
        readonly HashSet<string> open = new HashSet<string>();
        Vector2 scroll; bool showPlan = true;

        [MenuItem("Unity Unicorn/Asset Creator/Open checklist window")]
        public static void Open() => GetWindow<AssetCreatorWindow>("Asset Creator").Show();

        void OnGUI()
        {
            EditorGUILayout.LabelField("unity-asset-creator v" + AssetExporter.ToolVersion + " (Unity Unicorn)", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var r = (AssetRecipe)EditorGUILayout.ObjectField("Recipe", recipe, typeof(AssetRecipe), false);
                if (r != recipe) { recipe = r; results.Clear(); notes.Clear(); }
                if (GUILayout.Button("New for selected prefab", GUILayout.Width(170))) NewRecipe();
            }
            if (recipe == null) { EditorGUILayout.HelpBox("Pick a recipe, or select the asset's prefab in the Project window and press 'New for selected prefab'.", MessageType.Info); return; }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var so = new SerializedObject(recipe);
            showPlan = EditorGUILayout.Foldout(showPlan, "Plan, ledger row and changelog (v" + recipe.version + ")", true);
            if (showPlan)
            {
                EditorGUI.indentLevel++;
                var it = so.GetIterator(); it.NextVisible(true);
                while (it.NextVisible(false)) if (it.name != "m_Script") EditorGUILayout.PropertyField(it, true);
                EditorGUI.indentLevel--;
                so.ApplyModifiedProperties();
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run every check")) foreach (var st in AssetChecklist.Steps) results[st.id] = AssetChecklist.Run(st, recipe);
                if (GUILayout.Button("Render previews")) { AssetExporter.RenderPreviews(recipe); results.Remove("review"); }
            }

            for (int i = 0; i < AssetChecklist.Steps.Length; i++) DrawStep(i, AssetChecklist.Steps[i]);
            EditorGUILayout.EndScrollView();
        }

        void DrawStep(int i, Step step)
        {
            var st = recipe.State(step.id); bool done = recipe.IsDone(step.id);
            results.TryGetValue(step.id, out var res);
            var status = done ? "DONE" : res != null ? res.status.ToString().ToUpperInvariant() : "not checked";
            var color = done ? new Color(.45f, .85f, .5f) : res == null ? Color.gray : res.status == Status.Fail ? new Color(1f, .45f, .45f) : res.status == Status.Warn ? new Color(1f, .8f, .35f) : new Color(.6f, .85f, 1f);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var c = GUI.color; GUI.color = color; GUILayout.Label(status, EditorStyles.boldLabel, GUILayout.Width(90)); GUI.color = c;
                    bool isOpen = open.Contains(step.id);
                    if (EditorGUILayout.Foldout(isOpen, (i + 1) + ". " + step.title, true) != isOpen) { if (isOpen) open.Remove(step.id); else open.Add(step.id); }
                }
                if (!open.Contains(step.id) && !(res != null && !done)) { if (done) EditorGUILayout.LabelField("   " + st.doneAt + " · " + st.doneBy + (string.IsNullOrEmpty(st.note) ? "" : " · " + st.note), EditorStyles.miniLabel); return; }
                EditorGUILayout.LabelField(step.why, EditorStyles.wordWrappedMiniLabel);
                if (res != null) EditorGUILayout.HelpBox(res.Text, res.status == Status.Fail ? MessageType.Error : res.status == Status.Warn ? MessageType.Warning : MessageType.Info);
                else if (!string.IsNullOrEmpty(st.details)) EditorGUILayout.HelpBox("Last check: " + st.status + "\n" + st.details, MessageType.None);
                if (step.confirm != null) EditorGUILayout.LabelField("Confirm: " + step.confirm, EditorStyles.wordWrappedLabel);
                if (done) { EditorGUILayout.LabelField("Done " + st.doneAt + " by " + st.doneBy + (string.IsNullOrEmpty(st.note) ? "" : ": " + st.note), EditorStyles.wordWrappedMiniLabel); return; }

                if (!notes.ContainsKey(step.id)) notes[step.id] = st.note;
                notes[step.id] = EditorGUILayout.TextField("Note", notes[step.id]);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Check", GUILayout.Width(80))) results[step.id] = AssetChecklist.Run(step, recipe);
                    if (step.id == "export")
                    {
                        var whyNot = AssetChecklist.WhyNot(recipe, step, res, "export");
                        using (new EditorGUI.DisabledScope(whyNot != null))
                            if (GUILayout.Button("Export v" + recipe.version)) { AssetExporter.Export(recipe, "Unity editor window"); results.Remove(step.id); }
                        if (whyNot != null) GUILayout.Label(whyNot, EditorStyles.miniLabel);
                    }
                    else
                    {
                        var whyNot = AssetChecklist.WhyNot(recipe, step, res, notes[step.id]);
                        using (new EditorGUI.DisabledScope(whyNot != null))
                            if (GUILayout.Button("Mark done", GUILayout.Width(100))) AssetChecklist.MarkDone(recipe, step, notes[step.id], "Unity editor window");
                        if (whyNot != null) GUILayout.Label(whyNot, EditorStyles.miniLabel);
                    }
                }
            }
        }

        void NewRecipe()
        {
            var prefab = Selection.activeObject as GameObject;
            var path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : "";
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) { EditorUtility.DisplayDialog("Asset Creator", "Select the asset's prefab in the Project window first.", "OK"); return; }
            var r = CreateInstance<AssetRecipe>();
            r.prefab = prefab; r.displayName = prefab.name;
            r.id = System.Text.RegularExpressions.Regex.Replace(prefab.name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            var dest = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + r.id + "-asset-recipe.asset";
            AssetDatabase.CreateAsset(r, AssetDatabase.GenerateUniqueAssetPath(dest));
            recipe = r; results.Clear(); notes.Clear();
        }
    }
}
