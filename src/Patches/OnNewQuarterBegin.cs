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
        public static bool Prepare() => Core.Settings.BoardRefresh == BoardRefreshType.Monthly;

        public static void Postfix(SimGameState __instance)
        {
            var curSystem = __instance.CurSystem;
            var isCantina = curSystem.Tags.Contains(Core.Settings.PlanetTag);

            if (isCantina && Core.State.Board == null)
            {
                Core.Debug($"Add missing board for current system");
                Core.State.Board = new SystemBoard();
            }

            Core.Log($"[H1] Month end, refreshing board");
            BoardGenerator.RefreshBoard(curSystem);

            if (isCantina)
            {
                Notifications.OnNewQuarterBegin();
            }
        }
    }
}
