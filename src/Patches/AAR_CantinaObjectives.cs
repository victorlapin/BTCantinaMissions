using BattleTech.UI;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H7: appends cantina job results to the AAR's "Other Results"
    /// panel (same place as payment/reputation lines). Postfix on DisplayData
    /// fires after the game fills its own results — our lines appear below.</summary>
    [HarmonyPatch(typeof(AAR_OtherResultsWidget), nameof(AAR_OtherResultsWidget.DisplayData))]
    public static class AAR_CantinaResultsPatch
    {
        public static void Postfix(AAR_OtherResultsWidget __instance)
        {
            var entries = CombatFeedback.BuildAAREntries();
            if (entries == null || entries.Count == 0) return;

            foreach (var entry in entries)
            {
                var line = entry.Completed
                    ? UIColors.Wrap($"Cantina: {entry.Title}", UIColor.Green)
                    : UIColors.Wrap($"Cantina: {entry.Title}", UIColor.Gold);
                __instance.resultsText.AppendLine(line);
                Core.Debug($"[H7] AAR result: {entry.Title}");
            }

            __instance.resultsText.RefreshText(false);
        }
    }
}
