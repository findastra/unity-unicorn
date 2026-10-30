#nullable enable
// Fakes for the core seams, and the shim world the wrappers run in. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
using System.Collections.Generic;
using UdonKits.Cores;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

namespace UdonKits.Tests
{
    public sealed class FakeClock : IClock
    {
        public float Now { get; set; }
    }

    /// <summary>The core's random seam, drawing from System.Random exactly as the shim's UnityEngine.Random does.</summary>
    public sealed class FakeRandom : IRandomSource
    {
        private readonly System.Random source;
        public FakeRandom(int seed) { source = new System.Random(seed); }
        public int Range(int minInclusive, int maxExclusive) { return source.Next(minInclusive, maxExclusive); }
    }

    public sealed class FakeSync : ISyncSeam
    {
        public bool IsOwner { get; set; }
        public int Takes;
        public int Serializations;
        public void TakeOwnership() { Takes++; IsOwner = true; }
        public void RequestSerialization() { Serializations++; }
    }

    public sealed class FakeStore : IPlayerStore
    {
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public int Writes;
        public bool TryGetInt(string key, out int value) { return Values.TryGetValue(key, out value); }
        public void SetInt(string key, int value) { Values[key] = value; Writes++; }
    }

    public static class Harness
    {
        public static VRCPlayerApi Me = new VRCPlayerApi();
        public static VRCPlayerApi Other = new VRCPlayerApi();

        /// <summary>Before every test: a fresh clock, local player 1, player 2 owning everything, empty saves.</summary>
        public static void Reset()
        {
            Time.time = 0f;
            Time.deltaTime = 0f;
            UnityEngine.Random.Source = new System.Random(1);
            Me = new VRCPlayerApi { isLocal = true, playerId = 1, displayName = "me" };
            Other = new VRCPlayerApi { isLocal = false, playerId = 2, displayName = "other" };
            Networking.ResetForTest(Me, Other);
            PlayerData.ResetForTest();
        }

        /// <summary>Unity calls Start and Update itself; they are private in the wrappers.</summary>
        public static void Call(object behaviour, string method)
        {
            var m = behaviour.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Check.True(m != null, behaviour.GetType().Name + " has " + method);
            m!.Invoke(behaviour, null);
        }

        public static GameObject[] Objects(int n, string prefix)
        {
            var a = new GameObject[n];
            for (int i = 0; i < n; i++) a[i] = new GameObject(prefix + i);
            return a;
        }

        public static Transform[] Spots(int n, float y)
        {
            var a = new Transform[n];
            for (int i = 0; i < n; i++) a[i] = new Transform { position = new Vector3(i * 100f, y, 0f) };
            return a;
        }

        public static int ActiveIndex(GameObject[] objects)
        {
            int found = -1;
            for (int i = 0; i < objects.Length; i++)
                if (objects[i].activeSelf) { Check.True(found < 0, "only one active"); found = i; }
            return found;
        }

        /// <summary>A record of a listener's events, for kits that tell another behaviour.</summary>
        public sealed class Listener : UdonSharp.UdonSharpBehaviour { }
    }
}
