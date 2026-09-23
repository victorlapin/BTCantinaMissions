using System;
using BattleTech;
using BattleTech.Framework;
using BattleTech.UI;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H7b (v0.7.1): the only AAR path — appends cantina entries to
    /// the OBJECTIVES list of the contract results screen via
    /// AAR_ContractObjectivesWidget.AddObjective, the same mechanism
    /// DropCostsEnhanced uses, proven to render on this screen. Completed jobs
    /// render as green SUCCESS; partial progress as Ignored, which IRBTModUtils
    /// (hard dependency) recolors to gold "RESULT". Battles inside flashpoints
    /// are contracts too, so they are covered here as well. The dormant
    /// OtherResults widget is not usable: InitializeWidgets keeps it
    /// deactivated, and activating it shows the header but no text (23.09).</summary>
    [HarmonyPatch(typeof(AAR_ContractResults_Screen), nameof(AAR_ContractResults_Screen.FillInData))]
    public static class AAR_ContractResults_CantinaPatch
    {
        public static void Postfix(AAR_ContractResults_Screen __instance)
        {
            try
            {
                var entries = CombatFeedback.BuildAAREntries();
                if (entries == null || entries.Count == 0) return;

                foreach (var entry in entries)
                {
                    var result = new MissionObjectiveResult(
                        $"Cantina: {entry.Title}",
                        $"cantina_{entry.JobInstanceId}",
                        false, // secondary hex icon — not a contract objective
                        true,
                        entry.Completed ? ObjectiveStatus.Succeeded : ObjectiveStatus.Ignored,
                        false);
                    __instance.ContractObjectives.AddObjective(result);
                    Core.Debug($"[H7b] AAR objective (contract): {entry.Title} ({(entry.Completed ? "Succeeded" : "Ignored")})");
                }
            }
            catch (Exception e)
            {
                Core.Debug($"[H7b] failed: {e.Message}");
            }
        }
    }
}
