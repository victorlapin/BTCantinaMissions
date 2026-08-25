using BattleTech;
using BTCantinaMissions.Domain;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H1: refreshes board on month boundary.</summary>
    [HarmonyPatch(typeof(SimGameState), "OnNewQuarterBegin")]
    public static class OnNewQuarterBegin
    {
        public static void Postfix(SimGameState __instance)
        {
            var curSystem = __instance.CurSystem;

            if (curSystem.Tags.Contains(Core.Settings.PlanetTag) && Core.State.Board == null)
            {
                Core.Debug($"Add missing board for current system");
                Core.State.Board = new SystemBoard();
            }

            Core.Log($"[H1] Month end, refreshing board");
            BoardGenerator.RefreshBoard(curSystem);
        }
    }
}
