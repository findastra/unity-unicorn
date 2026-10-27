using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Udon kit quest-checklist, one step. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Interact ticks off step index; with completeOnTouch on, walking into its trigger does too. Another kit can tick it by
// sending it the event CompleteStep (for example a KitGlassAnimalSet's onAllFound), so kits chain without new code.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KitQuestStep : UdonSharpBehaviour
{
    public KitQuestChecklist quest;
    [Tooltip("Which step this is (0 to steps - 1).")]
    public int index;
    public bool completeOnTouch;
    [Tooltip("Off: no Interact prompt (the step is only ticked by touch or by another kit's event).")]
    public bool interactable = true;

    void Start()
    {
        DisableInteractive = !interactable;
    }

    public override void Interact()
    {
        CompleteStep();
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!completeOnTouch || !Utilities.IsValid(player) || !player.isLocal) return;
        CompleteStep();
    }

    public void CompleteStep()
    {
        if (quest != null) quest.CompleteStep(index);
    }
}
