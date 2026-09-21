using BattleTech;
using BTCantinaMissions.Domain;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H7 (v0.7): hangar removals — Deliver-mode CollectMech jobs
    /// re-mirror from the live unit count when the player scraps a unit
    /// (active or stored) or scraps parts. Delivery itself removes the unit
    /// AFTER the job left the active list, so these hooks never see our own
    /// removals — same ordering trick as RewardService item delivery.
    /// CustomSalvage patches these methods too (binary references); postfix
    /// ordering with CS is irrelevant — both react to the same end state.</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.ScrapActiveMech))]
    public static class ScrapActiveMechPatch
    {
        public static void Postfix()
        {
            InventoryTracking.TrackUnitsChanged();
        }
    }

    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.ScrapInactiveMech))]
    public static class ScrapInactiveMechPatch
    {
        public static void Postfix()
        {
            InventoryTracking.TrackUnitsChanged();
        }
    }

    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.ScrapMechPart))]
    public static class ScrapMechPartPatch
    {
        public static void Postfix(string id)
        {
            // ScrapMechPart routes through RemoveItemStat("MECHPART") internally,
            // which TrackPartChanged already handles — this hook is just the
            // belt-and-suspenders for modded call paths that bypass the stats
            InventoryTracking.TrackPartChanged(id.Replace("chassisdef", "mechdef"));
        }
    }
}
