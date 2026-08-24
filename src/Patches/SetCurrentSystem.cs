using BattleTech;
using BTCantinaMissions.Domain;
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

            // Compute current month from sim date
            var month = GetMonth(__instance);
            BoardGenerator.RefreshBoard(system, month);
        }

        /// <summary>Computes an absolute month number from the sim's current date.</summary>
        private static int GetMonth(SimGameState sim)
        {
            var date = sim.CurrentDate;
            return date.Year * 12 + date.Month;
        }
    }
}
