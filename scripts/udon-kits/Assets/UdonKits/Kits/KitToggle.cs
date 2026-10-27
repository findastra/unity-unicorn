using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Udon kit toggle-switch. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Mirrors scripts/udon-kits/Cores/ToggleCore.cs; the parity test runs both on the same presses and network values.
// Interact flips it. targets follow it (on when it is on); inverted do the opposite. Leave synced off for a mirror (each
// player's own); turn it on for lights everyone shares: the presser takes ownership and sends the value.
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class KitToggle : UdonSharpBehaviour
{
    public bool startOn;
    [Tooltip("On: everyone in the instance sees the same state. Off: only the presser's screen.")]
    public bool synced;
    public GameObject[] targets;
    public GameObject[] inverted;
    [Tooltip("Optional: told when it changes (onChanged).")]
    public UdonSharpBehaviour listener;
    public string onChanged = "";

    [UdonSynced] private bool syncedOn;
    private bool on;

    void Start()
    {
        on = startOn;
        syncedOn = startOn;
        Apply();
    }

    public bool IsOn() { return on; }

    public override void Interact()
    {
        Press();
    }

    public void Press()
    {
        Set(!on);
    }

    public void TurnOn() { Set(true); }
    public void TurnOff() { Set(false); }

    public void Set(bool value)
    {
        if (synced && !Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        bool changed = value != on;
        on = value;
        if (synced)
        {
            syncedOn = on;
            RequestSerialization();
        }
        if (changed) Changed();
    }

    public override void OnDeserialization()
    {
        if (!synced || syncedOn == on) return;
        on = syncedOn;
        Changed();
    }

    private void Changed()
    {
        Apply();
        if (listener != null && onChanged != null && onChanged.Length > 0) listener.SendCustomEvent(onChanged);
    }

    private void Apply()
    {
        if (targets != null) for (int i = 0; i < targets.Length; i++) if (targets[i] != null) targets[i].SetActive(on);
        if (inverted != null) for (int i = 0; i < inverted.Length; i++) if (inverted[i] != null) inverted[i].SetActive(!on);
    }
}
