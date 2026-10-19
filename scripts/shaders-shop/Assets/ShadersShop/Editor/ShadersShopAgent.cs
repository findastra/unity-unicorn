// Claude, 2026-10-18. shaders-shop for agents (no clicks), runnable from an editor queue by menu name:
//   Unity Unicorn/Shaders Shop/Scan this world          -> docs/shaders-in-world.json + Review/shaders-shop/scan.md
//   Unity Unicorn/Shaders Shop/Install from request file -> Review/shaders-shop/install.json:
//        {"ids": ["liltoon"], "approved_by": "Astra in chat, 2026-10-18"}
//     Only with the person's OK for exactly those shaders (the window's dialog does this for a person at the editor).
//     Results go to Review/shaders-shop/install-result.md as each install finishes. What's left is kept in
//     Review/shaders-shop/install-pending.json, so the list carries on after Unity reloads its scripts (installing a
//     package recompiles, which reloads the editor's code and would otherwise forget the rest).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn.ShadersShop
{
    [InitializeOnLoad]
    public static class ShadersShopAgent
    {
        const string Dir = "Review/shaders-shop";
        const string Pending = Dir + "/install-pending.json";
        const string Result = Dir + "/install-result.md";

        // Unfinished installs carry on by themselves: every few seconds, when the editor is idle, look for the pending file.
        static double nextLook;
        static ShadersShopAgent()
        {
            if (Application.isBatchMode) return;
            EditorApplication.update += Watch;
        }

        static void Watch()
        {
            if (EditorApplication.timeSinceStartup < nextLook) return;
            nextLook = EditorApplication.timeSinceStartup + 3;
            if (ShadersInstaller.Busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Pending)) Resume();
        }

        [MenuItem("Unity Unicorn/Shaders Shop/Resume pending installs")]
        public static void ResumeMenu() => Resume();

        [MenuItem("Unity Unicorn/Shaders Shop/Scan this world")]
        public static void ScanWorld()
        {
            var rec = WorldShaders.Scan(Catalog.Load());
            rec.scanned_by = "shaders-shop (agent queue)";
            WorldShaders.Save(rec);
            Directory.CreateDirectory(Dir);
            var lines = new List<string> { "# Shaders in " + rec.world, "Scanned " + rec.scanned_at + " (" + rec.scenes.Count + " scenes: " + string.Join(", ", rec.scenes) + ")", "" };
            foreach (var u in rec.shaders) lines.Add("- " + u.shader + ": " + u.catalog_name + " (" + u.license + "), " + u.source + (u.version != "" ? " " + u.version : "") + ", " + u.materials.Count + " materials");
            File.WriteAllText(Path.Combine(Dir, "scan.md"), string.Join("\n", lines) + "\n");
        }

        // Unity keeps shader compile errors out of Editor.log; this writes them out for every shader the shop knows about
        // (Unity Stable's own, and the installed packages), plus the world's own shaders.
        [MenuItem("Unity Unicorn/Shaders Shop/Report shader errors")]
        public static void ShaderErrors()
        {
            Directory.CreateDirectory(Dir);
            var lines = new List<string> { "# Shader compile messages " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"), "" };
            var roots = new[] { "Assets/ShadersShop", "Assets/LiminalDoors", "Assets/Mochie", "Packages/jp.lilxyzw.liltoon", "Packages/com.poiyomi.toon", "Packages/sh.orels.shaders" };
            int bad = 0, n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", roots.Where(r => Directory.Exists(r)).ToArray()))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (sh == null) continue; n++;
                var msgs = ShaderUtil.GetShaderMessages(sh);
                bool unsupported = !sh.isSupported;
                if (msgs.Length == 0 && !unsupported) continue;
                if (msgs.Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) || unsupported) bad++;
                lines.Add("## " + sh.name + (unsupported ? " (NOT SUPPORTED)" : "") + " - " + path);
                foreach (var m in msgs) lines.Add("- " + m.severity + " line " + m.line + ": " + m.message + (string.IsNullOrEmpty(m.messageDetails) ? "" : " | " + m.messageDetails.Replace("\n", " ")));
            }
            lines.Insert(1, n + " shaders checked, " + bad + " with errors");
            File.WriteAllText(Path.Combine(Dir, "shader-errors.md"), string.Join("\n", lines) + "\n");
        }

        [Serializable] class Request { public List<string> ids = new List<string>(); public string approved_by; }

        [MenuItem("Unity Unicorn/Shaders Shop/Install from request file")]
        public static void InstallFromRequest()
        {
            var path = Path.Combine(Dir, "install.json");
            Directory.CreateDirectory(Dir);
            Request req;
            try { req = JsonUtility.FromJson<Request>(File.ReadAllText(path)); File.Delete(path); }
            catch (Exception e) { File.WriteAllText(Result, "FAILED: no readable " + path + " (" + e.Message + ")\n"); return; }
            if (string.IsNullOrWhiteSpace(req.approved_by)) { File.WriteAllText(Result, "REFUSED: install.json has no approved_by\n"); return; }
            File.AppendAllText(Result, "# Shader installs " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") + " (approved by " + req.approved_by + ")\n");
            File.WriteAllText(Pending, JsonUtility.ToJson(req));
            Resume();
        }

        static void Resume()
        {
            if (ShadersInstaller.Busy || !File.Exists(Pending)) return;
            var req = JsonUtility.FromJson<Request>(File.ReadAllText(Pending));
            if (req.ids.Count == 0) { File.Delete(Pending); File.AppendAllText(Result, "finished\n"); return; }
            var id = req.ids[0];
            var e = Catalog.Load().shaders.FirstOrDefault(s => s.id == id);
            if (e == null) { File.AppendAllText(Result, "- " + id + ": not in the catalog\n"); Done(req); return; }
            ShadersInstaller.Install(e, req.approved_by, (ok, msg) =>
            {
                File.AppendAllText(Result, "- " + e.id + " " + e.version + ": " + (ok ? "done, " : "NOT done, ") + msg + "\n");
                Done(req);
            });
        }

        // Drop the finished id from the pending file, then go on (now, or after the reload the install may cause).
        static void Done(Request req)
        {
            req.ids.RemoveAt(0);
            File.WriteAllText(Pending, JsonUtility.ToJson(req));   // Watch picks up the next one (also after a reload)
        }
    }
}
