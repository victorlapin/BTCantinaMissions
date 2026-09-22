using BattleTech;
using BTCantinaMissions.Domain;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H9 (v0.7): hangar removals — Deliver-mode CollectMech jobs
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

    /// <summary>H9a: readying a stored mech. Suppresses the units mirror for the
    /// whole call — vanilla's ReadyMech scrap-removes the stored stat first and
    /// only then populates ReadyingMechs, so an in-between recount would drop
    /// the unit (field bug 22.09: "readying mech leaves the tracker"). The
    /// finalizer recounts once with the mech safely in ReadyingMechs.</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.ReadyMech))]
    public static class ReadyMechPatch
    {
        public static void Prefix()
        {
            InventoryTracking.SuppressUnitsMirror = true;
        }

        public static void Finalizer()
        {
            InventoryTracking.SuppressUnitsMirror = false;
            InventoryTracking.TrackUnitsChanged();
        }
    }

    /// <summary>H9b: ready completion — ReadyingMechs → ActiveMechs happens
    /// inside ML_ReadyMech without AddMech or scrap, so nothing else fires.
    /// Postfix recounts and the tracker regains the unit.</summary>
    [HarmonyPatch(typeof(SimGameState), "ML_ReadyMech")]
    public static class ML_ReadyMechPatch
    {
        public static void Postfix()
        {
            InventoryTracking.TrackUnitsChanged();
        }
    }

    /// <summary>H9c: cancelling a ready order returns the unit to storage
    /// through refund paths no other hook sees — a cheap catch-all recount.</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.CancelWorkOrder))]
    public static class CancelWorkOrderPatch
    {
        public static void Postfix()
        {
            InventoryTracking.TrackUnitsChanged();
        }
    }
}
