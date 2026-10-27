// Kit synced-counter (family sync). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26. One whole number everyone in the
// instance sees: a tip jar, a lap count, a score. The presser takes ownership, changes it and sends it. It stays between
// min and max, or wraps round when wrap is on.
using System;

namespace UdonKits.Cores
{
    public sealed class SyncedCounterCore
    {
        private readonly int min;
        private readonly int max;
        private readonly int step;
        private readonly int start;
        private readonly bool wrap;
        private readonly ISyncSeam sync;

        /// <summary>Raised whenever Value changes, locally or from the network.</summary>
        public event Action<int>? Changed;

        /// <summary>Raised when a local change lands on max.</summary>
        public event Action? ReachedMax;

        public SyncedCounterCore(int min, int max, int step, int start, bool wrap, ISyncSeam sync)
        {
            if (max < min) throw new ArgumentException("max must be at least min", nameof(max));
            this.min = min;
            this.max = max;
            this.step = Math.Max(1, step);
            this.start = Clamp(start, min, max);
            this.wrap = wrap;
            this.sync = sync;
            Value = this.start;
        }

        public int Value { get; private set; }
        public int Min => min;
        public int Max => max;

        public void Increment() { Change(Value + step); }
        public void Decrement() { Change(Value - step); }
        public void ResetCount() { Change(start); }

        /// <summary>A synced value arrived from the owner (OnDeserialization).</summary>
        public void ApplyRemote(int value)
        {
            int v = Clamp(value, min, max);
            if (v == Value) return;
            Value = v;
            Changed?.Invoke(Value);
        }

        private void Change(int wanted)
        {
            int v = Fit(wanted);
            if (!sync.IsOwner) sync.TakeOwnership();
            bool changed = v != Value;
            Value = v;
            sync.RequestSerialization();
            if (changed)
            {
                Changed?.Invoke(Value);
                if (Value == max) ReachedMax?.Invoke();
            }
        }

        private int Fit(int v)
        {
            if (!wrap) return Clamp(v, min, max);
            int span = max - min + 1;
            int r = (v - min) % span;
            if (r < 0) r += span;
            return min + r;
        }

        private static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
