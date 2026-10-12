// Unity Unicorn: independent feet sparks, continuous RGB, and every supplied fan variant.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityUnicorn
{
    public static class EffectAssets
    {
        public const string Root = UnicornCore.Root + "/generated";
        public static string Slug(string text) => Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        public static string NewPath(string folder, string description, string extension)
        {
            UnicornCore.EnsureFolder(folder);
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"); }
            catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time"); }
            var stamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            for (var n = 1; ; n++)
            {
                var path = $"{folder}/{Slug(description)}{(n == 1 ? "" : "-" + n)}-{stamp}.{extension.ToLowerInvariant()}";
                if (!File.Exists(path) && !AssetDatabase.LoadMainAssetAtPath(path)) return path;
            }
        }
        public static T Copy<T>(T source, string folder, string description) where T : Object
        {
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var path = NewPath(folder, description, Path.GetExtension(sourcePath).TrimStart('.'));
            if (!AssetDatabase.CopyAsset(sourcePath, path)) throw new InvalidOperationException("Couldn't copy " + sourcePath);
            var result = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!result) throw new InvalidOperationException("Couldn't load copied asset " + path);
            return result;
        }
        public static void Changed(Component component)
        {
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            if (component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
        public static void Changed(GameObject gameObject)
        {
            EditorUtility.SetDirty(gameObject);
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            if (gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    public static class SparksSplit
    {
        public const string FeetParam = "Sparks Feet On", RgbParam = "Sparks RGB On";
        public static List<Object> ControllerAssets(GameObject go)
        {
            var found = new List<Object>();
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (!comp || comp.GetType().FullName != "VF.Model.VRCFury") continue;
                var it = new SerializedObject(comp).GetIterator();
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue &&
                        (it.objectReferenceValue is AnimatorController || it.objectReferenceValue is ScriptableObject)) found.Add(it.objectReferenceValue);
            }
            return found.Distinct().ToList();
        }
        public static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
        {
            foreach (var state in machine.states) yield return state.state;
            foreach (var child in machine.stateMachines) foreach (var state in States(child.stateMachine)) yield return state;
        }

        // Clone the assets before editing; vendor clips and other avatars keep their original references.
        static List<Object> PrivateAssets(GameObject sparks, List<string> notes)
        {
            var refs = ControllerAssets(sparks);
            var ctrl = refs.OfType<AnimatorController>().FirstOrDefault();
            var menu = refs.OfType<ScriptableObject>().FirstOrDefault(o => o.GetType().Name == "VRCExpressionsMenu");
            var parameters = refs.OfType<ScriptableObject>().FirstOrDefault(o => o.GetType().Name == "VRCExpressionParameters");
            if (!ctrl || !menu || !parameters) throw new InvalidOperationException("Sparks needs a VRCFury Full Controller with controller, menu and parameters.");
            var owner = sparks.transform.root.gameObject;
            var folder = EffectAssets.Root + "/sparks/" + EffectAssets.Slug(owner.name);
            var shared = UnicornCore.SceneAvatars().Where(a => a != owner).Any(a => ControllerAssets(a).Contains(ctrl));
            if (!shared && new Object[] { ctrl, menu, parameters }.All(o => AssetDatabase.GetAssetPath(o).StartsWith(folder + "/", StringComparison.Ordinal))) return refs;
            var replacements = new Dictionary<Object, Object>
            {
                [ctrl] = EffectAssets.Copy(ctrl, folder, "sparks-controller"),
                [menu] = EffectAssets.Copy(menu, folder, "sparks-menu"),
                [parameters] = EffectAssets.Copy(parameters, folder, "sparks-parameters")
            };
            foreach (var component in sparks.GetComponentsInChildren<Component>(true))
            {
                if (!component || (!(component is Animator) && component.GetType().FullName != "VF.Model.VRCFury")) continue;
                Undo.RecordObject(component, "Private sparks assets");
                var so = new SerializedObject(component); var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue && replacements.TryGetValue(it.objectReferenceValue, out var replacement)) it.objectReferenceValue = replacement;
                    else if (it.propertyType == SerializedPropertyType.String)
                        foreach (var pair in replacements)
                        {
                            var oldPath = AssetDatabase.GetAssetPath(pair.Key); var newPath = AssetDatabase.GetAssetPath(pair.Value);
                            if (it.stringValue == AssetDatabase.AssetPathToGUID(oldPath) + "|" + oldPath) it.stringValue = AssetDatabase.AssetPathToGUID(newPath) + "|" + newPath;
                        }
                }
                so.ApplyModifiedProperties(); EffectAssets.Changed(component);
            }
            notes.Add("sparks: private controller, menu and parameters for " + owner.name);
            return ControllerAssets(sparks);
        }

        public static void SetFeetActive(GameObject sparks, bool active, List<string> notes)
        {
            var feet = sparks.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.IndexOf("(feet)", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!feet) throw new InvalidOperationException("No Phawksea feet branch was found.");
            foreach (var t in feet.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(t.gameObject, "Feet sparks available"); t.gameObject.SetActive(active);
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
            EffectAssets.Changed(feet);
            notes.Add("sparks: feet branch and both nested emitters " + (active ? "enabled" : "disabled"));
        }
        public static void Run(GameObject sparks, List<string> notes)
        {
            if (Preflight(ControllerAssets(sparks), FeetParam))
            { PrivateAssets(sparks, notes); SetFeetActive(sparks, true, notes); notes.Add("sparks: complete independent feet toggle already installed"); return; }
            var refs = PrivateAssets(sparks, notes);
            var ctrl = refs.OfType<AnimatorController>().First();
            var states = ctrl.layers.SelectMany(l => States(l.stateMachine)).ToList();
            var original = states.Select(s => s.motion).OfType<AnimationClip>().FirstOrDefault(c => AnimationUtility.GetCurveBindings(c).Any(b => b.path.Contains("(feet)") && b.propertyName.Contains("rateOverDistance")));
            if (!original) throw new InvalidOperationException("Sparks controller has no foot-emission clip to split.");
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ctrl)).Replace('\\', '/');
            var hands = EffectAssets.Copy(original, folder, "sparks-hands-on");
            var on = new AnimationClip { name = "Sparks Feet On" }; var off = new AnimationClip { name = "Sparks Feet Off" };
            foreach (var binding in AnimationUtility.GetCurveBindings(hands).Where(b => b.path.Contains("(feet)")).ToArray())
            {
                var curve = AnimationUtility.GetEditorCurve(hands, binding);
                AnimationUtility.SetEditorCurve(on, binding, AnimationCurve.Constant(0, 1f / 60, curve.Evaluate(original.length)));
                AnimationUtility.SetEditorCurve(off, binding, AnimationCurve.Constant(0, 1f / 60, curve.Evaluate(0)));
                AnimationUtility.SetEditorCurve(hands, binding, null);
            }
            AssetDatabase.CreateAsset(on, EffectAssets.NewPath(folder, "sparks-feet-on", "anim"));
            AssetDatabase.CreateAsset(off, EffectAssets.NewPath(folder, "sparks-feet-off", "anim"));
            foreach (var state in states.Where(s => s.motion == original)) { state.motion = hands; EditorUtility.SetDirty(state); }
            ctrl.AddParameter(FeetParam, AnimatorControllerParameterType.Bool); ctrl.AddLayer(FeetParam);
            var layers = ctrl.layers; layers[layers.Length - 1].defaultWeight = 1; ctrl.layers = layers;
            var machine = ctrl.layers.Last().stateMachine;
            var offState = machine.AddState("Feet Off"); offState.motion = off; offState.writeDefaultValues = false;
            var onState = machine.AddState("Feet On"); onState.motion = on; onState.writeDefaultValues = false;
            machine.defaultState = offState;
            Transition(offState, onState, FeetParam, true); Transition(onState, offState, FeetParam, false);
            AddToggle(refs, FeetParam, "Feet Sparks", true);
            SetFeetActive(sparks, true, notes);
            EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
            notes.Add("sparks: separate Hand Sparks and Feet Sparks toggles with explicit feet emission off/on clips");
        }
        public static void EnableContinuousRgb(GameObject sparks, List<string> notes, float cycleSeconds = 6f)
        {
            if (Preflight(ControllerAssets(sparks), RgbParam)) { PrivateAssets(sparks, notes); notes.Add("sparks: complete continuous RGB option already installed"); return; }
            var refs = PrivateAssets(sparks, notes); var ctrl = refs.OfType<AnimatorController>().First();
            var layer = ctrl.layers.FirstOrDefault(l => States(l.stateMachine).Any(s => s.timeParameterActive && s.timeParameter == "Sparks Hue" && s.motion is AnimationClip));
            if (layer == null) throw new InvalidOperationException("No manual Sparks Hue state was found.");
            var manual = States(layer.stateMachine).First(s => s.timeParameterActive && s.timeParameter == "Sparks Hue" && s.motion is AnimationClip);
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ctrl)).Replace('\\', '/');
            var loop = EffectAssets.Copy((AnimationClip)manual.motion, folder, "sparks-continuous-rgb");
            var settings = AnimationUtility.GetAnimationClipSettings(loop); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(loop, settings);
            ctrl.AddParameter(RgbParam, AnimatorControllerParameterType.Bool);
            var rgb = layer.stateMachine.AddState("Continuous RGB"); rgb.motion = loop;
            rgb.speed = loop.length / Mathf.Max(0.1f, cycleSeconds); rgb.timeParameterActive = false; rgb.writeDefaultValues = manual.writeDefaultValues;
            Transition(manual, rgb, RgbParam, true); Transition(rgb, manual, RgbParam, false);
            AddToggle(refs, RgbParam, "Continuous RGB", false);
            EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
            notes.Add("sparks: Continuous RGB loops the supplied hand/feet hue animation; off restores the Hue Shift control");
        }
        static void Transition(AnimatorState from, AnimatorState to, string parameter, bool enabled)
        {
            var t = from.AddTransition(to); t.hasExitTime = false; t.duration = 0;
            t.AddCondition(enabled ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
        }
        static bool Preflight(List<Object> refs, string parameter)
        {
            var ctrl = refs.OfType<AnimatorController>().FirstOrDefault();
            var menu = refs.OfType<ScriptableObject>().FirstOrDefault(o => o.GetType().Name == "VRCExpressionsMenu");
            var parameters = refs.OfType<ScriptableObject>().FirstOrDefault(o => o.GetType().Name == "VRCExpressionParameters");
            if (!ctrl || !menu || !parameters) throw new InvalidOperationException("Sparks needs its controller, menu and expression parameters before installation.");
            var menuSO = new SerializedObject(menu); var controls = menuSO.FindProperty("controls");
            var parametersSO = new SerializedObject(parameters); var list = parametersSO.FindProperty("parameters");
            if (controls == null || list == null) throw new InvalidOperationException("Unsupported sparks menu or parameter asset format.");
            var controlNames = Enumerable.Range(0, controls.arraySize).Select(i => controls.GetArrayElementAtIndex(i).FindPropertyRelative("parameter.name").stringValue).ToArray();
            var parameterNames = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue).ToArray();
            var animatorParameter = ctrl.parameters.FirstOrDefault(p => p.name == parameter);
            var existing = animatorParameter != null || controlNames.Contains(parameter) || parameterNames.Contains(parameter);
            if (existing)
            {
                var complete = animatorParameter != null && animatorParameter.type == AnimatorControllerParameterType.Bool && controlNames.Count(n => n == parameter) == 1 && parameterNames.Count(n => n == parameter) == 1;
                if (parameter == FeetParam)
                {
                    var layer = ctrl.layers.FirstOrDefault(l => l.name == FeetParam);
                    complete &= layer != null && States(layer.stateMachine).Any(s => s.name == "Feet Off" && s.motion is AnimationClip) && States(layer.stateMachine).Any(s => s.name == "Feet On" && s.motion is AnimationClip);
                }
                else complete &= ctrl.layers.SelectMany(l => States(l.stateMachine)).Any(s => s.name == "Continuous RGB" && s.motion is AnimationClip c && AnimationUtility.GetAnimationClipSettings(c).loopTime && !s.timeParameterActive);
                if (!complete) throw new InvalidOperationException(parameter + " has an incomplete previous installation. Restore its prior controller/menu/parameter set before retrying; nothing was changed.");
                return true;
            }
            if (controls.arraySize >= 8) throw new InvalidOperationException("Sparks menu has no free control slot; nothing was changed.");
            if (!controlNames.Contains("Sparks On") || !parameterNames.Contains("Sparks On")) throw new InvalidOperationException("The Sparks On menu/parameter template is missing; nothing was changed.");
            if (parameter == FeetParam && !ctrl.layers.SelectMany(l => States(l.stateMachine)).Select(s => s.motion).OfType<AnimationClip>().Any(c => AnimationUtility.GetCurveBindings(c).Any(b => b.path.Contains("(feet)") && b.propertyName.Contains("rateOverDistance")))) throw new InvalidOperationException("No foot-emission clip can be split; nothing was changed.");
            if (parameter == RgbParam && !ctrl.layers.SelectMany(l => States(l.stateMachine)).Any(s => s.timeParameterActive && s.timeParameter == "Sparks Hue" && s.motion is AnimationClip)) throw new InvalidOperationException("No manual Sparks Hue state is available; nothing was changed.");
            return false;
        }
        static void AddToggle(List<Object> refs, string parameter, string label, bool renameHands)
        {
            var menu = refs.OfType<ScriptableObject>().First(o => o.GetType().Name == "VRCExpressionsMenu");
            var parameters = refs.OfType<ScriptableObject>().First(o => o.GetType().Name == "VRCExpressionParameters");
            var mso = new SerializedObject(menu); var controls = mso.FindProperty("controls");
            if (controls.arraySize >= 8) throw new InvalidOperationException("Sparks menu has no room for another control.");
            var index = Enumerable.Range(0, controls.arraySize).First(i => controls.GetArrayElementAtIndex(i).FindPropertyRelative("parameter.name").stringValue == "Sparks On");
            if (renameHands) controls.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue = "Hand Sparks";
            controls.InsertArrayElementAtIndex(index);
            var toggle = controls.GetArrayElementAtIndex(index + 1); toggle.FindPropertyRelative("name").stringValue = label;
            toggle.FindPropertyRelative("parameter.name").stringValue = parameter; mso.ApplyModifiedProperties();
            var pso = new SerializedObject(parameters); var list = pso.FindProperty("parameters");
            index = Enumerable.Range(0, list.arraySize).First(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "Sparks On");
            list.InsertArrayElementAtIndex(index); var p = list.GetArrayElementAtIndex(index + 1);
            p.FindPropertyRelative("name").stringValue = parameter; p.FindPropertyRelative("defaultValue").floatValue = 0; pso.ApplyModifiedProperties();
        }
    }

    public static class FanColours
    {
        public static string Colour(string prefabName)
        {
            var name = Regex.Replace(prefabName, "neyfti'?s|promise|\\(\\d+\\)|fan", "", RegexOptions.IgnoreCase);
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Regex.Replace(name, "\\s+", " ").Trim().ToLowerInvariant());
        }
        static IEnumerable<Mesh> Meshes(GameObject prefab) => prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)
            .Concat(prefab.GetComponentsInChildren<MeshFilter>(true).Select(r => r.sharedMesh)).Where(m => m).Distinct();
        public static Dictionary<GameObject, string> StockColours(List<Template> templates)
        {
            var meshes = new HashSet<Mesh>(templates.Where(t => t.Prefab).SelectMany(t => Meshes(t.Prefab)));
            var folders = meshes.Select(m => Path.GetDirectoryName(AssetDatabase.GetAssetPath(m)).Replace('\\', '/'))
                .SelectMany(p => new[] { p, Path.GetDirectoryName(p)?.Replace('\\', '/') })
                .Where(p => !string.IsNullOrEmpty(p) && p != "Assets" && AssetDatabase.IsValidFolder(p)).Distinct().ToArray();
            if (folders.Length == 0) throw new InvalidOperationException("No source mesh folder could be found for the learned fans.");
            var prefabs = AssetDatabase.FindAssets("t:Prefab", folders).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !p.StartsWith(UnicornCore.Root + "/", StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p && Meshes(p).Any(meshes.Contains)).Distinct().OrderBy(p => p.name).ToList();
            var result = new Dictionary<GameObject, string>();
            foreach (var group in prefabs.GroupBy(p => Colour(p.name))) foreach (var prefab in group)
            {
                var variant = Regex.Match(prefab.name, "\\((\\d+)\\)").Groups[1].Value;
                result[prefab] = group.Count() == 1 ? group.Key : group.Key + " (" + (variant.Length > 0 ? variant : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)).Substring(0, 8)) + ")";
            }
            if (result.Count == 0) throw new InvalidOperationException("No stock fan prefabs matched the learned fan mesh.");
            return result;
        }
        public static List<GameObject> FillAll(GameObject avatar, List<Template> templates, bool exclusive, List<string> notes)
        {
            var made = new List<GameObject>();
            var left = templates.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("(left)") && !t.name.ToLowerInvariant().Contains("rgb"));
            var right = templates.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("(right)") && !t.name.ToLowerInvariant().Contains("rgb"));
            if (left == null || right == null) throw new InvalidOperationException("Fans need learned left and right stock fan setups.");
            var stock = StockColours(templates);
            foreach (var pair in stock) foreach (var side in new[] { (name: "left", template: left), (name: "right", template: right) })
            {
                var name = $"{pair.Value} Fan ({side.name})";
                if (avatar.transform.Find(name)) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(pair.Key, avatar.scene);
                Undo.RegisterCreatedObjectUndo(instance, "fan-fitter"); Undo.RecordObject(instance, "Fan name and visibility"); instance.transform.SetParent(avatar.transform, false); instance.name = name;
                var template = (GameObject)PrefabUtility.InstantiatePrefab(side.template.Prefab);
                try { UnicornCore.CloneSetup(template, instance, Regex.Match(side.template.name, "^(.*) Fan", RegexOptions.IgnoreCase).Groups[1].Value, pair.Value, notes); }
                finally { Object.DestroyImmediate(template); }
                var pose = new TemplatePose { category = "Fans", name = name, links = side.template.pose.links.Take(1).Select(l => new LinkPose { path = "", bone = l.bone, localPosition = l.localPosition, localRotation = l.localRotation, lossyScale = l.lossyScale, refLength = l.refLength }).ToList() };
                UnicornCore.Place(avatar, instance, pose, notes); instance.SetActive(false); EffectAssets.Changed(instance); EffectAssets.Changed(instance.transform);
                made.Add(instance); notes.Add("added " + name + " from " + AssetDatabase.GetAssetPath(pair.Key));
            }
            if (exclusive) MakeExclusive(avatar, notes);
            notes.Add($"fans: {stock.Count} supplied stock variants for both hands; added {made.Count} objects"); return made;
        }
        public static void MakeExclusive(GameObject avatar, List<string> notes)
        {
            var count = 0;
            foreach (Transform child in avatar.transform)
            {
                var side = Regex.Match(child.name, " Fan \\((left|right)\\)$", RegexOptions.IgnoreCase); if (!side.Success) continue;
                foreach (var component in child.GetComponents<Component>())
                {
                    if (!component || component.GetType().FullName != "VF.Model.VRCFury") continue;
                    Undo.RecordObject(component, "Exclusive fan toggles"); var so = new SerializedObject(component); var it = so.GetIterator(); var changed = false;
                    while (it.Next(true))
                    {
                        if (it.name == "enableExclusiveTag" && it.propertyType == SerializedPropertyType.Boolean) { it.boolValue = true; changed = true; }
                        if (it.name == "exclusiveTag" && it.propertyType == SerializedPropertyType.String) it.stringValue = side.Groups[1].Value.ToLowerInvariant() == "left" ? "Fans Left" : "Fans Right";
                    }
                    if (changed) { so.ApplyModifiedProperties(); EffectAssets.Changed(component); count++; }
                }
            }
            notes.Add($"fans: {count} exclusive toggles, one fan per hand");
        }
    }
}
