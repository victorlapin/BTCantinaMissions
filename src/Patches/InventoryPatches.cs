using System;
using BattleTech;
using BTCantinaMissions.Domain;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H3: tracks items entering the player's inventory (CollectItems progress).</summary>
    [HarmonyPatch(typeof(SimGameState), "AddItemStat", new Type[] { typeof(string), typeof(string), typeof(bool) })]
    public static class AddItemStatPatch
    {
        public static void Postfix(SimGameState __instance, string id, string type, bool damaged)
        {
            foreach (var task in Core.State.ActiveTasks)
            {
                if (task.DefId == null) continue;
                var def = TaskCatalog.GetDef(task.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectItems) continue;
                if (def.ItemPool == null || !def.ItemPool.Contains(id)) continue;

                task.AddProgress(1);
                Core.Log($"[H3] CollectItems progress: {task.ResolvedName} ({task.Progress}/{task.TargetCount})");
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

            foreach (var task in Core.State.ActiveTasks)
            {
                if (task.DefId == null) continue;
                var def = TaskCatalog.GetDef(task.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectMech) continue;
                if (!ChassisFamilyResolver.MatchesFamily(mech, task.ResolvedTarget)) continue;

                task.AddProgress(1);
                Core.Log($"[H4] CollectMech progress: {task.ResolvedName} ({task.Progress}/{task.TargetCount})");
            }
        }
    }

    /// <summary>H4a: tracks salvage parts entering inventory (CollectMechParts progress).</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.AddMechPart))]
    public static class AddMechPartPatch
    {
        public static void Postfix(SimGameState __instance, string id)
        {
            // id is the chassisdef id, e.g. "chassisdef_locust_LCT-1V"
            // TODO: AssemblyVariant custom component lookup
            // For now: check file name
            var mechId = id.Replace("chassisdef_", "mechdef_");

            if (!Core.DM.MechDefs.TryGet(mechId, out MechDef mechDef)) return;
            var family = ChassisFamilyResolver.GetFamily(mechDef);
            if (family == null) return;

            foreach (var task in Core.State.ActiveTasks)
            {
                if (task.DefId == null) continue;
                var def = TaskCatalog.GetDef(task.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectMechParts) continue;
                if (!string.Equals(task.ResolvedTarget, family, StringComparison.OrdinalIgnoreCase)) continue;

                task.AddProgress(1);
                Core.Log($"[H4a] CollectMechParts progress: {task.ResolvedName} ({task.Progress}/{task.TargetCount})");
            }
        }
    }
}
