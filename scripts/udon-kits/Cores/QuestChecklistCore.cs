// Kit quest-checklist (family quest). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26. A list of up to 30 steps the local
// player ticks off, saved to them (Piranesi House's Far Hall Quests are the worked example). Ordered: a step only counts
// once every step before it is done. Unordered: any order. Restores merge with what was done before the save arrived,
// and nothing is saved until the save has arrived, so early steps never overwrite an earlier visit's progress.
using System;

namespace UdonKits.Cores
{
    public sealed class QuestChecklistCore
    {
        private readonly int steps;
        private readonly bool ordered;
        private readonly string key;
        private readonly IPlayerStore store;
        private bool restored;

        /// <summary>Raised for each step done: which one, and how many are done now.</summary>
        public event Action<int, int>? StepDone;

        /// <summary>Raised when an ordered step is tried before the steps above it.</summary>
        public event Action<int>? Blocked;

        /// <summary>Raised once, when the last step is done.</summary>
        public event Action? QuestDone;

        /// <summary>Raised when the done set changes for any reason.</summary>
        public event Action? Changed;

        public QuestChecklistCore(int steps, bool ordered, string key, IPlayerStore store)
        {
            if (steps < 1 || steps > Mask.MaxItems) throw new ArgumentOutOfRangeException(nameof(steps), "1 to 30 steps");
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("a save key is needed", nameof(key));
            this.steps = steps;
            this.ordered = ordered;
            this.key = key;
            this.store = store;
        }

        public int Steps => steps;
        public int DoneMask { get; private set; }
        public int DoneCount => Mask.Count(DoneMask);
        public bool Complete => DoneMask == Mask.All(steps);
        public bool Restored => restored;

        /// <summary>The first step not done yet, or -1 when the quest is complete.</summary>
        public int NextStep
        {
            get
            {
                for (int i = 0; i < steps; i++) if ((DoneMask & (1 << i)) == 0) return i;
                return -1;
            }
        }

        public bool IsDone(int i)
        {
            return i >= 0 && i < steps && (DoneMask & (1 << i)) != 0;
        }

        public void Restore()
        {
            int saved;
            if (!store.TryGetInt(key, out saved)) saved = 0;
            int merged = (DoneMask | saved) & Mask.All(steps);
            bool wasComplete = Complete;
            bool changed = merged != DoneMask;
            DoneMask = merged;
            restored = true;
            if (merged != saved) store.SetInt(key, merged);
            if (changed) Changed?.Invoke();
            if (!wasComplete && Complete) QuestDone?.Invoke();
        }

        /// <summary>Tick off step <paramref name="i"/>. Returns false when done already, out of range or blocked.</summary>
        public bool CompleteStep(int i)
        {
            if (i < 0 || i >= steps) return false;
            int bit = 1 << i;
            if ((DoneMask & bit) != 0) return false;
            if (ordered && (DoneMask & (bit - 1)) != bit - 1)
            {
                Blocked?.Invoke(i);
                return false;
            }
            DoneMask |= bit;
            if (restored) store.SetInt(key, DoneMask);
            StepDone?.Invoke(i, DoneCount);
            Changed?.Invoke();
            if (Complete) QuestDone?.Invoke();
            return true;
        }

        /// <summary>Start the quest over (a reset lever). Saved at once, once the save has arrived.</summary>
        public void ResetQuest()
        {
            if (DoneMask == 0) return;
            DoneMask = 0;
            if (restored) store.SetInt(key, 0);
            Changed?.Invoke();
        }
    }
}
