using System;
using BattleTech;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H3: tracks items entering the player's inventory (CollectItems progress).
    /// Fires in bulk on salvage/shop — cheapest checks first, dictionary lookups only on match.</summary>
    [HarmonyPatch(typeof(SimGameState), "AddItemStat", new Type[] { typeof(string), typeof(string), typeof(bool) })]
    public static class AddItemStatPatch
    {
        public static void Postfix(SimGameState __instance, string id, string type, bool damaged)
        {
            var jobs = Core.State.ActiveJobs;
            if (jobs.Count == 0) return;

            foreach (var job in jobs)
            {
                // exact target match first: a def's pool may hold several item kinds, but the
                // job instance tracks only its own resolved target
                if (job.ResolvedTarget != id) continue;
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectItems) continue;

                job.AddProgress(1);
                Core.Log($"[H3] CollectItems progress: {job.ResolvedName} ({job.Progress}/{job.TargetCount})");
                Notifications.OnProgress(job);
            }
        }
    }

    /// <summary>H3a: mirrors H3 for removals — Deliver-mode CollectItems jobs lose
    /// progress when the collected items leave the inventory (sold, installed).
    /// Acquire jobs are one-way by design: the goal was reached once already.</summary>
    [HarmonyPatch(typeof(SimGameState), "RemoveItemStat", new Type[] { typeof(string), typeof(Type), typeof(bool) })]
    public static class RemoveItemStatPatch
    {
        public static void Postfix(SimGameState __instance, string id, Type type, bool damaged)
        {
            var jobs = Core.State.ActiveJobs;
            if (jobs.Count == 0) return;

            foreach (var job in jobs)
            {
                if (job.ResolvedTarget != id) continue;
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectItems) continue;
                if (def.ItemMode != ItemModeType.Deliver) continue;

                var wasReady = job.State == JobState.ReadyToDeliver;
                job.RemoveProgress(1);
                Core.Log($"[H3a] CollectItems reversal: {job.ResolvedName} ({job.Progress}/{job.TargetCount})");
                if (wasReady && job.State == JobState.Taken && Core.Settings.NotifyOnReady)
                    Notifications.OnReverted(job);
            }
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
            var jobs = Core.State.ActiveJobs;
            if (jobs.Count == 0) return;

            // id is the chassisdef id, e.g. "chassisdef_locust_LCT-1V"
            var mechId = id.Replace("chassisdef_", "mechdef_");
            var dm = UnityGameInstance.BattleTechGame.Simulation.DataManager;

            if (!dm.MechDefs.TryGet(mechId, out MechDef mechDef)) return;
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
