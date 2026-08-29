using BattleTech;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H2: detects arrival at a cantina planet and generates/refreshes the board.</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.SetCurrentSystem))]
    public static class SetCurrentSystem
    {
        public static void Postfix(SimGameState __instance, StarSystem system)
        {
            if (system == null) return;
            if (!system.Tags.Contains(Core.Settings.PlanetTag))
            {
                Core.Debug($"[H2] {system.Name}: no cantina tag, skipping");
                return;
            }

            Core.Log($"[H2] Arrived at cantina planet: {system.Name} (diff {system.Def.GetDifficulty(__instance.SimGameMode)})");

            if (Core.State.Board == null)
            {
                Core.State.Board = new SystemBoard();
            }

            BoardGenerator.RefreshBoard(system);
            Notifications.OnNewJobsAvailable();
        }
    }
}
