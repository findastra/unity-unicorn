// A Unity Unicorn tool by Astra. Prepared with OpenAI Codex (GPT-6), 2026-10-12.
// Editor-only extension; requires the existing UnicornCore.cs. No automatic scene saves.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityUnicorn
{
    public static class EffectsPreflight20261012
    {
        public static bool HasFeature(GameObject root, string feature)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (!component || component.GetType().FullName != "VF.Model.VRCFury") continue;
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ManagedReference &&
                            (property.managedReferenceFullTypename ?? "").EndsWith(" " + feature, StringComparison.Ordinal))
                            return true;
                        else if (property.propertyType == SerializedPropertyType.ManagedReference &&
                                 (property.managedReferenceFullTypename ?? "").EndsWith("." + feature, StringComparison.Ordinal))
                            return true;
                }
            }
            return false;
        }

        public static List<string> Check(GameObject avatar, Template template)
        {
            var errors = new List<string>();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                errors.Add("Wait for Unity to finish importing or compiling and leave Play Mode before editing.");
            if (!avatar || EditorUtility.IsPersistent(avatar) || !avatar.scene.IsValid() || !UnicornCore.IsAvatarRoot(avatar))
                errors.Add("Choose a scene avatar root with a humanoid Animator and Avatar Descriptor.");
            var animator = avatar ? avatar.GetComponent<Animator>() : null;
            if (!animator || !animator.isHuman) errors.Add("The target needs a valid humanoid Animator.");
            if (template == null || !template.Prefab || template.pose == null || template.pose.links == null || template.pose.links.Count == 0)
            {
                errors.Add("The selected template needs a prefab and learned bone-placement data.");
                return errors;
            }
            if (UnicornCore.MissingReferences(template.Prefab) > 0)
                errors.Add("The template has missing scripts or serialized references. Restore its dependencies first.");
            foreach (var link in template.pose.links)
            {
                if (link.bone < 0 || link.bone >= (int)HumanBodyBones.LastBone)
                { errors.Add("Invalid humanoid bone in template pose."); continue; }
                if (!UnicornCore.Find(template.Prefab.transform, link.path)) errors.Add("Missing template part: " + link.path);
                if (avatar && !UnicornCore.Bone(avatar, link.bone)) errors.Add("Target is missing bone: " + (HumanBodyBones)link.bone);
                if (link.refLength <= 0 || !Finite(link.refLength) || !Finite(link.localPosition) || !Finite(link.lossyScale) ||
                    !Finite(link.localRotation.x) || !Finite(link.localRotation.y) || !Finite(link.localRotation.z) || !Finite(link.localRotation.w))
                    errors.Add("Invalid placement data for: " + link.path);
            }
            // Do not inherit ambiguous repeated placement records from an unverified learned source.
            if (template.pose.links.GroupBy(link => link.path ?? "").Any(group => group.Count() > 1))
                errors.Add("The template repeats a linked part. Review its Armature Links and relearn a verified template first.");
            return errors.Distinct().ToList();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }

    public sealed class BendingAdder20261012 : EditorWindow
    {
        static readonly string[] Categories = { "Blood Bending", "Water Bending", "Sparkle Bending" };
        readonly List<Template> templates = new List<Template>();
        readonly HashSet<string> chosen = new HashSet<string>();
        GameObject avatar;
        Vector2 scroll;
        string result = "";
        bool reviewed;

        [MenuItem("Tools/Unity Unicorn/bending-adder", false, 30)]
        public static void Open()
        {
            var window = GetWindow<BendingAdder20261012>("Bending adder");
            window.avatar = UnicornCore.SelectedAvatar();
            window.minSize = new Vector2(500, 420);
            window.Reload();
        }

        void Reload()
        {
            templates.Clear();
            foreach (var category in Categories) templates.AddRange(UnicornCore.Templates(category));
            chosen.Clear();
            reviewed = false;
        }

        static bool IsUnconfirmedSparkle(Template template) => template.category == "Sparkle Bending" &&
            !Regex.IsMatch(template.name ?? "", @"sparkle[ _-]*bend", RegexOptions.IgnoreCase);

        void OnGUI()
        {
            EditorGUILayout.LabelField("Blood, water and sparkle bending", EditorStyles.boldLabel);
            var nextAvatar = (GameObject)EditorGUILayout.ObjectField("Scene avatar", avatar, typeof(GameObject), true);
            if (nextAvatar != avatar) { avatar = nextAvatar; reviewed = false; }
            EditorGUILayout.HelpBox("Adds reviewed learned templates without replacing existing add-ons. Original prefab, menu, material and controller references are preserved. Placement and runtime behavior still require review.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var category in Categories)
            {
                EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
                var matches = templates.Where(template => template.category == category).ToList();
                if (matches.Count == 0) EditorGUILayout.HelpBox("No learned template is available for this category.", MessageType.Warning);
                foreach (var template in matches)
                {
                    bool blocked = IsUnconfirmedSparkle(template);
                    using (new EditorGUI.DisabledScope(blocked))
                    {
                        bool current = chosen.Contains(template.prefabPath);
                        bool next = EditorGUILayout.ToggleLeft(template.name, current);
                        if (next != current)
                        { if (next) chosen.Add(template.prefabPath); else chosen.Remove(template.prefabPath); reviewed = false; }
                    }
                    if (blocked) EditorGUILayout.HelpBox(template.name + " is a star-effect template, not a verified sparkle-bending source. Use the star diagnostic for it. Supply and verify the intended sparkle-bending prefab before learning it into this category.", MessageType.Warning);
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Reload learned templates")) Reload();
            reviewed = EditorGUILayout.ToggleLeft("I reviewed these templates' effects and placement on their source avatar", reviewed);
            using (new EditorGUI.DisabledScope(!avatar || chosen.Count == 0))
                if (GUILayout.Button("Check selected templates")) result = PreflightText();
            using (new EditorGUI.DisabledScope(!avatar || chosen.Count == 0 || !reviewed))
                if (GUILayout.Button("Add missing selected effects with Undo")) AddSelected();
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        string PreflightText()
        {
            var notes = new List<string>();
            foreach (var template in templates.Where(template => chosen.Contains(template.prefabPath)))
            {
                var errors = EffectsPreflight20261012.Check(avatar, template);
                notes.Add(template.name + ": " + (errors.Count == 0 ? "placement preflight passed" : string.Join("; ", errors)));
                if (avatar && avatar.transform.Find(template.name)) notes.Add("Existing " + template.name + " will be left unchanged.");
            }
            return string.Join("\n", notes);
        }

        void AddSelected()
        {
            var selected = templates.Where(template => chosen.Contains(template.prefabPath)).ToList();
            var errors = selected.SelectMany(template => EffectsPreflight20261012.Check(avatar, template).Select(error => template.name + ": " + error)).ToList();
            if (selected.Any(IsUnconfirmedSparkle)) errors.Add("An unconfirmed sparkle-bending source was selected.");
            if (errors.Count > 0) { result = string.Join("\n", errors); return; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Unity Unicorn add bending templates");
            var notes = new List<string>();
            var created = new List<GameObject>();
            try
            {
                foreach (var template in selected)
                {
                    if (avatar.transform.Find(template.name)) { notes.Add(template.name + ": already present; unchanged"); continue; }
                    var instance = UnicornCore.Strap(avatar, template, notes);
                    if (!instance || UnicornCore.MissingReferences(instance) > 0)
                        throw new InvalidOperationException(template.name + ": creation or reference check failed.");
                    created.Add(instance);
                }
                Undo.CollapseUndoOperations(group);
                if (created.Count > 0) Selection.objects = created.Cast<UnityEngine.Object>().ToArray();
                result = "Added " + created.Count + " effect(s). Scene remains unsaved. Verify placement, menu controls and runtime behavior.\n" + string.Join("\n", notes);
                Debug.Log("[Unity Unicorn bending-adder] " + result, avatar);
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                result = "Rolled back this addition: " + error.Message;
                Debug.LogException(error);
            }
        }
    }

    [Serializable]
    public sealed class StarFinding20261012
    {
        public string id, severity, title, evidence, suggested_fix;
    }

    [Serializable]
    public sealed class StarReport20261012
    {
        public string schema = "unity-unicorn.structural-audit/v1";
        public string tool_id = "star-effects-diagnostic";
        public string completed_at, status = "completed";
        public StarProject20261012 project = new StarProject20261012();
        public List<StarFinding20261012> findings = new List<StarFinding20261012>();
        public string[] limitations = {
            "Read-only source hierarchy audit; no scene, prefab, controller, material or menu was changed.",
            "Unbuilt animation-path checks do not prove the final NDMF/VRCFury build bindings.",
            "A completed audit does not prove firing behavior, rendering, upload success or VRChat runtime behavior."
        };
    }

    [Serializable] public sealed class StarProject20261012 { public string name; }

    public static class StarDiagnostic20261012
    {
        static readonly Regex Names = new Regex(@"star[ _-]?(gun|burst|comet|shooter)|stardust", RegexOptions.IgnoreCase);

        [MenuItem("Tools/Unity Unicorn/star-effects-diagnostic (read only)", false, 31)]
        public static void Open() => StarDiagnosticWindow20261012.ShowForSelection();

        public static StarReport20261012 Inspect(GameObject selected)
        {
            var report = new StarReport20261012 { completed_at = DateTime.UtcNow.ToString("o") };
            report.project.name = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
            if (!selected) { Add(report, "error", "No selection", "Select an avatar or a star-effect prefab.", "Choose the intended object and rerun."); return report; }
            var candidates = selected.GetComponentsInChildren<Transform>(true).Where(t => Names.IsMatch(t.name)).ToList();
            var roots = candidates.Where(t => !candidates.Any(other => other != t && t.IsChildOf(other))).ToList();
            if (roots.Count == 0) { Add(report, "warning", "No star-effect roots found", selected.name, "Select the exact star-effect root or inspect its actual name."); return report; }
            foreach (var root in roots) InspectRoot(root.gameObject, report);
            return report;
        }

        static void Add(StarReport20261012 report, string severity, string title, string evidence, string fix)
        {
            report.findings.Add(new StarFinding20261012 { id = "star-" + (report.findings.Count + 1), severity = severity, title = title, evidence = evidence, suggested_fix = fix });
        }

        static void InspectRoot(GameObject root, StarReport20261012 report)
        {
            var components = root.GetComponentsInChildren<Component>(true).Where(component => component).ToList();
            int missing = UnicornCore.MissingReferences(root);
            Add(report, missing > 0 ? "error" : "note", root.name + ": references", missing + " missing scripts or serialized object references", missing > 0 ? "Restore the exact package dependencies before setup." : "No missing references detected by this audit.");
            var merges = components.Where(component => component.GetType().Name == "ModularAvatarMergeAnimator").ToList();
            var animators = root.GetComponentsInChildren<Animator>(true).Where(animator => animator.runtimeAnimatorController).ToList();
            bool fullController = EffectsPreflight20261012.HasFeature(root, "FullController");
            if (animators.Count > 0 && merges.Count == 0 && !fullController)
                Add(report, "warning", root.name + ": no local controller merge found", "An attached Animator has a controller, but this effect has neither MA Merge Animator nor a VRCFury FullController feature. A merge elsewhere on the avatar is outside this local check.", "Compare the original package prefab and avatar FX setup. Restore the intended merge and parameter routing only after checking for an existing equivalent.");
            foreach (var merge in merges)
            {
                using (var serialized = new SerializedObject(merge))
                {
                    var controller = serialized.FindProperty("animator")?.objectReferenceValue;
                    Add(report, controller ? "note" : "error", root.name + ": MA controller merge", merge.name + " -> " + (controller ? AssetDatabase.GetAssetPath(controller) : "missing controller"), controller ? "Verify the final merged controller in a build or emulator." : "Restore the intended controller reference.");
                }
            }
            foreach (var duplicate in UnicornCore.ArmatureLinks(root).GroupBy(link => link.obj).Where(group => group.Key && group.Count() > 1))
                Add(report, "warning", root.name + ": repeated Armature Link target", duplicate.Key.name + " has " + duplicate.Count() + " link records: " + string.Join(", ", duplicate.Select(link => ((HumanBodyBones)link.bone).ToString())), "Inspect each VRCFury feature and its intent before removing or relearning links.");
            foreach (var animator in animators) InspectController(animator, report);
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            Add(report, "note", root.name + ": particle inventory", particles.Length + " particle systems; " + particles.Count(particle => particle.gameObject.activeInHierarchy) + " currently active in hierarchy", "Inactive firing particles can be intentional. Exercise menu toggles and firing gestures in the emulator.");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (!material || !material.shader || material.shader.name == "Hidden/InternalErrorShader")
                        Add(report, "error", root.name + ": missing material or shader", renderer.name, "Restore the intended material and shader dependency.");
            if (components.Any(component => component.GetType().Name.StartsWith("ModularAvatar", StringComparison.Ordinal)))
                Add(report, "note", root.name + ": Modular Avatar", "Modular Avatar components are present and resolved.", "Their presence does not confirm correct build output or parameter routing.");
        }

        static void InspectController(Animator animator, StarReport20261012 report)
        {
            var runtime = animator.runtimeAnimatorController;
            var controller = runtime as AnimatorController;
            var overrides = runtime as AnimatorOverrideController;
            if (!controller && overrides) controller = overrides.runtimeAnimatorController as AnimatorController;
            Add(report, "note", animator.name + ": attached controller", AssetDatabase.GetAssetPath(runtime), "Check that its parameters reach the avatar's final FX controller.");
            if (controller)
            {
                Add(report, "note", animator.name + ": parameters", string.Join(", ", controller.parameters.Select(parameter => parameter.name + " (" + parameter.type + ")")), "Compare effect toggles with the menu and expression parameters.");
                var conditions = new HashSet<string>();
                foreach (var layer in controller.layers) GatherConditions(layer.stateMachine, conditions);
                Add(report, "note", animator.name + ": firing conditions", string.Join("; ", conditions.OrderBy(value => value)), "Exercise the recorded toggle and gesture conditions in Gesture Manager or AV3 Emulator.");
            }
            var missingPaths = new HashSet<string>();
            foreach (var clip in runtime.animationClips.Distinct())
                foreach (var binding in AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)))
                    if (!string.IsNullOrEmpty(binding.path) && !animator.transform.Find(binding.path)) missingPaths.Add(clip.name + " -> " + binding.path);
            if (missingPaths.Count > 0)
                Add(report, "warning", animator.name + ": unresolved paths relative to attached Animator", string.Join("; ", missingPaths.OrderBy(value => value)), "Confirm the intended merge path mode and build-time remapping before changing paths.");
        }

        static void GatherConditions(AnimatorStateMachine machine, HashSet<string> output)
        {
            if (!machine) return;
            foreach (var transition in machine.anyStateTransitions)
                foreach (var condition in transition.conditions) output.Add(condition.parameter + " " + condition.mode + " " + condition.threshold);
            foreach (var state in machine.states)
                foreach (var transition in state.state.transitions)
                    foreach (var condition in transition.conditions) output.Add(condition.parameter + " " + condition.mode + " " + condition.threshold);
            foreach (var child in machine.stateMachines) GatherConditions(child.stateMachine, output);
        }
    }

    public sealed class StarDiagnosticWindow20261012 : EditorWindow
    {
        GameObject target;
        StarReport20261012 report;
        Vector2 scroll;

        public static void ShowForSelection()
        {
            var window = GetWindow<StarDiagnosticWindow20261012>("Star diagnostics");
            window.target = UnicornCore.SelectedAvatar() ?? Selection.activeGameObject;
            window.minSize = new Vector2(580, 420);
            window.report = null;
        }

        void OnGUI()
        {
            target = (GameObject)EditorGUILayout.ObjectField("Avatar or effect", target, typeof(GameObject), true);
            if (GUILayout.Button("Inspect without changing assets or scene")) report = StarDiagnostic20261012.Inspect(target);
            if (report == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var finding in report.findings)
                EditorGUILayout.HelpBox(finding.title + "\n" + finding.evidence + "\n" + finding.suggested_fix,
                    finding.severity == "error" ? MessageType.Error : finding.severity == "warning" ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Export this private diagnostic report"))
            {
                TimeZoneInfo zone;
                try { zone = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"); }
                catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time"); }
                var date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
                var path = EditorUtility.SaveFilePanel("Save private diagnostic report", Path.GetDirectoryName(Application.dataPath), "star-effects-diagnostic-" + date.ToString("yyyyMMdd-HHmmss") + ".json", "json");
                if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
        }
    }
}
