// Udon kits: the seams every core is given instead of touching Unity or VRChat. Claude Opus 5.5 (claude-opus-5-5),
// 2026-10-26. Cores are plain C#: no UnityEngine, no VRC, no UdonSharp. A test passes a fake; the matching UdonSharp
// wrapper does the same job with Time.time, Random.Range, Networking and PlayerData.
namespace UdonKits.Cores
{
    /// <summary>Seconds since the world loaded (the wrapper uses Time.time).</summary>
    public interface IClock
    {
        float Now { get; }
    }

    /// <summary>A whole number from min (inclusive) to max (exclusive) (the wrapper uses UnityEngine.Random.Range).</summary>
    public interface IRandomSource
    {
        int Range(int minInclusive, int maxExclusive);
    }

    /// <summary>Manual sync on one object (the wrapper uses Networking.IsOwner, Networking.SetOwner and
    /// RequestSerialization).</summary>
    public interface ISyncSeam
    {
        bool IsOwner { get; }
        void TakeOwnership();
        void RequestSerialization();
    }

    /// <summary>What the local player keeps between visits (the wrapper uses VRC.SDK3.Persistence.PlayerData).</summary>
    public interface IPlayerStore
    {
        bool TryGetInt(string key, out int value);
        void SetInt(string key, int value);
    }

    /// <summary>The bit masks the collect and quest kits keep: at most 30 items, so the mask stays a positive int.</summary>
    public static class Mask
    {
        public const int MaxItems = 30;

        public static int All(int count)
        {
            return count >= 31 ? int.MaxValue : (1 << count) - 1;
        }

        public static int Count(int mask)
        {
            int n = 0;
            while (mask != 0) { n += mask & 1; mask >>= 1; }
            return n;
        }
    }
}
