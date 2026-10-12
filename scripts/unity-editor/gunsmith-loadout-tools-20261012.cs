// A Unity Unicorn companion by Astra. Prepared with OpenAI Codex (GPT-6), 2026-10-12.
// Staged Editor-only source. Requires Unity's com.unity.nuget.newtonsoft-json package.
// Imports data only. Never saves scenes, downloads assets, or configures creator packages.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityUnicorn.Gunsmith
{
    // Kept independent of Unity calls so malformed/untrusted manifests can be tested separately.
    public static class LoadoutData20261012
    {
        public const int MaxBytes = 32 * 1024 * 1024;
        public const int MaxAssets = 500;
        static readonly Regex IdPattern = new Regex(@"^[A-Za-z0-9_-]{1,100}$", RegexOptions.CultureInvariant);

        public sealed class Manifest
        {
            public string Avatar;
            public List<Asset> Assets = new List<Asset>();
        }
        public sealed class Asset
        {
            public string Id, Title, Creator, SourceUrl, VideoUrl, SourceInfo, Notes;
        }

        public static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        public static JObject ReadObject(string json, int limit = MaxBytes)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > limit) throw new FormatException("JSON exceeds the allowed size.");
            new StrictJson(json).Validate();
            // Reject comments, constructors, undefined values and multiple documents before parsing the object.
            using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 20, DateParseHandling = DateParseHandling.None })
                while (reader.Read())
                    if (reader.TokenType == JsonToken.Comment || reader.TokenType == JsonToken.StartConstructor || reader.TokenType == JsonToken.Undefined)
                        throw new FormatException("Only JSON data is accepted.");
            using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 20, DateParseHandling = DateParseHandling.None })
            {
                var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("Unexpected data after the JSON object.");
                return value;
            }
        }

        // Json.NET intentionally accepts JavaScript extensions (single quotes, trailing commas).
        // Validate the JSON grammar first so imports never silently accept those extensions.
        sealed class StrictJson
        {
            readonly string text;
            int index;
            public StrictJson(string value) { text = value; }
            void Bad() { throw new FormatException("Malformed JSON near character " + index + "."); }
            void Space() { while (index < text.Length && (text[index] == ' ' || text[index] == '\t' || text[index] == '\r' || text[index] == '\n')) index++; }
            bool Take(char value) { Space(); if (index < text.Length && text[index] == value) { index++; return true; } return false; }
            void Need(char value) { if (!Take(value)) Bad(); }
            public void Validate() { Value(0); Space(); if (index != text.Length) Bad(); }
            void String()
            {
                Need('"');
                while (index < text.Length)
                {
                    char c = text[index++];
                    if (c == '"') return;
                    if (c < 32) Bad();
                    if (c != '\\') continue;
                    if (index >= text.Length) Bad();
                    char escape = text[index++];
                    if (escape == 'u')
                    {
                        for (int n = 0; n < 4; n++)
                            if (index >= text.Length || !Uri.IsHexDigit(text[index++])) Bad();
                    }
                    else if ("\"\\/bfnrt".IndexOf(escape) < 0) Bad();
                }
                Bad();
            }
            bool Digit() => index < text.Length && text[index] >= '0' && text[index] <= '9';
            void Value(int depth)
            {
                if (depth > 20) Bad();
                Space(); if (index >= text.Length) { Bad(); return; }
                char c = text[index];
                if (c == '"') { String(); return; }
                if (c == '{')
                {
                    index++; if (Take('}')) return;
                    do { String(); Need(':'); Value(depth + 1); } while (Take(','));
                    Need('}'); return;
                }
                if (c == '[')
                {
                    index++; if (Take(']')) return;
                    do { Value(depth + 1); } while (Take(','));
                    Need(']'); return;
                }
                foreach (string literal in new[] { "true", "false", "null" })
                    if (text.Length - index >= literal.Length && string.CompareOrdinal(text, index, literal, 0, literal.Length) == 0) { index += literal.Length; return; }
                if (c == '-') index++;
                if (!Digit()) { Bad(); return; }
                if (text[index] == '0') index++; else while (Digit()) index++;
                if (index < text.Length && text[index] == '.') { index++; if (!Digit()) Bad(); while (Digit()) index++; }
                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    index++; if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                    if (!Digit()) Bad(); while (Digit()) index++;
                }
            }
        }

        static string Text(JObject item, string name, int length, bool required = false)
        {
            var value = item[name];
            if (value == null && !required) return "";
            if (value == null || value.Type != JTokenType.String) throw new FormatException(name + " must be text.");
            string text = (string)value;
            if (text.Length > length || (required && string.IsNullOrWhiteSpace(text))) throw new FormatException(name + " is empty or too long.");
            return text;
        }

        static void Fields(JObject item, params string[] allowed)
        {
            if (item.Properties().Any(p => !allowed.Contains(p.Name))) throw new FormatException("Unexpected JSON field.");
        }

        static long Integer(JToken value, long maximum, string field)
        {
            if (value == null || value.Type != JTokenType.Integer || !long.TryParse(value.ToString(), out long number) || number < 0 || number > maximum)
                throw new FormatException(field + " must be an allowed nonnegative integer.");
            return number;
        }

        static void Url(string value)
        {
            if (value.Length == 0) return;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo))
                throw new FormatException("Links must use HTTP or HTTPS without embedded credentials.");
        }

        public static Manifest Parse(string json)
        {
            var root = ReadObject(json);
            Fields(root, "app", "version", "mode", "avatar", "exportedAt", "assets", "resourceTotals");
            if (Text(root, "app", 80, true) != "dubstep-gunsmith-loadout" || Integer(root["version"], 1, "version") != 1 || Text(root, "mode", 20, true) != "planned")
                throw new FormatException("Choose a Dubstep Gunsmith planned loadout manifest, version 1.");
            string avatar = Text(root, "avatar", 100, true);
            string exported = Text(root, "exportedAt", 100, true);
            if (!DateTimeOffset.TryParse(exported, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)) throw new FormatException("Invalid export date.");
            var entries = root["assets"] as JArray;
            if (entries == null || entries.Count > MaxAssets) throw new FormatException("assets must contain at most 500 entries.");
            var manifest = new Manifest { Avatar = avatar };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in entries)
            {
                var item = token as JObject ?? throw new FormatException("Each asset must be an object.");
                Fields(item, "id", "title", "creator", "sourceUrl", "videoUrl", "sourceInfo", "status", "tags", "notes", "costs");
                var asset = new Asset {
                    Id = Text(item, "id", 100, true), Title = Text(item, "title", 120, true), Creator = Text(item, "creator", 100),
                    SourceUrl = Text(item, "sourceUrl", 2000), VideoUrl = Text(item, "videoUrl", 2000), SourceInfo = Text(item, "sourceInfo", 2000), Notes = Text(item, "notes", 4000)
                };
                if (!IdPattern.IsMatch(asset.Id) || !ids.Add(asset.Id)) throw new FormatException("Asset IDs must be valid and unique.");
                Url(asset.SourceUrl); Url(asset.VideoUrl);
                Text(item, "status", 40);
                if (item["tags"] != null)
                {
                    var tags = item["tags"] as JArray;
                    if (tags == null || tags.Count > 12 || tags.Any(t => t.Type != JTokenType.String || ((string)t).Length > 40)) throw new FormatException("Invalid tags.");
                }
                if (item["costs"] != null)
                {
                    var costs = item["costs"] as JObject ?? throw new FormatException("Invalid costs.");
                    Fields(costs, "drawCalls", "materials", "particles");
                    foreach (var cost in costs.Properties()) if (cost.Value.Type != JTokenType.Null) Integer(cost.Value, 99999999, cost.Name);
                }
                manifest.Assets.Add(asset);
            }
            if (root["resourceTotals"] != null)
            {
                var totals = root["resourceTotals"] as JObject ?? throw new FormatException("Invalid resource totals.");
                Fields(totals, "drawCalls", "materials", "particles");
                foreach (var field in totals.Properties())
                {
                    var total = field.Value as JObject ?? throw new FormatException("Invalid total.");
                    Fields(total, "knownTotal", "knownAssets", "unknownAssets");
                    Integer(total["knownTotal"], 49999999500L, "knownTotal");
                    var known = Integer(total["knownAssets"], MaxAssets, "knownAssets");
                    var unknown = Integer(total["unknownAssets"], MaxAssets, "unknownAssets");
                    if (known + unknown != entries.Count) throw new FormatException("Resource counts do not match the assets.");
                }
            }
            return manifest;
        }

        public static List<string> RemovedIds(IEnumerable<string> desired, IEnumerable<string> managed)
        {
            var wanted = new HashSet<string>(desired, StringComparer.Ordinal);
            return managed.Where(id => !wanted.Contains(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }
    }

    [Serializable] public sealed class GunsmithOwnedRecord20261012
    {
        public string recordId, assetId, targetId, instanceId, prefabGuid, parentId, sceneGuid;
        // A tombstone is history, not a permission flag. Undo can restore the exact recorded ID.
        public bool tombstone;
    }
    [Serializable] public sealed class GunsmithRegistry20261012
    {
        public int version = 1;
        public string project;
        public List<GunsmithOwnedRecord20261012> records = new List<GunsmithOwnedRecord20261012>();
    }

    public sealed class GunsmithLoadoutWindow20261012 : EditorWindow
    {
        [Serializable] sealed class Mapping
        {
            public string assetId;
            public GameObject prefab;
            public Transform parent;
        }
        sealed class Operation
        {
            public string kind, assetId, description;
            public GameObject instance, prefab;
            public Transform parent;
            public GunsmithOwnedRecord20261012 record;
        }
        sealed class Plan
        {
            public string fingerprint, registryJson, targetId;
            public GunsmithRegistry20261012 registry;
            public List<Operation> operations = new List<Operation>();
            public List<string> errors = new List<string>();
        }

        [SerializeField] GameObject avatar;
        [SerializeField] string manifestJson, manifestPath;
        [SerializeField] List<Mapping> mappings = new List<Mapping>();
        LoadoutData20261012.Manifest manifest;
        Plan preview;
        Vector2 scroll;
        string notice;
        bool reviewedCreatorSetup;
        static string Project => Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        static string RegistryKey => "UnityUnicorn.Gunsmith.Loadout.v1." + LoadoutData20261012.Hash(Project);

        [MenuItem("Tools/Unity Unicorn/gunsmith-loadouts")]
        static void Open() => GetWindow<GunsmithLoadoutWindow20261012>("Gunsmith loadouts");

        void OnEnable()
        {
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            if (!string.IsNullOrEmpty(manifestJson))
                try { manifest = LoadoutData20261012.Parse(manifestJson); } catch (Exception e) { notice = e.Message; }
        }
        void OnDisable()
        {
            Undo.undoRedoPerformed -= Invalidate;
            EditorApplication.hierarchyChanged -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
        }
        void Invalidate() { preview = null; Repaint(); }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Dubstep Gunsmith · planned loadouts", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose the exact scene avatar and map each catalog ID to a local creator prefab. The manifest avatar name is only a label. This tool attaches prefab instances; creator installation steps, animator/menu setup, dependencies and VRChat testing remain manual.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var chosen = (GameObject)EditorGUILayout.ObjectField("Exact scene avatar", avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) { avatar = chosen; mappings.Clear(); reviewedCreatorSetup = false; Invalidate(); }
            if (GUILayout.Button("Import planned loadout JSON…")) Import();
            if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, MessageType.Info);
            if (manifest == null) return;
            EditorGUILayout.LabelField("Manifest", manifest.Avatar + " · " + manifest.Assets.Count + " assets");
            EditorGUILayout.LabelField("Source file", Path.GetFileName(manifestPath ?? ""));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var asset in manifest.Assets)
            {
                var map = mappings.FirstOrDefault(m => m.assetId == asset.Id);
                if (map == null) { map = new Mapping { assetId = asset.Id }; mappings.Add(map); }
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(asset.Title, EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel("Catalog ID: " + asset.Id, GUILayout.Height(18));
                if (!string.IsNullOrWhiteSpace(asset.Creator)) EditorGUILayout.LabelField("Creator: " + asset.Creator);
                if (!string.IsNullOrWhiteSpace(asset.SourceInfo)) EditorGUILayout.HelpBox(asset.SourceInfo, MessageType.None);
                if (!string.IsNullOrWhiteSpace(asset.SourceUrl)) EditorGUILayout.SelectableLabel(asset.SourceUrl, GUILayout.Height(18));
                EditorGUI.BeginChangeCheck();
                map.prefab = (GameObject)EditorGUILayout.ObjectField("Local prefab asset", map.prefab, typeof(GameObject), false);
                map.parent = (Transform)EditorGUILayout.ObjectField("Parent within avatar", map.parent, typeof(Transform), true);
                if (EditorGUI.EndChangeCheck()) Invalidate();
                EditorGUILayout.EndVertical();
            }
            if (manifest.Assets.Count == 0) EditorGUILayout.HelpBox("This empty plan removes only verified Gunsmith-managed instances under the selected avatar. The catalog and other avatar objects remain separate.", MessageType.Warning);
            reviewedCreatorSetup = EditorGUILayout.ToggleLeft("I reviewed creator setup instructions and selected the intended scene avatar.", reviewedCreatorSetup);
            if (GUILayout.Button("Preview exact additions and removals"))
            {
                try { preview = BuildPlan(); notice = null; }
                catch (Exception e) { preview = null; notice = "Preview blocked: " + e.Message; }
            }
            if (preview != null)
            {
                foreach (var error in preview.errors) EditorGUILayout.HelpBox(error, MessageType.Error);
                foreach (var op in preview.operations) EditorGUILayout.HelpBox(op.description, MessageType.None);
                if (preview.operations.Count == 0 && preview.errors.Count == 0) EditorGUILayout.LabelField("No managed instance changes.");
                using (new EditorGUI.DisabledScope(!reviewedCreatorSetup || preview.errors.Count != 0 || !preview.operations.Any(o => o.kind != "KEEP")))
                    if (GUILayout.Button("Apply this preview with Undo")) Apply();
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Ownership is stored in this project's local EditorPrefs using persistent object IDs. Tombstones are retained for Undo. No existing object is adopted by its name. Missing/corrupt ownership records block removal; registry loss makes old instances unmanaged. Save the scene manually after reviewing. Clearing EditorPrefs or moving the project does not transfer ownership records.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void Import()
        {
            string path = EditorUtility.OpenFilePanel("Import planned loadout manifest", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > LoadoutData20261012.MaxBytes) throw new FormatException("Manifest exceeds 32 MiB.");
                string text = File.ReadAllText(path, new UTF8Encoding(false, true));
                var parsed = LoadoutData20261012.Parse(text);
                manifest = parsed; manifestJson = text; manifestPath = path;
                mappings.RemoveAll(m => !parsed.Assets.Any(a => a.Id == m.assetId));
                reviewedCreatorSetup = false; notice = "Manifest read. No scene changes made."; Invalidate();
            }
            catch (Exception e) { notice = "Import rejected; previous plan retained. " + e.Message; }
        }

        static string ObjectId(Object value)
        {
            if (!value) throw new InvalidOperationException("A required scene object is missing.");
            var id = GlobalObjectId.GetGlobalObjectIdSlow(value);
            if (id.identifierType != 2 || id.assetGUID.ToString() == new string('0', 32) || GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) != value)
                throw new InvalidOperationException("An object has no round-trip persistent scene ID. Save this scene manually first.");
            return id.ToString();
        }
        static Object Resolve(string value)
        {
            if (!GlobalObjectId.TryParse(value, out var id)) throw new FormatException("Invalid ownership object ID.");
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
        }
        static bool Under(Transform item, Transform root) => item && root && (item == root || item.IsChildOf(root));
        static string PathOf(Transform item, Transform root)
        {
            var parts = new List<string>();
            for (var cursor = item; cursor; cursor = cursor.parent) { parts.Add(cursor.name); if (cursor == root) break; }
            parts.Reverse(); return string.Join(" / ", parts);
        }

        static GunsmithRegistry20261012 ReadRegistry(out string raw)
        {
            raw = EditorPrefs.GetString(RegistryKey, "");
            if (raw.Length == 0) return new GunsmithRegistry20261012 { project = Project };
            var json = LoadoutData20261012.ReadObject(raw, 16 * 1024 * 1024);
            if (json.Properties().Any(p => p.Name != "version" && p.Name != "project" && p.Name != "records")) throw new FormatException("Unrecognized ownership registry.");
            if (json["version"]?.Type != JTokenType.Integer || json["project"]?.Type != JTokenType.String || !(json["records"] is JArray rows))
                throw new FormatException("Incomplete ownership registry; no records were reset.");
            foreach (var token in rows)
            {
                var row = token as JObject;
                if (row == null || row["tombstone"]?.Type != JTokenType.Boolean) throw new FormatException("Invalid ownership record.");
                foreach (string field in new[] { "recordId", "assetId", "targetId", "instanceId", "prefabGuid", "parentId", "sceneGuid" })
                    if (row[field]?.Type != JTokenType.String) throw new FormatException("Incomplete ownership record.");
            }
            var registry = json.ToObject<GunsmithRegistry20261012>(JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error }));
            if (registry == null || registry.version != 1 || registry.project != Project || registry.records == null || registry.records.Count > 20000)
                throw new FormatException("Ownership registry is invalid; no records were reset.");
            var recordIds = new HashSet<string>(); var instanceIds = new HashSet<string>();
            foreach (var row in registry.records)
            {
                if (row == null || !Guid.TryParseExact(row.recordId, "N", out _) || !recordIds.Add(row.recordId) || !Regex.IsMatch(row.assetId ?? "", @"^[A-Za-z0-9_-]{1,100}$") ||
                    !Regex.IsMatch(row.prefabGuid ?? "", @"^[a-f0-9]{32}$") || !Regex.IsMatch(row.sceneGuid ?? "", @"^[a-f0-9]{32}$") || !instanceIds.Add(row.instanceId ?? ""))
                    throw new FormatException("Ownership registry contains invalid or duplicate records.");
                foreach (var id in new[] { row.targetId, row.instanceId, row.parentId })
                    if (!GlobalObjectId.TryParse(id, out var parsed) || parsed.identifierType != 2 || parsed.assetGUID.ToString() != row.sceneGuid)
                        throw new FormatException("Ownership IDs do not identify the recorded scene.");
            }
            return registry;
        }
        static void SaveRegistry(GunsmithRegistry20261012 registry)
        {
            string text = JsonConvert.SerializeObject(registry, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });
            if (Encoding.UTF8.GetByteCount(text) > 16 * 1024 * 1024 || registry.records.Count > 20000) throw new InvalidOperationException("Ownership registry limit reached. Nothing can be pruned automatically.");
            EditorPrefs.SetString(RegistryKey, text);
            if (EditorPrefs.GetString(RegistryKey, "") != text) throw new IOException("Ownership registry could not be verified after writing.");
        }

        string PrefabPath(GameObject prefab)
        {
            if (!prefab || !EditorUtility.IsPersistent(prefab)) throw new InvalidOperationException("Choose a local prefab asset for every desired catalog ID.");
            string path = AssetDatabase.GetAssetPath(prefab);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || AssetDatabase.LoadAssetAtPath<GameObject>(path) != prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab))
                throw new InvalidOperationException("Select the root of a .prefab asset, not a model or nested child.");
            if (prefab.GetComponentsInChildren<Component>(true).Any(c => !c)) throw new InvalidOperationException("The selected prefab has missing scripts; install its creator dependencies first.");
            return path;
        }

        void VerifyOwned(GunsmithOwnedRecord20261012 row, GameObject instance, bool removing)
        {
            if (!instance || instance == avatar || EditorUtility.IsPersistent(instance) || instance.scene != avatar.scene || !Under(instance.transform, avatar.transform) || ObjectId(instance) != row.instanceId)
                throw new InvalidOperationException("Managed instance moved outside its recorded avatar or has the wrong identity.");
            if (ObjectId(instance.transform.parent) != row.parentId) throw new InvalidOperationException("Managed instance was reparented. Automatic removal or replacement is blocked.");
            string path = AssetDatabase.GUIDToAssetPath(row.prefabGuid);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!source || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || PrefabUtility.GetNearestPrefabInstanceRoot(instance) != instance ||
                PrefabUtility.GetPrefabInstanceStatus(instance) != PrefabInstanceStatus.Connected || PrefabUtility.GetCorrespondingObjectFromSource(instance) != source)
                throw new InvalidOperationException("Managed instance prefab source no longer matches its ownership record.");
            if (!removing) return;
            // Inspect the entire subtree: added-game-object APIs can stop at the root of an added nested prefab.
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.GetCorrespondingObjectFromSourceAtPath(transform.gameObject, path)) throw new InvalidOperationException("A managed instance contains added objects. Removal is blocked to preserve them.");
                foreach (var component in transform.GetComponents<Component>())
                    if (!component || !PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component, path)) throw new InvalidOperationException("A managed instance contains added or missing components. Removal is blocked.");
            }
            if (PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false)) throw new InvalidOperationException("A managed instance has nondefault prefab overrides. Review those edits manually before removal.");
        }

        static string Estimate(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
            long slots = renderers.Sum(r => (long)r.sharedMaterials.Length);
            long maximum = particles.Sum(p => (long)p.main.maxParticles);
            return "Prefab estimate (including inactive): " + renderers.Length + " renderers, " + slots + " material slots, " + particles.Length + " particle systems, summed maxParticles " + maximum + ". Not draw calls, actual particle count or a VRChat rating.";
        }

        Plan BuildPlan()
        {
            var plan = new Plan();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Exit Play Mode and wait for imports/compilation.");
            if (manifest == null || !avatar || EditorUtility.IsPersistent(avatar) || !avatar.scene.IsValid() || !avatar.scene.isLoaded || string.IsNullOrEmpty(avatar.scene.path) ||
                EditorSceneManager.IsPreviewScene(avatar.scene) || PrefabStageUtility.GetPrefabStage(avatar) != null)
                throw new InvalidOperationException("Choose an object in an already-saved, loaded scene, outside Prefab Mode.");
            // Reparse the retained data instead of trusting only the display model.
            manifest = LoadoutData20261012.Parse(manifestJson);
            plan.targetId = ObjectId(avatar);
            plan.registry = ReadRegistry(out plan.registryJson);
            var allManagedObjects = plan.registry.records.Select(r => Resolve(r.instanceId)).OfType<GameObject>().Where(o => o).ToList();
            if (allManagedObjects.Any(o => Under(avatar.transform, o.transform))) throw new InvalidOperationException("The selected avatar is inside a managed gun instance. Choose the intended avatar root.");
            var records = plan.registry.records.Where(r => r.targetId == plan.targetId).ToList();
            var live = new Dictionary<string, GunsmithOwnedRecord20261012>(StringComparer.Ordinal);
            var liveObjects = new List<GameObject>();
            var signature = new StringBuilder(LoadoutData20261012.Hash(manifestJson) + "|" + plan.targetId + "|" + LoadoutData20261012.Hash(plan.registryJson));
            foreach (var record in records)
            {
                var resolved = Resolve(record.instanceId);
                if (!resolved) continue; // Tombstones are never discarded, even when an Undo could later restore them.
                if (!(resolved is GameObject instance)) { plan.errors.Add("Recorded instance now resolves to a different object type."); continue; }
                try
                {
                    VerifyOwned(record, instance, false);
                    if (live.ContainsKey(record.assetId)) throw new InvalidOperationException("Multiple managed instances exist for catalog ID " + record.assetId + ". Resolve the ambiguity manually.");
                    live.Add(record.assetId, record); liveObjects.Add(instance);
                }
                catch (Exception e) { plan.errors.Add(record.assetId + ": " + e.Message); }
            }
            foreach (var asset in manifest.Assets)
            {
                try
                {
                    var matching = mappings.Where(m => m.assetId == asset.Id).ToList();
                    if (matching.Count != 1) throw new InvalidOperationException("Choose exactly one explicit prefab/parent mapping.");
                    var map = matching[0];
                    string path = PrefabPath(map.prefab);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (!map.parent || map.parent.gameObject.scene != avatar.scene || !Under(map.parent, avatar.transform) || allManagedObjects.Any(o => Under(map.parent, o.transform)))
                        throw new InvalidOperationException("Choose a parent inside the selected avatar and outside managed gun instances.");
                    string parentId = ObjectId(map.parent);
                    signature.Append("|MAP:").Append(asset.Id).Append(':').Append(guid).Append(':').Append(parentId).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path));
                    if (live.TryGetValue(asset.Id, out var record))
                    {
                        if (record.prefabGuid != guid || record.parentId != parentId) throw new InvalidOperationException("Existing managed mapping differs. Remove this ID in a separate reviewed plan before changing its prefab or parent.");
                        plan.operations.Add(new Operation { kind = "KEEP", assetId = asset.Id, record = record, instance = Resolve(record.instanceId) as GameObject, description = "KEEP · " + asset.Title + "\n" + PathOf(map.parent, avatar.transform) + "\n" + Estimate(map.prefab) });
                    }
                    else plan.operations.Add(new Operation { kind = "ADD", assetId = asset.Id, prefab = map.prefab, parent = map.parent, description = "ADD · " + asset.Title + "\nPrefab: " + path + "\nParent: " + PathOf(map.parent, avatar.transform) + "\n" + Estimate(map.prefab) });
                }
                catch (Exception e) { plan.errors.Add(asset.Title + ": " + e.Message); }
            }
            foreach (string id in LoadoutData20261012.RemovedIds(manifest.Assets.Select(a => a.Id), live.Keys))
            {
                var record = live[id]; var instance = Resolve(record.instanceId) as GameObject;
                try
                {
                    VerifyOwned(record, instance, true);
                    string path = AssetDatabase.GUIDToAssetPath(record.prefabGuid);
                    signature.Append("|REMOVE:").Append(record.recordId).Append(':').Append(record.instanceId).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path));
                    plan.operations.Add(new Operation { kind = "REMOVE", assetId = id, instance = instance, record = record, description = "REMOVE managed instance · " + id + "\n" + PathOf(instance.transform, avatar.transform) + "\nPrefab: " + path });
                }
                catch (Exception e) { plan.errors.Add(id + ": " + e.Message); }
            }
            if (plan.registry.records.Count + plan.operations.Count(o => o.kind == "ADD") > 20000) plan.errors.Add("Ownership history is full; no records are pruned automatically.");
            foreach (var op in plan.operations) signature.Append('|').Append(op.kind).Append(':').Append(op.assetId).Append(':').Append(op.record?.instanceId ?? "");
            plan.fingerprint = LoadoutData20261012.Hash(signature.ToString());
            return plan;
        }

        void Apply()
        {
            int undoGroup = -1;
            Plan fresh = null;
            try
            {
                if (!reviewedCreatorSetup || preview == null) throw new InvalidOperationException("Review creator setup and preview first.");
                fresh = BuildPlan();
                if (fresh.errors.Count != 0 || fresh.fingerprint != preview.fingerprint) throw new InvalidOperationException("The scene, mapping, manifest or ownership changed. Preview again.");
                Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Gunsmith planned loadout");
                foreach (var op in fresh.operations.Where(o => o.kind == "ADD"))
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(op.prefab, op.parent);
                    if (!instance) throw new InvalidOperationException("Prefab instantiation failed.");
                    try { Undo.RegisterCreatedObjectUndo(instance, "Add Gunsmith asset"); }
                    catch
                    {
                        // Only this just-created reference is eligible for cleanup before Undo registration.
                        Object.DestroyImmediate(instance);
                        throw;
                    }
                    var row = new GunsmithOwnedRecord20261012 {
                        recordId = Guid.NewGuid().ToString("N"), assetId = op.assetId, targetId = fresh.targetId, instanceId = ObjectId(instance),
                        prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(op.prefab)), parentId = ObjectId(op.parent),
                        sceneGuid = AssetDatabase.AssetPathToGUID(avatar.scene.path), tombstone = false
                    };
                    if (fresh.registry.records.Any(r => r.instanceId == row.instanceId)) throw new InvalidOperationException("New instance ID conflicts with retained ownership history.");
                    VerifyOwned(row, instance, false);
                    fresh.registry.records.Add(row);
                    SaveRegistry(fresh.registry); // Record ownership before any later operation can fail.
                }
                foreach (var op in fresh.operations.Where(o => o.kind == "REMOVE"))
                {
                    VerifyOwned(op.record, op.instance, true);
                    Undo.DestroyObjectImmediate(op.instance);
                    op.record.tombstone = true;
                    SaveRegistry(fresh.registry);
                }
                SaveRegistry(fresh.registry);
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(avatar.scene);
                notice = "Applied " + fresh.operations.Count(o => o.kind == "ADD") + " additions and " + fresh.operations.Count(o => o.kind == "REMOVE") + " removals. Undo is available. Inspect the creator setup and save the scene manually.";
            }
            catch (Exception e)
            {
                bool reverted = false;
                if (undoGroup >= 0)
                    try { Undo.RevertAllDownToGroup(undoGroup); reverted = true; }
                    catch (Exception rollbackError) { Debug.LogError("Gunsmith rollback could not be confirmed: " + rollbackError.Message); }
                // Retain every newly recorded identity even after rollback. Never adopt an unrelated object by name.
                if (fresh?.registry != null && undoGroup >= 0)
                    try { foreach (var row in fresh.registry.records) row.tombstone = !Resolve(row.instanceId); SaveRegistry(fresh.registry); }
                    catch (Exception registryError) { Debug.LogError("Gunsmith registry recovery requires review: " + registryError.Message); }
                notice = (undoGroup < 0 ? "Apply blocked before scene changes. " : reverted ? "Apply stopped; this operation was reverted. " : "Apply stopped; rollback requires manual review. ") + e.Message;
                Debug.LogWarning("Gunsmith: " + e.Message);
            }
            Invalidate();
        }
    }
}
#endif
