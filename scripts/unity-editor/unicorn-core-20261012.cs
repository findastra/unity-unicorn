// Unity Unicorn: shared helpers for the strapper tools.
// A pet app by Astra. Editor-only; nothing here ends up on an uploaded avatar.
//
// How the strappers work
// 1. "Learn" reads a reference avatar (chosen by the user) and saves every add-on it finds as a template:
//    a prefab copy under Assets/UnityUnicorn/Templates/<category>/ plus a pose file that records, for each
//    VRCFury Armature Link in the add-on, which humanoid bone it links to and where it sits relative to that bone.
// 2. A strapper instantiates a template under another avatar's root and places every linked part at the same
//    pose relative to that avatar's bone, scaled by the ratio of the two avatars' bone lengths. VRCFury's
//    "reparent" link mode keeps that world pose at build time.
// 3. It checks for parts that sit inside the body (clipping), selects them, and writes a dated report that the
//    Unity Unicorn tool library can import.
// VRChat and VRCFury types are reached by name, so this compiles even before those packages import.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityUnicorn
{
    [Serializable]
    public class LinkPose
    {
        public string path;          // path of the linked object inside the template ("" = template root)
        public int bone;             // HumanBodyBones value
        public Vector3 localPosition; // relative to the bone, in the bone's space, divided by refLength
        public Quaternion localRotation;
        public Vector3 lossyScale;   // world scale on the reference avatar, divided by refLength
        public float refLength;      // reference avatar's bone length used for scaling
    }

    [Serializable]
    public class TemplatePose
    {
        public string category;
        public string name;
        public string sourceAvatar;
        public string learnedAt;
        public List<LinkPose> links = new List<LinkPose>();
    }

    public class Template
    {
        public string category, name, prefabPath;
        public TemplatePose pose;
        public GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    public static class UnicornCore
    {
        public const string Root = "Assets/UnityUnicorn";
        public const string TemplateRoot = Root + "/Templates";
        const string VrcFuryType = "VF.Model.VRCFury";
        const string PhysBoneType = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";
        const string DescriptorType = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";

        // Category rules: the first rule whose pattern matches an add-on's name wins.
        public static readonly (string category, Regex pattern)[] Categories =
        {
            ("Sparkle Bending", new Regex("sparkle[ _-]*bend", RegexOptions.IgnoreCase)),
            ("Sparkles", new Regex("phawksea|spark", RegexOptions.IgnoreCase)),
            ("Blood Bending", new Regex("blood", RegexOptions.IgnoreCase)),
            ("Water Bending", new Regex("water|hydro", RegexOptions.IgnoreCase)),
            ("Sparkle Bending", new Regex("star ?burst|star ?gun|star ?comet|stardust|sparkle ?bend", RegexOptions.IgnoreCase)),
            ("Fans", new Regex("\\bfan\\b|neyfti|promise", RegexOptions.IgnoreCase)),
            ("Wings", new Regex("wing", RegexOptions.IgnoreCase)),
            ("Neck Bow", new Regex("\\bbow\\b|ribbon", RegexOptions.IgnoreCase)),
        };

        public static string CategoryFor(string name)
        {
            foreach (var (category, pattern) in Categories)
                if (pattern.IsMatch(name)) return category;
            return null;
        }

        // ---------- avatars and bones ----------

        public static GameObject SelectedAvatar()
        {
            var go = Selection.activeGameObject;
            while (go != null)
            {
                if (IsAvatarRoot(go)) return go;
                go = go.transform.parent ? go.transform.parent.gameObject : null;
            }
            return null;
        }

        public static bool IsAvatarRoot(GameObject go)
        {
            if (go.GetComponents<Component>().Any(c => c && c.GetType().FullName == DescriptorType)) return true;
            var a = go.GetComponent<Animator>();
            return a && a.isHuman && go.transform.parent == null;
        }

        public static IEnumerable<GameObject> SceneAvatars() =>
            Object.FindObjectsOfType<Transform>(true).Where(t => t.parent == null).Select(t => t.gameObject).Where(IsAvatarRoot);

        public static Transform Bone(GameObject avatar, int bone)
        {
            var a = avatar.GetComponent<Animator>();
            return a && a.isHuman ? a.GetBoneTransform((HumanBodyBones)bone) : null;
        }

        // A length that describes how big this part of the body is, for scaling props between avatars.
        public static float BoneLength(GameObject avatar, int bone)
        {
            var b = (HumanBodyBones)bone;
            HumanBodyBones next;
            switch (b)
            {
                case HumanBodyBones.LeftHand: next = HumanBodyBones.LeftMiddleProximal; break;
                case HumanBodyBones.RightHand: next = HumanBodyBones.RightMiddleProximal; break;
                case HumanBodyBones.LeftFoot: next = HumanBodyBones.LeftToes; break;
                case HumanBodyBones.RightFoot: next = HumanBodyBones.RightToes; break;
                case HumanBodyBones.Neck: next = HumanBodyBones.Head; break;
                case HumanBodyBones.Hips: next = HumanBodyBones.Spine; break;
                case HumanBodyBones.Spine: next = HumanBodyBones.Chest; break;
                case HumanBodyBones.Chest: next = HumanBodyBones.Neck; break;
                case HumanBodyBones.UpperChest: next = HumanBodyBones.Neck; break;
                case HumanBodyBones.Head: next = HumanBodyBones.Neck; break;
                default: next = HumanBodyBones.LastBone; break;
            }
            var t = Bone(avatar, bone);
            var n = next == HumanBodyBones.LastBone ? null : Bone(avatar, (int)next);
            if (t && n) return Mathf.Max(0.001f, Vector3.Distance(t.position, n.position));
            // Fallback: overall height so props still scale with the avatar.
            var head = Bone(avatar, (int)HumanBodyBones.Head);
            return head ? Mathf.Max(0.01f, (head.position - avatar.transform.position).magnitude * 0.08f) : 0.1f;
        }

        // ---------- VRCFury links read through serialization ----------

        public struct LinkInfo { public GameObject obj; public int bone; }

        public static List<LinkInfo> ArmatureLinks(GameObject root)
        {
            var result = new List<LinkInfo>();
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (!comp || comp.GetType().FullName != VrcFuryType) continue;
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                var props = new Dictionary<string, Object>();
                var bones = new Dictionary<string, int>();
                var useBone = new Dictionary<string, bool>();
                var isLink = new HashSet<string>();
                while (it.Next(true))
                {
                    var p = it.propertyPath;
                    if (it.propertyType == SerializedPropertyType.ManagedReference &&
                        (it.managedReferenceFullTypename ?? "").EndsWith("ArmatureLink")) isLink.Add(p);
                    if (it.name == "propBone" && it.propertyType == SerializedPropertyType.ObjectReference)
                        props[Parent(p)] = it.objectReferenceValue;
                    if (it.name == "bone" && it.propertyPath.Contains("linkTo") &&
                        (it.propertyType == SerializedPropertyType.Integer || it.propertyType == SerializedPropertyType.Enum))
                    {
                        var owner = p.Substring(0, p.IndexOf(".linkTo", StringComparison.Ordinal));
                        if (!bones.ContainsKey(owner)) bones[owner] = it.propertyType == SerializedPropertyType.Enum ? it.enumValueIndex : it.intValue;
                    }
                    if (it.name == "useBone" && it.propertyPath.Contains("linkTo") && it.propertyType == SerializedPropertyType.Boolean)
                    {
                        var owner = p.Substring(0, p.IndexOf(".linkTo", StringComparison.Ordinal));
                        if (!useBone.ContainsKey(owner)) useBone[owner] = it.boolValue;
                    }
                }
                foreach (var owner in bones.Keys)
                {
                    if (useBone.TryGetValue(owner, out var ub) && !ub) continue;
                    var obj = props.TryGetValue(owner, out var o) && o ? (o as GameObject ?? (o as Component)?.gameObject) : comp.gameObject;
                    result.Add(new LinkInfo { obj = obj ? obj : comp.gameObject, bone = bones[owner] });
                }
            }
            return result;
        }

        static string Parent(string path) { var i = path.LastIndexOf('.'); return i < 0 ? "" : path.Substring(0, i); }

        public static string RelPath(Transform root, Transform t)
        {
            if (t == root) return "";
            var parts = new List<string>();
            while (t && t != root) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        public static Transform Find(Transform root, string path) => string.IsNullOrEmpty(path) ? root : root.Find(path);

        // ---------- learning ----------

        public static List<Template> Learn(GameObject avatar, IEnumerable<string> onlyCategories = null)
        {
            var want = onlyCategories != null ? new HashSet<string>(onlyCategories) : null;
            var learned = new List<Template>();
            foreach (Transform child in avatar.transform)
            {
                var category = CategoryFor(child.name);
                if (category == null || (want != null && !want.Contains(category))) continue;
                var links = ArmatureLinks(child.gameObject);
                if (links.Count == 0) continue;
                var pose = new TemplatePose
                {
                    category = category, name = child.name, sourceAvatar = avatar.name,
                    learnedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                foreach (var l in links)
                {
                    var bone = Bone(avatar, l.bone);
                    if (!bone) continue;
                    var len = BoneLength(avatar, l.bone);
                    pose.links.Add(new LinkPose
                    {
                        path = RelPath(child, l.obj.transform), bone = l.bone, refLength = len,
                        localPosition = bone.InverseTransformPoint(l.obj.transform.position) / len,
                        localRotation = Quaternion.Inverse(bone.rotation) * l.obj.transform.rotation,
                        lossyScale = l.obj.transform.lossyScale / len,
                    });
                }
                if (pose.links.Count == 0) continue;
                learned.Add(SaveTemplate(child.gameObject, pose));
            }
            AssetDatabase.SaveAssets();
            return learned;
        }

        static Template SaveTemplate(GameObject source, TemplatePose pose)
        {
            var dir = $"{TemplateRoot}/{pose.category}";
            EnsureFolder(dir);
            var safe = Regex.Replace(pose.name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (string.IsNullOrEmpty(safe)) safe = "avatar-addon";
            var path = $"{dir}/{safe}-{DenverNow():yyyyMMdd}.prefab";
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            try
            {
                copy.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(copy, path);
            }
            finally { Object.DestroyImmediate(copy); }
            File.WriteAllText(PosePath(path), JsonUtility.ToJson(pose, true));
            AssetDatabase.ImportAsset(PosePath(path));
            return new Template { category = pose.category, name = pose.name, prefabPath = path, pose = pose };
        }

        public static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        // New dated assets use a dated JSON sidecar; existing learned templates retain their old path.
        static string PosePath(string prefabPath)
        {
            var name = Path.GetFileNameWithoutExtension(prefabPath);
            var match = Regex.Match(name, "^(.*)-(\\d{8}(?:-\\d{6})?)$");
            return match.Success
                ? Path.GetDirectoryName(prefabPath).Replace('\\', '/') + "/" + match.Groups[1].Value + "-pose-" + match.Groups[2].Value + ".json"
                : prefabPath.Replace(".prefab", ".pose.json");
        }

        public static DateTime DenverNow()
        {
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"); }
            catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time"); }
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        }

        public static List<Template> Templates(string category)
        {
            var dir = $"{TemplateRoot}/{category}";
            var list = new List<Template>();
            if (!AssetDatabase.IsValidFolder(dir)) return list;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var posePath = PosePath(path);
                if (!File.Exists(posePath)) posePath = path.Replace(".prefab", ".pose.json");
                if (!File.Exists(posePath)) continue;
                var pose = JsonUtility.FromJson<TemplatePose>(File.ReadAllText(posePath));
                list.Add(new Template { category = category, name = pose.name, prefabPath = path, pose = pose });
            }
            return list.GroupBy(t => t.name).Select(group => group.OrderByDescending(t => t.pose.learnedAt, StringComparer.Ordinal).First()).OrderBy(t => t.name).ToList();
        }

        // ---------- strapping ----------

        public static GameObject Strap(GameObject avatar, Template t, List<string> notes)
        {
            var prefab = t.Prefab;
            if (!prefab) { notes.Add($"{t.name}: template prefab is missing"); return null; }
            var existing = avatar.transform.Find(t.name);
            if (existing) { notes.Add($"{t.name}: already on {avatar.name}, updating in place"); return existing.gameObject; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, avatar.scene);
            Undo.RegisterCreatedObjectUndo(inst, "Unity Unicorn strap " + t.name);
            inst.transform.SetParent(avatar.transform, false);
            inst.name = t.name;
            var missing = MissingReferences(inst);
            if (missing > 0) notes.Add($"{t.name}: {missing} missing references (import the add-on's package into this project)");
            Place(avatar, inst, t.pose, notes);
            return inst;
        }

        public static void Place(GameObject avatar, GameObject inst, TemplatePose pose, List<string> notes)
        {
            foreach (var l in pose.links.OrderBy(x => x.path.Count(c => c == '/')))
            {
                var part = Find(inst.transform, l.path);
                var bone = Bone(avatar, l.bone);
                if (!part || !bone) { notes.Add($"{pose.name}: no {(HumanBodyBones)l.bone} or part '{l.path}'"); continue; }
                var len = BoneLength(avatar, l.bone);
                Undo.RecordObject(part, "Unity Unicorn place");
                part.position = bone.TransformPoint(l.localPosition * len);
                part.rotation = bone.rotation * l.localRotation;
                var parentScale = part.parent ? part.parent.lossyScale : Vector3.one;
                var target = l.lossyScale * len;
                part.localScale = new Vector3(Div(target.x, parentScale.x), Div(target.y, parentScale.y), Div(target.z, parentScale.z));
            }
        }

        static float Div(float a, float b) => Mathf.Abs(b) < 1e-6f ? a : a / b;

        public static int MissingReferences(GameObject root)
        {
            var count = 0;
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (!c) { count++; continue; }
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null &&
                        it.objectReferenceInstanceIDValue != 0) count++;
            }
            return count;
        }

        // ---------- SPS alignment ----------

        // Lines a linked part up with the avatar's SPS socket on the same hand or foot (Handjob Left/Right on the
        // hands, Steppies Left/Right on the feet), the way Astra lined up the reference avatar's hand sparkles by hand.
        // Falls back to the nearest socket within 20 cm of the bone. Returns the socket used, or null.
        public static Transform AlignToSps(GameObject avatar, Transform part, int bone)
        {
            var b = (HumanBodyBones)bone;
            var sockets = avatar.GetComponentsInChildren<Component>(true)
                .Where(c => c && c.GetType().Name.EndsWith("HapticSocket")).Select(c => c.transform).Distinct().ToList();
            if (sockets.Count == 0) return null;
            var side = b.ToString().StartsWith("Left") ? "left" : b.ToString().StartsWith("Right") ? "right" : "";
            var limb = b == HumanBodyBones.LeftFoot || b == HumanBodyBones.RightFoot ? "steppies|foot|feet|step" : "handjob|hand";
            var boneT = Bone(avatar, bone);
            var named = sockets.Where(s => Regex.IsMatch(s.name, limb, RegexOptions.IgnoreCase) &&
                                           (side == "" || s.name.ToLower().Contains(side)) &&
                                           !s.name.ToLower().Contains("double") && !s.name.ToLower().Contains("footjob")).ToList();
            Transform socket = null;
            if (boneT)
            {
                socket = named.OrderBy(s => Vector3.Distance(s.position, boneT.position)).FirstOrDefault();
                if (!socket) socket = sockets.Where(s => Vector3.Distance(s.position, boneT.position) < 0.2f)
                                             .OrderBy(s => Vector3.Distance(s.position, boneT.position)).FirstOrDefault();
            }
            else socket = named.FirstOrDefault();
            if (!socket) return null;
            Undo.RecordObject(part, "Unity Unicorn align to SPS");
            part.position = socket.position;
            part.rotation = socket.rotation;
            return socket;
        }

        // ---------- cloning a setup onto a new object ----------

        // Copies VRCFury, PhysBone and other non-mesh components from `template` onto `target` (matching children
        // by relative path, then by name), and points references that went into the template at the matching
        // objects in the target. String fields get `fromWord` replaced by `toWord` (menu names).
        public static void CloneSetup(GameObject template, GameObject target, string fromWord, string toWord, List<string> notes)
        {
            var map = new Dictionary<Object, Object>();
            var pairs = new List<(Transform from, Transform to)>();
            foreach (var t in template.GetComponentsInChildren<Transform>(true))
            {
                var rel = RelPath(template.transform, t);
                var match = Find(target.transform, rel) ?? target.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == t.name);
                if (!match) continue;
                pairs.Add((t, match));
                map[t] = match; map[t.gameObject] = match.gameObject;
            }
            var added = new List<Component>();
            foreach (var (from, to) in pairs)
            {
                foreach (var c in from.GetComponents<Component>())
                {
                    if (!c || c is Transform || c is Renderer || c is MeshFilter || c is Animator) continue;
                    if (to.GetComponents(c.GetType()).Length >= from.GetComponents(c.GetType()).Length) continue;
                    if (!UnityEditorInternal.ComponentUtility.CopyComponent(c)) continue;
                    if (!UnityEditorInternal.ComponentUtility.PasteComponentAsNew(to.gameObject)) continue;
                    var nc = to.GetComponents(c.GetType()).Last();
                    map[c] = nc; added.Add(nc);
                }
            }
            foreach (var nc in added)
            {
                var so = new SerializedObject(nc);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue &&
                        map.TryGetValue(it.objectReferenceValue, out var mapped)) it.objectReferenceValue = mapped;
                    else if (it.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(fromWord) &&
                             it.stringValue.Contains(fromWord)) it.stringValue = it.stringValue.Replace(fromWord, toWord);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            notes.Add($"{target.name}: copied {added.Count} components from {template.name}");
        }

        // Sets the humanoid bone every VRCFury Armature Link on `root` links to.
        public static void SetLinkBone(GameObject root, int bone)
        {
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (!comp || comp.GetType().FullName != VrcFuryType) continue;
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                while (it.Next(true))
                    if (it.name == "bone" && it.propertyPath.Contains("linkTo"))
                    {
                        if (it.propertyType == SerializedPropertyType.Enum) it.enumValueIndex = bone; else it.intValue = bone;
                    }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---------- clipping ----------

        // Counts body vertices that sit inside each prop renderer's box. Fans and props are held, so fingers
        // touching a handle are expected; a large count on a non-held part means it's buried in the body.
        public static List<(Renderer renderer, int inside)> Clipping(GameObject avatar, GameObject prop)
        {
            var bodies = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r.gameObject.activeInHierarchy && r.enabled && r.sharedMesh && !r.transform.IsChildOf(prop.transform) &&
                            Regex.IsMatch(r.name, "^(body|face|head)", RegexOptions.IgnoreCase))
                .ToList();
            var verts = new List<Vector3>();
            foreach (var r in bodies)
            {
                var baked = new Mesh();
                r.BakeMesh(baked, true);
                var m = r.transform.localToWorldMatrix;
                verts.AddRange(baked.vertices.Select(v => m.MultiplyPoint3x4(v)));
                Object.DestroyImmediate(baked);
            }
            var result = new List<(Renderer, int)>();
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Bounds local; Matrix4x4 toLocal;
                if (r is SkinnedMeshRenderer s && s.sharedMesh) { local = s.sharedMesh.bounds; toLocal = s.transform.worldToLocalMatrix; }
                else if (r.GetComponent<MeshFilter>() is MeshFilter mf && mf.sharedMesh) { local = mf.sharedMesh.bounds; toLocal = r.transform.worldToLocalMatrix; }
                else continue;
                local.extents *= 0.85f; // ignore grazing contact at the edges
                var n = verts.Count(v => local.Contains(toLocal.MultiplyPoint3x4(v)));
                result.Add((r, n));
            }
            return result;
        }

        // ---------- PhysBones (reached by name) ----------

        public static Type PhysBone => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(PhysBoneType, false)).FirstOrDefault(t => t != null);

        public static void SetField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field);
            if (f == null) return;
            if (f.FieldType.IsEnum && value is int i) value = Enum.ToObject(f.FieldType, i);
            else if (f.FieldType == typeof(float) && value is double d) value = (float)d;
            f.SetValue(target, value);
        }

        public static object GetField(object target, string field) => target.GetType().GetField(field)?.GetValue(target);

        // ---------- reports for the tool library ----------

        [Serializable] class Finding { public string severity; public string message; }
        [Serializable] class Project { public string name; }
        [Serializable]
        class Usage
        {
            public string schema = "unity-unicorn.tool-usage/v1";
            public string tool_id;
            public Project project = new Project();
            public string completed_at;
            public string status;
            public string evidence;
            public List<Finding> findings = new List<Finding>();
        }

        public static string Report(string toolId, string status, string evidence, IEnumerable<string> notes)
        {
            var u = new Usage
            {
                tool_id = toolId, status = status, evidence = evidence,
                completed_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                findings = notes.Select(n => new Finding { severity = n.Contains("clip") || n.Contains("missing") ? "warning" : "info", message = n }).ToList()
            };
            u.project.name = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "UnityUnicornReports");
            Directory.CreateDirectory(dir);
            var denver = DenverNow();
            var safeTool = Regex.Replace(toolId.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            var file = Path.Combine(dir, $"{safeTool}-{denver:yyyyMMdd-HHmmss}.json");
            for (int suffix = 2; File.Exists(file); suffix++) file = Path.Combine(dir, $"{safeTool}-{suffix}-{denver:yyyyMMdd-HHmmss}.json");
            File.WriteAllText(file, JsonUtility.ToJson(u, true));
            Debug.Log($"[Unity Unicorn] {toolId}: {status}. Report: {file}");
            return file;
        }
    }
}
