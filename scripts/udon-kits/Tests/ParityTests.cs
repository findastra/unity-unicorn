#nullable enable
// Parity tests: UdonSharp cannot run the cores (Udon has no plain C# classes), so each wrapper re-states its core's rules.
// These tests drive a core and its real wrapper file (compiled against Shims/) through the same long, seeded run of
// events and check they agree after every step. A wrapper edit that drifts from its core fails here.
// Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
using System;
using System.Reflection;
using UdonKits.Cores;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

namespace UdonKits.Tests
{
    public static class ParityTests
    {
        private const int Steps = 600;

        private static void SetField(object o, string name, object value)
        {
            FieldInfo? f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Check.True(f != null, o.GetType().Name + "." + name);
            f!.SetValue(o, value);
        }

        private static int Count(Harness.Listener l, string ev)
        {
            int n = 0;
            foreach (string s in l.received) if (s == ev) n++;
            return n;
        }

        // ---------- doorspace-door ----------

        [KitTest("doorspace-door", TestKind.Parity)]
        public static void TravelerAgreesWithItsCore()
        {
            foreach (int stops in new[] { 1, 2, 4, 9, 30 })
                foreach (int seed in new[] { 1, 2, 3 })
                    TravelRun(stops, seed);
        }

        private static void TravelRun(int n, int seed)
        {
            Harness.Reset();
            var clock = new FakeClock();
            var core = new DoorspaceTravelCore(n, 3, 0.75f, clock, new FakeRandom(seed));
            bool coreByWayBack = false;
            core.Traveled += (s, back) => coreByWayBack = back;
            core.Reset();

            UnityEngine.Random.Source = new System.Random(seed);
            var w = new KitDoorspaceTraveler { stops = Harness.Objects(n, "stop"), arrivals = Harness.Spots(n, 1f), returns = Harness.Spots(n, 2f), avoidRecent = 3, cooldown = 0.75f };
            var listener = new Harness.Listener();
            w.listener = listener;
            w.onTraveled = "Traveled";
            Harness.Call(w, "Start");

            var forward = new KitDoorspaceDoor[n];
            var back = new KitDoorspaceDoor[n];
            for (int i = 0; i < n; i++)
            {
                forward[i] = new KitDoorspaceDoor { traveler = w, stop = i, back = false };
                back[i] = new KitDoorspaceDoor { traveler = w, stop = i, back = true };
            }

            var ops = new System.Random(seed * 31 + n);
            int trips = 1;
            for (int step = 0; step < Steps; step++)
            {
                float dt = (float)ops.NextDouble() * 1.2f;
                clock.Now += dt;
                Time.time = clock.Now;
                int op = ops.Next(10);
                int dest;
                if (op < 5) { dest = core.Forward(core.Current); forward[w.CurrentStop()].OnPlayerTriggerEnter(Harness.Me); }
                else if (op < 8) { dest = core.Back(core.Current); back[w.CurrentStop()].OnPlayerTriggerEnter(Harness.Me); }
                else if (op == 8) { int wrong = (core.Current + 1) % n; dest = core.Forward(wrong); forward[wrong].OnPlayerTriggerEnter(Harness.Me); }
                else { core.Reset(); dest = 0; w.OnPlayerRespawn(Harness.Me); }
                if (dest >= 0) trips++;

                string at = " (stops " + n + ", seed " + seed + ", step " + step + ")";
                Check.Equal(core.Current, w.CurrentStop(), "current stop" + at);
                Check.Equal(core.WayBackLength, w.WayBackLength(), "way back length" + at);
                Check.Equal(core.Current, Harness.ActiveIndex(w.stops), "only the current stop is on" + at);
                Check.Equal(coreByWayBack, w.LastByWayBack(), "arrived by the way back" + at);
                if (dest >= 0 && op != 9)
                {
                    Transform spot = coreByWayBack ? w.returns[dest] : w.arrivals[dest];
                    Check.Equal(spot.position, Harness.Me.position, "landed on the right spot" + at);
                }
                Check.Equal(trips, Count(listener, "Traveled"), "one onTraveled per trip" + at);
            }
        }

        // ---------- teleport-pad ----------

