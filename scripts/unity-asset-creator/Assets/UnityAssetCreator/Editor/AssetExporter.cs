// Claude, 2026-10-18. unity-asset-creator: preview renders and the export (step 12).
// Previews: a temporary copy of the prefab, far from everything else in the open scene, lit by that scene's own sun,
// sky and ambient light ("in the world's light"), front / three-quarter / top. The copy is never saved.
// Export: <project>/Exports/unity-assets-library/<id>/<version>/ with the .unitypackage (the asset's folder and its
// dependencies), the previews and manifest.json. publish-asset-20261018.py turns that into a library pull request.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityUnicorn.AssetCreator
{
    public static class AssetExporter
    {
        public const string ToolId = "unity-asset-creator";
        public const string ToolVersion = "1.0.0";

        public static List<string> RenderPreviews(AssetRecipe r)
        {
            if (r.prefab == null) throw new Exception("No prefab on " + r.name);
            Directory.CreateDirectory(r.PreviewDir);
            var files = new List<string>();
            var copy = UnityEngine.Object.Instantiate(r.prefab, new Vector3(4000, 0, 4000), Quaternion.identity);
            foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            var camGo = new GameObject("_asset preview camera") { hideFlags = HideFlags.DontSave };
            try
            {
                foreach (var ps in copy.GetComponentsInChildren<ParticleSystem>(true)) ps.Simulate(6f, false, true, true);
                var rs = copy.GetComponentsInChildren<Renderer>(true).Where(x => !(x is ParticleSystemRenderer)).ToList();
                var b = rs.Count > 0 ? rs[0].bounds : new Bounds(copy.transform.position + Vector3.up, Vector3.one);
                foreach (var x in rs) b.Encapsulate(x.bounds);
                float rad = Mathf.Max(b.extents.magnitude, .3f);
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox; cam.fieldOfView = 32; cam.nearClipPlane = .02f; cam.farClipPlane = 3000; cam.allowHDR = true; cam.allowMSAA = true;
                var post = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
                // the world's own post-processing (e.g. bloom on the VRChat reference camera, which stays inactive)
                var refLayer = post != null ? Resources.FindObjectsOfTypeAll(post).OfType<Component>().FirstOrDefault(x => x.gameObject.scene.IsValid()) : null;
                if (refLayer != null)
                {
                    // set the layer up explicitly (copying it isn't enough): volume trigger, the world's volume layers, resources
                    var l = camGo.AddComponent(post);
                    post.GetField("volumeTrigger")?.SetValue(l, camGo.transform);
                    var mask = post.GetField("volumeLayer"); if (mask != null) mask.SetValue(l, mask.GetValue(refLayer));
                    var resType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessResources, Unity.Postprocessing.Runtime");
                    var resPath = AssetDatabase.FindAssets("t:PostProcessResources").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                    if (resType != null && resPath != null) post.GetMethod("Init", new[] { resType })?.Invoke(l, new object[] { AssetDatabase.LoadAssetAtPath(resPath, resType) });
                }
                float d = rad / Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * 1.08f;
                var c = b.center; var fwd = copy.transform.forward; var right = copy.transform.right;
                var shots = new (string name, Vector3 dir)[]
                {
                    ("front", (fwd + Vector3.up * .12f).normalized),
                    ("three-quarter", (fwd + right * .9f + Vector3.up * .45f).normalized),
                    ("top", (fwd * .45f + Vector3.up).normalized),
                };
                foreach (var s in shots)
                {
                    camGo.transform.position = c + s.dir * d;
                    camGo.transform.rotation = Quaternion.LookRotation(c - camGo.transform.position, Vector3.up);
                    var file = r.PreviewDir + "/" + r.id + "-v" + r.version + "-" + s.name + ".png";
                    Render(cam, 1024, 1024, file); files.Add(file);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(camGo); }
            Debug.Log("[asset-creator] " + files.Count + " previews for " + r.id + " v" + r.version);
            return files;
        }

        static void Render(Camera cam, int w, int h, string file)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            var prev = RenderTexture.active;
            try
            {
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
            }
            finally { cam.targetTexture = null; RenderTexture.active = prev; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }

        // ---------- export ----------

        public static string Export(AssetRecipe r, string by)
        {
            var exportStep = AssetChecklist.Get("export");
            var res = AssetChecklist.Run(exportStep, r);
            if (res.status == Status.Fail) throw new Exception("Not ready to export: " + res.Text);
            var scan = Scan.Of(r);
            Directory.CreateDirectory(r.ExportDir);
            var package = r.ExportDir + "/" + r.id + "-v" + r.version + ".unitypackage";
            // the prefab and everything it uses: not every file that happens to sit in the folder (the recipe stays out),
            // and not the shaders the shaders-shop installs (listed under requires_shaders instead)
            var files = AssetDatabase.GetDependencies(r.PrefabPath, true).Where(p => p.StartsWith("Assets/") && !ShopShaders.IsShopPath(p)).ToArray();
            AssetDatabase.ExportPackage(files, package, ExportPackageOptions.Default);
            if (!File.Exists(package)) throw new Exception("Unity didn't write " + package);

            var m = new Manifest
            {
                id = r.id, name = r.displayName, kind = r.kind, version = r.version, description = r.description,
                platforms = new List<string>(), creator = r.creator, source = r.source, license = r.license, attribution = r.attribution, public_ok = r.publicOk,
                changelog = r.changelog, exported_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };
            if (r.forPC) m.platforms.Add("pc"); if (r.forAndroid) m.platforms.Add("android");
            m.made_in.project = Application.productName; m.made_in.asset_folder = r.Folder; m.made_in.git_commit = Git("rev-parse HEAD"); m.made_in.git_branch = Git("rev-parse --abbrev-ref HEAD");
            m.made_with.tool = ToolId; m.made_with.tool_version = ToolVersion; m.made_with.unity = Application.unityVersion; m.made_with.vrchat_worlds_sdk = PackageVersion("com.vrchat.worlds");
            m.stats.triangles = scan.triangles; m.stats.materials = scan.materials.Count; m.stats.textures = scan.textures.Count;
            m.stats.max_texture_px = scan.textures.Count > 0 ? scan.textures.Max(t => Mathf.Max(t.width, t.height)) : 0;
            m.stats.particles_max = scan.particles.Sum(p => p.main.maxParticles); m.stats.colliders = scan.colliders.Count;
            if (scan.hasVisual) m.stats.size_m = new[] { Round(scan.visual.size.x), Round(scan.visual.size.y), Round(scan.visual.size.z) };
            m.requires_shaders = ShopShaders.For(scan.materials);
            m.files.package = new FileEntry { name = Path.GetFileName(package), bytes = new FileInfo(package).Length, sha256 = Sha(package) };
            if (Directory.Exists(r.PreviewDir))
                foreach (var p in Directory.GetFiles(r.PreviewDir, "*.png").OrderBy(x => x))
                    m.files.previews.Add(new FileEntry { name = "previews/" + Path.GetFileName(p), bytes = new FileInfo(p).Length, sha256 = Sha(p) });
            foreach (var st in AssetChecklist.Steps)
            {
                if (st.id == "export") continue;
                var s = r.State(st.id);
                m.checklist.Add(new ChecklistEntry { step = st.id, title = st.title, status = s.status, note = s.note, done_at = s.doneAt, done_by = s.doneBy });
            }
            File.WriteAllText(r.ExportDir + "/manifest.json", JsonUtility.ToJson(m, true) + "\n");

            AssetChecklist.MarkDone(r, exportStep, "exported to " + r.ExportDir, by);
            if (!r.exportedVersions.Contains(r.version)) r.exportedVersions.Add(r.version);
            EditorUtility.SetDirty(r); AssetDatabase.SaveAssets();
            WriteUsage(r, package);
            Debug.Log("[asset-creator] exported " + r.id + " v" + r.version + " to " + r.ExportDir);
            return r.ExportDir;
        }

        // Unity Unicorn tool-usage record (unity-unicorn.tool-usage/v1). Private run history: Review/ stays out of Git.
        static void WriteUsage(AssetRecipe r, string package)
        {
            Directory.CreateDirectory("Review/asset-creator");
            var u = new Usage { tool_id = ToolId, completed_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), status = "completed",
                summary = "Exported " + r.id + " v" + r.version, report_filename = "manifest.json", report_sha256 = Sha(r.ExportDir + "/manifest.json"), evidence_path = r.ExportDir };
            u.project.name = Application.productName;
            File.WriteAllText("Review/asset-creator/usage-" + r.id + "-v" + r.version + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json", JsonUtility.ToJson(u, true) + "\n");
        }

        static float Round(float v) => Mathf.Round(v * 100f) / 100f;

        static string Sha(string path)
        {
            using (var s = File.OpenRead(path)) using (var h = SHA256.Create())
                return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
        }

        static string PackageVersion(string name)
        {
            var p = "Packages/" + name + "/package.json";
            if (!File.Exists(p)) return "";
            var mm = Regex.Match(File.ReadAllText(p), "\"version\"\\s*:\\s*\"([^\"]+)\"");
            return mm.Success ? mm.Groups[1].Value : "";
        }

        static string Git(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi)) { var o = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(5000); return o; }
            }
            catch { return ""; }
        }

        [Serializable] public class FileEntry { public string name; public long bytes; public string sha256; }
        [Serializable] public class Files { public FileEntry package; public List<FileEntry> previews = new List<FileEntry>(); }
        [Serializable] public class MadeIn { public string project, asset_folder, git_branch, git_commit; }
        [Serializable] public class MadeWith { public string tool, tool_version, unity, vrchat_worlds_sdk; }
        [Serializable] public class Stats { public int triangles, materials, textures, max_texture_px, particles_max, colliders; public float[] size_m = new float[0]; }
        [Serializable] public class ChecklistEntry { public string step, title, status, note, done_at, done_by; }
        [Serializable]
        public class Manifest
        {
            public string schema = "unity-unicorn.asset/v1";
            public string id, name, kind, version, description;
            public List<string> platforms;
            public string creator, source, license, attribution; public bool public_ok;
            public MadeIn made_in = new MadeIn(); public MadeWith made_with = new MadeWith(); public Stats stats = new Stats(); public Files files = new Files();
            public List<ShopShaders.Required> requires_shaders = new List<ShopShaders.Required>();
            public List<ChecklistEntry> checklist = new List<ChecklistEntry>();
            public string changelog, exported_at;
        }
        [Serializable] public class UsageProject { public string name; }
        [Serializable]
        public class Usage
        {
            public string schema = "unity-unicorn.tool-usage/v1";
            public string tool_id; public UsageProject project = new UsageProject();
            public string completed_at, status, summary, report_filename, report_sha256, evidence_path;
        }
    }
}
