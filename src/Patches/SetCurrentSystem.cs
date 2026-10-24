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
    /// suppress the arrival refresh), on UX attach with CurSystem passed as
    /// itself, and — on a FRESH career — with force=true, because the initial
    /// system set must not take the game's own self-set early return.</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.SetCurrentSystem))]
    public static class SetCurrentSystem
    {
        private const int NormalArrival = 0;
        private const int SaveLoadNoOp = 1;
        private const int InitialForcedSelfSet = 2;

        // Classifies the call: the game's own guard (CurSystem == system && !force)
        // marks save-load re-attach no-ops; the same self-set WITH force is the
        // fresh-career initial system assignment.
        public static void Prefix(SimGameState __instance, StarSystem system, bool force, ref int __state)
        {
            if (system != null && __instance.CurSystem == system)
            {
                __state = force ? InitialForcedSelfSet : SaveLoadNoOp;
            }
            else
            {
                __state = NormalArrival;
            }
        }

        public static void Postfix(SimGameState __instance, StarSystem system, int __state)
        {
            if (__state == SaveLoadNoOp) return; // save load: keep the persisted board
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

            if (__state == InitialForcedSelfSet)
            {
                // Fresh career starting on a cantina world: the board is generated
                // above, but the toast stack is not wired yet this deep into
                // _OnAttachUXComplete — AddEventToast NREs (field bug 24.10).
                Core.Log("[H2] initial system set — board refreshed, toast skipped");
                return;
            }

            // Toast only for the route destination — intermediate hops refresh the
            // board silently. When idle (arrived / new campaign) the starmap keeps
            // Destination == CurSystem, so those still toast.
            var destination = __instance.Starmap?.GetDestinationSystem();
            if (destination == null || destination.ID == system.ID)
                Notifications.OnNewJobsAvailable();
        }
    }
}