        [KitTest("teleport-pad", TestKind.Parity)]
        public static void PadAgreesWithItsCore()
        {
            foreach (bool gated in new[] { false, true })
            {
                Harness.Reset();
                var clock = new FakeClock();
                var core = new TeleportPadCore(gated, 2.4f, 0.2f, 0.5f, clock);
                int coreSent = 0;
                core.Teleported += () => coreSent++;
                var target = new Transform { position = new Vector3(5f, 6f, 7f) };
                var w = new KitTeleportPad { target = target, near = gated ? new Transform() : null, nearRadius = 2.4f, checkEvery = 0.2f, cooldown = 0.5f, sound = new AudioSource() };
                Harness.Call(w, "Start");

                var ops = new System.Random(gated ? 11 : 12);
                for (int step = 0; step < Steps; step++)
                {
                    clock.Now += (float)ops.NextDouble() * 0.3f;
                    Time.time = clock.Now;
                    float distance = (float)ops.NextDouble() * 5f;
                    Harness.Me.position = new Vector3(distance, 0f, 0f);
                    core.Poll(distance);
                    Harness.Call(w, "Update");
                    if (ops.Next(3) == 0) { core.Use(); w.Interact(); }

                    string at = " (gated " + gated + ", step " + step + ")";
                    Check.Equal(core.Open, w.IsOpen(), "open" + at);
                    Check.Equal(!core.Open, w.DisableInteractive, "prompt hidden when closed" + at);
                    Check.Equal(coreSent, Harness.Me.teleports, "teleports" + at);
                    Check.Equal(coreSent, w.sound.plays, "a sound per teleport" + at);
                }
                Check.True(coreSent > 10, "the run used the pad");
            }
        }

        // ---------- glass-animal-collectible ----------

        [KitTest("glass-animal-collectible", TestKind.Parity)]
        public static void AnimalSetAgreesWithItsCore()
        {
            foreach (int count in new[] { 1, 3, 7, 30 })
            {
                Harness.Reset();
                const string key = "liminal.glass-animals";
                int earlier = (count * 2654435) & ((1 << count) - 1) & 0x2AAAAAAA; // some found on an earlier visit
                var store = new FakeStore();
                store.Values[key] = earlier;
                PlayerData.Seed(Harness.Me.playerId, key, earlier);

                var core = new CollectibleSetCore(count, key, store);
                int coreFound = 0, coreAll = 0;
                core.Found += (i, n) => coreFound++;
                core.AllFound += () => coreAll++;

                var listener = new Harness.Listener();
                var w = new KitGlassAnimalSet { count = count, saveKey = key, hidden = Harness.Objects(count, "animal"), shelf = Harness.Objects(count, "twin"), counts = Harness.Objects(count + 1, "plaque"), chime = new AudioSource(), listener = listener, onFound = "Found", onAllFound = "AllFound" };
                Harness.Call(w, "Start");
                var links = new KitGlassAnimalLink[count];
                for (int i = 0; i < count; i++) links[i] = new KitGlassAnimalLink { set = w, index = i, findOnTouch = i % 2 == 0 };

                var ops = new System.Random(count);
                bool restored = false;
                for (int step = 0; step < 200; step++)
                {
                    int op = ops.Next(12);
                    if (op == 0 && !restored) { core.Restore(); w.OnPlayerRestored(Harness.Me); restored = true; }
                    else if (op == 1) { core.Find(-1); w.Find(-1); core.Find(count); w.Find(count); }
                    else
                    {
                        int i = ops.Next(count);
                        bool touch = ops.Next(2) == 0;
                        if (touch && !links[i].findOnTouch) links[i].OnPlayerTriggerEnter(Harness.Me); // touch is off here: nothing happens
                        else
                        {
                            core.Find(i);
                            if (touch) links[i].OnPlayerTriggerEnter(Harness.Me); else links[i].Interact();
                        }
                    }

                    string at = " (count " + count + ", step " + step + ")";
                    Check.Equal(core.FoundMask, w.FoundMask(), "found mask" + at);
                    Check.Equal(core.FoundCount, w.FoundCount(), "found count" + at);
                    Check.Equal(core.Complete, w.IsComplete(), "complete" + at);
                    for (int i = 0; i < count; i++)
                    {
                        Check.Equal(!core.IsFound(i), w.hidden[i].activeSelf, "animal " + i + " hidden until found" + at);
                        Check.Equal(core.IsFound(i), w.shelf[i].activeSelf, "twin " + i + " on the shelf once found" + at);
                    }
                    Check.Equal(core.FoundCount, Harness.ActiveIndex(w.counts), "the plaque shows the count" + at);
                    int saved;
                    PlayerData.TryGetInt(Harness.Me, key, out saved);
                    Check.Equal(store.Values[key], saved, "saved the same" + at);
                    Check.Equal(coreFound, Count(listener, "Found"), "onFound per find" + at);
                    Check.Equal(coreAll, Count(listener, "AllFound"), "onAllFound once" + at);
                    Check.Equal(coreFound, w.chime.plays, "a chime per find" + at);
                }
            }
        }

