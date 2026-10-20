// Claude, 2026-10-18. unity-asset-creator for agents: the same checklist, driven from an editor queue (no clicks).
// Menu items under Unity Unicorn/Asset Creator/ (an agent queue runs them by name):
//   Check every recipe          -> Review/asset-creator/<id>-v<version>-checks.md, nothing marked done
//   Apply confirmations file    -> reads Review/asset-creator/confirm.json and marks those steps done, in order, with
//                                  the note and who confirmed (for a person's OK given in chat, say so in "by")
//   Render previews for every recipe
//   Export every ready recipe   -> every recipe whose steps 1-11 are done for its version
// confirm.json: {"recipe":"<id>","version":"1.0.0","steps":[{"step":"plan","note":"...","by":"Astra in chat"}]}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn.AssetCreator
{
    public static class AssetCreatorAgent
    {
        const string Dir = "Review/asset-creator";

        static List<AssetRecipe> Recipes() =>
            AssetDatabase.FindAssets("t:" + nameof(AssetRecipe)).Select(g => AssetDatabase.LoadAssetAtPath<AssetRecipe>(AssetDatabase.GUIDToAssetPath(g))).Where(r => r != null).ToList();

        [MenuItem("Unity Unicorn/Asset Creator/Check every recipe")]
        public static void CheckAll()
        {
            Directory.CreateDirectory(Dir);
            foreach (var r in Recipes())
            {
                var lines = new List<string> { "# " + r.id + " v" + r.version + " checks", "Run " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"), "" };
                for (int i = 0; i < AssetChecklist.Steps.Length; i++)
                {
                    var st = AssetChecklist.Steps[i]; var res = AssetChecklist.Run(st, r); var s = r.State(st.id);
                    lines.Add("## " + (i + 1) + ". " + st.title + ": " + (r.IsDone(st.id) ? "DONE (" + s.doneAt + ", " + s.doneBy + (s.note != "" ? ": " + s.note : "") + ")" : res.status.ToString().ToUpperInvariant()));
                    if (st.confirm != null) lines.Add("Confirm: " + st.confirm);
                    lines.AddRange(res.lines.Select(l => "- " + l)); lines.Add("");
                }
                File.WriteAllText(Path.Combine(Dir, r.id + "-v" + r.version + "-checks.md"), string.Join("\n", lines));
            }
            AssetDatabase.SaveAssets();
        }

        [Serializable] class ConfirmStep { public string step, note, by; }
        [Serializable] class Confirm { public string recipe, version; public List<ConfirmStep> steps = new List<ConfirmStep>(); }

        [MenuItem("Unity Unicorn/Asset Creator/Apply confirmations file")]
        public static void ApplyConfirmations()
        {
            var path = Path.Combine(Dir, "confirm.json");
            var log = new List<string> { "# Confirmations " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") };
            try
            {
                if (!File.Exists(path)) throw new Exception("no " + path);
                var c = JsonUtility.FromJson<Confirm>(File.ReadAllText(path));
                var r = Recipes().FirstOrDefault(x => x.id == c.recipe) ?? throw new Exception("no recipe with id " + c.recipe);
                if (c.version != r.version) throw new Exception("confirm.json is for v" + c.version + " but the recipe is at v" + r.version);
                foreach (var cs in c.steps)
                {
                    var st = AssetChecklist.Steps.FirstOrDefault(x => x.id == cs.step);
                    if (st == null) { log.Add("- " + cs.step + ": no such step; stopped"); break; }
                    if (st.id == "export") { log.Add("- export: use 'Export every ready recipe'; stopped"); break; }
                    var res = AssetChecklist.Run(st, r);
                    var whyNot = AssetChecklist.WhyNot(r, st, res, cs.note);
                    if (whyNot != null) { log.Add("- " + st.id + ": NOT done (" + whyNot + ")\n  " + res.Text.Replace("\n", "\n  ")); break; }
                    AssetChecklist.MarkDone(r, st, cs.note, string.IsNullOrWhiteSpace(cs.by) ? "agent" : cs.by);
                    log.Add("- " + st.id + ": done (" + res.status + ") by " + cs.by + ": " + cs.note);
                }
                File.Delete(path);
            }
            catch (Exception e) { log.Add("FAILED: " + e.Message); }
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, "confirm-result.md"), string.Join("\n", log) + "\n");
        }

        [MenuItem("Unity Unicorn/Asset Creator/Render previews for every recipe")]
        public static void PreviewAll() { foreach (var r in Recipes()) if (r.prefab != null) AssetExporter.RenderPreviews(r); }

        [MenuItem("Unity Unicorn/Asset Creator/Export every ready recipe")]
        public static void ExportReady()
        {
            var log = new List<string> { "# Export " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") };
            foreach (var r in Recipes())
            {
                var res = AssetChecklist.Run(AssetChecklist.Get("export"), r);
                if (res.status == Status.Fail) { log.Add("- " + r.id + " v" + r.version + ": not ready (" + res.Text.Replace("\n", "; ") + ")"); continue; }
                try { log.Add("- " + r.id + " v" + r.version + ": exported to " + AssetExporter.Export(r, "agent queue")); }
                catch (Exception e) { log.Add("- " + r.id + ": FAILED " + e.Message); }
            }
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, "export-result.md"), string.Join("\n", log) + "\n");
        }
    }
}
