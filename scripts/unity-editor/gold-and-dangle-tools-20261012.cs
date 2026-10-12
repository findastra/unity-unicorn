// Unity Unicorn tools by Astra. Prepared with OpenAI Codex (GPT-6), 2026-10-12.
// Editor-only, explicit selected slots/islands, cloned assets, no automatic scene saves.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityUnicorn
{
    public static class SurfaceToolAssets20261012
    {
        public static void RequireSceneRenderer(Renderer renderer)
        {
            if (!renderer || EditorUtility.IsPersistent(renderer) || !renderer.gameObject.scene.IsValid())
                throw new InvalidOperationException("Select a renderer on the intended scene avatar.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Leave Play Mode and wait for Unity to finish importing or compiling.");
        }

        public static string NewAssetPath(string category, string description, string extension)
        {
            var folder = "Assets/UnityUnicorn/generated/" + category;
            var parts = folder.Split('/'); var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"); }
            catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time"); }
            var stamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var name = Regex.Replace(description.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (name.Length == 0) name = category;
            for (int suffix = 1; ; suffix++)
            {
                var path = folder + "/" + name + (suffix == 1 ? "" : "-" + suffix) + "-" + stamp + "." + extension;
                if (!File.Exists(path) && !AssetDatabase.LoadMainAssetAtPath(path)) return path;
            }
        }

        public static void Changed(Renderer renderer)
        {
            EditorUtility.SetDirty(renderer);
            if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
        }
    }

    public sealed class GoldenPaint20261012 : EditorWindow
    {
        Renderer target;
        Material[] displayed = Array.Empty<Material>();
        bool[] slots = Array.Empty<bool>();
        Color gold = new Color(1f, 0.766f, 0.336f, 1f);
        float metallic = 1f, smoothness = 0.75f;
        bool applyMetallic = true;
        Vector2 scroll;
        string result = "";

        [MenuItem("Tools/Unity Unicorn/golden-painter", false, 42)]
        public static void Open()
        {
            var window = GetWindow<GoldenPaint20261012>("Golden painter");
            window.SetTarget(Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Renderer>() : null);
            window.minSize = new Vector2(440, 360);
        }

        void SetTarget(Renderer renderer)
        {
            target = renderer; displayed = target ? target.sharedMaterials : Array.Empty<Material>();
            slots = new bool[displayed.Length]; result = "";
        }

        void OnGUI()
        {
            var next = (Renderer)EditorGUILayout.ObjectField("Scene renderer", target, typeof(Renderer), true);
            if (next != target) SetTarget(next);
            if (target && !target.sharedMaterials.SequenceEqual(displayed)) SetTarget(target);
            EditorGUILayout.HelpBox("Select the exact material slots to tint. Creates material copies while retaining texture references, alpha and other authored settings. Undo restores the renderer's original assignments; generated copies remain available.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < slots.Length; i++)
                using (new EditorGUI.DisabledScope(!displayed[i])) slots[i] = EditorGUILayout.ToggleLeft("Slot " + i + ": " + (displayed[i] ? displayed[i].name : "empty"), slots[i]);
            EditorGUILayout.EndScrollView();
            gold = EditorGUILayout.ColorField("Gold tint", gold);
            applyMetallic = EditorGUILayout.Toggle("Set metallic finish", applyMetallic);
            using (new EditorGUI.DisabledScope(!applyMetallic))
            {
                metallic = EditorGUILayout.Slider("Metallic", metallic, 0, 1);
                smoothness = EditorGUILayout.Slider("Smoothness", smoothness, 0, 1);
            }
            using (new EditorGUI.DisabledScope(!target || !slots.Any(value => value)))
                if (GUILayout.Button("Create and assign gold material copies")) Apply();
            if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        static Material Prepare(Material source, Color tint, bool finish, float metallic, float smoothness)
        {
            if (!source || !source.shader) throw new InvalidOperationException("A selected slot has no usable material or shader.");
            var copy = new Material(source) { name = source.name + " Gold" };
            try
            {
                // An optimized shader may have compiled tint features away. Unlock only the unassigned copy.
                var original = copy.GetTag("OriginalShader", false, "");
                if (original.Length > 0)
                {
                    var shader = Shader.Find(original);
                    if (!shader)
                    {
                        var guid = copy.GetTag("OriginalShaderGUID", false, "");
                        if (guid.Length > 0) shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                    }
                    if (!shader) throw new InvalidOperationException(source.name + ": original shader is unavailable.");
                    copy.shader = shader; copy.SetOverrideTag("OriginalShader", ""); copy.SetOverrideTag("OriginalShaderGUID", "");
                    if (copy.HasProperty("_ShaderOptimizerEnabled")) copy.SetFloat("_ShaderOptimizerEnabled", 0);
                }
                string property = copy.HasProperty("_BaseColor") ? "_BaseColor" : copy.HasProperty("_Color") ? "_Color" : null;
                if (property == null) throw new InvalidOperationException(source.name + ": no supported base color property (_BaseColor or _Color).");
                if (copy.shader.GetPropertyType(copy.shader.FindPropertyIndex(property)) != ShaderPropertyType.Color)
                    throw new InvalidOperationException(source.name + ": its base-color property is not a Color property.");
                var before = copy.GetColor(property);
                copy.SetColor(property, new Color(before.r * tint.r, before.g * tint.g, before.b * tint.b, before.a));
                if (finish)
                {
                    string shine = copy.HasProperty("_Smoothness") ? "_Smoothness" : copy.HasProperty("_Glossiness") ? "_Glossiness" : null;
                    if (!copy.HasProperty("_Metallic") || shine == null)
                        throw new InvalidOperationException(source.name + ": this shader has no supported metallic/smoothness pair. Turn off Set metallic finish for tint-only copies, or use the shader's own controls.");
                    foreach (var scalar in new[] { "_Metallic", shine })
                    {
                        var type = copy.shader.GetPropertyType(copy.shader.FindPropertyIndex(scalar));
                        if (type != ShaderPropertyType.Float && type != ShaderPropertyType.Range)
                            throw new InvalidOperationException(source.name + ": unsupported property type for " + scalar);
                    }
                    copy.SetFloat("_Metallic", metallic); copy.SetFloat(shine, smoothness);
                }
                MaterialEditor.ApplyMaterialPropertyDrawers(copy);
                return copy;
            }
            catch { Object.DestroyImmediate(copy); throw; }
        }

        void Apply()
        {
            var prepared = new Dictionary<Material, Material>();
            var createdPaths = new List<string>();
            int group = -1;
            try
            {
                SurfaceToolAssets20261012.RequireSceneRenderer(target);
                var original = target.sharedMaterials;
                if (!original.SequenceEqual(displayed)) throw new InvalidOperationException("Material assignments changed. Review the slots again.");
                // Preflight every selected material in memory before writing any assets or changing assignments.
                foreach (var material in original.Where((material, index) => slots[index]).Distinct())
                    prepared.Add(material, Prepare(material, gold, applyMetallic, metallic, smoothness));
                var assigned = (Material[])original.Clone();
                foreach (var pair in prepared)
                {
                    var path = SurfaceToolAssets20261012.NewAssetPath("gold-materials", pair.Value.name, "mat");
                    AssetDatabase.CreateAsset(pair.Value, path);
                    if (AssetDatabase.GetAssetPath(pair.Value) != path) throw new InvalidOperationException("Failed to create material copy: " + path);
                    createdPaths.Add(path); AssetDatabase.SaveAssetIfDirty(pair.Value);
                }
                for (int i = 0; i < assigned.Length; i++) if (slots[i]) assigned[i] = prepared[original[i]];
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Unity Unicorn gold material assignments");
                Undo.RecordObject(target, "Gold material assignments"); target.sharedMaterials = assigned; SurfaceToolAssets20261012.Changed(target);
                Undo.CollapseUndoOperations(group);
                result = "Created " + prepared.Count + " material copy/copies for " + slots.Count(value => value) + " selected slots. Texture references and original materials retained. Scene remains unsaved; inspect under the intended lighting. Texture maps or shader features can override the visible tint/metallic result.";
                Debug.Log("[Unity Unicorn golden-painter] " + result, target);
                displayed = target.sharedMaterials; slots = new bool[displayed.Length];
            }
            catch (Exception error)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                foreach (var path in createdPaths) AssetDatabase.DeleteAsset(path);
                foreach (var material in prepared.Values) if (material && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material);
                result = "No gold assignment retained: " + error.Message; Debug.LogException(error);
            }
        }
    }

    public sealed class ChainIsland20261012
    {
        public int[] vertices;
        public Bounds bounds;
        public int mappedBone = -1;
        public string existingInfluences;
    }

    public sealed class ChainDangleWeights20261012 : EditorWindow
    {
        SkinnedMeshRenderer target;
        Mesh analyzedMesh;
        Transform[] analyzedBones;
        Vector3[] analyzedVertices;
        int[][] analyzedIndices;
        readonly List<ChainIsland20261012> islands = new List<ChainIsland20261012>();
        Vector2 scroll;
        int focus = -1;
        string result = "";
        const int MaxEditableIslands = 256;

        [MenuItem("Tools/Unity Unicorn/chain-dangle-weights", false, 43)]
        public static void Open()
        {
            var window = GetWindow<ChainDangleWeights20261012>("Chain dangle weights");
            window.target = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>() : null;
            window.Clear(); window.minSize = new Vector2(620, 450);
        }

        void OnEnable() { SceneView.duringSceneGui += DrawIsland; }
        void OnDisable() { SceneView.duringSceneGui -= DrawIsland; }
        void Clear() { analyzedMesh = null; islands.Clear(); focus = -1; }

        void OnGUI()
        {
            var next = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Scene skinned renderer", target, typeof(SkinnedMeshRenderer), true);
            if (next != target) { target = next; Clear(); result = ""; }
            EditorGUILayout.HelpBox("Inspect disconnected indexed mesh islands and map only chosen islands to existing bones. Creates a cloned mesh with rigid 100% weights for those islands. Vertices, triangles, gaps, UVs, blendshapes and unselected weights remain unchanged. This does not create a chain rig or physics.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!target)) if (GUILayout.Button("Inspect mesh islands (read only)")) Analyze();
            if (analyzedMesh)
            {
                EditorGUILayout.LabelField(islands.Count + " indexed islands in " + analyzedMesh.name, EditorStyles.boldLabel);
                if (islands.Count > MaxEditableIslands) EditorGUILayout.HelpBox("Too many islands for reliable manual mapping here. UV seams or exported split vertices may divide one visible chain link into many islands. Inspect the source mesh in Blender; this tool does not weld by position or guess which pieces belong together.", MessageType.Warning);
                var options = new[] { "Unchanged" }.Concat(analyzedBones.Select((bone, i) => i + ": " + (bone ? bone.name : "MISSING BONE"))).ToArray();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                for (int i = 0; i < Math.Min(islands.Count, MaxEditableIslands); i++)
                {
                    var island = islands[i]; EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Inspect " + i, GUILayout.Width(90))) { focus = i; SceneView.RepaintAll(); }
                    EditorGUILayout.LabelField(island.vertices.Length + " vertices", GUILayout.Width(90));
                    island.mappedBone = EditorGUILayout.Popup(island.mappedBone + 1, options) - 1;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.LabelField("Existing: " + island.existingInfluences, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndScrollView();
                using (new EditorGUI.DisabledScope(islands.Count > MaxEditableIslands || !islands.Any(island => island.mappedBone >= 0)))
                    if (GUILayout.Button("Clone mesh and apply explicitly mapped island weights")) Apply();
            }
            if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        void Analyze()
        {
            Clear();
            try
            {
                SurfaceToolAssets20261012.RequireSceneRenderer(target);
                var mesh = target.sharedMesh;
                if (!mesh || !mesh.isReadable) throw new InvalidOperationException("A readable skinned mesh is required. Inspect its import settings; this tool does not change them automatically.");
                var bones = target.bones;
                if (bones.Length == 0 || mesh.bindposes.Length != bones.Length) throw new InvalidOperationException("Existing bones and matching bind poses are required. Build or repair the rig before assigning weights.");
                var vertices = mesh.vertices; var parents = Enumerable.Range(0, vertices.Length).ToArray();
                int Find(int n) { while (parents[n] != n) { parents[n] = parents[parents[n]]; n = parents[n]; } return n; }
                void Join(int a, int b) { int x = Find(a), y = Find(b); if (x != y) parents[y] = x; }
                var indices = new int[mesh.subMeshCount][];
                var referenced = new bool[vertices.Length];
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidOperationException("Triangle submeshes are required for island analysis.");
                    indices[sub] = mesh.GetIndices(sub);
                    if (indices[sub].Length % 3 != 0) throw new InvalidOperationException("Malformed triangle index count.");
                    foreach (int index in indices[sub])
                    { if (index < 0 || index >= vertices.Length) throw new InvalidOperationException("Out-of-range mesh index."); referenced[index] = true; }
                    for (int i = 0; i < indices[sub].Length; i += 3) { Join(indices[sub][i], indices[sub][i + 1]); Join(indices[sub][i], indices[sub][i + 2]); }
                }
                {
                    // Mesh-owned Allocator.None views: read while the mesh is unchanged; do not dispose them.
                    var counts = mesh.GetBonesPerVertex();
                    var weights = mesh.GetAllBoneWeights();
                    if (counts.Length != vertices.Length || counts.SumBytes() != weights.Length) throw new InvalidOperationException("The mesh needs a valid existing skin-weight buffer.");
                    int offset = 0; var vertexBones = new int[vertices.Length][];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var used = new HashSet<int>();
                        for (int j = 0; j < counts[i]; j++)
                        {
                            var weight = weights[offset++];
                            if (weight.boneIndex < 0 || weight.boneIndex >= bones.Length) throw new InvalidOperationException("Existing weights reference a missing bone index.");
                            if (float.IsNaN(weight.weight) || float.IsInfinity(weight.weight) || weight.weight < 0)
                                throw new InvalidOperationException("Existing weights contain invalid values.");
                            if (weight.weight > 0) used.Add(weight.boneIndex);
                        }
                        vertexBones[i] = used.ToArray();
                    }
                    foreach (var group in Enumerable.Range(0, vertices.Length).Where(i => referenced[i]).GroupBy(Find).OrderBy(group => group.Min()))
                    {
                        var members = group.ToArray(); var bounds = new Bounds(vertices[members[0]], Vector3.zero);
                        foreach (int i in members) bounds.Encapsulate(vertices[i]);
                        var used = members.SelectMany(i => vertexBones[i]).Distinct().OrderBy(i => i).ToArray();
                        islands.Add(new ChainIsland20261012 { vertices = members, bounds = bounds, existingInfluences = string.Join(", ", used.Select(i => i + ":" + (bones[i] ? bones[i].name : "MISSING"))) });
                    }
                }
                analyzedMesh = mesh; analyzedBones = bones; analyzedVertices = vertices; analyzedIndices = indices;
                result = "Analysis only. Islands use shared vertex indices, not distance welding. " + referenced.Count(value => !value) + " unused vertices will remain untouched. Select Inspect to see a cyan rest-mesh bounding box, then map each intended link explicitly.";
            }
            catch (Exception error) { Clear(); result = "Cannot map this mesh: " + error.Message; Debug.LogException(error); }
        }

        void DrawIsland(SceneView view)
        {
            if (!target || !analyzedMesh || focus < 0 || focus >= islands.Count) return;
            var previous = Handles.matrix; var color = Handles.color;
            try
            {
                Handles.matrix = target.transform.localToWorldMatrix; Handles.color = Color.cyan;
                var bounds = islands[focus].bounds; Handles.DrawWireCube(bounds.center, bounds.size);
                Handles.Label(bounds.center, "Island " + focus + " — rest mesh bounds");
            }
            finally { Handles.matrix = previous; Handles.color = color; }
        }

        void Apply()
        {
            Mesh copy = null; string path = null; bool assetCreated = false; int undoGroup = -1;
            try
            {
                SurfaceToolAssets20261012.RequireSceneRenderer(target);
                if (target.sharedMesh != analyzedMesh || !target.bones.SequenceEqual(analyzedBones) || !analyzedMesh.vertices.SequenceEqual(analyzedVertices) || analyzedMesh.subMeshCount != analyzedIndices.Length)
                    throw new InvalidOperationException("The mesh or rig changed. Inspect it again before applying.");
                for (int sub = 0; sub < analyzedIndices.Length; sub++)
                    if (analyzedMesh.GetTopology(sub) != MeshTopology.Triangles || !analyzedMesh.GetIndices(sub).SequenceEqual(analyzedIndices[sub])) throw new InvalidOperationException("Mesh topology changed; inspect again.");
                if (islands.Count > MaxEditableIslands) throw new InvalidOperationException("This mesh has too many indexed islands for this manual mapping tool.");
                if (analyzedMesh.bindposes.Length != analyzedBones.Length) throw new InvalidOperationException("Bind poses changed; inspect again.");
                var map = Enumerable.Repeat(-1, analyzedVertices.Length).ToArray(); int mapped = 0;
                foreach (var island in islands.Where(island => island.mappedBone >= 0))
                {
                    if (island.mappedBone >= analyzedBones.Length || !analyzedBones[island.mappedBone]) throw new InvalidOperationException("A selected target bone is missing.");
                    foreach (int vertex in island.vertices) map[vertex] = island.mappedBone;
                    mapped++;
                }
                if (mapped == 0) throw new InvalidOperationException("Map at least one island to an existing bone.");
                copy = Object.Instantiate(analyzedMesh); copy.name = analyzedMesh.name + " Dangle Weights";
                {
                    var oldCounts = analyzedMesh.GetBonesPerVertex();
                    var oldWeights = analyzedMesh.GetAllBoneWeights();
                    if (oldCounts.Length != analyzedVertices.Length || oldCounts.SumBytes() != oldWeights.Length) throw new InvalidOperationException("Skin-weight buffer changed; inspect again.");
                    var counts = new byte[map.Length]; var weights = new List<BoneWeight1>(); int offset = 0;
                    for (int vertex = 0; vertex < map.Length; vertex++)
                    {
                        if (map[vertex] >= 0) { counts[vertex] = 1; weights.Add(new BoneWeight1 { boneIndex = map[vertex], weight = 1f }); }
                        else
                        {
                            counts[vertex] = oldCounts[vertex];
                            for (int j = 0; j < oldCounts[vertex]; j++) weights.Add(oldWeights[offset + j]);
                        }
                        offset += oldCounts[vertex];
                    }
                    using (var nativeCounts = new NativeArray<byte>(counts, Allocator.Temp))
                    using (var nativeWeights = new NativeArray<BoneWeight1>(weights.ToArray(), Allocator.Temp)) copy.SetBoneWeights(nativeCounts, nativeWeights);
                }
                if (!copy.vertices.SequenceEqual(analyzedVertices)) throw new InvalidOperationException("Unexpected vertex changes; clone not assigned.");
                for (int sub = 0; sub < analyzedIndices.Length; sub++)
                    if (!copy.GetIndices(sub).SequenceEqual(analyzedIndices[sub])) throw new InvalidOperationException("Unexpected topology change; clone not assigned.");
                VerifyWeights(analyzedMesh, copy, map);
                path = SurfaceToolAssets20261012.NewAssetPath("dangle-meshes", copy.name, "asset");
                AssetDatabase.CreateAsset(copy, path);
                if (AssetDatabase.GetAssetPath(copy) != path) throw new InvalidOperationException("Failed to create the cloned mesh asset.");
                assetCreated = true;
                AssetDatabase.SaveAssetIfDirty(copy);
                Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Unity Unicorn dangle weights");
                Undo.RecordObject(target, "Dangle mesh assignment"); target.sharedMesh = copy; SurfaceToolAssets20261012.Changed(target);
                Undo.CollapseUndoOperations(undoGroup);
                result = "Assigned a mesh copy with " + mapped + " explicitly mapped island(s). Original mesh and chain topology retained. Scene remains unsaved. Check bind pose, bone movement and chain gaps; existing physics/rigging still determine movement. Undo restores the original mesh assignment.";
                Debug.Log("[Unity Unicorn chain-dangle-weights] " + result, target); Clear();
            }
            catch (Exception error)
            {
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                if (assetCreated) AssetDatabase.DeleteAsset(path);
                if (copy && !EditorUtility.IsPersistent(copy)) Object.DestroyImmediate(copy);
                result = "No mesh assignment retained: " + error.Message; Debug.LogException(error);
            }
        }

        static void VerifyWeights(Mesh original, Mesh copy, int[] map)
        {
            {
                var oldCounts = original.GetBonesPerVertex();
                var oldWeights = original.GetAllBoneWeights();
                var newCounts = copy.GetBonesPerVertex();
                var newWeights = copy.GetAllBoneWeights();
                if (newCounts.Length != map.Length || newCounts.SumBytes() != newWeights.Length)
                    throw new InvalidOperationException("The cloned weight buffer is invalid; mesh not assigned.");
                int oldOffset = 0, newOffset = 0;
                for (int vertex = 0; vertex < map.Length; vertex++)
                {
                    if (map[vertex] >= 0)
                    {
                        if (newCounts[vertex] != 1 || newWeights[newOffset].boneIndex != map[vertex] || Mathf.Abs(newWeights[newOffset].weight - 1f) > 0.00001f)
                            throw new InvalidOperationException("A mapped island did not retain its requested rigid weight.");
                    }
                    else
                    {
                        if (newCounts[vertex] != oldCounts[vertex]) throw new InvalidOperationException("An unselected vertex's influence count changed.");
                        for (int j = 0; j < oldCounts[vertex]; j++)
                            if (oldWeights[oldOffset + j].boneIndex != newWeights[newOffset + j].boneIndex || Mathf.Abs(oldWeights[oldOffset + j].weight - newWeights[newOffset + j].weight) > 0.00001f)
                                throw new InvalidOperationException("An unselected vertex's weights changed; mesh not assigned.");
                    }
                    oldOffset += oldCounts[vertex]; newOffset += newCounts[vertex];
                }
            }
        }
    }

    static class BoneCountUtility20261012
    {
        public static int SumBytes(this NativeArray<byte> values) { int sum = 0; for (int i = 0; i < values.Length; i++) sum += values[i]; return sum; }
    }
}