        // ---------- toggle-switch ----------

        [KitTest("toggle-switch", TestKind.Parity)]
        public static void ToggleAgreesWithItsCore()
        {
            foreach (bool synced in new[] { false, true })
                foreach (bool startOn in new[] { false, true })
                {
                    Harness.Reset();
                    var sync = new FakeSync { IsOwner = false };
                    var core = new ToggleCore(startOn, synced, sync);
                    int coreChanges = 0;
                    core.Changed += _ => coreChanges++;

                    var listener = new Harness.Listener();
                    var w = new KitToggle { startOn = startOn, synced = synced, targets = Harness.Objects(2, "mirror"), inverted = Harness.Objects(1, "dark"), listener = listener, onChanged = "Changed" };
                    Harness.Call(w, "Start");

                    var ops = new System.Random(synced ? 21 : 22);
                    for (int step = 0; step < Steps; step++)
                    {
                        int op = ops.Next(6);
                        if (op < 2) { core.Press(); w.Interact(); }
                        else if (op == 2) { bool v = ops.Next(2) == 0; core.Set(v); if (v) w.TurnOn(); else w.TurnOff(); }
                        else
                        {
                            // another player takes it, flips or keeps it, and sends it
                            bool v = ops.Next(2) == 0;
                            Networking.SetOwner(Harness.Other, w.gameObject);
                            sync.IsOwner = false;
                            SetField(w, "syncedOn", v);
                            w.OnDeserialization();
                            core.ApplyRemote(v);
                        }

                        string at = " (synced " + synced + ", start " + startOn + ", step " + step + ")";
                        Check.Equal(core.On, w.IsOn(), "on" + at);
                        Check.Equal(core.On, w.targets[0].activeSelf && w.targets[1].activeSelf, "targets follow" + at);
                        Check.Equal(!core.On, w.inverted[0].activeSelf, "inverted do the opposite" + at);
                        Check.Equal(sync.Serializations, w.serializations, "serializations" + at);
                        Check.Equal(sync.IsOwner, Networking.IsOwner(w.gameObject), "owner" + at);
                        Check.Equal(coreChanges, Count(listener, "Changed"), "onChanged per change" + at);
                    }
                }
        }

        // ---------- synced-counter ----------

        [KitTest("synced-counter", TestKind.Parity)]
        public static void CounterAgreesWithItsCore()
        {
            foreach (bool wrap in new[] { false, true })
            {
                Harness.Reset();
                var sync = new FakeSync { IsOwner = false };
                var core = new SyncedCounterCore(-2, 5, 2, 1, wrap, sync);
                int coreChanges = 0, coreMax = 0;
                core.Changed += _ => coreChanges++;
                core.ReachedMax += () => coreMax++;

                var listener = new Harness.Listener();
                var w = new KitSyncedCounter { min = -2, max = 5, step = 2, start = 1, wrap = wrap, digits = Harness.Objects(8, "digit"), listener = listener, onChanged = "Changed", onReachedMax = "Max" };
                Harness.Call(w, "Start");

                var ops = new System.Random(wrap ? 31 : 32);
                for (int step = 0; step < Steps; step++)
                {
                    int op = ops.Next(8);
                    if (op < 3) { core.Increment(); w.Increment(); }
                    else if (op < 5) { core.Decrement(); w.Decrement(); }
                    else if (op == 5) { core.ResetCount(); w.SendCustomEvent("ResetCount"); }
                    else
                    {
                        int v = ops.Next(-6, 10);
                        Networking.SetOwner(Harness.Other, w.gameObject);
                        sync.IsOwner = false;
                        SetField(w, "syncedValue", v);
                        w.OnDeserialization();
                        core.ApplyRemote(v);
                    }

                    string at = " (wrap " + wrap + ", step " + step + ")";
                    Check.Equal(core.Value, w.Value(), "value" + at);
                    Check.Equal(core.Value - core.Min, Harness.ActiveIndex(w.digits), "the digit shows the value" + at);
                    Check.Equal(sync.Serializations, w.serializations, "serializations" + at);
                    Check.Equal(sync.IsOwner, Networking.IsOwner(w.gameObject), "owner" + at);
                    Check.Equal(coreChanges, Count(listener, "Changed"), "onChanged" + at);
                    Check.Equal(coreMax, Count(listener, "Max"), "onReachedMax" + at);
                }
            }
        }

