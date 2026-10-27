using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Persistence;

// Udon kit glass-animal-collectible, the set (one per world). Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
// Mirrors scripts/udon-kits/Cores/CollectibleSetCore.cs; the parity test runs both on the same finds.
// Up to 30 hidden things (Liminal Doors: one glass animal behind every door), found by each player on their own and saved
// to them with PlayerData under saveKey. A found animal leaves its hiding place and its twin appears on the shelf; the
// plaques show the count (counts[k] is on when k are found). Each animal has a KitGlassAnimalLink that calls Find.
// Nothing is saved until the player's save arrives (OnPlayerRestored); early finds are merged in then, never lost.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KitGlassAnimalSet : UdonSharpBehaviour
{
    [Tooltip("How many there are (1 to 30).")]
    public int count = 1;
    [Tooltip("The PlayerData key. One per world, for example liminal.glass-animals.")]
    public string saveKey = "udonkits.collectibles";
    [Tooltip("Optional: the hidden animals, switched off once found.")]
    public GameObject[] hidden;
    [Tooltip("Optional: the twins on the shelf, switched on once found.")]
    public GameObject[] shelf;
    [Tooltip("Optional: plaques 0..count; only counts[found] is on.")]
    public GameObject[] counts;
    public AudioSource chime;
    [Tooltip("Optional: told about finds (onFound) and the last find (onAllFound).")]
    public UdonSharpBehaviour listener;
    public string onFound = "";
    public string onAllFound = "";

    private int foundMask;
    private int lastFound = -1;
    private bool restored;

    void Start()
    {
        Apply();
    }

    public int FoundMask() { return foundMask; }
    public int FoundCount() { return CountBits(foundMask); }
    public int LastFound() { return lastFound; }
    public bool IsComplete() { return foundMask == AllMask(); }

    public bool IsFound(int i)
    {
        return i >= 0 && i < Size() && (foundMask & (1 << i)) != 0;
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;
        int saved;
        if (!PlayerData.TryGetInt(player, saveKey, out saved)) saved = 0;
        int merged = (foundMask | saved) & AllMask();
        bool wasComplete = IsComplete();
        foundMask = merged;
        restored = true;
        if (merged != saved) PlayerData.SetInt(saveKey, merged);
        Apply();
        if (!wasComplete && IsComplete()) Tell(onAllFound);
    }

    public bool Find(int i)
    {
        if (i < 0 || i >= Size()) return false;
        int bit = 1 << i;
        if ((foundMask & bit) != 0) return false;
        foundMask = foundMask | bit;
        lastFound = i;
        if (restored) PlayerData.SetInt(saveKey, foundMask);
        if (chime != null) chime.Play();
        Apply();
        Tell(onFound);
        if (IsComplete()) Tell(onAllFound);
        return true;
    }

    private int Size()
    {
        if (count < 1) return 1;
        if (count > 30) return 30;
        return count;
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
        for (int i = 0; i < n; i++)
        {
            bool has = (foundMask & (1 << i)) != 0;
            if (hidden != null && i < hidden.Length && hidden[i] != null) hidden[i].SetActive(!has);
            if (shelf != null && i < shelf.Length && shelf[i] != null) shelf[i].SetActive(has);
        }
        int got = CountBits(foundMask);
        if (counts != null) for (int k = 0; k < counts.Length; k++) if (counts[k] != null) counts[k].SetActive(k == got);
    }

    private void Tell(string eventName)
    {
        if (listener != null && eventName != null && eventName.Length > 0) listener.SendCustomEvent(eventName);
    }
}
