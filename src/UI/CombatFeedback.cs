using System.Collections.Generic;
using BattleTech;
using BattleTech.Framework;
using BattleTech.UI;
using BTCantinaMissions.Domain;
using HBS.Collections;

namespace BTCantinaMissions.UI
{
    /// <summary>Combat-side cantina feedback: floaties over killed targets and a
    /// post-mission summary. The kill matching mirrors H6's logic but runs at
    /// HandleDeath time (not CompleteContract) so the player sees feedback in
    /// real time; the actual progress is still applied only in H6.</summary>
    internal static class CombatFeedback
    {
        /// <summary>Running count of cantina-relevant kills per job in the current
        /// combat — lets the floatie show a live "3/5" without waiting for H6.
        /// Cleared alongside PlayerKillTracker.</summary>
        private static readonly Dictionary<string, int> sessionKills = new Dictionary<string, int>();

        /// <summary>Per-combat snapshot of job progress at mission start (the sim
        /// state value); floaties show sessionKills + this baseline.</summary>
        private static readonly Dictionary<string, int> baselineProgress = new Dictionary<string, int>();

        /// <summary>Called at combat start (first H5 kill) to snapshot baselines.</summary>
        private static bool baselined;

        internal static void Reset()
        {
            sessionKills.Clear();
            baselineProgress.Clear();
            baselined = false;
            trackedCombat = null;
        }

        /// <summary>Combat reference of the session that's currently baselined —
        /// detects a new mission (different Combat instance) and resets state.</summary>
        private static BattleTech.CombatGameState trackedCombat;

        /// <summary>Called from H5/H5a after a kill enters the tracker: checks
        /// active DestroyTagged / DestroyChassis jobs, publishes a floatie over
        /// the victim, and bumps the per-combat counter.</summary>
        internal static void OnKill(AbstractActor victim)
        {
            if (Core.State?.ActiveJobs == null || Core.State.ActiveJobs.Count == 0) return;

            // new combat → wipe the previous session's counters, re-baseline
            if (trackedCombat != victim.Combat)
            {
                sessionKills.Clear();
                baselineProgress.Clear();
                trackedCombat = victim.Combat;
                baselined = false;
            }

            if (!baselined)
            {
                foreach (var job in Core.State.ActiveJobs)
                    baselineProgress[job.InstanceId] = job.Progress;
                baselined = true;
            }

            var tags = BuildTagSet(victim);
            var family = BuildFamily(victim);
            if (tags == null && family == null) return;

            foreach (var job in Core.State.ActiveJobs)
            {
                var def = JobCatalog.GetDef(job.DefId);
                if (def == null) continue;

                var isMatch = false;
                if (def.ObjectiveType == ObjectiveType.DestroyTagged && tags != null)
                {
                    isMatch = BoardGenerator.MatchesTarget(tags, job.ResolvedTarget);
                }
                else if (def.ObjectiveType == ObjectiveType.DestroyChassis && family != null)
                {
                    isMatch = string.Equals(family, job.ResolvedTarget, System.StringComparison.OrdinalIgnoreCase);
                }

                if (!isMatch) continue;

                sessionKills.TryGetValue(job.InstanceId, out var session);
                session++;
                sessionKills[job.InstanceId] = session;

                baselineProgress.TryGetValue(job.InstanceId, out var baseline);
                var total = baseline + session;

                // session tracking continues even with floaties disabled (AAR needs it)
                if (Core.Settings.CombatFloaties)
                {
                    var text = total >= job.TargetCount
                        ? UIColors.Wrap($"Cantina: {job.ResolvedName} — COMPLETE!", UIColor.Green)
                        : UIColors.Wrap($"Cantina: {job.ResolvedName} {total}/{job.TargetCount}", UIColor.Blue);
                    PublishFloatie(victim, text);
                    Core.Debug($"[CombatFeedback] {text} over {victim.Description.Name}");
                }
            }
        }

        /// <summary>Builds the summary toast shown after combat (via H6 → the
        /// existing Notifications pipeline). Returns null if no cantina kills.</summary>
        internal static string BuildCombatSummary()
        {
            if (sessionKills.Count == 0) return null;

            var lines = new List<string>();
            foreach (var job in Core.State.ActiveJobs)
            {
                if (!sessionKills.TryGetValue(job.InstanceId, out var kills) || kills == 0) continue;
                baselineProgress.TryGetValue(job.InstanceId, out var baseline);
                var total = baseline + kills;
                lines.Add(total >= job.TargetCount
                    ? UIColors.Wrap($"  {job.ResolvedName}: COMPLETE ({total}/{job.TargetCount})", UIColor.Green)
                    : $"  {job.ResolvedName}: +{kills} → {total}/{job.TargetCount}");
            }

            if (lines.Count == 0) return null;
            return "Cantina objectives:\n" + string.Join("\n", lines);
        }

        /// <summary>AAR entries: both completed AND partial progress (Other Results
        /// panel shows text lines, no Failed/Success binary — partial is fine here).</summary>
        internal static List<AAREntry> BuildAAREntries()
        {
            var result = new List<AAREntry>();
            if (sessionKills.Count == 0) return result;

            foreach (var job in Core.State.ActiveJobs)
            {
                if (!sessionKills.TryGetValue(job.InstanceId, out var kills) || kills == 0) continue;
                baselineProgress.TryGetValue(job.InstanceId, out var baseline);
                var total = baseline + kills;
                result.Add(new AAREntry
                {
                    JobInstanceId = job.InstanceId,
                    Title = total >= job.TargetCount
                        ? $"{job.ResolvedName} — COMPLETE ({total}/{job.TargetCount})"
                        : $"{job.ResolvedName} progress: +{kills} → {total}/{job.TargetCount}",
                    Completed = total >= job.TargetCount
                });
            }
            return result;
        }

        internal class AAREntry
        {
            internal string JobInstanceId;
            internal string Title;
            internal bool Completed;
        }

        private static void PublishFloatie(AbstractActor victim, string text)
        {
            var combat = victim.Combat;
            if (combat?.MessageCenter == null) return;
            combat.MessageCenter.PublishMessage(
                new FloatieMessage(victim.GUID, victim.GUID, text, FloatieMessage.MessageNature.Buff));
        }

        private static TagSet BuildTagSet(AbstractActor actor)
        {
            var tags = new TagSet();

            var mech = actor as Mech;
            if (mech?.MechDef?.Chassis != null)
            {
                foreach (var t in mech.MechDef.Chassis.ChassisTags) tags.Add(t);
                foreach (var t in mech.MechDef.MechTags) tags.Add(t);
                return tags;
            }

            var vehicle = actor as Vehicle;
            if (vehicle?.VehicleDef != null)
            {
                foreach (var t in vehicle.VehicleDef.VehicleTags) tags.Add(t);
                return tags;
            }

            var turret = actor as Turret;
            if (turret?.TurretDef != null)
            {
                foreach (var t in turret.TurretDef.TurretTags) tags.Add(t);
                return tags;
            }

            return null;
        }

        private static string BuildFamily(AbstractActor actor)
        {
            var mech = actor as Mech;
            return mech?.MechDef != null ? ChassisFamilyResolver.GetFamily(mech.MechDef) : null;
        }
    }
}