        // ---------- timed-respawn ----------

        [KitTest("timed-respawn", TestKind.Parity)]
        public static void RespawnAgreesWithItsCore()
        {
            foreach (bool withSync in new[] { false, true })
            {
                Harness.Reset();
                var clock = new FakeClock();
                var core = new TimedRespawnCore(4f, clock);
                int coreHome = 0;
                core.Respawned += () => coreHome++;

                var go = new GameObject("pickup");
                var w = new KitTimedRespawn { delay = 4f, listener = new Harness.Listener(), onRespawned = "Home" };
                w.AttachTo(go);
                go.transform.position = new Vector3(1f, 2f, 3f);
                VRCObjectSync? os = null;
                if (withSync) { os = new VRCObjectSync(); os.AttachTo(go); w.objectSync = os; }
                Harness.Call(w, "Start");

                var ops = new System.Random(withSync ? 41 : 42);
                for (int step = 0; step < Steps; step++)
                {
                    clock.Now += (float)ops.NextDouble() * 1.5f;
                    Time.time = clock.Now;
                    int op = ops.Next(6);
                    if (op == 0) { core.Pickup(); w.OnPickup(); go.transform.position = new Vector3(50f, 0f, 0f); }
                    else if (op == 1) { core.Drop(); w.OnDrop(); }
                    else if (op == 2) { core.ForceRespawn(); w.ForceRespawn(); }
                    core.Tick();
                    Harness.Call(w, "Update");

                    string at = " (sync " + withSync + ", step " + step + ")";
                    Check.Equal((int)core.State, w.State(), "state" + at);
                    Check.Equal(coreHome, Count((Harness.Listener)w.listener, "Home"), "onRespawned per respawn" + at);
                    if (withSync) Check.Equal(coreHome, os!.respawns, "VRC Object Sync respawns" + at);
                    else if (core.State == RespawnState.Home && coreHome > 0) Check.Equal(new Vector3(1f, 2f, 3f), go.transform.position, "back where it started" + at);
                }
                Check.True(coreHome > 5, "the run respawned it");
                if (withSync) Check.True(Networking.IsOwner(go), "it took ownership to respawn");
            }
        }

        // ---------- audio-zone ----------

        [KitTest("audio-zone", TestKind.Parity)]
        public static void AudioZoneAgreesWithItsCore()
        {
            foreach (float fade in new[] { 0f, 1.5f })
            {
                Harness.Reset();
                var core = new AudioZoneCore(0.6f, fade);
                int coreIn = 0, coreOut = 0;
                core.Entered += () => coreIn++;
                core.Exited += () => coreOut++;

                var listener = new Harness.Listener();
                var w = new KitAudioZone { source = new AudioSource(), maxVolume = 0.6f, fadeSeconds = fade, listener = listener, onEntered = "In", onExited = "Out" };
                Harness.Call(w, "Start");

                var ops = new System.Random(fade > 0f ? 51 : 52);
                for (int step = 0; step < Steps; step++)
                {
                    int op = ops.Next(8);
                    if (op == 0) { core.Enter(); w.OnPlayerTriggerEnter(Harness.Me); }
                    else if (op == 1) { core.Exit(); w.OnPlayerTriggerExit(Harness.Me); }
                    else if (op == 2) { core.Leave(); w.OnPlayerRespawn(Harness.Me); }
                    else if (op == 3) { w.OnPlayerTriggerEnter(Harness.Other); } // someone else: ignored
                    float dt = (float)ops.NextDouble() * 0.4f;
                    Time.deltaTime = dt;
                    core.Tick(dt);
                    Harness.Call(w, "Update");

                    string at = " (fade " + fade + ", step " + step + ")";
                    Check.Equal(core.Inside, w.IsInside(), "inside" + at);
                    Check.Near(core.Volume, w.Volume(), "volume" + at);
                    Check.Near(core.Volume, w.source.volume, "source volume" + at);
                    Check.Equal(core.Volume > 0f, w.source.isPlaying, "plays only while audible" + at);
                    Check.Equal(coreIn, Count(listener, "In"), "onEntered" + at);
                    Check.Equal(coreOut, Count(listener, "Out"), "onExited" + at);
                }
            }
        }

        // ---------- quest-checklist ----------

