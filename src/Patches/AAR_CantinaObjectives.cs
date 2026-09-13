using BattleTech;
using BattleTech.Framework;
using BattleTech.UI;
using BTCantinaMissions.UI;
using HarmonyLib;
using UnityEngine;

namespace BTCantinaMissions.Patches
{
    /// <summary>H7: appends cantina objective lines to the After Action Report's
    /// objectives widget (same prefab the game uses). Postfix on FillInObjectives
    /// fires after vanilla objectives are placed — our lines appear below them.</summary>
    [HarmonyPatch(typeof(AAR_ContractObjectivesWidget), "FillInObjectives")]
    public static class AAR_CantinaObjectivesPatch
    {
        private const string PrefabId = "uixPrfPanl_SIM_objective-element";

        public static void Postfix(AAR_ContractObjectivesWidget __instance)
        {
            var sim = UnityGameInstance.BattleTechGame?.Simulation;
            if (sim == null) return;

            // build summary lines from jobs with combat kills this session
            var entries = CombatFeedback.BuildAAREntries();
            if (entries == null || entries.Count == 0) return;

            var dm = sim.DataManager;
            var parent = __instance.ObjectiveListParent;

            foreach (var entry in entries)
            {
                var go = dm.PooledInstantiate(
                    PrefabId, BattleTechResourceType.UIModulePrefabs, null, null, parent);
                if (go == null)
                {
                    Core.Debug("[H7] AAR prefab pooled-instantiate failed");
                    continue;
                }
                go.transform.localScale = Vector3.one;

                var item = go.GetComponent<AAR_ObjectiveListItem>();
                if (item == null)
                {
                    Core.Debug("[H7] AAR_ObjectiveListItem not found on prefab");
                    continue;
                }

                var result = new MissionObjectiveResult(
                    entry.Title,
                    $"cantina_{entry.JobInstanceId}",
                    false,          // not primary — shows as optional
                    true,           // displayToUser
                    entry.Completed ? ObjectiveStatus.Succeeded : ObjectiveStatus.Active,
                    false);         // not a contract objective

                item.Init(result, sim, UnityGameInstance.BattleTechGame.Combat?.ActiveContract);
                Core.Debug($"[H7] AAR line: {entry.Title} (status={result.status})");
            }
        }
    }
}
