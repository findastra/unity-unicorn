// Kit doorspace-door (family travel). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26. The rules of Liminal Doors'
// LiminalTraveler (Claude, 2026-10-18), made into a kit: a forward door takes you to a random Doorspace that is not the one
// you are in and not one of your last few; an arrival door takes you back the way you came; stop 0 (the start) is never a
// random destination. Each player travels alone, so there is no sync.
using System;

namespace UdonKits.Cores
{
    public sealed class DoorspaceTravelCore
    {
        public const int PathSize = 256;
        public const int RecentSize = 16;

        private readonly int stopCount;
        private readonly int avoidRecent;
        private readonly float cooldown;
        private readonly IClock clock;
        private readonly IRandomSource random;
        private readonly int[] path = new int[PathSize];
        private readonly int[] recent = new int[RecentSize];
        private int depth;
        private int recentN;
        private float lastTrip = -10f;

        /// <summary>Raised after every move: the stop you are in now, and whether you came by the way back.</summary>
        public event Action<int, bool>? Traveled;

        public DoorspaceTravelCore(int stopCount, int avoidRecent, float cooldown, IClock clock, IRandomSource random)
        {
            if (stopCount < 1) throw new ArgumentOutOfRangeException(nameof(stopCount), "a world needs at least the start stop");
            this.stopCount = stopCount;
            this.avoidRecent = Math.Max(0, avoidRecent);
            this.cooldown = Math.Max(0f, cooldown);
            this.clock = clock;
            this.random = random;
        }

        public int Current { get; private set; }
        public int WayBackLength => depth;
        public int StopCount => stopCount;

        /// <summary>Start, or a respawn: you are in stop 0 and the way back is empty.</summary>
        public void Reset()
        {
            depth = 0;
            Arrive(0, false);
        }

        /// <summary>A forward door in stop <paramref name="from"/>. Returns the stop you land in, or -1 when ignored
        /// (a door in another stop, or within the cooldown).</summary>
        public int Forward(int from)
        {
            if (!Ready(from)) return -1;
            int dest = Pick();
            if (dest < 0)
            {
                // no other stop exists yet: the door leads back to the start and the way back starts over
                depth = 0;
                Arrive(0, true);
                return 0;
            }
            if (depth < PathSize) { path[depth] = Current; depth++; }
            Arrive(dest, false);
            return dest;
        }

        /// <summary>An arrival door (the way back) in stop <paramref name="from"/>. Returns the stop you land in, or -1.</summary>
        public int Back(int from)
        {
            if (!Ready(from)) return -1;
            int dest = 0;
            if (depth > 0) { depth--; dest = path[depth]; }
            Arrive(dest, true);
            return dest;
        }

        private bool Ready(int from)
        {
            return from == Current && clock.Now - lastTrip >= cooldown;
        }

        private int Pick()
        {
            int avoid = Math.Min(avoidRecent, Math.Max(0, stopCount - 3));
            int[] options = new int[stopCount];
            int count = 0;
            for (int i = 1; i < stopCount; i++) if (i != Current && !Recent(i, avoid)) { options[count] = i; count++; }
            if (count == 0) for (int i = 1; i < stopCount; i++) if (i != Current) { options[count] = i; count++; }
            if (count == 0) return -1;
            return options[random.Range(0, count)];
        }

        private bool Recent(int stop, int avoid)
        {
            for (int k = 0; k < avoid && k < recentN; k++) if (recent[k] == stop) return true;
            return false;
        }

        private void Arrive(int dest, bool byWayBack)
        {
            Current = dest;
            lastTrip = clock.Now;
            for (int k = RecentSize - 1; k > 0; k--) recent[k] = recent[k - 1];
            recent[0] = dest;
            if (recentN < RecentSize) recentN++;
            Traveled?.Invoke(dest, byWayBack);
        }
    }
}
