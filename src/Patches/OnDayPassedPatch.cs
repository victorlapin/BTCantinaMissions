using System;
using System.Linq;
using BattleTech;
using BTCantinaMissions.Domain;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H1: detects day/month change and refreshes all boards on month boundary.</summary>
    [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
    public static class OnDayPassedPatch
    {
        private static DateTime lastDate;

        public static void Prefix(SimGameState __instance, int timeLapse = 0)
        {
            lastDate = __instance.CurrentDate;
        }

        public static void Postfix(SimGameState __instance)
        {
            var currentDate = __instance.CurrentDate;
            if (currentDate.Month == lastDate.Month && currentDate.Year == lastDate.Year)
                return;

            var month = currentDate.Year * 12 + currentDate.Month;
            Core.Log($"[H1] Month changed: {currentDate:yyyy-MM}, refreshing all boards");

            foreach (var board in Core.State.Boards.Values)
            {
                if (board.LastRefreshMonth >= month) continue;
                var system = __instance.StarSystems.FirstOrDefault(s => s.ID == board.SystemId);
                if (system == null) continue;
                BoardGenerator.RefreshBoard(system, month);
            }
        }
    }
}
