using BattleTech;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H1: refreshes board on month boundary.</summary>
    [HarmonyPatch(typeof(SimGameState), "OnNewQuarterBegin")]
    public static class OnNewQuarterBegin
    {
        public static void Postfix(SimGameState __instance)
        {
            // The financial report can fire mid-flight — CurSystem is not a meaningful
            // "local planet" then, so skip the refresh; the next cantina arrival regenerates
            if (__instance.TravelState != SimGameTravelStatus.IN_SYSTEM)
            {
                Core.Debug("[H1] Financial report in transit — board refresh skipped");
                return;
            }

            var curSystem = __instance.CurSystem;
            var isCantina = curSystem.Tags.Contains(Core.Settings.PlanetTag);

            if (isCantina && Core.State.Board != null)
            {
                Core.Log($"[H1] Month end, refreshing board");
                BoardGenerator.RefreshBoard(curSystem);
                Notifications.OnNewQuarterBegin();
            }
        }
    }
}
