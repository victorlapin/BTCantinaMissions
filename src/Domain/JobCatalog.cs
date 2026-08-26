using System;
using System.Collections.Generic;
using System.IO;
using BattleTech;
using Newtonsoft.Json;

namespace BTCantinaMissions.Domain
{
    /// <summary>Loads and indexes CantinaJobDef files from the custom resource manifest.</summary>
    public static class JobCatalog
    {
        private const string ResourceTypeName = "CantinaJobDef";

        private static readonly Dictionary<string, CantinaJobDef> defs = new Dictionary<string, CantinaJobDef>();
        private static bool loaded;

        public static int Count => defs.Count;
        public static bool IsLoaded => loaded;
        public static IEnumerable<CantinaJobDef> AllDefs => defs.Values;
        public static CantinaJobDef GetDef(string id) =>
            defs.TryGetValue(id, out var def) ? def : null;

        public static void FinishedLoading(List<string> loadOrder,
            Dictionary<string, Dictionary<string, VersionManifestEntry>> customResources)
        {
            defs.Clear();
            loaded = false;

            if (customResources == null || !customResources.TryGetValue(ResourceTypeName, out var entries) || entries.Count == 0)
            {
                Core.LogWarning($"[{nameof(JobCatalog)}] No '{ResourceTypeName}' resources registered, job catalog is empty");
                loaded = true;
                return;
            }

            foreach (var entry in entries.Values)
            {
                LoadDef(entry);
            }

            loaded = true;
            Core.Log($"[{nameof(JobCatalog)}] Loaded {defs.Count} CantinaJobDef(s) from {entries.Count} file(s)");
        }

        private static void LoadDef(VersionManifestEntry entry)
        {
            var path = entry.FilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Core.LogWarning($"[{nameof(JobCatalog)}] File not found: {path ?? "<null>"} (id={entry.Id})");
                return;
            }

            try
            {
                var def = JsonConvert.DeserializeObject<CantinaJobDef>(File.ReadAllText(path));
                if (def?.Description == null || string.IsNullOrEmpty(def.Description.Id))
                {
                    Core.LogWarning($"[{nameof(JobCatalog)}] Invalid def (no Description.Id): {path}");
                    return;
                }
                if (defs.ContainsKey(def.Description.Id))
                {
                    Core.LogWarning($"[{nameof(JobCatalog)}] Duplicate def id '{def.Description.Id}', skipping {path}");
                    return;
                }
                if (def.ObjectiveType == ObjectiveType.CollectItems && def.ItemPool != null)
                {
                    foreach (var item in def.ItemPool)
                    {
                        if (!ItemCatalog.TryResolveType(item.ItemType, out _))
                            Core.LogWarning($"[{nameof(JobCatalog)}] ItemType '{item.ItemType}' for '{item.Id}' in {path} " +
                                "has no inventory store (expected Weapon | AmmunitionBox | HeatSink | JumpJet | Upgrade)");
                    }
                }
                defs.Add(def.Description.Id, def);
                Core.Debug($"[{nameof(JobCatalog)}]   {def.Description.Id}: {def.Description.Name} ({def.ObjectiveType})");
            }
            catch (Exception e)
            {
                Core.LogError($"[{nameof(JobCatalog)}] Failed to parse {path}: {e.Message}");
            }
        }
    }
}
