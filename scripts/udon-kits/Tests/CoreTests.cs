#nullable enable
// Core tests: each kit's pure rules, with fake seams. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
using System;
using System.Collections.Generic;
using UdonKits.Cores;

namespace UdonKits.Tests
{
    public static class CoreTests
    {
        // ---------- doorspace-door ----------

        private static DoorspaceTravelCore Travel(int stops, FakeClock clock, int seed = 7, int avoid = 3)
        {
            var t = new DoorspaceTravelCore(stops, avoid, 0.75f, clock, new FakeRandom(seed));
            t.Reset();
            return t;
        }

        [KitTest("doorspace-door", TestKind.Core)]
        public static void ForwardNeverPicksTheStartOrWhereYouAre()
        {
            var clock = new FakeClock();
            var t = Travel(6, clock);
            for (int trip = 0; trip < 200; trip++)
            {
                clock.Now += 1f;
                int from = t.Current;
                int dest = t.Forward(from);
                Check.True(dest >= 1 && dest < 6, "a forward door lands in a Doorspace, never the start");
                Check.True(dest != from, "never the stop you are in");
            }
        }

        [KitTest("doorspace-door", TestKind.Core)]
        public static void ForwardAvoidsYourLastStops()
        {
            var clock = new FakeClock();
            var t = Travel(9, clock, seed: 3, avoid: 3);
            var last = new List<int>();
            for (int trip = 0; trip < 300; trip++)
            {
                clock.Now += 1f;
                int dest = t.Forward(t.Current);
                for (int k = 0; k < 3 && k < last.Count; k++) Check.True(last[last.Count - 1 - k] != dest, "not one of the last 3 stops");
                last.Add(dest);
            }
        }

        [KitTest("doorspace-door", TestKind.Core)]
        public static void BackRetracesTheWayYouCame()
        {
            var clock = new FakeClock();
            var t = Travel(8, clock);
            var visited = new List<int> { 0 };
            for (int i = 0; i < 5; i++) { clock.Now += 1f; visited.Add(t.Forward(t.Current)); }
            Check.Equal(5, t.WayBackLength, "five steps on the way back");
            for (int i = 4; i >= 0; i--)
            {
                clock.Now += 1f;
                Check.Equal(visited[i], t.Back(t.Current), "back to stop " + i + " of the trip");
            }
            clock.Now += 1f;
            Check.Equal(0, t.Back(0), "the way back from the start stays at the start");
            Check.Equal(0, t.WayBackLength, "the way back is empty");
        }

        [KitTest("doorspace-door", TestKind.Core)]
        public static void DoorsInOtherStopsAndTheCooldownAreIgnored()
        {
            var clock = new FakeClock();
            var t = Travel(5, clock);
            Check.Equal(-1, t.Forward(0), "no door works in the first 0.75 s");
            clock.Now = 1f;
            int first = t.Forward(0);
            Check.True(first > 0, "then it does");
            clock.Now = 1.5f;
            Check.Equal(-1, t.Forward(first), "within the cooldown");
            clock.Now = 3f;
            Check.Equal(-1, t.Forward(0), "a door in a stop you are not in");
            Check.Equal(first, t.Current, "still where you were");
        }

        [KitTest("doorspace-door", TestKind.Core)]
        public static void AWorldWithOnlyTheStartLeadsBackToIt()
        {
            var clock = new FakeClock { Now = 0f };
            var t = Travel(1, clock);
            bool byWayBack = false;
            t.Traveled += (stop, back) => byWayBack = back;
            clock.Now = 5f;
            Check.Equal(0, t.Forward(0), "nowhere else to go");
            Check.True(byWayBack, "it lands you at the start's return spot");
            Check.Throws<ArgumentOutOfRangeException>(() => new DoorspaceTravelCore(0, 3, 0.75f, clock, new FakeRandom(1)), "a world needs a start");
        }

        // ---------- teleport-pad ----------

        [KitTest("teleport-pad", TestKind.Core)]
        public static void AnUngatedPadIsAlwaysOpen()
        {
            var clock = new FakeClock();
            var p = new TeleportPadCore(false, 2.4f, 0.2f, 0.5f, clock);
            Check.True(p.Open, "open");
            Check.True(!p.Poll(100f), "distance does not matter");
            Check.True(p.Use(), "it sends you");
            Check.True(!p.Use(), "not twice in the cooldown");
            clock.Now = 0.6f;
            Check.True(p.Use(), "again after it");
        }

        [KitTest("teleport-pad", TestKind.Core)]
        public static void AGatedPadOpensOnlyNearItsSpotAndChecksAFewTimesASecond()
        {
            var clock = new FakeClock();
            var p = new TeleportPadCore(true, 2.4f, 0.2f, 0.5f, clock);
            int changes = 0;
            p.OpenChanged += _ => changes++;
            Check.True(!p.Open && !p.Use(), "closed far away");
            Check.True(p.Poll(1f), "opens when you step near");
            clock.Now = 0.1f;
            Check.True(!p.Poll(10f), "not checked again yet");
            Check.True(p.Open, "still open until the next check");
            clock.Now = 0.25f;
            Check.True(p.Poll(10f) && !p.Open, "closes at the next check");
            Check.Equal(2, changes, "two changes");
        }

