// Kit audio-zone (family audio). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26. A sound that fades in while the local
// player is inside a trigger and fades out after they leave: a Doorspace's hum, the pool's water, a room tone. Enter and
// exit are counted, so overlapping trigger boxes on one zone never cut the sound early.
using System;

namespace UdonKits.Cores
{
    public sealed class AudioZoneCore
    {
        private readonly float maxVolume;
        private readonly float fadeSeconds;
        private int inside;

        public event Action? Entered;
        public event Action? Exited;

        /// <summary>Raised on the frame the volume reaches 0 after a fade out (the wrapper stops the source).</summary>
        public event Action? Silent;

        public AudioZoneCore(float maxVolume, float fadeSeconds)
        {
            this.maxVolume = Clamp01(maxVolume);
            this.fadeSeconds = Math.Max(0f, fadeSeconds);
        }

        public bool Inside => inside > 0;
        public float Volume { get; private set; }

        public void Enter()
        {
            inside++;
            if (inside == 1) Entered?.Invoke();
        }

        public void Exit()
        {
            if (inside == 0) return;
            inside--;
            if (inside == 0) Exited?.Invoke();
        }

        /// <summary>Respawn or teleport out: no trigger exit is promised, so leave at once.</summary>
        public void Leave()
        {
            if (inside == 0) return;
            inside = 0;
            Exited?.Invoke();
        }

        /// <summary>Called every frame with the frame time. Returns the volume to set.</summary>
        public float Tick(float deltaSeconds)
        {
            float target = Inside ? maxVolume : 0f;
            float before = Volume;
            if (fadeSeconds <= 0f) Volume = target;
            else Volume = MoveTowards(Volume, target, maxVolume / fadeSeconds * Math.Max(0f, deltaSeconds));
            if (before > 0f && Volume <= 0f) Silent?.Invoke();
            return Volume;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
