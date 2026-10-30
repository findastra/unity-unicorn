// Kit glass-animal-collectible (family collect). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26. Liminal Doors hides one
// little glass animal behind every door; Piranesi House's HouseQuest keeps its prizes per player in PlayerData. This core
// keeps which of up to 30 hidden things the local player has found, as a bit mask saved under one key, and counts them.
// Finds made before the saved mask arrives are kept: the two are merged, and nothing is saved until the save has
// arrived, so an early find can never overwrite an earlier visit's progress.
using System;

namespace UdonKits.Cores
{
    public sealed class CollectibleSetCore
    {
        private readonly int count;
        private readonly string key;
        private readonly IPlayerStore store;
        private bool restored;

        /// <summary>Raised for each new find: which one, and how many are found now.</summary>
        public event Action<int, int>? Found;

        /// <summary>Raised once, when the last one is found.</summary>
        public event Action? AllFound;

        /// <summary>Raised when the found set changes for any reason (a find or a restore).</summary>
        public event Action? Changed;

        public CollectibleSetCore(int count, string key, IPlayerStore store)
        {
            if (count < 1 || count > Mask.MaxItems) throw new ArgumentOutOfRangeException(nameof(count), "1 to 30 collectibles");
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("a save key is needed", nameof(key));
            this.count = count;
            this.key = key;
            this.store = store;
        }

        public int Count => count;
        public int FoundMask { get; private set; }
        public int FoundCount => Mask.Count(FoundMask);
        public bool Complete => FoundMask == Mask.All(count);
        public bool Restored => restored;

        public bool IsFound(int i)
        {
            return i >= 0 && i < count && (FoundMask & (1 << i)) != 0;
        }

        /// <summary>The player's saved data arrived (OnPlayerRestored). Merges it with anything found already.</summary>
        public void Restore()
        {
            int saved;
            if (!store.TryGetInt(key, out saved)) saved = 0;
            int merged = (FoundMask | saved) & Mask.All(count);
            bool wasComplete = Complete;
            bool changed = merged != FoundMask;
            FoundMask = merged;
            restored = true;
            if (merged != saved) store.SetInt(key, merged);
            if (changed) Changed?.Invoke();
            if (!wasComplete && Complete) AllFound?.Invoke();
        }

        /// <summary>The player found number <paramref name="i"/>. Returns false when it was found already or is out of range.</summary>
        public bool Find(int i)
        {
            if (i < 0 || i >= count) return false;
            int bit = 1 << i;
            if ((FoundMask & bit) != 0) return false;
            FoundMask |= bit;
            if (restored) store.SetInt(key, FoundMask);
            Found?.Invoke(i, FoundCount);
            Changed?.Invoke();
            if (Complete) AllFound?.Invoke();
            return true;
        }
    }
}
