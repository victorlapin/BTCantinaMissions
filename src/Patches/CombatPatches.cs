using System.Collections.Generic;
using BattleTech;
using BTCantinaMissions.Domain;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H5-H6: scans dead hostile units at contract completion and applies progress
    /// to active DestroyUnits tasks. Works regardless of mission result.</summary>
    [HarmonyPatch(typeof(Contract), nameof(Contract.CompleteContract))]
    public static class CompleteContractPatch
    {
        public static void Postfix(Contract __instance, MissionResult result, bool isGoodFaithEffort)
        {
            var combat = UnityGameInstance.BattleTechGame?.Combat;
            if (combat == null) return;

            var playerTeam = combat.LocalPlayerTeam;
            if (playerTeam == null) return;

            var deadTags = new List<List<string>>();
            foreach (var actor in combat.AllActors)
            {
                if (actor == null || !(actor.IsDead || actor.IsFlaggedForDeath)) continue;
                if (!playerTeam.IsEnemy(actor.team)) continue;

                var tags = new List<string>();
                var mech = actor as Mech;
                var vehicle = actor as Vehicle;

                if (mech?.MechDef?.Chassis != null)
                {
                    foreach (var t in mech.MechDef.Chassis.ChassisTags) tags.Add(t);
                    foreach (var t in mech.MechDef.MechTags) tags.Add(t);
                }
                else if (vehicle?.VehicleDef != null)
                {
                    foreach (var t in vehicle.VehicleDef.VehicleTags) tags.Add(t);
                }

                deadTags.Add(tags);
            }

            if (deadTags.Count == 0) return;
            Core.Log($"[H5-H6] Mission ended ({result}), {deadTags.Count} hostile units destroyed");

            foreach (var task in Core.State.ActiveTasks)
            {
                var def = TaskCatalog.GetDef(task.DefId);
                if (def?.ObjectiveType != ObjectiveType.DestroyUnits) continue;

                var count = 0;
                foreach (var tags in deadTags)
                {
                    var tagSet = new HBS.Collections.TagSet();
                    foreach (var t in tags) tagSet.Add(t);
                    if (BoardGenerator.MatchesTarget(tagSet, task.ResolvedTarget))
                        count++;
                }

                if (count > 0)
                {
                    task.AddProgress(count);
                    Core.Log($"[H5-H6] {task.ResolvedName}: +{count} → {task.Progress}/{task.TargetCount}");
                }
            }
        }
    }
}
