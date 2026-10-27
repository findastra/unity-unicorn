// Kit teleport-pad (family travel). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26. A fixed teleport you use with
// Interact, after Piranesi House's HouseTeleport: optionally it only offers itself while you stand near a spot, checked a
// few times a second rather than every frame. A short cooldown stops a double press sending you twice.
using System;

namespace UdonKits.Cores
{
    public sealed class TeleportPadCore
    {
        private readonly bool gated;
        private readonly float nearRadius;
        private readonly float checkEvery;
        private readonly float cooldown;
        private readonly IClock clock;
        private float nextCheck;
        private float lastUse = -10f;

        /// <summary>Raised when the interact prompt appears (true) or goes away (false).</summary>
        public event Action<bool>? OpenChanged;

        /// <summary>Raised when the pad sends the player.</summary>
        public event Action? Teleported;

        public TeleportPadCore(bool gated, float nearRadius, float checkEvery, float cooldown, IClock clock)
        {
            this.gated = gated;
            this.nearRadius = Math.Max(0f, nearRadius);
            this.checkEvery = Math.Max(0f, checkEvery);
            this.cooldown = Math.Max(0f, cooldown);
            this.clock = clock;
            Open = !gated;
        }

        public bool Open { get; private set; }

        /// <summary>Called every frame with the player's distance to the spot. Returns true when Open changed.</summary>
        public bool Poll(float distance)
        {
            if (!gated) return false;
            if (clock.Now < nextCheck) return false;
            nextCheck = clock.Now + checkEvery;
            bool want = distance <= nearRadius;
            if (want == Open) return false;
            Open = want;
            OpenChanged?.Invoke(Open);
            return true;
        }

        /// <summary>The player pressed Interact. Returns true when they should be teleported.</summary>
        public bool Use()
        {
            if (!Open) return false;
            if (clock.Now - lastUse < cooldown) return false;
            lastUse = clock.Now;
            Teleported?.Invoke();
            return true;
        }
    }
}
