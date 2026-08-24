using System;
using System.Collections.Generic;
using System.IO;
using BattleTech;
using Newtonsoft.Json;

namespace BTCantinaMissions.Domain
{
    /// <summary>Loads and indexes CantinaTaskDef files from the custom resource manifest.</summary>
    public static class TaskCatalog
    {
        private const string ResourceTypeName = "CantinaTaskDef";

        private static readonly Dictionary<string, CantinaTaskDef> defs = new Dictionary<string, CantinaTaskDef>();
        private static bool loaded;

        public static int Count => defs.Count;
        public static bool IsLoaded => loaded;
        public static IEnumerable<CantinaTaskDef> AllDefs => defs.Values;
        public static CantinaTaskDef GetDef(string id) =>
            defs.TryGetValue(id, out var def) ? def : null;

        public static void FinishedLoading(List<string> loadOrder,
            Dictionary<string, Dictionary<string, VersionManifestEntry>> customResources)
        {
            defs.Clear();
            loaded = false;

            if (customResources == null || !customResources.TryGetValue(ResourceTypeName, out var entries) || entries.Count == 0)
            {
                Core.LogWarning($"[{nameof(TaskCatalog)}] No '{ResourceTypeName}' resources registered, task catalog is empty");
                loaded = true;
                return;
            }

            foreach (var entry in entries.Values)
            {
                LoadDef(entry);
            }

            loaded = true;
            Core.Log($"[{nameof(TaskCatalog)}] Loaded {defs.Count} CantinaTaskDef(s) from {entries.Count} file(s)");
        }

        private static void LoadDef(VersionManifestEntry entry)
        {
            var path = entry.FilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Core.LogWarning($"[{nameof(TaskCatalog)}] File not found: {path ?? "<null>"} (id={entry.Id})");
                return;
            }

            try
            {
                var def = JsonConvert.DeserializeObject<CantinaTaskDef>(File.ReadAllText(path));
                if (def?.Description == null || string.IsNullOrEmpty(def.Description.Id))
                {
                    Core.LogWarning($"[{nameof(TaskCatalog)}] Invalid def (no Description.Id): {path}");
                    return;
                }
                if (defs.ContainsKey(def.Description.Id))
                {
                    Core.LogWarning($"[{nameof(TaskCatalog)}] Duplicate def id '{def.Description.Id}', skipping {path}");
                    return;
                }
                defs.Add(def.Description.Id, def);
                Core.Debug($"[{nameof(TaskCatalog)}]   {def.Description.Id}: {def.Description.Name} ({def.ObjectiveType})");
            }
            catch (Exception e)
            {
                Core.LogError($"[{nameof(TaskCatalog)}] Failed to parse {path}: {e.Message}");
            }
        }
    }
}
