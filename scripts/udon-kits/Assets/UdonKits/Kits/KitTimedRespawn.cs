using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;

// Udon kit timed-respawn. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Mirrors scripts/udon-kits/Cores/TimedRespawnCore.cs; the parity test runs both on the same pickups, drops and clock.
// Put it on a VRC Pickup. delay seconds after it is dropped it goes home, unless someone picks it up first. With a
// VRC Object Sync, its Respawn() sends it home for everyone; without one, it moves to home (or where it started).
// Only the player who dropped it (its owner) runs the timer.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KitTimedRespawn : UdonSharpBehaviour
{
    public float delay = 30f;
    [Tooltip("Optional: where home is. Empty: where it was at Start.")]
    public Transform home;
    [Tooltip("Optional: the object's VRC Object Sync.")]
    public VRCObjectSync objectSync;
    [Tooltip("Optional: told when it goes home (onRespawned).")]
    public UdonSharpBehaviour listener;
    public string onRespawned = "";

    // 0 home, 1 held, 2 waiting: the same numbers as RespawnState in the core
    private int state;
    private float dueAt;
    private Vector3 startPosition;
    private Quaternion startRotation;

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        state = 0;
    }

    void Update()
    {
        Tick();
    }

    public int State() { return state; }

    public override void OnPickup()
    {
        state = 1;
    }

    public override void OnDrop()
    {
        state = 2;
        dueAt = Time.time + Mathf.Max(0f, delay);
    }

    public bool Tick()
    {
        if (state != 2 || Time.time < dueAt) return false;
        state = 0;
        GoHome();
        return true;
    }

    public bool ForceRespawn()
    {
        if (state == 1) return false;
        state = 0;
        GoHome();
        return true;
    }

    private void GoHome()
    {
        if (objectSync != null)
        {
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
            objectSync.Respawn();
        }
        else if (home != null) transform.SetPositionAndRotation(home.position, home.rotation);
        else transform.SetPositionAndRotation(startPosition, startRotation);
        if (listener != null && onRespawned != null && onRespawned.Length > 0) listener.SendCustomEvent(onRespawned);
    }
}
