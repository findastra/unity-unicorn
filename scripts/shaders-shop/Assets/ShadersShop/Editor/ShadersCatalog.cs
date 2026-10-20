// Claude, 2026-10-18. shaders-shop (Unity Stable, Unity Unicorn's program): the catalog the shop sells from.
// Assets/ShadersShop/shaders-catalog.json is written by Tools/shaders-shop/refresh-catalog-20261018.py, which asks each
// source (VPM listings, GitHub releases) for exact versions, files and sizes. Schema unity-unicorn.shaders-catalog/v1.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn.ShadersShop
{
    [Serializable] public class Pkg { public string name, version, url; public long bytes; }
    [Serializable] public class FileItem { public string url, name; public long bytes; }

    [Serializable]
    public class ShaderEntry
    {
        public string id, name, author, license, homepage, kind, why, install, vpm_repo, version, unitypackage_url, files_dest;
        public long unitypackage_bytes;
        public List<string> good_for = new List<string>(), platforms = new List<string>(), features = new List<string>(), detect_paths = new List<string>(), shader_prefixes = new List<string>();
        public List<Pkg> packages = new List<Pkg>();
        public List<FileItem> files = new List<FileItem>();

        public long TotalBytes => packages.Sum(p => p.bytes) + unitypackage_bytes + files.Sum(f => f.bytes);
        public bool Downloads => install == "vpm" || install == "zip" || install == "unitypackage" || install == "files";
        public bool Installed => install == "builtin" || detect_paths.Any(p => Directory.Exists(p) || File.Exists(p));
        public string Size => TotalBytes <= 0 ? "" : TotalBytes >= 1 << 20 ? (TotalBytes / 1048576.0).ToString("0.0") + " MB" : (TotalBytes / 1024.0).ToString("0") + " KB";
    }

    [Serializable]
    public class Catalog
    {
        public string schema, tool, tool_version, updated;
        public List<ShaderEntry> shaders = new List<ShaderEntry>();

        public const string Path = "Assets/ShadersShop/shaders-catalog.json";

        public static Catalog Load()
        {
            if (!File.Exists(Path)) return new Catalog();
            return JsonUtility.FromJson<Catalog>(File.ReadAllText(Path)) ?? new Catalog();
        }

        // Which catalog entry a shader belongs to: its own files first, then its name, then the built-ins.
        public ShaderEntry Match(string shaderName, string shaderPath)
        {
            shaderPath = shaderPath ?? "";
            return shaders.FirstOrDefault(s => s.install != "builtin" && s.detect_paths.Any(p => shaderPath.StartsWith(p + "/")))
                ?? shaders.FirstOrDefault(s => s.install != "builtin" && s.shader_prefixes.Any(p => shaderName.StartsWith(p)))
                ?? shaders.FirstOrDefault(s => s.id == "vrchat-sdk" && (shaderPath.StartsWith("Packages/com.vrchat.") || shaderName.StartsWith("VRChat/")))
                ?? (shaderPath == "Resources/unity_builtin_extra" || shaderPath.StartsWith("Library/") || shaderPath == "" ? shaders.FirstOrDefault(s => s.id == "unity-builtin") : null);
        }
    }
}
