// Smoke test for vine-spawn-with-trumpet-flowers (Claude Opus 5.5, 2026-10-19). Not part of the tool. Run it in a
// throwaway project that also holds the tool (Assets/UnityUnicorn/Editor/), never in a world that is open:
//   Unity -batchmode -projectPath <scratch project> -executeMethod UnityUnicornTests.VineSpawnSmoke20261019.Run -logFile run.log
// It builds a wall, an arch and a column without colliders, grows vines on them, checks the meshes, the shader and the job
// inbox, renders two pictures to VineSmoke/, and writes vine-smoke-result.txt. The exit code is 0 when every check passes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityUnicorn;
using Object = UnityEngine.Object;

namespace UnityUnicornTests
{
    public static class VineSpawnSmoke20261019
    {
        public static void Run()
        {
            var log = new List<string>(); int fails = 0;
            void Check(bool ok, string what) { log.Add((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
            try
            {
                if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var stone = new Material(Shader.Find("Standard")) { color = new Color(0.66f, 0.63f, 0.6f) };
                stone.SetFloat("_Glossiness", 0.15f);
                GameObject Shape(PrimitiveType type, string name, Vector3 pos, Vector3 size, Transform parent = null)
                {
                    var g = GameObject.CreatePrimitive(type); g.name = name;
                    g.transform.position = pos; g.transform.localScale = size; if (parent) g.transform.SetParent(parent, true);
                    g.GetComponent<Renderer>().sharedMaterial = stone;
                    Object.DestroyImmediate(g.GetComponent<Collider>());          // the tool must not need colliders
                    return g;
                }
                Shape(PrimitiveType.Cube, "Ground", new Vector3(0, -0.05f, 0), new Vector3(16, 0.1f, 9));
                var wall = Shape(PrimitiveType.Cube, "Wall", new Vector3(-3.4f, 1.6f, 0), new Vector3(4f, 3.2f, 0.4f));
                var arch = new GameObject("Arch");
                Shape(PrimitiveType.Cube, "Left pier", new Vector3(0.1f, 1.4f, 0), new Vector3(0.6f, 2.8f, 0.6f), arch.transform);
                Shape(PrimitiveType.Cube, "Right pier", new Vector3(2.1f, 1.4f, 0), new Vector3(0.6f, 2.8f, 0.6f), arch.transform);
                Shape(PrimitiveType.Cube, "Lintel", new Vector3(1.1f, 3.05f, 0), new Vector3(2.8f, 0.5f, 0.7f), arch.transform);
                var column = Shape(PrimitiveType.Cylinder, "Column", new Vector3(4.3f, 1.6f, 0.2f), new Vector3(0.5f, 1.6f, 0.5f));

                var colours = new[] { "#ff5a1f", "#ff3cc7", "#ffd23f", "#a855f7", "#2ef2ff" }.Select(VineJobs20261019.Parse).ToList();
                var lines = new List<string>();
                var root = VineBuilder20261019.Grow(new[] { wall, arch, column }, new VineSettings20261019 { vinesPerObject = 7, lengthM = 3.6f, seed = 7, glow = 0.25f }, colours, lines);
                log.AddRange(lines.Select(l => "  " + l));
                Check(root != null, "Grow made a vines object");
                var parts = root ? root.GetComponentsInChildren<MeshFilter>() : new MeshFilter[0];
                Check(parts.Length == 3, "one vine mesh per selected object (3), found " + parts.Length);
                foreach (var f in parts)
                {
                    var m = f.sharedMesh; var col = m.colors; var v = m.vertices; var nrm = m.normals;
                    Check(m.triangles.Length / 3 > 500, f.name + ": " + m.triangles.Length / 3 + " triangles");
                    Check(nrm.All(x => !float.IsNaN(x.x) && x.sqrMagnitude > 0.5f), f.name + ": every normal is finite");
                    Check(!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)), f.name + ": saved as " + AssetDatabase.GetAssetPath(m));
                    Check(col.Count(c => c.a > 0.99f) > 100 && col.Count(c => c.a < 0.01f) > 100, f.name + ": has petals (alpha 1) and stems and leaves (alpha 0)");
                    var target = f.name.StartsWith("Wall") ? wall : f.name.StartsWith("Arch") ? arch : column;
                    var b = new Bounds(target.transform.position, Vector3.zero);
                    foreach (var r in target.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    b.Expand(0.7f);
                    int outside = v.Count(p => !b.Contains(p));
                    Check(outside == 0, f.name + ": every vertex within 0.35 m of the object (" + outside + " outside)");
                }
                // every picked colour shows up on some petal (petals keep their hue from throat to rim)
                var petals = parts.SelectMany(f => f.sharedMesh.colors).Where(c => c.a > 0.99f).Select(c => c.gamma).ToList();
                foreach (var want in colours)
                {
                    Color.RGBToHSV(want, out var h, out _, out _);
                    Check(petals.Any(c => { Color.RGBToHSV(c, out var ph, out var ps, out _); return ps > 0.3f && Mathf.Abs(Mathf.DeltaAngle(ph * 360, h * 360)) < 12; }), "petals in #" + ColorUtility.ToHtmlStringRGB(want));
                }
                var shader = Shader.Find(VineBuilder20261019.ShaderName);
                Check(shader != null, "the Unity Stable/Vine Bloom shader was written and found");
                Check(shader != null && !ShaderUtil.ShaderHasError(shader), "the shader compiles without errors");

                var lines2 = new List<string>();
                var hang = VineBuilder20261019.Grow(new[] { arch }, new VineSettings20261019 { grow = VineGrow20261019.Hang, vinesPerObject = 4, seed = 11 }, colours.Take(2).ToList(), lines2);
                log.AddRange(lines2.Select(l => "  " + l));
                Check(hang != null, "hanging vines grow from the top down");
                Undo.PerformUndo();
                Check(hang == null && root != null, "one Undo takes the last vines away and keeps the first ones");

                // the job inbox
                VineWorlds20261019.SetThisWorld("liminal-doors");
                Directory.CreateDirectory("UnityUnicornInbox");
                string Job(string world, int n) => "{\"schema\":\"unity-unicorn.vine-spawn-job/v1\",\"tool_id\":\"vine-spawn-with-trumpet-flowers\",\"world\":\"" + world +
                    "\",\"made_at\":\"2026-10-19T20:00:00Z\",\"flower_colors\":[" + string.Join(",", Enumerable.Range(0, n).Select(i => "\"#ff3cc" + (i % 10) + "\"")) +
                    "],\"settings\":{\"vines\":9,\"length_m\":4.5,\"flowers_per_m\":4,\"grow\":\"both\",\"seed\":42}}";
                string other = "UnityUnicornInbox/unity-unicorn-vine-spawn-piranesi-house-20261019-200001.json";
                string bad = "UnityUnicornInbox/unity-unicorn-vine-spawn-liminal-doors-20261019-200002.json";
                string good = "UnityUnicornInbox/unity-unicorn-vine-spawn-liminal-doors-20261019-200003 (1).json";
                File.WriteAllText(other, Job("piranesi-house", 3));
                File.WriteAllText(bad, Job("liminal-doors", 11));
                File.WriteAllText(good, Job("liminal-doors", 3));
                File.SetLastWriteTimeUtc(good, DateTime.UtcNow.AddSeconds(-30));
                bool took;
                try { took = VineInbox20261019.LookNow(false); }
                catch (Exception e) { log.Add("  (the window could not open in batch mode: " + e.GetType().Name + ")"); took = !File.Exists(good); }
                Check(took, "a job for this world is taken from the inbox");
                Check(!File.Exists(good) && File.Exists("UnityUnicornReports/jobs/unity-unicorn-vine-spawn-liminal-doors-20261019-200003(1).json"), "the taken job moved to UnityUnicornReports/jobs/");
                Check(File.Exists(other), "a job for another world stays for that world's editor");
                Check(File.Exists(bad), "a job with 11 colours is refused and left alone");
                var j = JsonUtility.FromJson<VineJob20261019>(Job("liminal-doors", 3));
                var st = VineJobs20261019.Settings(j);
                Check(st.vinesPerObject == 9 && Mathf.Approximately(st.lengthM, 4.5f) && st.grow == VineGrow20261019.Both && st.seed == 42, "job settings carry over");
                j.world = "somewhere"; Check(!VineJobs20261019.Check(j, out _), "an unknown world is refused");
                j.world = "liminal-doors"; j.flower_colors = new[] { "red" }; Check(!VineJobs20261019.Check(j, out _), "a colour that is not #rrggbb is refused");
                VineBuilder20261019.WriteReceipt(lines, "completed", "");
                var receipt = Directory.GetFiles("UnityUnicornReports", "vine-spawn-with-trumpet-flowers-*.json").OrderBy(x => x).LastOrDefault();
                Check(receipt != null && File.ReadAllText(receipt).Contains("\"schema\": \"unity-unicorn.tool-usage/v1\""), "a usage receipt is written");

                Directory.CreateDirectory("VineSmoke");
                Render(new Vector3(0.6f, 2.1f, -7.2f), new Vector3(0.6f, 1.5f, 0), 42, "VineSmoke/vines-wide.png");
                Render(new Vector3(-2.2f, 2.0f, -1.6f), new Vector3(-2.9f, 1.9f, 0.1f), 34, "VineSmoke/vines-close.png");
                Check(File.Exists("VineSmoke/vines-wide.png") && File.Exists("VineSmoke/vines-close.png"), "two pictures rendered");
            }
            catch (Exception e) { log.Add("FAIL exception: " + e); fails++; }
            log.Insert(0, (fails == 0 ? "ALL PASS" : fails + " FAILED") + " - vine-spawn-with-trumpet-flowers smoke test, " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            File.WriteAllLines("vine-smoke-result.txt", log);
            EditorApplication.Exit(fails == 0 ? 0 : 1);
        }

        // The hand-off from the page: put a job file the page saved into UnityUnicornInbox/ (or Downloads), then run
        //   Unity -batchmode -projectPath <scratch project> -executeMethod UnityUnicornTests.VineSpawnSmoke20261019.RunPageJob
        // It takes the newest job for liminal-doors the way the open editor does, grows vines on a test arch and wall with the
        // job's colours and settings, and renders VineSmoke/vines-from-page-job.png.
        public static void RunPageJob()
        {
            var log = new List<string>(); int fails = 0;
            void Check(bool ok, string what) { log.Add((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
            try
            {
                if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
                VineWorlds20261019.SetThisWorld("liminal-doors");
                bool took;
                try { took = VineInbox20261019.LookNow(false); } catch (Exception e) { log.Add("  (window: " + e.GetType().Name + ")"); took = true; }
                Check(took, "the page's job was taken from the inbox");
                var kept = Directory.GetFiles("UnityUnicornReports/jobs", "unity-unicorn-vine-spawn-liminal-doors-*.json").OrderBy(File.GetLastWriteTimeUtc).LastOrDefault();
                Check(kept != null, "the job is kept in UnityUnicornReports/jobs: " + kept);
                Check(VineJobs20261019.TryRead(kept, out var job, out var why), "the page's job file reads and checks out " + (why ?? ""));
                var s = VineJobs20261019.Settings(job); var colours = VineJobs20261019.Colours(job);
                log.Add("  job: " + job.flower_colors.Length + " colours (" + string.Join(" ", job.flower_colors) + "), grow " + s.grow + ", glow " + s.glow + ", leaves " + s.leavesPerM + "/m, flowers " + s.flowersPerM + "/m");
                Check(colours.Count == job.flower_colors.Length && s.grow == VineGrow20261019.Both && Mathf.Approximately(s.glow, 1.2f), "colours, grow and glow come from the page");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var stone = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.53f, 0.56f) };
                GameObject Box(string name, Vector3 pos, Vector3 size, Transform parent = null)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.position = pos; g.transform.localScale = size;
                    if (parent) g.transform.SetParent(parent, true); g.GetComponent<Renderer>().sharedMaterial = stone; Object.DestroyImmediate(g.GetComponent<Collider>()); return g;
                }
                Box("Ground", new Vector3(0, -0.05f, 0), new Vector3(12, 0.1f, 8));
                var arch = new GameObject("Doorway");
                Box("Left post", new Vector3(-1.1f, 1.5f, 0), new Vector3(0.5f, 3f, 0.5f), arch.transform);
                Box("Right post", new Vector3(1.1f, 1.5f, 0), new Vector3(0.5f, 3f, 0.5f), arch.transform);
                Box("Lintel", new Vector3(0, 3.2f, 0), new Vector3(2.9f, 0.45f, 0.6f), arch.transform);
                var lines = new List<string>();
                s.vinesPerObject = 10;
                var root = VineBuilder20261019.Grow(new[] { arch }, s, colours, lines);
                log.AddRange(lines.Select(l => "  " + l));
                Check(root != null, "vines grown with the page's job");
                Directory.CreateDirectory("VineSmoke");
                Render(new Vector3(0.4f, 1.9f, -5.6f), new Vector3(0, 1.75f, 0), 40, "VineSmoke/vines-from-page-job.png");
                Check(File.Exists("VineSmoke/vines-from-page-job.png"), "picture rendered");
            }
            catch (Exception e) { log.Add("FAIL exception: " + e); fails++; }
            log.Insert(0, (fails == 0 ? "ALL PASS" : fails + " FAILED") + " - page job hand-off, " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            File.WriteAllLines("vine-page-job-result.txt", log);
            EditorApplication.Exit(fails == 0 ? 0 : 1);
        }

        static void Render(Vector3 from, Vector3 at, float fov, string path)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f; sun.color = new Color(1f, 0.95f, 0.88f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38, 28, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.44f, 0.5f); RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.32f); RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.17f);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.position = from; cam.transform.LookAt(at); cam.fieldOfView = fov;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.149f, 0.153f, 0.169f);
            QualitySettings.shadowDistance = 30;
            var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(sun.gameObject); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }
    }
}
