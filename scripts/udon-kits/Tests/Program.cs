#nullable enable
// Udon kits test runner: no test packages (NuGet is not needed, so it runs offline). Finds every [KitTest] method, runs it,
// prints one line per test and a count per kit, checks the manifest, and exits 1 on any failure.
// Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace UdonKits.Tests
{
    public enum TestKind { Core, Parity }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class KitTestAttribute : Attribute
    {
        public KitTestAttribute(string kit, TestKind kind) { Kit = kit; Kind = kind; }
        public string Kit { get; }
        public TestKind Kind { get; }
    }

    public sealed class CheckFailed : Exception
    {
        public CheckFailed(string message) : base(message) { }
    }

    public static class Check
    {
        public static void True(bool condition, string what)
        {
            if (!condition) throw new CheckFailed(what);
        }

        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new CheckFailed(what + ": expected " + expected + ", got " + actual);
        }

        public static void Near(float expected, float actual, string what, float tolerance = 1e-5f)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new CheckFailed(what + ": expected " + expected + ", got " + actual);
        }

        public static void Throws<TException>(Action action, string what) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new CheckFailed(what + ": expected " + typeof(TException).Name);
        }
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            var tests = typeof(Program).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Select(m => (method: m, attr: m.GetCustomAttribute<KitTestAttribute>()))
                .Where(x => x.attr != null)
                .OrderBy(x => x.attr!.Kit).ThenBy(x => x.attr!.Kind).ThenBy(x => x.method.Name)
                .ToList();

            int failed = 0;
            var perKit = new SortedDictionary<string, int[]>();
            foreach (var (method, attr) in tests)
            {
                string label = attr!.Kit + " " + attr.Kind.ToString().ToLowerInvariant() + " " + method.Name;
                if (!perKit.ContainsKey(attr.Kit)) perKit[attr.Kit] = new int[3];
                perKit[attr.Kit][(int)attr.Kind]++;
                try
                {
                    Harness.Reset();
                    method.Invoke(null, null);
                    Console.WriteLine("ok     " + label);
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    perKit[attr.Kit][2]++;
                    Console.WriteLine("FAILED " + label + ": " + (e.InnerException?.Message ?? e.Message));
                }
            }

            failed += CheckManifest(perKit);

            Console.WriteLine();
            foreach (var kv in perKit)
                Console.WriteLine($"{kv.Key,-26} core {kv.Value[0],2}  parity {kv.Value[1],2}  failed {kv.Value[2]}");
            Console.WriteLine($"tests {tests.Count}  failed {failed}");
            return failed == 0 ? 0 : 1;
        }

        // The manifest is the contract agents compose from: every kit in it must have core and parity tests here, every
        // test must name a kit in it, and `tested: true` must be true.
        private static int CheckManifest(SortedDictionary<string, int[]> perKit)
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "udon-kits-manifest.json"))) dir = Path.GetDirectoryName(dir);
            if (dir == null) { Console.WriteLine("FAILED manifest: udon-kits-manifest.json not found above " + AppContext.BaseDirectory); return 1; }
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "udon-kits-manifest.json")));
            var ids = doc.RootElement.GetProperty("ids").EnumerateArray().Select(e => e.GetString()!).ToList();
            var kits = doc.RootElement.GetProperty("kits").EnumerateArray().ToList();
            int bad = 0;
            void Fail(string why) { bad++; Console.WriteLine("FAILED manifest: " + why); }
            if (!kits.Select(k => k.GetProperty("id").GetString()).SequenceEqual(ids)) Fail("kits are not the ids, in order");
            foreach (var k in kits)
            {
                string id = k.GetProperty("id").GetString()!;
                bool tested = k.GetProperty("tested").GetBoolean();
                int[] c = perKit.TryGetValue(id, out var v) ? v : new int[3];
                if (tested && (c[0] == 0 || c[1] == 0)) Fail(id + " says tested but has " + c[0] + " core and " + c[1] + " parity tests");
                if (tested && c[2] > 0) Fail(id + " says tested but " + c[2] + " of its tests failed");
                if (!tested) Fail(id + " is not tested: agents may only compose tested kits, so finish it or take it out");
            }
            foreach (var kit in perKit.Keys) if (!ids.Contains(kit)) Fail("tests name " + kit + ", which is not in the manifest");
            if (bad == 0) Console.WriteLine("ok     manifest: " + ids.Count + " kits, each with core and parity tests");
            return bad;
        }
    }
}
