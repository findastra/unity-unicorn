// Kit toggle-switch (family toggle). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26. On or off: a mirror, the lights,
// a stop's decorations. Local (only your screen; right for mirrors) or synced (everyone sees it; the presser takes
// ownership, flips it and sends it; everyone else applies what arrives).
using System;

namespace UdonKits.Cores
{
    public sealed class ToggleCore
    {
        private readonly bool synced;
        private readonly ISyncSeam sync;

        /// <summary>Raised whenever On changes, locally or from the network.</summary>
        public event Action<bool>? Changed;

        public ToggleCore(bool startOn, bool synced, ISyncSeam sync)
        {
            On = startOn;
            this.synced = synced;
            this.sync = sync;
        }

        public bool On { get; private set; }
        public bool Synced => synced;

        /// <summary>Interact: flip it.</summary>
        public void Press()
        {
            Set(!On);
        }

        /// <summary>Set it to a value (a call from another kit).</summary>
        public void Set(bool value)
        {
            if (synced && !sync.IsOwner) sync.TakeOwnership();
            bool changed = value != On;
            On = value;
            if (synced) sync.RequestSerialization();
            if (changed) Changed?.Invoke(On);
        }

        /// <summary>A synced value arrived from the owner (OnDeserialization).</summary>
        public void ApplyRemote(bool value)
        {
            if (!synced || value == On) return;
            On = value;
            Changed?.Invoke(On);
        }
    }
}
