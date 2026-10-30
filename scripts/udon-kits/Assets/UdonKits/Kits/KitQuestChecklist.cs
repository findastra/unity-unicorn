using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Persistence;

// Udon kit quest-checklist, the list (one per quest). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Mirrors scripts/udon-kits/Cores/QuestChecklistCore.cs; the parity test runs both on the same steps.
// Up to 30 steps the local player ticks off, saved to them with PlayerData under saveKey (Piranesi House's Far Hall
// Quests are the worked example). Ordered: a step only counts after every step before it. ticks[i] is on once step i is
// done; plaques[k] is on when k steps are done. KitQuestStep (or any kit's event) calls CompleteStep.
// Nothing is saved until the player's save arrives (OnPlayerRestored); early steps are merged in then, never lost.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KitQuestChecklist : UdonSharpBehaviour
{
    [Tooltip("How many steps (1 to 30).")]
    public int steps = 1;
    public bool ordered;
    [Tooltip("The PlayerData key. One per quest, for example piranesi.quests.")]
    public string saveKey = "udonkits.quest";
    public GameObject[] ticks;
    public GameObject[] plaques;
    public AudioSource chime;
    [Tooltip("Optional: told about each step (onStepDone), a step tried too early (onBlocked) and the end (onQuestDone).")]
    public UdonSharpBehaviour listener;
    public string onStepDone = "";
    public string onBlocked = "";
    public string onQuestDone = "";

    private int doneMask;
    private int lastStep = -1;
    private bool restored;

    void Start()
    {
        Apply();
    }

    public int DoneMask() { return doneMask; }
    public int DoneCount() { return CountBits(doneMask); }
    public int LastStep() { return lastStep; }
    public bool IsComplete() { return doneMask == AllMask(); }

    public bool IsDone(int i)
    {
        return i >= 0 && i < Size() && (doneMask & (1 << i)) != 0;
    }

    public int NextStep()
    {
        int n = Size();
        for (int i = 0; i < n; i++) if ((doneMask & (1 << i)) == 0) return i;
        return -1;
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;
        int saved;
        if (!PlayerData.TryGetInt(player, saveKey, out saved)) saved = 0;
        int merged = (doneMask | saved) & AllMask();
        bool wasComplete = IsComplete();
        doneMask = merged;
        restored = true;
        if (merged != saved) PlayerData.SetInt(saveKey, merged);
        Apply();
        if (!wasComplete && IsComplete()) Tell(onQuestDone);
    }

    public bool CompleteStep(int i)
    {
        if (i < 0 || i >= Size()) return false;
        int bit = 1 << i;
        if ((doneMask & bit) != 0) return false;
        lastStep = i;
        if (ordered && (doneMask & (bit - 1)) != bit - 1)
        {
            Tell(onBlocked);
            return false;
        }
        doneMask = doneMask | bit;
        if (restored) PlayerData.SetInt(saveKey, doneMask);
        if (chime != null) chime.Play();
        Apply();
        Tell(onStepDone);
        if (IsComplete()) Tell(onQuestDone);
        return true;
    }

    public void ResetQuest()
    {
        if (doneMask == 0) return;
        doneMask = 0;
        if (restored) PlayerData.SetInt(saveKey, 0);
        Apply();
    }

    private int Size()
    {
        if (steps < 1) return 1;
        if (steps > 30) return 30;
        return steps;
    }

    private int AllMask()
    {
        return (1 << Size()) - 1;
    }

    private int CountBits(int mask)
    {
        int n = 0;
        while (mask != 0) { n += mask & 1; mask = mask >> 1; }
        return n;
    }

    private void Apply()
    {
        int n = Size();
        if (ticks != null) for (int i = 0; i < n && i < ticks.Length; i++) if (ticks[i] != null) ticks[i].SetActive((doneMask & (1 << i)) != 0);
        int got = CountBits(doneMask);
        if (plaques != null) for (int k = 0; k < plaques.Length; k++) if (plaques[k] != null) plaques[k].SetActive(k == got);
    }

    private void Tell(string eventName)
    {
        if (listener != null && eventName != null && eventName.Length > 0) listener.SendCustomEvent(eventName);
    }
}