        // ---------- glass-animal-collectible ----------

        [KitTest("glass-animal-collectible", TestKind.Core)]
        public static void FindingCountsOnceAndSaves()
        {
            var store = new FakeStore();
            var set = new CollectibleSetCore(4, "liminal.glass-animals", store);
            set.Restore();
            var found = new List<int>();
            set.Found += (i, n) => found.Add(i * 10 + n);
            Check.True(set.Find(2), "a new find");
            Check.True(!set.Find(2), "the same one again is not new");
            Check.True(!set.Find(4) && !set.Find(-1), "out of range");
            Check.True(set.Find(0), "another");
            Check.Equal(2, set.FoundCount, "two found");
            Check.Equal(0b101, store.Values["liminal.glass-animals"], "saved as a mask");
            Check.Equal("21,2", string.Join(",", found), "events say which (2, then 0) and how many (1, then 2)");
        }

        [KitTest("glass-animal-collectible", TestKind.Core)]
        public static void AllFoundFiresOnceOnTheLastFind()
        {
            var set = new CollectibleSetCore(3, "k", new FakeStore());
            int all = 0;
            set.AllFound += () => all++;
            set.Find(0); set.Find(1);
            Check.Equal(0, all, "not yet");
            set.Find(2);
            Check.Equal(1, all, "on the last one");
            set.Find(2);
            Check.Equal(1, all, "only once");
            Check.True(set.Complete, "complete");
        }

        [KitTest("glass-animal-collectible", TestKind.Core)]
        public static void RestoreMergesWithFindsMadeBeforeTheSaveArrived()
        {
            var store = new FakeStore();
            store.Values["k"] = 0b0011 | (1 << 20); // two found last visit, plus a stray bit beyond the count
            var set = new CollectibleSetCore(4, "k", store);
            set.Find(3);
            Check.Equal(0b0011 | (1 << 20), store.Values["k"], "a find before the save arrives saves nothing");
            set.Restore();
            Check.Equal(0b1011, set.FoundMask, "both kept, the stray bit dropped");
            Check.Equal(0b1011, store.Values["k"], "the merge is saved");
            Check.Throws<ArgumentOutOfRangeException>(() => new CollectibleSetCore(31, "k", store), "at most 30");
            Check.Throws<ArgumentException>(() => new CollectibleSetCore(3, "", store), "a key is needed");
        }

        // ---------- toggle-switch ----------

        [KitTest("toggle-switch", TestKind.Core)]
        public static void ALocalToggleNeverTouchesTheNetwork()
        {
            var sync = new FakeSync();
            var t = new ToggleCore(false, false, sync);
            t.Press(); t.Press(); t.Press();
            Check.True(t.On, "odd presses: on");
            Check.Equal(0, sync.Takes + sync.Serializations, "no ownership, no serialization");
            t.ApplyRemote(false);
            Check.True(t.On, "network values are ignored when local");
        }

        [KitTest("toggle-switch", TestKind.Core)]
        public static void ASyncedToggleTakesOwnershipSendsAndAppliesRemoteValues()
        {
            var sync = new FakeSync { IsOwner = false };
            var t = new ToggleCore(true, true, sync);
            int changes = 0;
            t.Changed += _ => changes++;
            t.Press();
            Check.True(!t.On, "off");
            Check.Equal(1, sync.Takes, "took ownership once");
            Check.Equal(1, sync.Serializations, "sent once");
            t.Press();
            Check.Equal(1, sync.Takes, "already the owner");
            t.ApplyRemote(false);
            t.ApplyRemote(false);
            Check.True(!t.On, "the remote value applies");
            Check.Equal(3, changes, "two presses and one real remote change");
            t.Set(false);
            Check.Equal(3, changes, "setting the same value is not a change");
        }

        // ---------- synced-counter ----------

        [KitTest("synced-counter", TestKind.Core)]
        public static void ACounterStaysBetweenMinAndMax()
        {
            var sync = new FakeSync();
            var c = new SyncedCounterCore(0, 3, 1, 0, false, sync);
            int max = 0;
            c.ReachedMax += () => max++;
            for (int i = 0; i < 6; i++) c.Increment();
            Check.Equal(3, c.Value, "stops at max");
            Check.Equal(1, max, "reached max once (pressing at max is no change)");
            for (int i = 0; i < 6; i++) c.Decrement();
            Check.Equal(0, c.Value, "stops at min");
            Check.Equal(1, sync.Takes, "took ownership once");
            Check.Equal(12, sync.Serializations, "every press is sent");
        }

        [KitTest("synced-counter", TestKind.Core)]
        public static void ACounterWrapsAndResets()
        {
            var c = new SyncedCounterCore(1, 4, 1, 2, true, new FakeSync { IsOwner = true });
            c.Increment(); c.Increment(); c.Increment();
            Check.Equal(1, c.Value, "4 then wraps to 1");
            c.Decrement();
            Check.Equal(4, c.Value, "wraps the other way");
            c.ResetCount();
            Check.Equal(2, c.Value, "reset goes to start");
            c.ApplyRemote(99);
            Check.Equal(4, c.Value, "remote values are clamped");
            Check.Throws<ArgumentException>(() => new SyncedCounterCore(5, 1, 1, 0, false, new FakeSync()), "max below min");
        }

