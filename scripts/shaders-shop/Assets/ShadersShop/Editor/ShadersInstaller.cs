// Claude, 2026-10-18. shaders-shop: putting a shader into this world, the way VRChat Creator Companion would.
//   vpm / zip     -> each package zip (with its dependencies, resolved into the catalog) unpacked into Packages/<name>,
//                    and Packages/vpm-manifest.json updated so VCC sees them as installed
//   unitypackage  -> downloaded and imported (no import dialog)
//   files         -> loose files (e.g. MIT shader files) saved into the entry's folder under Assets/ShadersShop/Imported
//   builtin / own / manual / store -> nothing to download; manual and store open the shader's page
// Every install is appended to Review/shaders-shop/installs.jsonl (private run history, not in Git).
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityUnicorn.ShadersShop
{
    public static class ShadersInstaller
    {
        public static bool Busy { get; private set; }
        public static string Progress { get; private set; } = "";

        public static string Describe(ShaderEntry e)
        {
            var sb = new StringBuilder();
            sb.AppendLine(e.name + " " + e.version + " by " + e.author + " (licence " + e.license + ")");
            foreach (var p in e.packages) sb.AppendLine("- " + p.name + " " + p.version + ", " + Mb(p.bytes) + " from " + p.url);
            if (!string.IsNullOrEmpty(e.unitypackage_url)) sb.AppendLine("- " + Mb(e.unitypackage_bytes) + " from " + e.unitypackage_url);
            foreach (var f in e.files) sb.AppendLine("- " + f.name + ", " + Mb(f.bytes) + " from " + f.url);
            sb.Append("Total " + Mb(e.TotalBytes) + ".");
            return sb.ToString();
        }

        static string Mb(long b) => b >= 1 << 20 ? (b / 1048576.0).ToString("0.0") + " MB" : (b / 1024.0).ToString("0") + " KB";

        public static void Install(ShaderEntry e, string approvedBy, Action<bool, string> done)
        {
            if (Busy) { done(false, "another install is running"); return; }
            if (e.install == "manual" || e.install == "store") { Application.OpenURL(e.homepage); done(false, e.name + " is a " + e.install + " install: its page is open"); return; }
            if (!e.Downloads) { done(true, e.name + " needs no download (" + e.install + ")"); return; }
            Busy = true;
            var jobs = new List<(string url, string dest, long bytes)>();
            var tmp = "Temp/ShadersShop"; Directory.CreateDirectory(tmp);
            foreach (var p in e.packages) jobs.Add((p.url, tmp + "/" + p.name + "-" + p.version + ".zip", p.bytes));
            if (!string.IsNullOrEmpty(e.unitypackage_url)) jobs.Add((e.unitypackage_url, tmp + "/" + e.id + "-" + e.version + ".unitypackage", e.unitypackage_bytes));
            foreach (var f in e.files) jobs.Add((f.url, tmp + "/" + e.id + "-" + f.name, f.bytes));
            Download(jobs, 0, ok =>
            {
                string msg;
                try { msg = ok ? Place(e, jobs) : "download failed: " + Progress; }
                catch (Exception ex) { ok = false; msg = "install failed: " + ex.Message; Debug.LogException(ex); }
                Busy = false; Progress = "";
                Log(e, approvedBy, ok, msg);
                done(ok, msg);
            });
        }

        static void Download(List<(string url, string dest, long bytes)> jobs, int i, Action<bool> finished)
        {
            if (i >= jobs.Count) { finished(true); return; }
            var job = jobs[i];
            // already downloaded in full (e.g. before a domain reload): don't fetch it again
            if (job.bytes > 0 && File.Exists(job.dest) && new FileInfo(job.dest).Length == job.bytes) { Download(jobs, i + 1, finished); return; }
            var req = new UnityWebRequest(job.url, UnityWebRequest.kHttpVerbGET) { downloadHandler = new DownloadHandlerFile(job.dest) { removeFileOnAbort = true } };
            req.SetRequestHeader("User-Agent", "unity-stable-shaders-shop");
            var op = req.SendWebRequest();
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                Progress = "downloading " + Path.GetFileName(job.dest) + " " + (op.progress * 100).ToString("0") + "% (" + (i + 1) + "/" + jobs.Count + ")";
                if (!op.isDone) return;
                EditorApplication.update -= tick;
                bool ok = req.result == UnityWebRequest.Result.Success;
                if (!ok) Progress = req.error + " for " + job.url;
                req.Dispose();
                if (ok) Download(jobs, i + 1, finished); else finished(false);
            };
            EditorApplication.update += tick;
        }

        static string Place(ShaderEntry e, List<(string url, string dest, long bytes)> jobs)
        {
            if (e.install == "vpm" || e.install == "zip")
            {
                for (int k = 0; k < e.packages.Count; k++)
                {
                    var p = e.packages[k]; var target = "Packages/" + p.name;
                    if (Directory.Exists(target)) Directory.Delete(target, true);   // replacing a version, as VCC does
                    Extract(jobs[k].dest, target);
                }
                VpmManifest.Add(e.packages, e.packages[0].name);
                AssetDatabase.Refresh();
                UnityEditor.PackageManager.Client.Resolve();
                return "installed " + string.Join(", ", e.packages.Select(p => p.name + " " + p.version)) + " into Packages/";
            }
            if (e.install == "unitypackage")
            {
                AssetDatabase.ImportPackage(jobs[0].dest, false);
                return "imported " + Path.GetFileName(jobs[0].dest);
            }
            if (e.install == "files")
            {
                Directory.CreateDirectory(e.files_dest);
                for (int k = 0; k < e.files.Count; k++) File.Copy(jobs[k].dest, e.files_dest + "/" + e.files[k].name, true);
                AssetDatabase.Refresh();
                return "saved " + e.files.Count + " files into " + e.files_dest;
            }
            return "nothing to place";
        }

        // Unzip, refusing any entry that would land outside the target folder.
        static void Extract(string zip, string target)
        {
            var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
            using (var a = new ZipArchive(File.OpenRead(zip), ZipArchiveMode.Read))
                foreach (var entry in a.Entries)
                {
                    var dest = Path.GetFullPath(Path.Combine(target, entry.FullName));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new Exception("zip entry outside the package folder: " + entry.FullName);
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")) { Directory.CreateDirectory(dest); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    using (var src = entry.Open()) using (var dst = File.Create(dest)) src.CopyTo(dst);
                }
        }

        static void Log(ShaderEntry e, string by, bool ok, string msg)
        {
            Directory.CreateDirectory("Review/shaders-shop");
            var line = "{\"schema\":\"unity-unicorn.tool-usage/v1\",\"tool_id\":\"shaders-shop\",\"project\":{\"name\":" + Q(Application.productName) +
                       "},\"completed_at\":" + Q(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")) + ",\"status\":" + Q(ok ? "completed" : "failed") +
                       ",\"summary\":" + Q("install " + e.id + " " + e.version + ": " + msg) + ",\"approved_by\":" + Q(by) + "}";
            File.AppendAllText("Review/shaders-shop/installs.jsonl", line + "\n");
        }

        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    // Packages/vpm-manifest.json, the list VCC keeps: {"dependencies": {name: {"version"}}, "locked": {name: {"version", "dependencies"}}}
    public static class VpmManifest
    {
        const string Path = "Packages/vpm-manifest.json";

        public static void Add(List<Pkg> pkgs, string topLevel)
        {
            var root = File.Exists(Path) ? MiniJson.Parse(File.ReadAllText(Path)) as Dictionary<string, object> : null;
            root = root ?? new Dictionary<string, object>();
            var deps = Obj(root, "dependencies"); var locked = Obj(root, "locked");
            var top = pkgs.First(p => p.name == topLevel);
            deps[top.name] = new Dictionary<string, object> { { "version", top.version } };
            foreach (var p in pkgs)
            {
                var d = new Dictionary<string, object>();
                var pj = "Packages/" + p.name + "/package.json";
                if (File.Exists(pj) && MiniJson.Parse(File.ReadAllText(pj)) is Dictionary<string, object> meta && meta.TryGetValue("vpmDependencies", out var vd) && vd is Dictionary<string, object> vdd)
                    foreach (var kv in vdd) { var dep = pkgs.FirstOrDefault(x => x.name == kv.Key); d[kv.Key] = dep != null ? dep.version : kv.Value; }
                locked[p.name] = new Dictionary<string, object> { { "version", p.version }, { "dependencies", d } };
            }
            File.WriteAllText(Path, MiniJson.Write(root) + "\n");
        }

        static Dictionary<string, object> Obj(Dictionary<string, object> parent, string key)
        {
            if (!(parent.TryGetValue(key, out var o) && o is Dictionary<string, object> d)) { d = new Dictionary<string, object>(); parent[key] = d; }
            return d;
        }
    }

    // A very small JSON reader/writer for the manifests (objects, arrays, strings, numbers, true/false/null).
    public static class MiniJson
    {
        public static object Parse(string s) { int i = 0; var v = Value(s, ref i); return v; }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i); if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++;
                while (true) { Ws(s, ref i); if (s[i] == '}') { i++; return d; } var k = Str(s, ref i); Ws(s, ref i); i++; d[k] = Value(s, ref i); Ws(s, ref i); if (s[i] == ',') { i++; continue; } i++; return d; }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++;
                while (true) { Ws(s, ref i); if (s[i] == ']') { i++; return l; } l.Add(Value(s, ref i)); Ws(s, ref i); if (s[i] == ',') { i++; continue; } i++; return l; }
            }
            if (c == '"') return Str(s, ref i);
            int start = i; while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0) i++;
            var tok = s.Substring(start, i - start);
            if (tok == "true") return true; if (tok == "false") return false; if (tok == "null") return null;
            return double.TryParse(tok, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? (object)n : tok;
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder(); i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++; char e = s[i];
                    if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++; return sb.ToString();
        }

        public static string Write(object o, int depth = 0)
        {
            string pad = new string(' ', (depth + 1) * 2), end = new string(' ', depth * 2);
            switch (o)
            {
                case null: return "null";
                case bool b: return b ? "true" : "false";
                case double d: return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case string str: return "\"" + str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
                case Dictionary<string, object> dict:
                    if (dict.Count == 0) return "{}";
                    return "{\n" + string.Join(",\n", dict.Select(kv => pad + Write(kv.Key) + ": " + Write(kv.Value, depth + 1))) + "\n" + end + "}";
                case List<object> list:
                    if (list.Count == 0) return "[]";
                    return "[\n" + string.Join(",\n", list.Select(x => pad + Write(x, depth + 1))) + "\n" + end + "]";
                default: return Write(o.ToString());
            }
        }
    }
}
