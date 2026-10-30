using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Udon kit doorspace-door, one door: a trigger box in a doorway. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Walking in sends the local player on (a forward door: somewhere random) or back the way they came (an arrival door).
// Put it on a GameObject with a trigger collider; KitDoorspaceTraveler does the moving.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KitDoorspaceDoor : UdonSharpBehaviour
{
    public KitDoorspaceTraveler traveler;
    [Tooltip("Index of the stop this door stands in (0 = the start).")]
    public int stop;
    [Tooltip("On: an arrival door (the way back). Off: a forward door (somewhere random).")]
    public bool back;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal || traveler == null) return;
        if (back) traveler.Back(stop);
        else traveler.Forward(stop);
    }
}