        // ---------- timed-respawn ----------

        [KitTest("timed-respawn", TestKind.Core)]
        public static void ItGoesHomeAfterTheDelayUnlessPickedUpAgain()
        {
            var clock = new FakeClock();
            var r = new TimedRespawnCore(30f, clock);
            int home = 0;
            r.Respawned += () => home++;
            r.Pickup(); clock.Now = 5f; r.Drop();
            clock.Now = 34f;
            Check.True(!r.Tick(), "not yet");
            Check.Near(1f, r.SecondsLeft, "a second left");
            r.Pickup();
            clock.Now = 40f;
            Check.True(!r.Tick(), "picked up again: the timer stops");
            r.Drop();
            clock.Now = 70f;
            Check.True(r.Tick(), "goes home 30 s after the last drop");
            Check.True(!r.Tick(), "once");
            Check.Equal(RespawnState.Home, r.State, "home");
            Check.Equal(1, home, "one respawn");
        }

        [KitTest("timed-respawn", TestKind.Core)]
        public static void ForceRespawnIsIgnoredWhileHeld()
        {
            var r = new TimedRespawnCore(10f, new FakeClock());
            r.Pickup();
            Check.True(!r.ForceRespawn(), "not out of someone's hand");
            r.Drop();
            Check.True(r.ForceRespawn(), "once dropped");
            Check.Equal(RespawnState.Home, r.State, "home");
        }

        // ---------- audio-zone ----------

        [KitTest("audio-zone", TestKind.Core)]
        public static void ItFadesInWhileInsideAndOutAfter()
        {
            var z = new AudioZoneCore(0.6f, 1.5f);
            int silent = 0;
            z.Silent += () => silent++;
            z.Enter();
            Check.Near(0.2f, z.Tick(0.5f), "a third of the way up");
            Check.Near(0.6f, z.Tick(5f), "full, never above max");
            z.Exit();
            Check.Near(0.4f, z.Tick(0.5f), "fading");
            Check.Near(0f, z.Tick(2f), "silent");
            Check.Equal(1, silent, "silent once");
            z.Tick(1f);
            Check.Equal(1, silent, "staying silent is not a new event");
        }

        [KitTest("audio-zone", TestKind.Core)]
        public static void OverlappingBoxesCountAndLeaveClearsThem()
        {
            var z = new AudioZoneCore(1f, 0f);
            int entered = 0, exited = 0;
            z.Entered += () => entered++;
            z.Exited += () => exited++;
            z.Enter(); z.Enter(); z.Exit();
            Check.True(z.Inside, "still inside the second box");
            Check.Near(1f, z.Tick(0.01f), "no fade: full at once");
            z.Exit(); z.Exit();
            Check.True(!z.Inside, "outside");
            Check.Equal(1, entered, "entered once");
            Check.Equal(1, exited, "exited once, the extra exit ignored");
            z.Enter(); z.Enter(); z.Leave();
            Check.True(!z.Inside, "a respawn leaves at once");
            var loud = new AudioZoneCore(4f, 1f);
            loud.Enter();
            Check.Near(0f, loud.Tick(0f), "a zero-length frame changes nothing");
            Check.Near(1f, loud.Tick(10f), "max volume is clamped to 1");
        }

        // ---------- quest-checklist ----------

        [KitTest("quest-checklist", TestKind.Core)]
        public static void AnOrderedQuestBlocksStepsDoneTooEarly()
        {
            var store = new FakeStore();
            var q = new QuestChecklistCore(3, true, "piranesi.quests", store);
            q.Restore();
            var blocked = new List<int>();
            q.Blocked += i => blocked.Add(i);
            Check.True(!q.CompleteStep(1), "step 1 before step 0");
            Check.True(q.CompleteStep(0) && q.CompleteStep(1), "in order");
            Check.Equal(2, q.NextStep, "next is step 2");
            Check.True(q.CompleteStep(2) && q.Complete, "done");
            Check.Equal(-1, q.NextStep, "no next step");
            Check.Equal("1", string.Join(",", blocked), "one blocked try");
            Check.Equal(0b111, store.Values["piranesi.quests"], "saved");
        }

        [KitTest("quest-checklist", TestKind.Core)]
        public static void AnUnorderedQuestRestoresAndResets()
        {
            var store = new FakeStore();
            store.Values["q"] = 0b100;
            var q = new QuestChecklistCore(3, false, "q", store);
            int done = 0;
            q.QuestDone += () => done++;
            q.CompleteStep(0);
            q.CompleteStep(1);
            Check.Equal(0, done, "not yet: the save has not arrived");
            q.Restore();
            Check.Equal(1, done, "the restore completes it");
            Check.Equal(0b111, store.Values["q"], "merged and saved");
            q.ResetQuest();
            Check.Equal(0, q.DoneCount, "reset");
            Check.Equal(0, store.Values["q"], "reset is saved");
        }
    }
}
