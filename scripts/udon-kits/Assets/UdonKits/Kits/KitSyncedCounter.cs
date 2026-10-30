using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Udon kit synced-counter. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Mirrors scripts/udon-kits/Cores/SyncedCounterCore.cs; the parity test runs both on the same presses and network values.
// One number everyone sees. Other kits or buttons call Increment, Decrement or ResetCount (SendCustomEvent works too).
// digits[k] is on when the value is min + k. The presser takes ownership, changes it and sends it.
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class KitSyncedCounter : UdonSharpBehaviour
{
    public int min;
    public int max = 10;
    public int step = 1;
    public int start;
    [Tooltip("On: going past max starts again at min (and the other way).")]
    public bool wrap;
    [Tooltip("Optional: digits[k] is on when the value is min + k.")]
    public GameObject[] digits;
    [Tooltip("Optional: told when it changes (onChanged) and when a local change lands on max (onReachedMax).")]
    public UdonSharpBehaviour listener;
    public string onChanged = "";
    public string onReachedMax = "";

    [UdonSynced] private int syncedValue;
    private int value;

    void Start()
    {
        if (max < min) max = min;
        if (step < 1) step = 1;
        start = Clamp(start);
        value = start;
        syncedValue = start;
        Apply();
    }

    public int Value() { return value; }

    public void Increment() { Change(value + step); }
    public void Decrement() { Change(value - step); }
    public void ResetCount() { Change(start); }

    public override void OnDeserialization()
    {
        int v = Clamp(syncedValue);
        if (v == value) return;
        value = v;
        Changed(false);
    }

    private void Change(int wanted)
    {
        int v = Fit(wanted);
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        bool changed = v != value;
        value = v;
        syncedValue = v;
        RequestSerialization();
        if (changed) Changed(true);
    }

    private int Fit(int v)
    {
        if (!wrap) return Clamp(v);
        int span = max - min + 1;
        int r = (v - min) % span;
        if (r < 0) r += span;
        return min + r;
    }

    private int Clamp(int v)
    {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }

    private void Changed(bool local)
    {
        Apply();
        if (listener == null) return;
        if (onChanged != null && onChanged.Length > 0) listener.SendCustomEvent(onChanged);
        if (local && value == max && onReachedMax != null && onReachedMax.Length > 0) listener.SendCustomEvent(onReachedMax);
    }

    private void Apply()
    {
        if (digits == null) return;
        for (int k = 0; k < digits.Length; k++) if (digits[k] != null) digits[k].SetActive(k == value - min);
    }
}
