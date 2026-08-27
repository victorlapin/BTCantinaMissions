using System;
using System.IO;
using BattleTech;
using BattleTech.Save.SaveGameStructure;
using BTCantinaMissions.Domain;
using HarmonyLib;
using Newtonsoft.Json;

namespace BTCantinaMissions.Patches
{
    [HarmonyPatch(typeof(SimGameState), "Init")]
    public static class SimGameState_InitPatch
    {
        public static void Postfix()
        {
            Core.Debug("SimGameState Init");
            // New campaign: no save block will be loaded for it — start clean
            Core.ResetState();
        }
    }

    /// <summary>Campaign save load entry point (both Load overloads funnel here).
    /// JwTweaks' LoadCustomSaveBlocks runs later in this pipeline (inside
    /// SaveBlock.GetSaveData) and only calls our LoadData when the block exists in
    /// the save — a pre-mod save would otherwise keep state from the previous
    /// playthrough, so reset before the save is read.</summary>
    [HarmonyPatch(typeof(SaveGameStructure), "Load", new Type[] { typeof(string) })]
    public static class SaveGameStructure_LoadPatch
    {
        public static void Prefix()
        {
            Core.Debug("SaveGameStructure Load");
            Core.ResetState();
        }
    }

    /// <summary>After a save is rehydrated, re-syncs Deliver CollectItems progress
    /// with the actual inventory — self-healing against any drift between saves.</summary>
    [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
    public static class SimGameState_RehydratePatch
    {
        public static void Postfix(SimGameState __instance)
        {
            foreach (var job in Core.State.ActiveJobs)
            {
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectItems) continue;
                if (def.ItemMode != ItemModeType.Deliver) continue;

                var before = job.Progress;
                job.SyncProgress(ItemCatalog.GetInventoryCount(__instance, def, job));
                if (job.Progress != before)
                    Core.Log($"[Load] Progress sync: {job.ResolvedName} {before} → {job.Progress}/{job.TargetCount}");
            }
        }
    }

    /// <summary>Dumps CampaignState to a readable JSON file when the game saves.
    /// JwTweaks writes the actual CustomSaveBlock; this is a parallel debug dump.</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.Dehydrate))]
    public static class SimGameState_DehydratePatch
    {
        public static bool Prepare() => Core.Settings.DumpStateOnSave;

        // Parameter types matched by name; the third parameter's type is in
        // BattleTech.Save.Test namespace which is awkward to reference.
        public static void Postfix(SimGameState __instance)
        {
            try
            {
                var path = Path.Combine(Core.ModDir, "state_dump.json");
                File.WriteAllText(path,
                    JsonConvert.SerializeObject(Core.State, Formatting.Indented));
                Core.Debug($"[DumpState] State dumped to {path}");
            }
            catch (Exception e)
            {
                Core.LogWarning($"[DumpState] Failed to dump state: {e.Message}");
            }
        }
    }
}
