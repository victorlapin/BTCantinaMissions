using System;
using BattleTech;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H3: tracks items entering the player's inventory (CollectItems progress).
    /// Fires in bulk on salvage/shop — cheapest checks first, dictionary lookups only on match.
    /// Both AddItemStat overloads must be patched: they are self-contained (neither calls
    /// the other), and callers are split between them — salvage/parts use the string-type
    /// one, shop purchases (AddFromShopDefItem) the Type one.</summary>
    [HarmonyPatch(typeof(SimGameState), "AddItemStat", new Type[] { typeof(string), typeof(string), typeof(bool) })]
    public static class AddItemStatStringPatch
    {
        public static void Postfix(SimGameState __instance, string id, string type, bool damaged)
        {
            // parts are H4a's exclusive territory: AddMechPart calls AddItemStat(id, "MECHPART",
            // ...) internally, so every salvaged part would light up both hooks; CollectItems
            // targets are ComponentType-scoped and can never match a part id anyway
            if (type == "MECHPART") return;

            Core.Debug($"[H3] AddItemStat: {id} ({type}, damaged={damaged})");
            InventoryTracking.TrackItemAdded(id);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "AddItemStat", new Type[] { typeof(string), typeof(Type), typeof(bool) })]
    public static class AddItemStatTypePatch
    {
        public static void Postfix(SimGameState __instance, string id, Type type, bool damaged)
        {
            Core.Debug($"[H3] AddItemStat: {id} ({type.Name}, damaged={damaged})");
            InventoryTracking.TrackItemAdded(id);
        }
    }

    /// <summary>H3a: mirrors H3 for removals — Deliver-mode CollectItems jobs lose
    /// progress when the collected items leave the inventory (sold, installed).
    /// Acquire jobs are one-way by design: the goal was reached once already.
    /// Both RemoveItemStat overloads are patched for the same reason as H3: parts
    /// consumed by AddMechPart go through the private string-type overload.</summary>
    [HarmonyPatch(typeof(SimGameState), "RemoveItemStat", new Type[] { typeof(string), typeof(Type), typeof(bool) })]
    public static class RemoveItemStatTypePatch
    {
        public static void Postfix(SimGameState __instance, string id, Type type, bool damaged)
        {
            Core.Debug($"[H3a] RemoveItemStat: {id} ({type.Name}, damaged={damaged}), activeJobs={Core.State.ActiveJobs.Count}");
            InventoryTracking.TrackItemRemoved(id);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "RemoveItemStat", new Type[] { typeof(string), typeof(string), typeof(bool) })]
    public static class RemoveItemStatStringPatch
    {
        public static void Postfix(SimGameState __instance, string id, string type, bool damaged)
        {
            // see AddItemStatStringPatch: part stats churn (assembly consumes N parts at
            // once) must not spam the log — CollectItems jobs can't hold part targets
            if (type == "MECHPART") return;

            Core.Debug($"[H3a] RemoveItemStat: {id} ({type}, damaged={damaged}), activeJobs={Core.State.ActiveJobs.Count}");
            InventoryTracking.TrackItemRemoved(id);
        }
    }

    /// <summary>H4: tracks complete mechs entering the bay (CollectMech progress).</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.AddMech))]
    public static class AddMechPatch
    {
        public static void Postfix(SimGameState __instance, int idx, MechDef mech,
            bool active, bool forcePlacement, bool displayMechPopup, string mechAddedHeader = null)
        {
            if (mech?.Chassis == null) return;

            // entry diagnostics: shows fake LT vehicles (fake_vehicle in MechTags) in the log
            Core.Debug($"[H4] AddMech: {mech.Description.Id} chassis={mech.Chassis.Description.Id} " +
                       $"fakeVehicle={mech.MechTags.Contains("fake_vehicle") || mech.Chassis.ChassisTags.Contains("fake_vehicle_chassis")}");

            var jobs = Core.State.ActiveJobs;
            if (jobs.Count == 0) return;

            foreach (var job in jobs)
            {
                if (job.DefId == null) continue;
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectMech) continue;
                if (!ChassisFamilyResolver.MatchesFamily(mech, job.ResolvedTarget)) continue;

                job.AddProgress(1);
                Core.Log($"[H4] CollectMech progress: {job.ResolvedName} ({job.Progress}/{job.TargetCount})");
                Notifications.OnProgress(job);
            }
        }
    }

    /// <summary>H4a: tracks salvage parts entering inventory (CollectMechParts progress).</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.AddMechPart))]
    public static class AddMechPartPatch
    {
        public static void Postfix(SimGameState __instance, string id)
        {
            Core.Debug($"[H4a] AddMechPart: {id}");

            var jobs = Core.State.ActiveJobs;
            if (jobs.Count == 0) return;

            string defId;
            if (id.StartsWith("vehiclechassisdef_", StringComparison.Ordinal))
                defId = "vehicledef_" + id.Substring("vehiclechassisdef_".Length);
            else if (id.StartsWith("chassisdef_", StringComparison.Ordinal))
                defId = "mechdef_" + id.Substring("chassisdef_".Length);
            else
                defId = id;

            var dm = UnityGameInstance.BattleTechGame.Simulation.DataManager;

            if (!dm.MechDefs.TryGet(defId, out MechDef mechDef)) return;
            var family = ChassisFamilyResolver.GetFamily(mechDef);
            if (family == null) return;

            foreach (var job in jobs)
            {
                if (job.DefId == null) continue;
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectMechParts) continue;
                if (!string.Equals(job.ResolvedTarget, family, StringComparison.OrdinalIgnoreCase)) continue;

                job.AddProgress(1);
                Core.Log($"[H4a] CollectMechParts progress: {job.ResolvedName} ({job.Progress}/{job.TargetCount})");
                Notifications.OnProgress(job);
            }
        }
    }
}
