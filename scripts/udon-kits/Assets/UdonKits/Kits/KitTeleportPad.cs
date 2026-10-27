using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Udon kit teleport-pad. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Mirrors scripts/udon-kits/Cores/TeleportPadCore.cs; the parity test runs both on the same walk.
// Interact sends you to target. With near set, the prompt only shows while you stand within nearRadius of it
// (checked every checkEvery seconds), like Piranesi House's Climb vine.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KitTeleportPad : UdonSharpBehaviour
{
    public Transform target;
    [Tooltip("Optional: only offer the teleport near this spot.")]
    public Transform near;
    public float nearRadius = 2.4f;
    public float checkEvery = 0.2f;
    public float cooldown = 0.5f;
    public AudioSource sound;
    [Tooltip("Optional: told when the pad sends the player.")]
    public UdonSharpBehaviour listener;
    public string onTeleported = "";

    private bool open = true;
    private float nextCheck;
    private float lastUse = -10f;

    void Start()
    {
        open = near == null;
        DisableInteractive = !open;
    }

    void Update()
    {
        if (near == null) return;
        VRCPlayerApi me = Networking.LocalPlayer;
        if (!Utilities.IsValid(me)) return;
        Poll(Vector3.Distance(me.GetPosition(), near.position));
    }

    public bool IsOpen() { return open; }

    public bool Poll(float distance)
    {
        if (near == null) return false;
        if (Time.time < nextCheck) return false;
        nextCheck = Time.time + Mathf.Max(0f, checkEvery);
        bool want = distance <= nearRadius;
        if (want == open) return false;
        open = want;
        DisableInteractive = !open;
        return true;
    }

    public override void Interact()
    {
        Use();
    }

    public bool Use()
    {
        if (!open) return false;
        if (Time.time - lastUse < cooldown) return false;
        lastUse = Time.time;
        VRCPlayerApi me = Networking.LocalPlayer;
        if (sound != null) sound.Play();
        if (target != null && Utilities.IsValid(me)) me.TeleportTo(target.position, target.rotation);
        if (listener != null && onTeleported != null && onTeleported.Length > 0) listener.SendCustomEvent(onTeleported);
        return true;
    }
}
