using System.Collections.Generic;
using BattleTech;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;
using HBS.Collections;

namespace BTCantinaMissions.Patches
{
    /// <summary>H5-H6: scans dead hostile units at contract completion and applies progress
    /// to active DestroyUnits jobs. Works regardless of mission result.</summary>
    [HarmonyPatch(typeof(Contract), nameof(Contract.CompleteContract))]
    public static class CompleteContractPatch
    {
        public static void Postfix(Contract __instance, MissionResult result, bool isGoodFaithEffort)
        {
            var combat = UnityGameInstance.BattleTechGame?.Combat;
            if (combat == null) return;

            var playerTeam = combat.LocalPlayerTeam;
            if (playerTeam == null) return;

            var deadTags = new List<TagSet>();
            foreach (var actor in combat.AllActors)
            {
                if (actor == null || !(actor.IsDead || actor.IsFlaggedForDeath)) continue;
                if (!playerTeam.IsEnemy(actor.team)) continue;

                var mech = actor as Mech;
                var vehicle = actor as Vehicle;

                if (mech?.MechDef?.Chassis != null)
                {
                    Core.Debug($"[H5-H6] Found destroyed mech: {mech.MechDef.Chassis.Description.Name} ({mech.MechDef.Description.Id})");
                    var tags = new TagSet();
                    foreach (var t in mech.MechDef.Chassis.ChassisTags) tags.Add(t);
                    foreach (var t in mech.MechDef.MechTags) tags.Add(t);
                    deadTags.Add(tags);
                }
                else if (vehicle?.VehicleDef != null)
                {
                    Core.Debug($"[H5-H6] Found destroyed vehicle: {vehicle.VehicleDef.Chassis.Description.Name} ({vehicle.VehicleDef.Description.Id})");
                    var tags = new TagSet();
                    foreach (var t in vehicle.VehicleDef.VehicleTags) tags.Add(t);
                    deadTags.Add(tags);
                }
            }

            if (deadTags.Count == 0) return;
            Core.Log($"[H5-H6] Mission ended ({result}), {deadTags.Count} hostile units destroyed");

            foreach (var job in Core.State.ActiveJobs)
            {
                var def = JobCatalog.GetDef(job.DefId);
                if (def?.ObjectiveType != ObjectiveType.DestroyUnits) continue;

                var count = 0;
                foreach (var tagSet in deadTags)
                {
                    if (BoardGenerator.MatchesTarget(tagSet, job.ResolvedTarget))
                        count++;
                }

                if (count > 0)
                {
                    job.AddProgress(count);
                    Core.Log($"[H5-H6] {job.ResolvedName}: +{count} → {job.Progress}/{job.TargetCount}");
                    Notifications.OnProgress(job);
                }
            }
        }
    }
}
