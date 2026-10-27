// Kit timed-respawn (family spawn). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26. A pickup goes home a set time after
// it is dropped, unless someone picks it up again first (Astra's Infinite Pole has pickups that end up lost: see its
// pickup-audit tool). Only the player who dropped it runs the timer, and that player owns it, so one respawn happens.
using System;

namespace UdonKits.Cores
{
    public enum RespawnState
    {
        Home = 0,
        Held = 1,
        Waiting = 2,
    }

    public sealed class TimedRespawnCore
    {
        private readonly float delay;
        private readonly IClock clock;
        private float dueAt;

        /// <summary>Raised when it should go home now.</summary>
        public event Action? Respawned;

        public TimedRespawnCore(float delay, IClock clock)
        {
            this.delay = Math.Max(0f, delay);
            this.clock = clock;
            State = RespawnState.Home;
        }

        public RespawnState State { get; private set; }
        public float SecondsLeft => State == RespawnState.Waiting ? Math.Max(0f, dueAt - clock.Now) : 0f;

        public void Pickup()
        {
            State = RespawnState.Held;
        }

        public void Drop()
        {
            State = RespawnState.Waiting;
            dueAt = clock.Now + delay;
        }

        /// <summary>Called every frame. Returns true on the frame it should go home.</summary>
        public bool Tick()
        {
            if (State != RespawnState.Waiting || clock.Now < dueAt) return false;
            State = RespawnState.Home;
            Respawned?.Invoke();
            return true;
        }

        /// <summary>Send it home now (a call from another kit). Ignored while it is held.</summary>
        public bool ForceRespawn()
        {
            if (State == RespawnState.Held) return false;
            State = RespawnState.Home;
            Respawned?.Invoke();
            return true;
        }
    }
}
