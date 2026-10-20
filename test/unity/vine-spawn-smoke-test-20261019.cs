// Smoke test for vine-spawn-with-trumpet-flowers (Claude Opus 5.5, 2026-10-19). Not part of the tool. Run it in a
// throwaway project that also holds the tool (Assets/UnityUnicorn/Editor/), never in a world that is open:
//   Unity -batchmode -projectPath <scratch project> -executeMethod UnityUnicornTests.VineSpawnSmoke20261019.Run -logFile run.log
// It builds a wall, an arch and a column without colliders, grows vines on them, checks the meshes, the shader and the job
// inbox, renders two pictures to VineSmoke/, and writes vine-smoke-result.txt. The exit code is 0 when every check passes.
// Added after the first real use (Claude Opus 5.5, 2026-10-19): the first version's shader is replaced, Leaf glow keeps leaves
// green with no scene light, old jobs without leaf_glow get the default, vines on a closed and an open phone kiosk stay
// outside, and the window's squares fit its width. The inbox looks only in the project's own UnityUnicornInbox/ here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
                VineInbox20261019.OnlyProjectInbox = true;
                if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
                // a world that installed the first version has its shader without Leaf glow; the next Grow must replace it
                Directory.CreateDirectory(Path.GetDirectoryName(ShaderAt));
                File.WriteAllText(ShaderAt, FirstShader);
                AssetDatabase.ImportAsset(ShaderAt, ImportAssetOptions.ForceSynchronousImport);
                var first = Shader.Find(VineBuilder20261019.ShaderName);
                Check(first != null && first.FindPropertyIndex("_LeafGlow") < 0, "a first-version shader (no Leaf glow) is in the project before the Grow");
                // and a material of vines grown with it, like the Pole's "Vine Bloom glow 1.5.mat"
                Directory.CreateDirectory("Assets/UnityUnicorn/Generated/Vines");
                const string OldMat = "Assets/UnityUnicorn/Generated/Vines/Vine Bloom glow 1.5.mat";
                AssetDatabase.DeleteAsset(OldMat);
                var older = new Material(first); older.SetFloat("_Glow", 1.5f); AssetDatabase.CreateAsset(older, OldMat); AssetDatabase.SaveAssets();
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
                Check(shader != null && shader.FindPropertyIndex("_LeafGlow") >= 0 && File.ReadAllText(ShaderAt).Contains("_LeafGlow"), "the first-version shader was replaced by the one with Leaf glow");
                var olderNow = AssetDatabase.LoadAssetAtPath<Material>(OldMat);
                Check(olderNow != null && olderNow.shader == shader && Mathf.Approximately(olderNow.GetFloat("_LeafGlow"), 0.4f) && Mathf.Approximately(olderNow.GetFloat("_Glow"), 1.5f),
                      "vines grown before the update get Leaf glow 0.4 and keep their Flower glow 1.5 (" + (olderNow ? olderNow.GetFloat("_LeafGlow") + ", " + olderNow.GetFloat("_Glow") : "no material") + ")");
                var mat = parts.Length > 0 ? parts[0].GetComponent<MeshRenderer>().sharedMaterial : null;
                Check(mat != null && Mathf.Approximately(mat.GetFloat("_LeafGlow"), 0.4f) && Mathf.Approximately(mat.GetFloat("_Glow"), 0.25f) && AssetDatabase.GetAssetPath(mat).EndsWith(" leaf 0.40.mat"),
                      "the material has the Grow's Flower glow (0.25) and the default Leaf glow (0.4): " + (mat ? AssetDatabase.GetAssetPath(mat) : "none"));

                // Leaf glow in a world with no scene light at all (the Pole is lit mostly by glowing materials)
                Directory.CreateDirectory("VineSmoke");
                int greenLit = Render(new Vector3(-2.2f, 2.0f, -1.6f), new Vector3(-2.9f, 1.9f, 0.1f), 34, "VineSmoke/vines-dark-leaf-glow-0.4.png", true);
                if (mat) mat.SetFloat("_LeafGlow", 0f);
                int greenOff = Render(new Vector3(-2.2f, 2.0f, -1.6f), new Vector3(-2.9f, 1.9f, 0.1f), 34, "VineSmoke/vines-dark-leaf-glow-0.png", true);
                if (mat) mat.SetFloat("_LeafGlow", 0.4f);
                log.Add("  green pixels with no scene light: Leaf glow 0.4 -> " + greenLit + ", Leaf glow 0 -> " + greenOff);
                Check(greenLit > 3000 && greenOff * 10 < greenLit, "with no scene light, leaves read green with Leaf glow 0.4 and black with 0");

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
                var j = VineJobs20261019.FromText(Job("liminal-doors", 3));
                var st = VineJobs20261019.Settings(j);
                Check(st.vinesPerObject == 9 && Mathf.Approximately(st.lengthM, 4.5f) && st.grow == VineGrow20261019.Both && st.seed == 42, "job settings carry over");
                // jobs made before Leaf glow (no leaf_glow, as Job() writes them) keep the default; 0 is a real choice
                Check(Mathf.Approximately(st.leafGlow, 0.4f), "a job without leaf_glow gets the default Leaf glow 0.4 (got " + st.leafGlow + ")");
                string WithLeafGlow(string v) => Job("liminal-doors", 3).Replace("\"seed\":42", "\"seed\":42,\"leaf_glow\":" + v);
                Check(Mathf.Approximately(VineJobs20261019.Settings(VineJobs20261019.FromText(WithLeafGlow("0"))).leafGlow, 0f), "leaf_glow 0 in a job means no Leaf glow");
                Check(Mathf.Approximately(VineJobs20261019.Settings(VineJobs20261019.FromText(WithLeafGlow("7"))).leafGlow, 1f), "leaf_glow is held to 0 to 1");
                Check(Mathf.Approximately(VineJobs20261019.Settings(VineJobs20261019.FromText(WithLeafGlow("0.65"))).leafGlow, 0.65f), "leaf_glow 0.65 carries over");
                var bare = JsonUtility.FromJson<VineJob20261019>(Job("liminal-doors", 3));
                log.Add("  (JsonUtility alone gives a missing leaf_glow as " + bare.settings.leaf_glow + "; FromText makes it -1)");
                j.world = "somewhere"; Check(!VineJobs20261019.Check(j, out _), "an unknown world is refused");
                j.world = "liminal-doors"; j.flower_colors = new[] { "red" }; Check(!VineJobs20261019.Check(j, out _), "a colour that is not #rrggbb is refused");
                VineBuilder20261019.WriteReceipt(lines, "completed", "");
                var receipt = Directory.GetFiles("UnityUnicornReports", "vine-spawn-with-trumpet-flowers-*.json").OrderBy(x => x).LastOrDefault();
                Check(receipt != null && File.ReadAllText(receipt).Contains("\"schema\": \"unity-unicorn.tool-usage/v1\""), "a usage receipt is written");

                // phone kiosks: vines start outside and stay outside (in the Pole they started on the floor inside and climbed the glass)
                foreach (var (name, doorOpen, grow) in new[] { ("Closed kiosk", false, VineGrow20261019.Both), ("Kiosk with an open door", true, VineGrow20261019.Climb) })
                {
                    var at = new Vector3(doorOpen ? 1.6f : -1.6f, 0, 3.2f);
                    var kiosk = Kiosk(name, at, doorOpen, Shape);
                    var kl = new List<string>();
                    var kr = VineBuilder20261019.Grow(new[] { kiosk }, new VineSettings20261019 { grow = grow, vinesPerObject = 10, lengthM = 3f, seed = 5 }, colours, kl);
                    log.AddRange(kl.Select(l => "  " + l));
                    var kv = kr ? kr.GetComponentsInChildren<MeshFilter>().SelectMany(f => f.sharedMesh.vertices).ToList() : new List<Vector3>();
                    var room = new Bounds(at + new Vector3(0, 1.25f, 0), new Vector3(0.9f, 2.28f, 0.9f));
                    var deep = new Bounds(room.center, room.size - Vector3.one * 0.3f);       // more than 15 cm in from every wall
                    int inside = kv.Count(room.Contains), deepIn = kv.Count(deep.Contains);
                    Check(kv.Count > 2000, name + ": vines grew (" + kv.Count + " vertices)");
                    if (!doorOpen) Check(inside == 0, name + ": no part of a vine inside the kiosk (" + inside + " of " + kv.Count + " vertices inside)");
                    else
                    {
                        // leaves on the door posts may lean a few cm into the doorway; nothing grows into the kiosk itself
                        Check(deepIn == 0, name + ": nothing more than 15 cm inside (" + deepIn + " vertices)");
                        Check(inside * 100 < kv.Count, name + ": under 1% of the vertices lean in at the doorway (" + inside + " of " + kv.Count + ")");
                    }
                    Check(!kl.Any(l => l.Contains("inner face")), name + ": every vine started on an outer face");
                }

                // the window: squares wrap to its width, from the smallest it can be to the size it opens at
                foreach (float w in new[] { 340f, 420f, VineSpawnWindow20261019.OpenWidth, 1200f })
                {
                    float wide = w - 26f;
                    int c30 = VineSpawnWindow20261019.Columns(wide, 30), c36 = VineSpawnWindow20261019.Columns(wide, 36);
                    Check(c30 * 30 + 4 <= wide && c36 * 36 + 4 <= wide && c30 >= 10, "a " + w + "-wide window: " + c30 + " palette squares and " + c36 + " chosen squares a row, all inside");
                }
                Check(VineSpawnWindow20261019.Columns(VineSpawnWindow20261019.OpenWidth - 26f, 36) >= 10, "at the size it opens at, the 10 chosen colours fit in one row");

                Render(new Vector3(0.6f, 2.1f, -7.2f), new Vector3(0.6f, 1.5f, 0), 42, "VineSmoke/vines-wide.png");
                Render(new Vector3(-2.2f, 2.0f, -1.6f), new Vector3(-2.9f, 1.9f, 0.1f), 34, "VineSmoke/vines-close.png");
                Render(new Vector3(0, 2.3f, 0.7f), new Vector3(0, 1.2f, 3.2f), 62, "VineSmoke/vines-kiosks.png");
                Check(File.Exists("VineSmoke/vines-wide.png") && File.Exists("VineSmoke/vines-close.png") && File.Exists("VineSmoke/vines-kiosks.png"), "three pictures rendered");
            }
            catch (Exception e) { log.Add("FAIL exception: " + e); fails++; }
            log.Insert(0, (fails == 0 ? "ALL PASS" : fails + " FAILED") + " - vine-spawn-with-trumpet-flowers smoke test, " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            File.WriteAllLines("vine-smoke-result.txt", log);
            EditorApplication.Exit(fails == 0 ? 0 : 1);
        }

        // The hand-off from the page: put a job file the page saved into the scratch project's UnityUnicornInbox/, then run
        //   Unity -batchmode -projectPath <scratch project> -executeMethod UnityUnicornTests.VineSpawnSmoke20261019.RunPageJob
        // It takes the newest job for liminal-doors the way the open editor does, grows vines on a test arch and wall with the
        // job's colours and settings, and renders VineSmoke/vines-from-page-job.png. The job is expected to say grow "both",
        // Flower glow 1.2 and Leaf glow 0.6. It never looks in Downloads, where a world's open editor may be waiting for a job.
        public static void RunPageJob()
        {
            var log = new List<string>(); int fails = 0;
            void Check(bool ok, string what) { log.Add((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
            try
            {
                VineInbox20261019.OnlyProjectInbox = true;
                if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
                VineWorlds20261019.SetThisWorld("liminal-doors");
                bool took;
                try { took = VineInbox20261019.LookNow(false); } catch (Exception e) { log.Add("  (window: " + e.GetType().Name + ")"); took = true; }
                Check(took, "the page's job was taken from the inbox");
                var kept = Directory.GetFiles("UnityUnicornReports/jobs", "unity-unicorn-vine-spawn-liminal-doors-*.json").OrderBy(File.GetLastWriteTimeUtc).LastOrDefault();
                Check(kept != null, "the job is kept in UnityUnicornReports/jobs: " + kept);
                Check(VineJobs20261019.TryRead(kept, out var job, out var why), "the page's job file reads and checks out " + (why ?? ""));
                var s = VineJobs20261019.Settings(job); var colours = VineJobs20261019.Colours(job);
                log.Add("  job: " + job.flower_colors.Length + " colours (" + string.Join(" ", job.flower_colors) + "), grow " + s.grow + ", glow " + s.glow + ", leaf glow " + s.leafGlow + ", leaves " + s.leavesPerM + "/m, flowers " + s.flowersPerM + "/m");
                Check(colours.Count == job.flower_colors.Length && s.grow == VineGrow20261019.Both && Mathf.Approximately(s.glow, 1.2f), "colours, grow and glow come from the page");
                Check(Mathf.Approximately(s.leafGlow, 0.6f), "Leaf glow comes from the page (0.6)");
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

        // The window as Astra sees it. It needs a real editor window, so run it WITHOUT -batchmode:
        //   Unity -projectPath <scratch project> -executeMethod UnityUnicornTests.VineSpawnSmoke20261019.RunWindowPicture
        // It opens the window the way a page job does (10 colours, a long job file name), takes a picture at the size it opens
        // at and at its smallest (VineSmoke/window-open.png, VineSmoke/window-smallest.png), writes vine-window-result.txt and
        // quits Unity. Look at the pictures: nothing may be cut off on the right.
        public static void RunWindowPicture()
        {
            var log = new List<string>();
            VineInbox20261019.OnlyProjectInbox = true;
            VineWorlds20261019.SetThisWorld("astras-infinite-pole");
            foreach (var open in Resources.FindObjectsOfTypeAll<VineSpawnWindow20261019>()) open.Close();
            var job = VineJobs20261019.FromText("{\"schema\":\"unity-unicorn.vine-spawn-job/v1\",\"tool_id\":\"vine-spawn-with-trumpet-flowers\",\"world\":\"astras-infinite-pole\",\"made_at\":\"2026-10-20T03:48:07Z\"," +
                "\"flower_colors\":[\"#ff3cc7\",\"#2ef2ff\",\"#d86bff\",\"#c4b5fd\",\"#ff2bd6\",\"#ff5a1f\",\"#ffd23f\",\"#b6ff3c\",\"#3b82f6\",\"#ffffff\"],\"settings\":{\"grow\":\"climb\",\"glow\":1.5,\"leaf_glow\":0.4}}");
            VineSpawnWindow20261019.OpenWithJob(job, "unity-unicorn-vine-spawn-astras-infinite-pole-20261020-034807.json");
            var w = EditorWindow.GetWindow<VineSpawnWindow20261019>();
            log.Add("opened at " + w.position + ", main window " + EditorGUIUtility.GetMainWindowPosition() + ", " + EditorGUIUtility.pixelsPerPoint + " pixels a point");
            double start = EditorApplication.timeSinceStartup; int stage = 0;
            // Unity's own pixels of the window (GUIView.GrabPixels), never the screen: the screen may show someone's other windows.
            void Shot(string path)
            {
                float ppp = EditorGUIUtility.pixelsPerPoint; var r = w.position;
                int pw = Mathf.RoundToInt(r.width * ppp), ph = Mathf.RoundToInt(r.height * ppp);
                const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var host = typeof(EditorWindow).GetField("m_Parent", any)?.GetValue(w);
                MethodInfo grab = null;
                for (var t = host?.GetType(); t != null && grab == null; t = t.BaseType) grab = t.GetMethod("GrabPixels", any, null, new[] { typeof(RenderTexture), typeof(Rect) }, null);
                if (grab == null) { log.Add(path + ": not taken (GUIView.GrabPixels not found in this Unity)"); return; }
                var rt = new RenderTexture(pw, ph, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);   // the pixels as they are
                grab.Invoke(host, new object[] { rt, new Rect(0, 0, pw, ph) });
                var was = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(pw, ph, TextureFormat.RGB24, false, true); tex.ReadPixels(new Rect(0, 0, pw, ph), 0, 0);
                RenderTexture.active = was;
                var px = tex.GetPixels32(); var up = new Color32[px.Length];                // the grab comes bottom row first
                for (int y = 0; y < ph; y++) Array.Copy(px, y * pw, up, (ph - 1 - y) * pw, pw);
                tex.SetPixels32(up); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
                log.Add(path + ": " + r);
            }
            void Tick()
            {
                w.Repaint();
                double t = EditorApplication.timeSinceStartup - start;
                if (stage == 0 && t > 3) { Directory.CreateDirectory("VineSmoke"); w.Focus(); Shot("VineSmoke/window-open.png"); w.position = new Rect(w.position.x, w.position.y, w.minSize.x, 620); stage = 1; }
                else if (stage == 1 && t > 6) { w.Focus(); Shot("VineSmoke/window-smallest.png"); stage = 2; }
                else if (stage == 2 && t > 7)
                {
                    EditorApplication.update -= Tick;
                    File.WriteAllLines("vine-window-result.txt", log);
                    EditorApplication.Exit(0);
                }
            }
            EditorApplication.update += Tick;
        }

        const string ShaderAt = "Assets/UnityUnicorn/Shaders/Unity Stable Vine Bloom.shader";
        // The shape of the first version's shader: Flower glow only, no Leaf glow.
        const string FirstShader = "Shader \"Unity Stable/Vine Bloom\"\n{\n    Properties { _Glow (\"Flower glow\", Range(0,3)) = 0.2 }\n    SubShader\n    {\n        Tags { \"RenderType\"=\"Opaque\" }\n        CGPROGRAM\n" +
            "        #pragma surface surf Standard\n        half _Glow;\n        struct Input { float4 color : COLOR; };\n" +
            "        void surf(Input IN, inout SurfaceOutputStandard o) { o.Albedo = IN.color.rgb; o.Emission = IN.color.rgb * _Glow * IN.color.a; }\n        ENDCG\n    }\n}\n";

        // A phone kiosk like the Pole's K6 booths: floor, four thin glass walls and a roof that overhangs, about 0.9 m inside.
        // With doorOpen the front wall has a 0.6 m doorway.
        static GameObject Kiosk(string name, Vector3 at, bool doorOpen, Func<PrimitiveType, string, Vector3, Vector3, Transform, GameObject> shape)
        {
            var k = new GameObject(name); k.transform.position = at;
            GameObject Box(string part, Vector3 pos, Vector3 size) => shape(PrimitiveType.Cube, part, at + pos, size, k.transform);
            Box("Floor", new Vector3(0, 0.05f, 0), new Vector3(1f, 0.1f, 1f));
            Box("Roof", new Vector3(0, 2.475f, 0), new Vector3(1.12f, 0.15f, 1.12f));
            Box("Back glass", new Vector3(0, 1.25f, 0.48f), new Vector3(1f, 2.3f, 0.04f));
            Box("Left glass", new Vector3(-0.48f, 1.25f, 0), new Vector3(0.04f, 2.3f, 0.92f));
            Box("Right glass", new Vector3(0.48f, 1.25f, 0), new Vector3(0.04f, 2.3f, 0.92f));
            if (!doorOpen) Box("Door glass", new Vector3(0, 1.25f, -0.48f), new Vector3(1f, 2.3f, 0.04f));
            else
            {
                Box("Door post left", new Vector3(-0.4f, 1.25f, -0.48f), new Vector3(0.2f, 2.3f, 0.04f));
                Box("Door post right", new Vector3(0.4f, 1.25f, -0.48f), new Vector3(0.2f, 2.3f, 0.04f));
                Box("Over the door", new Vector3(0, 2.2f, -0.48f), new Vector3(0.6f, 0.4f, 0.04f));
            }
            return k;
        }

        // Renders a picture and returns how many of its pixels are leaf green. dark: no lights and no ambient light at all.
        static int Render(Vector3 from, Vector3 at, float fov, string path, bool dark = false)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f; sun.color = new Color(1f, 0.95f, 0.88f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38, 28, 0);
            sun.enabled = !dark;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.44f, 0.5f); RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.32f); RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.17f);
            if (dark) { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = Color.black; RenderSettings.reflectionIntensity = 0; }
            else RenderSettings.reflectionIntensity = 1;
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
            int green = tex.GetPixels32().Count(p => p.g > 30 && p.g > p.r * 1.4f && p.g > p.b * 1.4f);
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(sun.gameObject); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            return green;
        }
    }
}
