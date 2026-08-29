using BattleTech;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H2: detects entering a cantina system and refreshes the board.
    /// Call-site semantics (dump research): the game calls SetCurrentSystem on every
    /// jump hop of a route (waypoints included, flybys), on the final hop while still
    /// WARMING_ENGINES (two states before IN_SYSTEM — a travel-state guard would
    /// suppress the arrival refresh), and on UX attach with CurSystem passed as
    /// itself (a no-op early-return in the original when force is false — e.g. every
    /// save load, which must NOT regenerate the persisted board).</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.SetCurrentSystem))]
    public static class SetCurrentSystem
    {
        // Records whether the original will early-return (the game's own guard:
        // CurSystem == system && !force) — a self-set on UX re-attach
        public static void Prefix(SimGameState __instance, StarSystem system, bool force, ref bool __state)
        {
            __state = system != null && __instance.CurSystem == system && !force;
        }

        public static void Postfix(SimGameState __instance, StarSystem system, bool __state)
        {
            if (__state) return; // no-op self-set (save load): keep the persisted board
            if (system == null) return;
            if (!system.Tags.Contains(Core.Settings.PlanetTag))
            {
                Core.Debug($"[H2] {system.Name}: no cantina tag, skipping");
                return;
            }

            Core.Log($"[H2] Cantina system: {system.Name} (diff {system.Def.GetDifficulty(__instance.SimGameMode)})");

            if (Core.State.Board == null)
            {
                Core.State.Board = new SystemBoard();
            }

            BoardGenerator.RefreshBoard(system);

            // Toast only for the route destination — intermediate hops refresh the
            // board silently. When idle (arrived / new campaign) the starmap keeps
            // Destination == CurSystem, so those still toast.
            var destination = __instance.Starmap?.GetDestinationSystem();
            if (destination == null || destination.ID == system.ID)
                Notifications.OnNewJobsAvailable();
        }
    }
}
