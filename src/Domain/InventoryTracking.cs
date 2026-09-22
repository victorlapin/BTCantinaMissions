using System;
using BattleTech;
using BTCantinaMissions.UI;

namespace BTCantinaMissions.Domain
{
    /// <summary>Shared logic of the H3/H3a inventory hooks: turns raw AddItemStat /
    /// RemoveItemStat events into CollectItems job progress (and reversals for Deliver
    /// jobs), so the patch classes stay thin one-liners over all four method overloads.</summary>
    internal static class InventoryTracking
    {
        /// <summary>Some inventory call sites (notably shop sell) pass the normalized stat
        /// key ("Item.JumpJetDef.Gear_...") instead of the raw def id; job targets store the
        /// raw form. Strips the "Item.{type}." prefix (and a .DAMAGED suffix if present) so
        /// comparisons always see the canonical id.</summary>
        private static string Normalize(string id)
        {
            if (id == null) return id;
            if (id.EndsWith(".DAMAGED", StringComparison.Ordinal))
                id = id.Substring(0, id.Length - ".DAMAGED".Length);
            if (!id.StartsWith("Item.", StringComparison.Ordinal)) return id;

            var rest = id.Substring("Item.".Length);
            var cut = rest.IndexOf('.');
            return cut < 0 ? rest : rest.Substring(cut + 1);
        }

        internal static void TrackItemAdded(string id)
        {
            id = Normalize(id);
            var jobs = Core.State.ActiveJobs;
            if (jobs.Count == 0) return;

            foreach (var job in jobs)
            {
                // exact target match first: a def's pool may hold several item kinds, but the
                // job instance tracks only its own resolved target
                if (job.ResolvedTarget != id) continue;
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectItems) continue;

                if (def.ItemMode == ItemModeType.Deliver)
                {
                    // Deliver progress mirrors the live inventory — see TrackItemRemoved
                    if (!SyncFromInventory(job, def)) continue;
                }
                else
                {
                    // Acquire is one-way: a monotonic counter, never decremented
                    job.AddProgress(1);
                }

                Core.Log($"[H3] CollectItems progress: {job.ResolvedName} ({job.Progress}/{job.TargetCount})");
                Notifications.OnProgress(job);
            }
        }

        internal static void TrackItemRemoved(string id)
        {
            id = Normalize(id);
            var jobs = Core.State.ActiveJobs;
            if (jobs.Count == 0) return;

            foreach (var job in jobs)
            {
                if (job.ResolvedTarget != id) continue;
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectItems) continue;
                if (def.ItemMode != ItemModeType.Deliver) continue;

                // Deliver progress mirrors the live inventory, not a ±1 delta: stock above
                // the target keeps the job ready when single items are sold or installed
                var wasReady = job.State == JobState.ReadyToDeliver;
                if (!SyncFromInventory(job, def)) continue;

                Core.Log($"[H3a] CollectItems reversal: {job.ResolvedName} ({job.Progress}/{job.TargetCount})");
                if (wasReady && job.State == JobState.Taken && Core.Settings.NotifyOnReady)
                    Notifications.OnReverted(job);
            }
        }

        /// <summary>Recomputes a Deliver job's progress from the live inventory (postfixes
        /// run after the stat change, so the count is already up to date). Returns true only
        /// when progress or state actually moved — callers log/notify only then, so stock
        /// churn above the target stays silent.</summary>
        private static bool SyncFromInventory(JobInstance job, CantinaJobDef def)
        {
            var before = job.Progress;
            var stateBefore = job.State;
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            job.SyncProgress(ItemCatalog.GetInventoryCount(sim, def, job));
            return job.Progress != before || job.State != stateBefore;
        }

        /// <summary>v0.7: a MECHPART stat changed — Deliver-mode CollectMechParts
        /// jobs of the matching family re-mirror their progress from the live
        /// part count (sold, scrapped, consumed by an assembly).</summary>
        internal static void TrackPartChanged(string rawId)
        {
            var jobs = Core.State.ActiveJobs;
            if (jobs.Count == 0) return;

            var family = FamilyInventory.StatIdToFamily(rawId);
            if (family == null) return;

            foreach (var job in jobs)
            {
                if (!string.Equals(job.ResolvedTarget, family, StringComparison.OrdinalIgnoreCase)) continue;
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectMechParts) continue;
                if (def.ItemMode != ItemModeType.Deliver) continue;

                var wasReady = job.State == JobState.ReadyToDeliver;
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var before = job.Progress;
                job.SyncProgress(FamilyInventory.CountParts(sim, family));

                if (job.Progress != before)
                    Core.Log($"[H3a] CollectMechParts reversal: {job.ResolvedName} ({job.Progress}/{job.TargetCount})");
                if (wasReady && job.State == JobState.Taken && Core.Settings.NotifyOnReady)
                    Notifications.OnReverted(job);
            }
        }

        /// <summary>Set while SimGameState.ReadyMech runs: vanilla removes the
        /// stored stat via ScrapInactiveMech BEFORE populating ReadyingMechs, so
        /// the H7 recount in that gap would see the mech nowhere and wrongly
        /// reverse Deliver jobs. ReadyMech's finalizer clears it and recounts.</summary>
        internal static bool SuppressUnitsMirror;

        /// <summary>v0.7: hangar composition changed (AddMech / scrap / ready
        /// transitions) — Deliver-mode CollectMech jobs re-mirror from the live
        /// unit count.</summary>
        internal static void TrackUnitsChanged()
        {
            if (SuppressUnitsMirror) return;
            var jobs = Core.State.ActiveJobs;
            if (jobs.Count == 0) return;

            var sim = UnityGameInstance.BattleTechGame.Simulation;
            foreach (var job in jobs)
            {
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectMech) continue;
                if (def.ItemMode != ItemModeType.Deliver) continue;

                var wasReady = job.State == JobState.ReadyToDeliver;
                var before = job.Progress;
                job.SyncProgress(FamilyInventory.CountUnits(sim, job.ResolvedTarget));

                if (job.Progress != before)
                    Core.Log($"[H9] CollectMech mirror: {job.ResolvedName} ({job.Progress}/{job.TargetCount})");
                if (wasReady && job.State == JobState.Taken && Core.Settings.NotifyOnReady)
                    Notifications.OnReverted(job);
            }
        }
    }
}
