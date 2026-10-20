// Claude, 2026-10-18. unity-asset-creator (Unity Unicorn, pet-program-5): one recipe per asset. It holds the plan,
// the asset-ledger row and how far the asset has come through the ordered checklist (AssetChecklist). A recipe is a
// small asset file next to the asset, so its progress is kept in Git with it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityUnicorn.AssetCreator
{
    [CreateAssetMenu(menuName = "Unity Unicorn/Asset Recipe", fileName = "asset-recipe")]
    public class AssetRecipe : ScriptableObject
    {
        [Header("1. Plan")]
        [Tooltip("<world-or-avatar>-<asset>, lowercase words joined by hyphens")] public string id = "";
        public string displayName = "";
        [Tooltip("model, prop, effect, sky, building, ...")] public string kind = "prop";
        [Tooltip("MAJOR.MINOR.PATCH")] public string version = "1.0.0";
        [TextArea(2, 5)] public string description = "";
        public bool forPC = true;
        public bool forAndroid = false;
        [Tooltip("Off: the asset never moves, so it should be static (batching, lightmaps, occlusion)")] public bool moves = false;
        [Tooltip("The one prefab that is the asset; its folder is the asset's folder")] public GameObject prefab;
        public int maxTriangles = 10000;
        public int maxMaterials = 4;
        public int maxTextureSize = 1024;
        public int maxParticles = 300;

        [Header("2. Provenance (asset-ledger row)")]
        public string creator = "";
        public string source = "";
        [Tooltip("SPDX id (CC0-1.0, MIT, CC-BY-4.0) or own-work, store-personal, store-commercial, research-only, unclear")] public string license = "own-work";
        [TextArea(1, 3)] public string attribution = "";
        public bool publicOk = true;

        [Header("This version")]
        [TextArea(2, 6)] public string changelog = "";

        [HideInInspector] public List<StepState> steps = new List<StepState>();
        [HideInInspector] public List<string> exportedVersions = new List<string>();

        public string PrefabPath => prefab != null ? AssetDatabase.GetAssetPath(prefab) : "";
        public string Folder => prefab != null ? Path.GetDirectoryName(PrefabPath).Replace('\\', '/') : "";
        public string ExportDir => "Exports/unity-assets-library/" + id + "/" + version;
        public string PreviewDir => ExportDir + "/previews";

        public StepState State(string stepId)
        {
            var s = steps.FirstOrDefault(x => x.id == stepId);
            if (s == null) { s = new StepState { id = stepId }; steps.Add(s); }
            return s;
        }

        // A step counts as done only for the version it was done for: a new version walks the list again.
        public bool IsDone(string stepId)
        {
            var s = steps.FirstOrDefault(x => x.id == stepId);
            return s != null && s.done && s.version == version;
        }
    }

    [Serializable]
    public class StepState
    {
        public string id;
        public bool done;
        public string version = "";
        public string status = "not run";
        public string note = "";
        public string doneAt = "";
        public string doneBy = "";
        [TextArea] public string details = "";
    }
}
