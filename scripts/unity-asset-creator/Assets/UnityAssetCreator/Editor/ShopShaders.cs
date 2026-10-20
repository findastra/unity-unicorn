// Claude, 2026-10-18. unity-asset-creator <-> shaders-shop. An asset that uses a shader the shaders-shop installs
// (lilToon, Poiyomi, Mochie, ORL, ...) doesn't carry that shader's files in its package: the manifest lists it under
// requires_shaders, and the world gets it from the shop. Reads the shop's catalog file directly, so neither tool needs
// the other's code.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn.AssetCreator
{
    public static class ShopShaders
    {
        const string CatalogPath = "Assets/ShadersShop/shaders-catalog.json";

        [Serializable] class Entry { public string id, name, version, install; public List<string> detect_paths = new List<string>(), shader_prefixes = new List<string>(); }
        [Serializable] class Cat { public List<Entry> shaders = new List<Entry>(); }

        [Serializable] public class Required { public string id, name, version, shader; }

        static Cat Load() => File.Exists(CatalogPath) ? JsonUtility.FromJson<Cat>(File.ReadAllText(CatalogPath)) ?? new Cat() : new Cat();

        // A file that belongs to a shader the shop installs (so it stays out of the asset's package).
        public static bool IsShopPath(string path) =>
            path.StartsWith("Packages/") || Load().shaders.Where(e => e.install != "builtin" && e.install != "own").Any(e => e.detect_paths.Any(d => path.StartsWith(d + "/")));

        // A file of our own Unity Stable shaders: packed with the asset, so it works in a world without the shop.
        public static bool IsOwnPath(string path) =>
            Load().shaders.Where(e => e.install == "own").Any(e => e.detect_paths.Any(d => path.StartsWith(d + "/")));

        public static List<Required> For(IEnumerable<Material> materials)
        {
            var cat = Load(); var list = new List<Required>();
            foreach (var m in materials)
            {
                if (m == null || m.shader == null) continue;
                var path = AssetDatabase.GetAssetPath(m.shader); var name = m.shader.name;
                var e = cat.shaders.FirstOrDefault(x => x.install != "builtin" && x.detect_paths.Any(d => path.StartsWith(d + "/")))
                        ?? cat.shaders.FirstOrDefault(x => x.install != "builtin" && x.shader_prefixes.Any(p => name.StartsWith(p)));
                if (e != null && e.install == "own") continue;   // our own shaders travel inside the package
                if (e == null || list.Any(r => r.id == e.id && r.shader == name)) continue;
                list.Add(new Required { id = e.id, name = e.name, version = InstalledVersion(path, e.version), shader = name });
            }
            return list;
        }

        static string InstalledVersion(string shaderPath, string catalogVersion)
        {
            if (shaderPath.StartsWith("Packages/"))
            {
                var pj = "Packages/" + shaderPath.Split('/')[1] + "/package.json";
                if (File.Exists(pj)) { var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(pj), "\"version\"\\s*:\\s*\"([^\"]+)\""); if (m.Success) return m.Groups[1].Value; }
            }
            return catalogVersion;
        }
    }
}
