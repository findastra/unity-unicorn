using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Udon kit glass-animal-collectible, one animal. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Put it on the animal (with a collider). Interact finds it; with findOnTouch on, walking into its trigger finds it too.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KitGlassAnimalLink : UdonSharpBehaviour
{
    public KitGlassAnimalSet set;
    [Tooltip("Which animal this is (0 to count - 1).")]
    public int index;
    [Tooltip("Also find it by walking into its trigger collider.")]
    public bool findOnTouch;

    public override void Interact()
    {
        if (set != null) set.Find(index);
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!findOnTouch || set == null || !Utilities.IsValid(player) || !player.isLocal) return;
        set.Find(index);
    }
}
