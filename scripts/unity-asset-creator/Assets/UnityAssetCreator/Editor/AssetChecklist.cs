// Claude, 2026-10-18. unity-asset-creator: the ordered checklist and what the tool checks by itself at each step.
// The steps, their reasons and their sources are written out in
// Tools/unity-asset-creator/unity-asset-creator-checklist-20261018.md (the trick follows the same list).
// A failed check blocks a step; a warning passes only with a written note; steps with a "confirm" also need a person.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityUnicorn.AssetCreator
{
    public enum Status { NotRun, Pass, Warn, Fail }

    public class Result
    {
        public Status status = Status.Pass;
        public readonly List<string> lines = new List<string>();
        public void Info(string s) => lines.Add(s);
        public void Warn(string s) { lines.Add("WARN: " + s); if (status < Status.Warn) status = Status.Warn; }
        public void Fail(string s) { lines.Add("FAIL: " + s); status = Status.Fail; }
        public string Text => string.Join("\n", lines);
    }

    public class Step
    {
        public string id, title, why, confirm;   // confirm: what a person has to confirm (null = nothing)
        public Func<AssetRecipe, Scan, Result> check;
    }

    // Everything the checks need, read once from the prefab asset (no instance, nothing in the scene changes).
    public class Scan
    {
        public GameObject root;
        public string prefabPath, folder;
        public readonly List<MeshFilter> meshes = new List<MeshFilter>();
        public readonly List<Renderer> renderers = new List<Renderer>();
        public readonly List<ParticleSystem> particles = new List<ParticleSystem>();
        public readonly List<Collider> colliders = new List<Collider>();
        public readonly List<Light> lights = new List<Light>();
        public readonly List<Material> materials = new List<Material>();
        public readonly List<Texture> textures = new List<Texture>();
        public Bounds visual; public bool hasVisual; public int triangles;

        public Matrix4x4 ToRoot(Transform t) => root.transform.worldToLocalMatrix * t.localToWorldMatrix;

        public static Scan Of(AssetRecipe r)
        {
            var s = new Scan { root = r.prefab, prefabPath = r.PrefabPath, folder = r.Folder };
            if (s.root == null) return s;
            foreach (var mf in s.root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                s.meshes.Add(mf);
                var m = mf.sharedMesh;
                for (int i = 0; i < m.subMeshCount; i++) if (m.GetTopology(i) == MeshTopology.Triangles) s.triangles += (int)(m.GetIndexCount(i) / 3);
                var M = s.ToRoot(mf.transform); var b = m.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var p = M.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (!s.hasVisual) { s.visual = new Bounds(p, Vector3.zero); s.hasVisual = true; } else s.visual.Encapsulate(p);
                }
            }
            s.renderers.AddRange(s.root.GetComponentsInChildren<Renderer>(true));
            s.particles.AddRange(s.root.GetComponentsInChildren<ParticleSystem>(true));
            s.colliders.AddRange(s.root.GetComponentsInChildren<Collider>(true));
            s.lights.AddRange(s.root.GetComponentsInChildren<Light>(true));
            foreach (var rr in s.renderers) foreach (var mat in rr.sharedMaterials) if (mat != null && !s.materials.Contains(mat)) s.materials.Add(mat);
            foreach (var mat in s.materials)
                foreach (var prop in mat.GetTexturePropertyNames())
                {
                    var t = mat.GetTexture(prop);
                    if (t != null && !s.textures.Contains(t) && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(t))) s.textures.Add(t);
                }
            return s;
        }
    }

    public static class AssetChecklist
    {
        static readonly Regex IdPattern = new Regex("^[a-z0-9]+(-[a-z0-9]+)*$");
        static readonly Regex Semver = new Regex(@"^\d+\.\d+\.\d+$");
        static readonly string[] Licenses = { "own-work", "CC0-1.0", "CC-BY-4.0", "CC-BY-SA-4.0", "MIT", "Apache-2.0", "store-personal", "store-commercial", "research-only", "unclear" };

        public static readonly Step[] Steps =
        {
            new Step { id = "plan", title = "Plan: name, version, budget",
                why = "A budget per asset keeps the whole world inside VRChat's (about 250k triangles, 1k textures on Android).",
                check = (r, s) =>
                {
                    var res = new Result();
                    if (!IdPattern.IsMatch(r.id ?? "")) res.Fail("id must be lowercase words joined by hyphens (<world-or-avatar>-<asset>): '" + r.id + "'");
                    if (string.IsNullOrWhiteSpace(r.displayName)) res.Fail("no display name");
                    if (!Semver.IsMatch(r.version ?? "")) res.Fail("version must be MAJOR.MINOR.PATCH: '" + r.version + "'");
                    if (r.prefab == null) res.Fail("no prefab assigned");
                    if (!r.forPC && !r.forAndroid) res.Fail("pick at least one platform");
                    if (r.maxTriangles <= 0 || r.maxMaterials <= 0 || r.maxTextureSize <= 0 || r.maxParticles < 0) res.Fail("set every budget");
                    res.Info(r.id + " v" + r.version + " (" + r.kind + ") for " + (r.forPC ? "PC" : "") + (r.forPC && r.forAndroid ? " + " : "") + (r.forAndroid ? "Android" : "") +
                             "; budget " + r.maxTriangles + " triangles, " + r.maxMaterials + " materials, " + r.maxTextureSize + " px, " + r.maxParticles + " particles");
                    return res;
                } },

            new Step { id = "provenance", title = "Provenance: who made it, from where, licence",
                why = "No row, no ship (Unity Unicorn asset-ledger).",
                check = (r, s) =>
                {
                    var res = new Result();
                    if (string.IsNullOrWhiteSpace(r.creator)) res.Fail("no creator");
                    if (string.IsNullOrWhiteSpace(r.source)) res.Fail("no source");
                    if (string.IsNullOrWhiteSpace(r.license)) res.Fail("no licence");
                    else if (!Licenses.Contains(r.license)) res.Warn("licence '" + r.license + "' is not one of: " + string.Join(", ", Licenses));
                    if (r.publicOk && (r.license == "unclear" || r.license == "research-only" || r.license == "store-personal" || r.license == "store-commercial"))
                        res.Fail("licence '" + r.license + "' can't be public; set public OK off or replace the asset");
                    res.Info("creator: " + r.creator + "; source: " + r.source + "; licence: " + r.license + "; public: " + (r.publicOk ? "yes" : "no"));
                    return res;
                } },

            new Step { id = "scale-pivot", title = "Scale, pivot and facing",
                why = "1 unit = 1 m, pivot at the base centre, front +Z: it drops into any world without fixing transforms.",
                confirm = "The front of the asset faces +Z (blue arrow).",
                check = (r, s) =>
                {
                    var res = new Result(); if (s.root == null) { res.Fail("no prefab"); return res; }
                    var t = s.root.transform;
                    if ((t.localScale - Vector3.one).sqrMagnitude > 1e-6f) res.Fail("prefab root scale is " + t.localScale + ", not 1");
                    if (Quaternion.Angle(t.localRotation, Quaternion.identity) > .01f) res.Fail("prefab root is rotated " + t.localEulerAngles);
                    if (!s.hasVisual) { res.Fail("no visible meshes"); return res; }
                    if (Mathf.Abs(s.visual.min.y) > .05f) res.Warn("lowest visible point is " + s.visual.min.y.ToString("0.00") + " m from the pivot (base should be at 0)");
                    float off = new Vector2(s.visual.center.x, s.visual.center.z).magnitude;
                    if (off > Mathf.Max(.1f, s.visual.size.magnitude * .1f)) res.Warn("pivot is " + off.ToString("0.00") + " m from the middle of the footprint");
                    res.Info("size " + s.visual.size.x.ToString("0.00") + " x " + s.visual.size.y.ToString("0.00") + " x " + s.visual.size.z.ToString("0.00") + " m (w x h x d)");
                    return res;
                } },

            new Step { id = "geometry", title = "Geometry: triangles, normals, lightmap UVs",
                why = "Triangles are VRChat's headline world budget; separate objects stay separate so they can be culled.",
                check = (r, s) =>
                {
                    var res = new Result();
                    if (s.triangles > r.maxTriangles) res.Fail(s.triangles + " triangles, budget " + r.maxTriangles);
                    foreach (var mf in s.meshes)
                    {
                        var m = mf.sharedMesh;
                        if (m.normals == null || m.normals.Length == 0) res.Fail(mf.name + ": mesh '" + m.name + "' has no normals");
                        if (!r.moves && (m.uv2 == null || m.uv2.Length == 0)) res.Warn(mf.name + ": no lightmap UVs (UV2); bake with light probes or add UV2");
                    }
                    res.Info(s.triangles + " triangles in " + s.meshes.Count + " meshes (budget " + r.maxTriangles + ")");
                    return res;
                } },

            new Step { id = "materials", title = "Materials and shaders",
                why = "Each material adds draw calls (VRChat is CPU-bound); GPU instancing on all; avoid transparency.",
                check = (r, s) =>
                {
                    var res = new Result(); int transparent = 0;
                    if (s.materials.Count > r.maxMaterials) res.Fail(s.materials.Count + " materials, budget " + r.maxMaterials);
                    foreach (var rr in s.renderers) foreach (var mat in rr.sharedMaterials) if (mat == null) res.Fail(rr.name + " has an empty material slot");
                    foreach (var mat in s.materials)
                    {
                        string sh = mat.shader != null ? mat.shader.name : "none";
                        if (mat.shader == null || sh == "Hidden/InternalErrorShader") res.Fail(mat.name + ": shader missing or broken");
                        if (!mat.enableInstancing) res.Warn(mat.name + ": GPU instancing is off");
                        bool see = mat.renderQueue >= (int)RenderQueue.AlphaTest + 50 || mat.GetTag("RenderType", false) == "Transparent";
                        bool particle = s.particles.Any(p => { var pr = p.GetComponent<ParticleSystemRenderer>(); return pr != null && pr.sharedMaterial == mat; });
                        if (see && !particle) transparent++;
                        if (r.forAndroid && !sh.StartsWith("VRChat/Mobile/") && !sh.StartsWith("Mobile/") && !sh.StartsWith("Legacy Shaders/Particles/")) res.Warn(mat.name + ": '" + sh + "' is not a mobile shader (Android)");
                        bool glows = mat.IsKeywordEnabled("_EMISSION") || (mat.HasProperty("_EmissionColor") && mat.GetColor("_EmissionColor").maxColorComponent > .01f);
                        res.Info(mat.name + ": " + sh + (see ? " (transparent)" : "") + (glows ? " (glows)" : ""));
                    }
                    if (transparent > 1) res.Warn(transparent + " transparent surface materials; keep it to one");
                    else if (transparent == 1 && r.forAndroid) res.Warn("one transparent surface material; VRChat advises none on Android");
                    return res;
                } },

            new Step { id = "textures", title = "Textures: size, compression, mipmaps",
                why = "Textures are the main memory cost; VRChat says 1k at most on Android.",
                check = (r, s) =>
                {
                    var res = new Result();
                    foreach (var t in s.textures)
                    {
                        var path = AssetDatabase.GetAssetPath(t); var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                        string line = t.name + " " + t.width + "x" + t.height;
                        if (Mathf.Max(t.width, t.height) > r.maxTextureSize) res.Fail(line + " is over " + r.maxTextureSize + " px");
                        if (!Mathf.IsPowerOfTwo(t.width) || !Mathf.IsPowerOfTwo(t.height)) res.Warn(line + " is not a power of two");
                        if (imp != null)
                        {
                            if (imp.maxTextureSize > r.maxTextureSize) res.Warn(t.name + ": import max size " + imp.maxTextureSize + " (set " + r.maxTextureSize + ")");
                            if (imp.textureCompression == TextureImporterCompression.Uncompressed) res.Warn(t.name + ": uncompressed");
                            if (!imp.mipmapEnabled && imp.textureType == TextureImporterType.Default) res.Warn(t.name + ": no mipmaps");
                        }
                        res.Info(line + (imp != null ? ", " + imp.textureCompression + (imp.mipmapEnabled ? ", mipmaps" : "") : ""));
                    }
                    if (s.textures.Count == 0) res.Info("no textures (flat or vertex colours)");
                    return res;
                } },

            new Step { id = "colliders", title = "Colliders: simple, where people stand, never above the surface",
                why = "Physics is allowed but shouldn't be overused; a walkable collider is thin and under the visible surface (Astra's rule).",
                confirm = "The colliders match where people stand or touch, and nothing invisible sticks out.",
                check = (r, s) =>
                {
                    var res = new Result();
                    foreach (var c in s.colliders)
                    {
                        string kind = c.GetType().Name + (c.isTrigger ? " (trigger)" : "");
                        float top = ColliderTop(s, c);
                        if (!c.isTrigger && s.hasVisual && top > s.visual.max.y + .02f) res.Fail(c.name + ": " + kind + " top " + top.ToString("0.00") + " m is above the asset's highest point " + s.visual.max.y.ToString("0.00") + " m");
                        if (c is MeshCollider mc && !mc.convex) res.Warn(c.name + ": non-convex mesh collider (heavier); prefer boxes");
                        res.Info(c.name + ": " + kind + ", top " + top.ToString("0.00") + " m");
                    }
                    if (s.colliders.Count == 0) res.Info("no colliders");
                    return res;
                } },

            new Step { id = "lighting", title = "Lighting: static, emission, no realtime lights",
                why = "Baking is essential; realtime lights are very expensive; mark unmoving objects static.",
                check = (r, s) =>
                {
                    var res = new Result();
                    foreach (var l in s.lights) if (l.lightmapBakeType != LightmapBakeType.Baked) res.Warn(l.name + ": " + l.lightmapBakeType + " light inside the asset; use emission or bake it");
                    if (!r.moves)
                        foreach (var rr in s.renderers)
                        {
                            if (rr is ParticleSystemRenderer) continue;
                            var f = GameObjectUtility.GetStaticEditorFlags(rr.gameObject);
                            if ((f & StaticEditorFlags.BatchingStatic) == 0 || (f & StaticEditorFlags.OccludeeStatic) == 0) res.Warn(rr.name + ": not static (batching / occludee)");
                        }
                    var glow = s.materials.Where(m => m.IsKeywordEnabled("_EMISSION") || (m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > .01f)).Select(m => m.name).ToList();
                    res.Info(s.lights.Count + " lights; glowing materials: " + (glow.Count > 0 ? string.Join(", ", glow) : "none") + (r.moves ? "; asset moves (not static)" : "; asset is static"));
                    return res;
                } },

            new Step { id = "effects", title = "Effects: particles capped, cheap, low overdraw",
                why = "Stacked transparent particles cost GPU time; collisions cost CPU; mesh particles render faster instanced.",
                check = (r, s) =>
                {
                    var res = new Result(); int sum = 0;
                    foreach (var p in s.particles)
                    {
                        var main = p.main; sum += main.maxParticles;
                        var pr = p.GetComponent<ParticleSystemRenderer>();
                        if (pr == null || pr.sharedMaterial == null) res.Fail(p.name + ": no particle material");
                        if (p.collision.enabled) res.Warn(p.name + ": particle collision is on");
                        if (pr != null && pr.renderMode == ParticleSystemRenderMode.Mesh && !pr.enableGPUInstancing) res.Warn(p.name + ": mesh particles without GPU instancing");
                        if (main.cullingMode == ParticleSystemCullingMode.AlwaysSimulate) res.Warn(p.name + ": always simulates, even off screen");
                        res.Info(p.name + ": max " + main.maxParticles + ", " + (pr != null && pr.sharedMaterial != null ? pr.sharedMaterial.shader.name : "no material") + ", " + main.simulationSpace + " space");
                    }
                    if (sum > r.maxParticles) res.Fail(sum + " particles max in total, budget " + r.maxParticles);
                    if (s.particles.Count == 0) res.Info("no particle systems");
                    return res;
                } },

            new Step { id = "prefab", title = "Prefab: one clean, self-contained prefab",
                why = "Everything the asset needs in its own folder makes the package self-contained.",
                check = (r, s) =>
                {
                    var res = new Result(); if (s.root == null) { res.Fail("no prefab"); return res; }
                    int missing = 0;
                    foreach (var t in s.root.GetComponentsInChildren<Transform>(true))
                    {
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                        if (t.name.StartsWith("GameObject")) res.Warn("child named '" + t.name + "'");
                    }
                    if (missing > 0) res.Fail(missing + " missing scripts");
                    var outside = AssetDatabase.GetDependencies(s.prefabPath, true).Where(p => p.StartsWith("Assets/") && !p.StartsWith(s.folder + "/") && p != s.prefabPath).ToList();
                    foreach (var p in outside.Where(p => !ShopShaders.IsShopPath(p) && !ShopShaders.IsOwnPath(p))) res.Warn("depends on " + p + " outside " + s.folder);
                    foreach (var p in outside.Where(ShopShaders.IsOwnPath)) res.Info("packs our own " + Path.GetFileName(p) + " (Unity Stable shaders) so it works without the shop");
                    foreach (var q in ShopShaders.For(s.materials)) res.Info("requires " + q.name + " " + q.version + " (" + q.shader + ") from the shaders-shop; not packed");
                    res.Info("prefab " + s.prefabPath + "; folder " + s.folder + "; " + AssetDatabase.GetDependencies(s.prefabPath, true).Count(p => p.StartsWith("Assets/")) + " project files in the package");
                    return res;
                } },

            new Step { id = "review", title = "Review in the world: renders and desktop test",
                why = "The editor isn't the client: renders catch looks, the desktop test catches what only VRChat shows.",
                confirm = "You looked at the preview renders in the world's light, and the VRChat desktop test ran with it.",
                check = (r, s) =>
                {
                    var res = new Result();
                    var dir = r.PreviewDir; int n = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png").Length : 0;
                    if (n < 3) res.Fail("no preview renders for v" + r.version + " yet (Render previews)");
                    else res.Info(n + " preview renders in " + dir);
                    return res;
                } },

            new Step { id = "export", title = "Export and publish",
                why = "The package, previews and manifest go to unity-assets-library as a pull request; Astra merges it.",
                check = (r, s) =>
                {
                    var res = new Result();
                    foreach (var st in Steps.TakeWhile(x => x.id != "export")) if (!r.IsDone(st.id)) res.Fail("step '" + st.title + "' isn't done");
                    if (r.exportedVersions.Contains(r.version)) res.Fail("v" + r.version + " is already exported; raise the version");
                    if (res.status != Status.Fail) res.Info("ready to export v" + r.version + " to " + r.ExportDir);
                    return res;
                } },
        };

        public static Step Get(string id) => Steps.First(s => s.id == id);
        public static int IndexOf(string id) => Array.FindIndex(Steps, s => s.id == id);

        public static Result Run(Step step, AssetRecipe r)
        {
            Result res;
            try { res = step.check(r, Scan.Of(r)); }
            catch (Exception e) { res = new Result(); res.Fail("check crashed: " + e.Message); }
            var st = r.State(step.id);
            st.status = res.status.ToString().ToLowerInvariant(); st.details = res.Text;
            EditorUtility.SetDirty(r);
            return res;
        }

        // Ordered: every step above must be done. Fails block; warnings and confirmations need a note.
        public static string WhyNot(AssetRecipe r, Step step, Result res, string note)
        {
            int i = IndexOf(step.id);
            for (int k = 0; k < i; k++) if (!r.IsDone(Steps[k].id)) return "step " + (k + 1) + " (" + Steps[k].title + ") isn't done yet";
            if (res == null) return "run the check first";
            if (res.status == Status.Fail) return "the check failed";
            if (res.status == Status.Warn && string.IsNullOrWhiteSpace(note)) return "a warning needs a note saying why it is acceptable";
            if (step.confirm != null && string.IsNullOrWhiteSpace(note)) return "this step needs a person's confirmation note";
            return null;
        }

        public static void MarkDone(AssetRecipe r, Step step, string note, string by)
        {
            var st = r.State(step.id);
            st.done = true; st.version = r.version; st.note = note ?? ""; st.doneBy = by;
            st.doneAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            EditorUtility.SetDirty(r); AssetDatabase.SaveAssets();
        }

        static float ColliderTop(Scan s, Collider c)
        {
            var M = s.ToRoot(c.transform);
            switch (c)
            {
                case BoxCollider b:
                    float top = float.MinValue;
                    for (int k = 0; k < 8; k++)
                        top = Mathf.Max(top, M.MultiplyPoint3x4(b.center + Vector3.Scale(b.size * .5f, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))).y);
                    return top;
                case SphereCollider sp: return M.MultiplyPoint3x4(sp.center).y + sp.radius * c.transform.lossyScale.y;
                case CapsuleCollider cp: return M.MultiplyPoint3x4(cp.center).y + cp.height * .5f * c.transform.lossyScale.y;
                case MeshCollider mc when mc.sharedMesh != null:
                    var bb = mc.sharedMesh.bounds; return M.MultiplyPoint3x4(bb.center + new Vector3(0, bb.extents.y, 0)).y;
                default: return 0;
            }
        }
    }
}
