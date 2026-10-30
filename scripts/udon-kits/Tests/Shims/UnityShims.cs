#nullable enable
// Compile-only stand-ins for the small part of Unity, VRChat and UdonSharp that the kit wrappers use, so the wrappers
// compile and run headlessly next to their cores. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// These are NOT Unity. They copy the real names and signatures the wrappers call (the list is in docs/udon-kits-20261026.md)
// and nothing else. The real compile is UdonSharp in Unity 2022.3 on Astra's PC.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public string name = "";
    }

    public class GameObject : Object
    {
        private bool active = true;
        public readonly Transform transform = new Transform();
        public readonly List<Component> components = new List<Component>();

        public GameObject() { }
        public GameObject(string name) { this.name = name; }

        public bool activeSelf { get { return active; } }
        public void SetActive(bool value) { active = value; }
    }

    public class Component : Object
    {
        private GameObject? owner;

        public GameObject gameObject
        {
            get
            {
                if (owner == null) { owner = new GameObject(GetType().Name); owner.components.Add(this); }
                return owner;
            }
        }

        public Transform transform { get { return gameObject.transform; } }

        /// <summary>Test helper: put this component on a GameObject.</summary>
        public void AttachTo(GameObject go) { owner = go; go.components.Add(this); }
    }

    public class Transform : Component
    {
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;

        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
    }

    public class Behaviour : Component
    {
        public bool enabled = true;
    }

    public class MonoBehaviour : Behaviour { }

    public class AudioSource : Behaviour
    {
        public float volume = 1f;
        public bool isPlaying;
        public int plays;
        public int stops;

        public void Play() { isPlaying = true; plays++; }
        public void Stop() { isPlaying = false; stops++; }
    }

    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public bool Equals(Vector3 o) { return x == o.x && y == o.y && z == o.z; }
        public override bool Equals(object? obj) { return obj is Vector3 v && Equals(v); }
        public override int GetHashCode() { return HashCode.Combine(x, y, z); }
        public static bool operator ==(Vector3 a, Vector3 b) { return a.Equals(b); }
        public static bool operator !=(Vector3 a, Vector3 b) { return !a.Equals(b); }
        public override string ToString() { return "(" + x + ", " + y + ", " + z + ")"; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;

        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }

        public static Quaternion identity { get { return new Quaternion(0f, 0f, 0f, 1f); } }
    }

    public static class Mathf
    {
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Sign(float f) { return f >= 0f ? 1f : -1f; }

        // Unity's own MoveTowards, as documented
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Sign(target - current) * maxDelta;
        }
    }

    /// <summary>Unity's Time; the tests set the clock.</summary>
    public static class Time
    {
        public static float time;
        public static float deltaTime;
    }

    /// <summary>Unity's Random; the tests seed it the same as the core's random seam.</summary>
    public static class Random
    {
        public static System.Random Source = new System.Random(1);
        public static int Range(int minInclusive, int maxExclusive) { return Source.Next(minInclusive, maxExclusive); }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { Tooltip = tooltip; }
        public string Tooltip { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { Min = min; Max = max; }
        public float Min { get; }
        public float Max { get; }
    }
}

namespace VRC.SDKBase
{
    using UnityEngine;

    public class VRCPlayerApi
    {
        public bool isLocal;
        public int playerId;
        public string displayName = "";
        public Vector3 position;
        public int teleports;
        public Vector3 lastTeleport;

        public Vector3 GetPosition() { return position; }

        public void TeleportTo(Vector3 teleportPos, Quaternion teleportRot)
        {
            teleports++;
            lastTeleport = teleportPos;
            position = teleportPos;
        }
    }

    public static class Networking
    {
        private static readonly Dictionary<GameObject, VRCPlayerApi> Owners = new Dictionary<GameObject, VRCPlayerApi>();
        public static VRCPlayerApi? Local;
        public static VRCPlayerApi? DefaultOwner;

        public static VRCPlayerApi LocalPlayer { get { return Local!; } }

        public static bool IsOwner(GameObject obj) { return Local != null && GetOwner(obj) == Local; }

        public static VRCPlayerApi GetOwner(GameObject obj)
        {
            VRCPlayerApi? p;
            if (Owners.TryGetValue(obj, out p)) return p;
            return DefaultOwner!;
        }

        public static void SetOwner(VRCPlayerApi player, GameObject obj) { Owners[obj] = player; }

        public static void ResetForTest(VRCPlayerApi local, VRCPlayerApi defaultOwner)
        {
            Owners.Clear();
            Local = local;
            DefaultOwner = defaultOwner;
        }
    }

    public static class Utilities
    {
        public static bool IsValid(object? obj) { return obj != null; }
    }
}

namespace VRC.SDK3.Persistence
{
    using VRC.SDKBase;

    /// <summary>VRChat's per-player saved data (SDK 3.7+). The shim keeps one dictionary per player id.</summary>
    public static class PlayerData
    {
        private static readonly Dictionary<int, Dictionary<string, int>> Ints = new Dictionary<int, Dictionary<string, int>>();
        public static int Writes;

        public static bool TryGetInt(VRCPlayerApi player, string key, out int value)
        {
            Dictionary<string, int>? d;
            value = 0;
            return Ints.TryGetValue(player.playerId, out d) && d.TryGetValue(key, out value);
        }

        public static void SetInt(string key, int value)
        {
            int id = Networking.LocalPlayer.playerId;
            Dictionary<string, int>? d;
            if (!Ints.TryGetValue(id, out d)) { d = new Dictionary<string, int>(); Ints[id] = d; }
            d[key] = value;
            Writes++;
        }

        /// <summary>Test helper: what a player had saved before this visit.</summary>
        public static void Seed(int playerId, string key, int value)
        {
            Dictionary<string, int>? d;
            if (!Ints.TryGetValue(playerId, out d)) { d = new Dictionary<string, int>(); Ints[playerId] = d; }
            d[key] = value;
        }

        public static void ResetForTest() { Ints.Clear(); Writes = 0; }
    }
}

namespace VRC.SDK3.Components
{
    using UnityEngine;

    public class VRCObjectSync : Component
    {
        public int respawns;
        public void Respawn() { respawns++; }
    }
}

namespace UdonSharp
{
    using UnityEngine;
    using VRC.SDKBase;

    public enum BehaviourSyncMode
    {
        Any,
        Continuous,
        Manual,
        None,
        NoVariableSync,
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class UdonBehaviourSyncModeAttribute : Attribute
    {
        public UdonBehaviourSyncModeAttribute(BehaviourSyncMode mode) { Mode = mode; }
        public BehaviourSyncMode Mode { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UdonSyncedAttribute : Attribute { }

    public abstract class UdonSharpBehaviour : MonoBehaviour
    {
        public readonly List<string> received = new List<string>();
        public int serializations;

        public bool DisableInteractive { get; set; }

        public virtual void Interact() { }
        public virtual void OnPlayerTriggerEnter(VRCPlayerApi player) { }
        public virtual void OnPlayerTriggerExit(VRCPlayerApi player) { }
        public virtual void OnPlayerRespawn(VRCPlayerApi player) { }
        public virtual void OnPlayerRestored(VRCPlayerApi player) { }
        public virtual void OnDeserialization() { }
        public virtual void OnPickup() { }
        public virtual void OnDrop() { }

        public void RequestSerialization() { serializations++; }

        /// <summary>Like Udon: runs the public method with no parameters of that name, if there is one.</summary>
        public void SendCustomEvent(string eventName)
        {
            received.Add(eventName);
            MethodInfo? m = GetType().GetMethod(eventName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (m != null) m.Invoke(this, null);
        }
    }
}
