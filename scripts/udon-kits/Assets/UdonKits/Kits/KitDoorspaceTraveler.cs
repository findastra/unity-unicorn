using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Udon kit doorspace-door, the traveler (one per world). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Mirrors scripts/udon-kits/Cores/DoorspaceTravelCore.cs line for line; the parity test runs both on the same trips.
// Every KitDoorspaceDoor calls Forward or Back here. Only the stop you are in is switched on, on your screen only.
// Stop [0] is the start and never a random destination. No sync: each player travels alone.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KitDoorspaceTraveler : UdonSharpBehaviour
{
    [Tooltip("Stop roots in order; [0] is the start (the booth).")]
    public GameObject[] stops;
    [Tooltip("Where a forward door lands you in each stop (in front of its arrival door).")]
    public Transform[] arrivals;
    [Tooltip("Where you land when you come back to each stop by the way back.")]
    public Transform[] returns;
    [Tooltip("A forward door avoids this many of your last stops, once there are enough stops.")]
    public int avoidRecent = 3;
    [Tooltip("Seconds after a trip before any door works again.")]
    public float cooldown = 0.75f;
    [Tooltip("Optional: told about every trip.")]
    public UdonSharpBehaviour listener;
    public string onTraveled = "";

    private int current;
    private int[] path = new int[256];
    private int depth;
    private int[] recent = new int[16];
    private int recentN;
    private float lastTrip = -10f;
    private bool lastByWayBack;

    void Start()
    {
        ResetTravel();
    }

    public int CurrentStop() { return current; }
    public int WayBackLength() { return depth; }
    public bool LastByWayBack() { return lastByWayBack; }

    public void ResetTravel()
    {
        depth = 0;
        Arrive(0, false);
    }

    public int Forward(int from)
    {
        if (!Ready(from)) return -1;
        int dest = Pick();
        if (dest < 0)
        {
            depth = 0;
            Arrive(0, true);
            return 0;
        }
        if (depth < path.Length) { path[depth] = current; depth++; }
        Arrive(dest, false);
        return dest;
    }

    public int Back(int from)
    {
        if (!Ready(from)) return -1;
        int dest = 0;
        if (depth > 0) { depth--; dest = path[depth]; }
        Arrive(dest, true);
        return dest;
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;
        ResetTravel();
    }

    private bool Ready(int from)
    {
        return from == current && Time.time - lastTrip >= cooldown;
    }

    private int Pick()
    {
        int n = stops.Length;
        int avoid = Mathf.Min(Mathf.Max(0, avoidRecent), Mathf.Max(0, n - 3));
        int[] options = new int[n];
        int count = 0;
        for (int i = 1; i < n; i++) if (i != current && !Recent(i, avoid)) { options[count] = i; count++; }
        if (count == 0) for (int i = 1; i < n; i++) if (i != current) { options[count] = i; count++; }
        if (count == 0) return -1;
        return options[Random.Range(0, count)];
    }

    private bool Recent(int stop, int avoid)
    {
        for (int k = 0; k < avoid && k < recentN; k++) if (recent[k] == stop) return true;
        return false;
    }

    // switch the destination on first, move the player, then switch every other stop off
    private void Arrive(int dest, bool byWayBack)
    {
        if (stops == null || dest < 0 || dest >= stops.Length) return;
        if (stops[dest] != null) stops[dest].SetActive(true);
        Transform at = null;
        if (byWayBack) { if (returns != null && dest < returns.Length) at = returns[dest]; }
        else if (arrivals != null && dest < arrivals.Length) at = arrivals[dest];
        VRCPlayerApi me = Networking.LocalPlayer;
        if (at != null && Utilities.IsValid(me)) me.TeleportTo(at.position, at.rotation);
        for (int i = 0; i < stops.Length; i++) if (i != dest && stops[i] != null) stops[i].SetActive(false);
        current = dest;
        lastByWayBack = byWayBack;
        lastTrip = Time.time;
        for (int k = recent.Length - 1; k > 0; k--) recent[k] = recent[k - 1];
        recent[0] = dest;
        if (recentN < recent.Length) recentN++;
        if (listener != null && onTraveled != null && onTraveled.Length > 0) listener.SendCustomEvent(onTraveled);
    }
}