        [KitTest("quest-checklist", TestKind.Parity)]
        public static void QuestAgreesWithItsCore()
        {
            foreach (bool ordered in new[] { false, true })
                foreach (int steps in new[] { 1, 6, 30 })
                {
                    Harness.Reset();
                    const string key = "piranesi.quests";
                    int earlier = ordered ? 1 : (steps > 2 ? 0b100 : 0);
                    var store = new FakeStore();
                    store.Values[key] = earlier;
                    PlayerData.Seed(Harness.Me.playerId, key, earlier);
                    var core = new QuestChecklistCore(steps, ordered, key, store);
                    int coreDone = 0, coreBlocked = 0, coreQuest = 0;
                    core.StepDone += (i, n) => coreDone++;
                    core.Blocked += i => coreBlocked++;
                    core.QuestDone += () => coreQuest++;

                    var listener = new Harness.Listener();
                    var w = new KitQuestChecklist { steps = steps, ordered = ordered, saveKey = key, ticks = Harness.Objects(steps, "tick"), plaques = Harness.Objects(steps + 1, "plaque"), chime = new AudioSource(), listener = listener, onStepDone = "Step", onBlocked = "Blocked", onQuestDone = "Done" };
                    Harness.Call(w, "Start");
                    var links = new KitQuestStep[steps];
                    for (int i = 0; i < steps; i++) { links[i] = new KitQuestStep { quest = w, index = i, completeOnTouch = true }; Harness.Call(links[i], "Start"); }

                    var ops = new System.Random(steps * (ordered ? 3 : 5));
                    bool restored = false;
                    for (int step = 0; step < 300; step++)
                    {
                        int op = ops.Next(20);
                        if (op == 0 && !restored) { core.Restore(); w.OnPlayerRestored(Harness.Me); restored = true; }
                        else if (op == 1) { core.ResetQuest(); w.ResetQuest(); }
                        else
                        {
                            int i = ordered && ops.Next(2) == 0 ? Math.Max(0, core.NextStep) : ops.Next(steps);
                            core.CompleteStep(i);
                            if (ops.Next(2) == 0) links[i].Interact(); else links[i].OnPlayerTriggerEnter(Harness.Me);
                        }

                        string at = " (ordered " + ordered + ", steps " + steps + ", step " + step + ")";
                        Check.Equal(core.DoneMask, w.DoneMask(), "done mask" + at);
                        Check.Equal(core.NextStep, w.NextStep(), "next step" + at);
                        for (int i = 0; i < steps; i++) Check.Equal(core.IsDone(i), w.ticks[i].activeSelf, "tick " + i + at);
                        Check.Equal(core.DoneCount, Harness.ActiveIndex(w.plaques), "plaque" + at);
                        int saved;
                        PlayerData.TryGetInt(Harness.Me, key, out saved);
                        Check.Equal(store.Values[key], saved, "saved the same" + at);
                        Check.Equal(coreDone, Count(listener, "Step"), "onStepDone" + at);
                        Check.Equal(coreBlocked, Count(listener, "Blocked"), "onBlocked" + at);
                        Check.Equal(coreQuest, Count(listener, "Done"), "onQuestDone" + at);
                    }
                }
        }

        // Kits chain through events with no new code: finding every glass animal ticks a quest step.
        [KitTest("quest-checklist", TestKind.Parity)]
        public static void KitsChainThroughEvents()
        {
            var quest = new KitQuestChecklist { steps = 2, ordered = true, saveKey = "q" };
            Harness.Call(quest, "Start");
            var step0 = new KitQuestStep { quest = quest, index = 0, interactable = false };
            Harness.Call(step0, "Start");
            Check.True(step0.DisableInteractive, "a step ticked only by events shows no prompt");
            var set = new KitGlassAnimalSet { count = 2, saveKey = "a", listener = step0, onAllFound = "CompleteStep" };
            Harness.Call(set, "Start");
            set.Find(0);
            Check.Equal(0, quest.DoneCount(), "not every animal yet");
            set.Find(1);
            Check.True(quest.IsDone(0), "the last animal ticked step 0");
            var door = new KitToggle { synced = false, targets = Harness.Objects(1, "door-to-the-last-room") };
            Harness.Call(door, "Start");
            quest.listener = door;
            quest.onQuestDone = "TurnOn";
            quest.CompleteStep(1);
            Check.True(door.targets[0].activeSelf, "finishing the quest opened the last room");
        }
    }
}
