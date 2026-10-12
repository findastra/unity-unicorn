// Unity Unicorn: copyright-logger and copyright-catcher. A pet app by Astra.
//
// copyright-logger keeps a private dated ledger outside Assets: for every
// asset folder, where it came from, who made it, and what their terms allow.
// copyright-catcher checks a use against those terms at the moment something is added to an avatar (the
// strappers call it, or run it from the menu after adding something by hand). It records that check with the
// date and the use as it was then; it does not re-judge old uses later.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityUnicorn
{
    [Serializable]
    public class AssetTerms
    {
        public string folder;              // Assets/... top folder of the asset
        public string sourceUrl;           // where it was bought or downloaded (BOOTH, Gumroad, Jinxxy, Drive, Discord)
        public string artist;
        public string policyUrl;           // terms page, or the licence file inside the folder
        public string policySummary;       // the artist's words, short
        public string acquired;            // yyyy-mm-dd
        public bool editsAllowed = true;
        public bool multipleAccountsAllowed;
        public bool publicUploadAllowed;
        public bool redistributionAllowed; // reselling or sharing the files themselves
        public bool creditRequired;
        public string creditText;
        public string notes;
    }

    [Serializable]
    public class UseRecord
    {
        public string date;      // UTC timestamp of implementation
        public string avatar;
        public string addedObject;
        public string tool;
        public string folder;
        public string result;    // ok / outside policy / no terms logged
        public string detail;
    }

    [Serializable]
    public class Ledger
    {
        public List<AssetTerms> assets = new List<AssetTerms>();
        public List<UseRecord> uses = new List<UseRecord>();

        static string DirectoryPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "UnityUnicornReports");
        public static string Path => System.IO.Path.Combine(DirectoryPath, $"copyright-ledger-{UnicornCore.DenverNow():yyyyMMdd}.json");
        public static Ledger Load()
        {
            var latest = Directory.Exists(DirectoryPath) ? Directory.GetFiles(DirectoryPath, "copyright-ledger-*.json").OrderByDescending(file => file, StringComparer.Ordinal).FirstOrDefault() : null;
            var source = File.Exists(Path) ? Path : latest ?? UnicornCore.Root + "/Copyright/copyright-ledger.json";
            return File.Exists(source) ? JsonUtility.FromJson<Ledger>(File.ReadAllText(source)) ?? new Ledger() : new Ledger();
        }
        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path, JsonUtility.ToJson(this, true));
        }
        public AssetTerms For(string folder) => assets.FirstOrDefault(a => a.folder == folder);
    }

    // How this avatar is used right now. Saved per project; the catcher compares it with each asset's terms.
    public static class UseContext
    {
        static string K(string k) => "UnityUnicorn.Use." + Application.dataPath + "." + k;
        public static int Accounts { get => EditorPrefs.GetInt(K("accounts"), 1); set => EditorPrefs.SetInt(K("accounts"), value); }
        public static bool PublicUpload { get => EditorPrefs.GetBool(K("public"), false); set => EditorPrefs.SetBool(K("public"), value); }
        public static bool Edited { get => EditorPrefs.GetBool(K("edited"), true); set => EditorPrefs.SetBool(K("edited"), value); }
        public static bool Sharing { get => EditorPrefs.GetBool(K("sharing"), false); set => EditorPrefs.SetBool(K("sharing"), value); }
    }

    public class CopyrightLogger : EditorWindow
    {
        Ledger ledger; Vector2 scroll; string filter = ""; string selected;

        [MenuItem("Tools/Unity Unicorn/copyright-logger", false, 60)]
        static void Open() { var w = GetWindow<CopyrightLogger>(true, "Unity Unicorn · copyright-logger"); w.minSize = new Vector2(560, 520); w.Refresh(); }

        static readonly Regex Skip = new Regex("^Assets/(UnityUnicorn|Editor|Plugins|Resources|StreamingAssets|Gizmos|_PoiyomiShaders|VRCSDK|Thry)$", RegexOptions.IgnoreCase);

        public static IEnumerable<string> AssetFolders() =>
            AssetDatabase.GetSubFolders("Assets").Where(f => !Skip.IsMatch(f));

        void Refresh()
        {
            ledger = Ledger.Load();
            foreach (var f in AssetFolders())
                if (ledger.For(f) == null) ledger.assets.Add(Guess(f));
        }

        // Prefill from what the folder already says: .url shortcuts, readme/licence/terms files.
        static AssetTerms Guess(string folder)
        {
            var t = new AssetTerms { folder = folder };
            var full = System.IO.Path.GetFullPath(folder);
            if (!Directory.Exists(full)) return t;
            foreach (var f in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories).Take(4000))
            {
                var name = System.IO.Path.GetFileName(f).ToLower();
                if (name.EndsWith(".url") && string.IsNullOrEmpty(t.sourceUrl))
                {
                    var m = Regex.Match(File.ReadAllText(f), "URL=(.+)");
                    if (m.Success) t.sourceUrl = m.Groups[1].Value.Trim();
                }
                if (Regex.IsMatch(name, "(licen[cs]e|terms|tos|readme|rules|policy).*\\.(txt|md|pdf|html)$") && string.IsNullOrEmpty(t.policyUrl))
                    t.policyUrl = "Assets" + f.Substring(Application.dataPath.Length).Replace('\\', '/');
            }
            var by = Regex.Match(folder, "by ([^/]+)$", RegexOptions.IgnoreCase);
            if (by.Success) t.artist = by.Groups[1].Value;
            return t;
        }

        void OnGUI()
        {
            if (ledger == null) Refresh();
            EditorGUILayout.HelpBox("One row per asset folder. Fill in where it came from, who made it and what their terms allow. copyright-catcher checks new uses against this when you add something to an avatar.", MessageType.None);
            filter = EditorGUILayout.TextField("Find", filter);
            EditorGUILayout.BeginHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(220));
            foreach (var a in ledger.assets.Where(a => a.folder.ToLower().Contains(filter.ToLower())))
            {
                var label = (Complete(a) ? "✓ " : "• ") + a.folder.Substring("Assets/".Length);
                if (GUILayout.Button(label, selected == a.folder ? EditorStyles.boldLabel : EditorStyles.label)) selected = a.folder;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginVertical();
            var s = ledger.For(selected);
            if (s != null)
            {
                EditorGUILayout.LabelField(s.folder, EditorStyles.boldLabel);
                s.sourceUrl = EditorGUILayout.TextField("Where you got it", s.sourceUrl);
                s.artist = EditorGUILayout.TextField("Artist", s.artist);
                s.policyUrl = EditorGUILayout.TextField("Terms (link or file)", s.policyUrl);
                EditorGUILayout.LabelField("Their terms, short");
                s.policySummary = EditorGUILayout.TextArea(s.policySummary ?? "", GUILayout.MinHeight(50));
                s.acquired = EditorGUILayout.TextField("Got it on (yyyy-mm-dd)", s.acquired);
                s.editsAllowed = EditorGUILayout.ToggleLeft("Edits allowed", s.editsAllowed);
                s.multipleAccountsAllowed = EditorGUILayout.ToggleLeft("Upload to more than one account allowed", s.multipleAccountsAllowed);
                s.publicUploadAllowed = EditorGUILayout.ToggleLeft("Public avatar upload allowed", s.publicUploadAllowed);
                s.redistributionAllowed = EditorGUILayout.ToggleLeft("Sharing or reselling the files allowed", s.redistributionAllowed);
                s.creditRequired = EditorGUILayout.ToggleLeft("Credit required", s.creditRequired);
                if (s.creditRequired) s.creditText = EditorGUILayout.TextField("Credit text", s.creditText);
                s.notes = EditorGUILayout.TextField("Notes", s.notes);
                if (!string.IsNullOrEmpty(s.sourceUrl) && GUILayout.Button("Open source link")) Application.OpenURL(s.sourceUrl);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rescan folders")) { Save(); Refresh(); }
            if (GUILayout.Button("Save ledger", GUILayout.Height(28))) Save();
            EditorGUILayout.EndHorizontal();
        }

        static bool Complete(AssetTerms a) => !string.IsNullOrEmpty(a.sourceUrl) && !string.IsNullOrEmpty(a.artist) && (!string.IsNullOrEmpty(a.policySummary) || !string.IsNullOrEmpty(a.policyUrl));

        void Save()
        {
            ledger.Save();
            var missing = ledger.assets.Where(a => !Complete(a)).Select(a => $"no source, artist or terms yet: {a.folder}").ToList();
            UnicornCore.Report("copyright-logger", "completed", $"{ledger.assets.Count} asset folders, {ledger.assets.Count - missing.Count} complete", missing);
        }
    }

    public class CopyrightCatcher : EditorWindow
    {
        GameObject avatar; string result = "";

        [MenuItem("Tools/Unity Unicorn/copyright-catcher", false, 61)]
        static void Open() { var w = GetWindow<CopyrightCatcher>(true, "Unity Unicorn · copyright-catcher"); w.avatar = UnicornCore.SelectedAvatar(); w.minSize = new Vector2(440, 300); }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Set how this avatar is used now. The strappers check every add-on against its logged terms when they add it. To check something you added by hand, select it and press Check.", MessageType.None);
            UseContext.Accounts = EditorGUILayout.IntSlider("Accounts it's uploaded to", UseContext.Accounts, 1, 5);
            UseContext.PublicUpload = EditorGUILayout.ToggleLeft("Uploaded as a public avatar", UseContext.PublicUpload);
            UseContext.Edited = EditorGUILayout.ToggleLeft("Assets get edited (recoloured, moved, retextured)", UseContext.Edited);
            UseContext.Sharing = EditorGUILayout.ToggleLeft("The avatar or its files get shared or sold", UseContext.Sharing);
            avatar = (GameObject)EditorGUILayout.ObjectField("Avatar", avatar, typeof(GameObject), true);
            GUI.enabled = avatar && Selection.activeGameObject;
            if (GUILayout.Button("Check selected object now", GUILayout.Height(28)))
            {
                var notes = new List<string>();
                CheckAtImplementation(avatar, Selection.activeGameObject, "copyright-catcher", notes);
                result = string.Join("\n", notes) + "\nReport: " + UnicornCore.Report("copyright-catcher", notes.Any(n => n.Contains("outside")) ? "failed" : "completed", $"Checked {Selection.activeGameObject.name}", notes);
            }
            GUI.enabled = true;
            if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        // Called the moment something is added to an avatar. Finds every asset folder the object depends on
        // and compares the current use with that folder's logged terms. Records the result in the ledger.
        public static void CheckAtImplementation(GameObject avatar, GameObject added, string tool, List<string> notes)
        {
            var ledger = Ledger.Load();
            var deps = EditorUtility.CollectDependencies(new Object[] { added })
                .Select(AssetDatabase.GetAssetPath).Where(p => !string.IsNullOrEmpty(p) && p.StartsWith("Assets/"))
                .Select(p => string.Join("/", p.Split('/').Take(2))).Where(f => AssetDatabase.IsValidFolder(f))
                .Distinct().Where(f => !f.StartsWith(UnicornCore.Root));
            foreach (var folder in deps)
            {
                var t = ledger.For(folder);
                var problems = new List<string>();
                string res;
                if (t == null || string.IsNullOrEmpty(t.artist) && string.IsNullOrEmpty(t.policySummary) && string.IsNullOrEmpty(t.policyUrl))
                    res = "no terms logged";
                else
                {
                    if (UseContext.Accounts > 1 && !t.multipleAccountsAllowed) problems.Add($"uploaded to {UseContext.Accounts} accounts, terms allow one");
                    if (UseContext.PublicUpload && !t.publicUploadAllowed) problems.Add("public upload not allowed");
                    if (UseContext.Edited && !t.editsAllowed) problems.Add("edits not allowed");
                    if (UseContext.Sharing && !t.redistributionAllowed) problems.Add("sharing or reselling not allowed");
                    if (t.creditRequired) problems.Add("needs credit: " + (string.IsNullOrEmpty(t.creditText) ? t.artist : t.creditText));
                    res = problems.Any(p => !p.StartsWith("needs credit")) ? "outside policy" : "ok";
                }
                var detail = string.Join("; ", problems);
                ledger.uses.Add(new UseRecord
                {
                    date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), avatar = avatar ? avatar.name : "", addedObject = added.name,
                    tool = tool, folder = folder, result = res, detail = detail
                });
                notes.Add(res == "ok" ? $"copyright ok: {folder}{(detail.Length > 0 ? " (" + detail + ")" : "")}"
                    : res == "no terms logged" ? $"copyright: no terms logged for {folder} yet (copyright-logger)"
                    : $"copyright outside policy: {folder}: {detail}");
            }
            ledger.Save();
        }
    }
}
