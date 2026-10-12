// Unity Unicorn strappers: fan-fitter, sparkle-strapper, blood-bender-strapper, water-bender-strapper,
// sparkle-bender-strapper, wing-strapper and neck-bow-strapper. A pet app by Astra.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn
{
    public static class StrapperMenu
    {
        const string M = "Tools/Unity Unicorn/";

        [MenuItem(M + "Learn add-ons from selected avatar", false, 0)]
        static void Learn()
        {
            var avatar = UnicornCore.SelectedAvatar();
            if (!avatar) { EditorUtility.DisplayDialog("Unity Unicorn", "Select the avatar to learn from (the intended source avatar) in the Hierarchy first.", "OK"); return; }
            var learned = UnicornCore.Learn(avatar);
            var notes = learned.Select(t => $"learned {t.category}: {t.name} ({t.pose.links.Count} linked parts)").ToList();
            if (notes.Count == 0) notes.Add("no add-ons with VRCFury Armature Links found under the avatar root");
            UnicornCore.Report("learn-add-ons", "completed", $"Learned from {avatar.name}", notes);
            EditorUtility.DisplayDialog("Unity Unicorn", $"Learned {learned.Count} add-ons from {avatar.name}.\n\n" + string.Join("\n", notes.Take(25)), "OK");
        }

        [MenuItem(M + "fan-fitter", false, 20)] static void Fans() => StrapperWindow.Open("fan-fitter", "Fans", "Fan");
        [MenuItem(M + "sparkle-strapper", false, 21)] static void Sparks() => StrapperWindow.Open("sparkle-strapper", "Sparkles", null);
        [MenuItem(M + "blood-bender-strapper", false, 22)] static void Blood() => StrapperWindow.Open("blood-bender-strapper", "Blood Bending", null);
        [MenuItem(M + "water-bender-strapper", false, 23)] static void Water() => StrapperWindow.Open("water-bender-strapper", "Water Bending", null);
        [MenuItem(M + "sparkle-bender-strapper", false, 24)] static void Stars() => StrapperWindow.Open("sparkle-bender-strapper", "Sparkle Bending", null);
        [MenuItem(M + "wing-strapper", false, 25)] static void Wings() => StrapperWindow.Open("wing-strapper", "Wings", "Wings");
        [MenuItem(M + "neck-bow-strapper", false, 26)] static void Bow() => StrapperWindow.Open("neck-bow-strapper", "Neck Bow", "Bow");
    }

    public class StrapperWindow : EditorWindow
    {
        string toolId, category, variantWord;
        GameObject avatar;
        List<Template> templates = new List<Template>();
        readonly Dictionary<string, bool> pick = new Dictionary<string, bool>();
        bool left = true, right = true, feet = true, sps = true, exclusive = true;
        bool continuousRgb = true;
        GameObject newPrefab;          // a prefab with no template yet (new fan colour, the neck bow, new wings)
        Template setupFrom;            // finished template whose setup gets copied onto newPrefab
        Vector2 scroll;
        string lastResult = "";

        public static void Open(string toolId, string category, string variantWord)
        {
            var w = GetWindow<StrapperWindow>(true, "Unity Unicorn · " + toolId);
            w.toolId = toolId; w.category = category; w.variantWord = variantWord;
            w.avatar = UnicornCore.SelectedAvatar();
            w.Reload();
            w.minSize = new Vector2(420, 420);
        }

        void Reload()
        {
            templates = UnicornCore.Templates(category);
            foreach (var t in templates) if (!pick.ContainsKey(t.name)) pick[t.name] = false;
            setupFrom = setupFrom ?? templates.FirstOrDefault();
        }

        bool IsLeft(Template t) => t.pose.links.All(l => l.bone == (int)HumanBodyBones.LeftHand || l.bone == (int)HumanBodyBones.LeftFoot) || t.name.ToLower().Contains("left");
        bool IsRight(Template t) => t.pose.links.All(l => l.bone == (int)HumanBodyBones.RightHand || l.bone == (int)HumanBodyBones.RightFoot) || t.name.ToLower().Contains("right");

        void OnGUI()
        {
            EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
            avatar = (GameObject)EditorGUILayout.ObjectField("Avatar", avatar, typeof(GameObject), true);
            if (avatar && !UnicornCore.IsAvatarRoot(avatar)) EditorGUILayout.HelpBox("Pick the avatar's root object (the one with the Avatar Descriptor).", MessageType.Warning);

            if (templates.Count == 0)
                EditorGUILayout.HelpBox($"No {category} templates yet. Select an avatar that already has them (your verified source) and run Tools → Unity Unicorn → Learn add-ons from selected avatar.", MessageType.Info);

            if (category != "Wings" && category != "Neck Bow" && category != "Sparkles")
            {
                EditorGUILayout.BeginHorizontal();
                left = EditorGUILayout.ToggleLeft("Left hand", left, GUILayout.Width(120));
                right = EditorGUILayout.ToggleLeft("Right hand", right, GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();
            }
            if (category == "Sparkles")
            {
                feet = EditorGUILayout.ToggleLeft("Include feet with their own on/off toggle", feet);
                continuousRgb = EditorGUILayout.ToggleLeft("Add a Continuous RGB menu option", continuousRgb);
            }
            sps = EditorGUILayout.ToggleLeft("Line up hands and feet with the avatar's SPS sockets when it has them", sps);
            EditorGUILayout.HelpBox("Running on an avatar that already has the add-on updates it in place (feet on/off, SPS line-up) instead of adding a second copy.", MessageType.None);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(140));
            foreach (var t in templates)
            {
                if (!left && IsLeft(t) && !IsRight(t)) continue;
                if (!right && IsRight(t) && !IsLeft(t)) continue;
                pick[t.name] = EditorGUILayout.ToggleLeft($"{t.name}   ({string.Join(", ", t.pose.links.Select(l => ((HumanBodyBones)l.bone).ToString()).Distinct())})", pick[t.name]);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All")) foreach (var k in pick.Keys.ToList()) pick[k] = true;
            if (GUILayout.Button("None")) foreach (var k in pick.Keys.ToList()) pick[k] = false;
            if (GUILayout.Button("Reload templates")) Reload();
            EditorGUILayout.EndHorizontal();

            if (variantWord != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Add a new one", EditorStyles.boldLabel);
                newPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab or model", newPrefab, typeof(GameObject), false);
                var names = templates.Select(t => t.name).ToArray();
                var idx = setupFrom == null ? -1 : templates.IndexOf(setupFrom);
                if (names.Length > 0) idx = EditorGUILayout.Popup("Copy setup from", Mathf.Max(0, idx), names);
                setupFrom = idx >= 0 && idx < templates.Count ? templates[idx] : null;
                EditorGUILayout.HelpBox(category == "Neck Bow" && templates.Count == 0
                    ? "No bow template yet: the bow is placed at the front of the neck and linked to the Neck bone with a toggle. Adjust it once, then Learn so later avatars copy your placement."
                    : "Copies the menu toggle, Armature Link and PhysBones from the chosen finished one, renamed for this prefab.", MessageType.None);
            }

            var ok = avatar && UnicornCore.IsAvatarRoot(avatar);
            if (category == "Fans" && ok)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("All colours", EditorStyles.boldLabel);
                exclusive = EditorGUILayout.ToggleLeft("One fan per hand at a time (exclusive toggles)", exclusive);
                if (GUILayout.Button("Add every colour from the fans folder (both hands)")) Extra(() =>
                { var n = new List<string>(); var made = FanColours.FillAll(avatar, templates, exclusive, n); foreach (var g in made) CopyrightCatcher.CheckAtImplementation(avatar, g, toolId, n); return n; });
                if (GUILayout.Button("Make the RGB fans actually cycle colours")) Extra(() =>
                {
                    var n = new List<string>();
                    foreach (Transform c in avatar.transform)
                        if (c.name.ToLower().Contains("rgb fan")) n.Add(c.name + ": " + RgbPainter.PaintInPlace(c.gameObject).Replace("\n", "; "));
                    if (n.Count == 0) n.Add("no RGB Fan objects on this avatar");
                    return n;
                });
            }
            if (category == "Sparkles" && ok)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Give feet sparkles their own toggle (like the hands)")) Extra(() =>
                {
                    var n = new List<string>();
                    var inst = avatar.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.StartsWith("Phawksea's Sparks (hands)"));
                    if (inst) SparksSplit.Run(inst.gameObject, n); else n.Add("no Phawksea's Sparks on this avatar");
                    return n;
                });
                if (GUILayout.Button("Add continuous RGB while keeping the manual hue control")) Extra(() =>
                {
                    var n = new List<string>();
                    var inst = avatar.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.StartsWith("Phawksea's Sparks (hands)") || x.name.StartsWith("Phawkseas Sparks (hands)"));
                    if (inst) SparksSplit.EnableContinuousRgb(inst.gameObject, n); else n.Add("no Phawksea's Sparks on this avatar");
                    return n;
                });
            }

            EditorGUILayout.Space();
            GUI.enabled = ok;
            if (GUILayout.Button("Strap onto " + (avatar ? avatar.name : "avatar"), GUILayout.Height(32))) Run();
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(lastResult)) EditorGUILayout.HelpBox(lastResult, MessageType.Info);
        }

        void Run()
        {
            var notes = new List<string>();
            var made = new List<GameObject>();
            foreach (var t in templates.Where(t => pick.TryGetValue(t.name, out var on) && on))
            {
                var go = UnicornCore.Strap(avatar, t, notes);
                if (!go) continue;
                made.Add(go);
                if (category == "Sparkles")
                {
                    if (feet) SparksSplit.Run(go, notes);
                    else SparksSplit.SetFeetActive(go, false, notes);
                    if (continuousRgb) SparksSplit.EnableContinuousRgb(go, notes);
                    foreach (var l in t.pose.links)
                    {
                        var part = UnicornCore.Find(go.transform, l.path);
                        if (!part) continue;
                        var socket = UnicornCore.AlignToSps(avatar, part, l.bone);
                        notes.Add(socket ? $"{part.name}: lined up with SPS socket {socket.name}" : $"{part.name}: no SPS socket on {(HumanBodyBones)l.bone}, kept the learned placement");
                    }
                }
                notes.Add($"strapped {t.name}");
            }
            if (newPrefab) { var go = StrapNew(notes); if (go) made.Add(go); }
            foreach (var go in made) CopyrightCatcher.CheckAtImplementation(avatar, go, toolId, notes);

            var clipped = new List<Object>();
            foreach (var go in made)
                foreach (var (r, n) in UnicornCore.Clipping(avatar, go))
                    if (n > 25) { clipped.Add(r.gameObject); notes.Add($"possible clipping: {r.name} has {n} body points inside it"); }
            if (clipped.Count > 0) Selection.objects = clipped.ToArray();
            else if (made.Count > 0) Selection.objects = made.Cast<Object>().ToArray();

            var status = made.Count > 0 ? "completed" : "failed";
            var file = UnicornCore.Report(toolId, status, $"Strapped {made.Count} onto {avatar.name}", notes);
            lastResult = string.Join("\n", notes.Take(20)) + $"\n\nReport: {file}";
            if (clipped.Count > 0) lastResult = "Clipping to fix (selected in the Hierarchy):\n" + lastResult;
        }

        // SPS sockets: hands use "Handjob Left/Right", feet use "Footjob Target Left/Right" (how Astra lined up
        // the reference avatar's hand sparkles). Only the position moves; the part keeps its rotation.
        void LineUpWithSps(GameObject go, Template t, List<string> notes)
        {
            var all = avatar.GetComponentsInChildren<Transform>(true);
            foreach (var l in t.pose.links)
            {
                string socket;
                switch ((HumanBodyBones)l.bone)
                {
                    case HumanBodyBones.LeftHand: socket = "Handjob Left"; break;
                    case HumanBodyBones.RightHand: socket = "Handjob Right"; break;
                    case HumanBodyBones.LeftFoot: socket = "Footjob Target Left"; break;
                    case HumanBodyBones.RightFoot: socket = "Footjob Target Right"; break;
                    default: continue;
                }
                var target = all.FirstOrDefault(x => x.name == socket && !x.IsChildOf(go.transform));
                var part = UnicornCore.Find(go.transform, l.path);
                if (!target || !part) continue;
                Undo.RecordObject(part, "SPS line-up");
                part.position = target.position;
                notes.Add($"{go.name}/{part.name}: lined up with SPS {socket}");
            }
        }

        void Extra(System.Func<List<string>> work)
        {
            List<string> notes;
            try { notes = work(); }
            catch (System.Exception error) { notes = new List<string> { "ERROR: " + error.Message }; Debug.LogException(error); }
            var file = UnicornCore.Report(toolId, notes.Any(n => n.StartsWith("ERROR:") || n.StartsWith("no ") || n.Contains("couldn't")) ? "failed" : "completed", $"{toolId} on {avatar.name}", notes);
            lastResult = string.Join("\n", notes.Take(30)) + $"\n\nReport: {file}";
        }

        GameObject StrapNew(List<string> notes)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, avatar.scene);
            Undo.RegisterCreatedObjectUndo(inst, "Unity Unicorn strap new");
            inst.transform.SetParent(avatar.transform, false);
            if (setupFrom != null && setupFrom.Prefab)
            {
                var template = (GameObject)PrefabUtility.InstantiatePrefab(setupFrom.Prefab);
                try
                {
                    var fromWord = Common(setupFrom.name, newPrefab.name, out var toWord);
                    UnicornCore.CloneSetup(template, inst, fromWord, toWord, notes);
                    inst.name = string.IsNullOrEmpty(fromWord) ? newPrefab.name : setupFrom.name.Replace(fromWord, toWord);
                    var pose = setupFrom.pose;
                    var copy = new TemplatePose { category = category, name = inst.name, links = pose.links.Select(l => new LinkPose { path = "", bone = l.bone, localPosition = l.localPosition, localRotation = l.localRotation, lossyScale = l.lossyScale, refLength = l.refLength }).Take(1).ToList() };
                    UnicornCore.Place(avatar, inst, copy, notes);
                }
                finally { DestroyImmediate(template); }
            }
            else if (category == "Neck Bow")
            {
                PlaceAtNeckFront(inst, notes);
                notes.Add($"{inst.name}: placed at the front of the neck. No finished bow to copy a toggle from: add a VRCFury Toggle (Accessories/Neck Bow) and an Armature Link to Neck, then Learn.");
            }
            else notes.Add($"{newPrefab.name}: placed under the avatar, but there's no finished one to copy a setup from");
            if (category == "Neck Bow") UnicornCore.SetLinkBone(inst, (int)HumanBodyBones.Neck);
            return inst;
        }

        // The word that differs between the two names, so "Silver Red Fan (left)" + "gold blue Neyfti's promise"
        // renames menu paths sensibly. Falls back to the whole prefab name.
        static string Common(string templateName, string newName, out string toWord)
        {
            toWord = newName;
            var colours = new[] { "Silver Red", "Silver Pink", "Silver Yellow", "Silver White", "Silver Blue", "Silver Green", "Silver Orange", "Purple Silver", "RGB", "Plain", "Bandaged", "Chained" };
            var from = colours.FirstOrDefault(c => templateName.ToLower().Contains(c.ToLower()));
            var to = colours.FirstOrDefault(c => newName.ToLower().Contains(c.ToLower()));
            if (from != null && to != null) { toWord = to; return from; }
            if (from != null) { toWord = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(newName.Replace("Neyfti's promise", "").Trim()); return from; }
            return "";
        }

        void PlaceAtNeckFront(GameObject inst, List<string> notes)
        {
            var neck = UnicornCore.Bone(avatar, (int)HumanBodyBones.Neck);
            var head = UnicornCore.Bone(avatar, (int)HumanBodyBones.Head);
            if (!neck || !head) { notes.Add("no Neck/Head bone"); return; }
            var len = Vector3.Distance(neck.position, head.position);
            var forward = avatar.transform.forward;
            inst.transform.position = Vector3.Lerp(neck.position, head.position, 0.35f) + forward * len * 0.55f;
            inst.transform.rotation = Quaternion.LookRotation(forward, avatar.transform.up);
        }
    }
}
