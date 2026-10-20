// Claude, 2026-10-18. shaders-shop: which shaders this world uses, where they come from, and under which licence.
// Every material the world's scenes use (directly or through prefabs, the skybox included) is traced to its shader,
// and each shader to a catalog entry. Saved with the world as docs/shaders-in-world.json
// (schema unity-unicorn.world-shaders/v1). Other worlds' records, made offline by
// Tools/shaders-shop/scan-world-shaders-20261018.py, sit in Tools/shaders-shop/worlds/ and show up in the shop too.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn.ShadersShop
{
    [Serializable]
    public class ShaderUse
    {
        public string shader, shader_path, catalog_id, catalog_name, license, source, version;
        public List<string> materials = new List<string>(), scenes = new List<string>();
    }

    [Serializable]
    public class WorldRecord
    {
        public string schema = "unity-unicorn.world-shaders/v1";
        public string world, scanned_at, unity, scanned_by = "shaders-shop (Unity editor)";
        public List<string> scenes = new List<string>();
        public List<ShaderUse> shaders = new List<ShaderUse>();
    }

    public static class WorldShaders
    {
        public const string RecordPath = "docs/shaders-in-world.json";
        public const string OtherWorlds = "Tools/shaders-shop/worlds";

        public static WorldRecord Scan(Catalog cat)
        {
            var rec = new WorldRecord { world = Application.productName, scanned_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), unity = Application.unityVersion };
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToList();
            if (scenes.Count == 0) scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            rec.scenes = scenes;
            var uses = new Dictionary<string, ShaderUse>();
            foreach (var scene in scenes)
                foreach (var matPath in AssetDatabase.GetDependencies(scene, true).Where(p => p.EndsWith(".mat")))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null || mat.shader == null) continue;
                    var name = mat.shader.name; var path = AssetDatabase.GetAssetPath(mat.shader);
                    if (!uses.TryGetValue(name, out var u))
                    {
                        var entry = cat.Match(name, path);
                        u = new ShaderUse { shader = name, shader_path = path, catalog_id = entry?.id ?? "unknown", catalog_name = entry?.name ?? "not in the catalog", license = entry?.license ?? "unknown" };
                        if (entry == null && path.StartsWith("Assets/")) { u.catalog_id = "in-assets"; u.catalog_name = "inside the world's Assets folder (own or bundled; check)"; u.license = "check"; }
                        if (path.StartsWith("Packages/")) { var pkg = path.Split('/')[1]; u.source = pkg; u.version = PackageVersion(pkg); }
                        else if (path.StartsWith("Assets/")) u.source = Path.GetDirectoryName(path).Replace('\\', '/');
                        else u.source = "Unity built-in";
                        uses[name] = u;
                    }
                    if (!u.materials.Contains(matPath)) u.materials.Add(matPath);
                    if (!u.scenes.Contains(scene)) u.scenes.Add(scene);
                }
            rec.shaders = uses.Values.OrderBy(x => x.catalog_id).ThenBy(x => x.shader).ToList();
            return rec;
        }

        public static void Save(WorldRecord rec)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecordPath));
            File.WriteAllText(RecordPath, JsonUtility.ToJson(rec, true) + "\n");
        }

        public static WorldRecord LoadThisWorld() => File.Exists(RecordPath) ? JsonUtility.FromJson<WorldRecord>(File.ReadAllText(RecordPath)) : null;

        public static List<WorldRecord> LoadOtherWorlds()
        {
            var list = new List<WorldRecord>();
            if (!Directory.Exists(OtherWorlds)) return list;
            foreach (var f in Directory.GetFiles(OtherWorlds, "*.json"))
                try { var r = JsonUtility.FromJson<WorldRecord>(File.ReadAllText(f)); if (r != null && r.world != Application.productName) list.Add(r); }
                catch { }
            return list;
        }

        static string PackageVersion(string pkg)
        {
            var p = "Packages/" + pkg + "/package.json";
            if (!File.Exists(p)) return "";
            var m = Regex.Match(File.ReadAllText(p), "\"version\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : "";
        }
    }
}
