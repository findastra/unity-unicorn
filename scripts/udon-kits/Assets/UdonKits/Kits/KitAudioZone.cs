using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Udon kit audio-zone. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Mirrors scripts/udon-kits/Cores/AudioZoneCore.cs; the parity test runs both on the same walk and frame times.
// Put it on a GameObject with trigger colliders. While the local player is inside, source fades up to maxVolume over
// fadeSeconds; after they leave it fades down and stops. Enter and exit are counted, so overlapping boxes are fine.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KitAudioZone : UdonSharpBehaviour
{
    public AudioSource source;
    [Range(0f, 1f)]
    public float maxVolume = 0.6f;
    public float fadeSeconds = 1.5f;
    [Tooltip("Optional: told when the player comes in (onEntered) and leaves (onExited).")]
    public UdonSharpBehaviour listener;
    public string onEntered = "";
    public string onExited = "";

    private int inside;
    private float volume;

    void Start()
    {
        if (maxVolume < 0f) maxVolume = 0f;
        if (maxVolume > 1f) maxVolume = 1f;
        if (fadeSeconds < 0f) fadeSeconds = 0f;
        volume = 0f;
        if (source != null) { source.volume = 0f; source.Stop(); }
    }

    void Update()
    {
        Tick(Time.deltaTime);
    }

    public bool IsInside() { return inside > 0; }
    public float Volume() { return volume; }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;
        inside++;
        if (inside == 1) Tell(onEntered);
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal || inside == 0) return;
        inside--;
        if (inside == 0) Tell(onExited);
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal || inside == 0) return;
        inside = 0;
        Tell(onExited);
    }

    public float Tick(float deltaSeconds)
    {
        float target = 0f;
        if (inside > 0) target = maxVolume;
        float before = volume;
        if (fadeSeconds <= 0f) volume = target;
        else volume = Mathf.MoveTowards(volume, target, maxVolume / fadeSeconds * Mathf.Max(0f, deltaSeconds));
        if (source != null)
        {
            source.volume = volume;
            if (volume > 0f && !source.isPlaying) source.Play();
            if (before > 0f && volume <= 0f) source.Stop();
        }
        return volume;
    }

    private void Tell(string eventName)
    {
        if (listener != null && eventName != null && eventName.Length > 0) listener.SendCustomEvent(eventName);
    }
}
