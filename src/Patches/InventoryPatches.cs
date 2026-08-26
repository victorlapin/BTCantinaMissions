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
            var tasks = Core.State.ActiveTasks;
            if (tasks.Count == 0) return;

            foreach (var task in tasks)
            {
                // exact target match first: a def's pool may hold several item kinds, but the
                // task instance tracks only its own resolved target
                if (task.ResolvedTarget != id) continue;
                var def = TaskCatalog.GetDef(task.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectItems) continue;

                task.AddProgress(1);
                Core.Log($"[H3] CollectItems progress: {task.ResolvedName} ({task.Progress}/{task.TargetCount})");
                Notifications.OnProgress(task);
            }
        }
    }

    /// <summary>H3a: mirrors H3 for removals — Deliver-mode CollectItems tasks lose
    /// progress when the collected items leave the inventory (sold, installed).
    /// Acquire tasks are one-way by design: the goal was reached once already.</summary>
    [HarmonyPatch(typeof(SimGameState), "RemoveItemStat", new Type[] { typeof(string), typeof(Type), typeof(bool) })]
    public static class RemoveItemStatPatch
    {
        public static void Postfix(SimGameState __instance, string id, Type type, bool damaged)
        {
            var tasks = Core.State.ActiveTasks;
            if (tasks.Count == 0) return;

            foreach (var task in tasks)
            {
                if (task.ResolvedTarget != id) continue;
                var def = TaskCatalog.GetDef(task.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectItems) continue;
                if (def.ItemMode != ItemModeType.Deliver) continue;

                var wasReady = task.State == TaskState.ReadyToDeliver;
                task.RemoveProgress(1);
                Core.Log($"[H3a] CollectItems reversal: {task.ResolvedName} ({task.Progress}/{task.TargetCount})");
                if (wasReady && task.State == TaskState.Taken && Core.Settings.NotifyOnReady)
                    Notifications.OnReverted(task);
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

            var tasks = Core.State.ActiveTasks;
            if (tasks.Count == 0) return;

            foreach (var task in tasks)
            {
                if (task.DefId == null) continue;
                var def = TaskCatalog.GetDef(task.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectMech) continue;
                if (!ChassisFamilyResolver.MatchesFamily(mech, task.ResolvedTarget)) continue;

                task.AddProgress(1);
                Core.Log($"[H4] CollectMech progress: {task.ResolvedName} ({task.Progress}/{task.TargetCount})");
                Notifications.OnProgress(task);
            }
        }
    }

    /// <summary>H4a: tracks salvage parts entering inventory (CollectMechParts progress).</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.AddMechPart))]
    public static class AddMechPartPatch
    {
        public static void Postfix(SimGameState __instance, string id)
        {
            var tasks = Core.State.ActiveTasks;
            if (tasks.Count == 0) return;

            // id is the chassisdef id, e.g. "chassisdef_locust_LCT-1V"
            var mechId = id.Replace("chassisdef_", "mechdef_");
            var dm = UnityGameInstance.BattleTechGame.Simulation.DataManager;

            if (!dm.MechDefs.TryGet(mechId, out MechDef mechDef)) return;
            var family = ChassisFamilyResolver.GetFamily(mechDef);
            if (family == null) return;

            foreach (var task in tasks)
            {
                if (task.DefId == null) continue;
                var def = TaskCatalog.GetDef(task.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectMechParts) continue;
                if (!string.Equals(task.ResolvedTarget, family, StringComparison.OrdinalIgnoreCase)) continue;

                task.AddProgress(1);
                Core.Log($"[H4a] CollectMechParts progress: {task.ResolvedName} ({task.Progress}/{task.TargetCount})");
                Notifications.OnProgress(task);
            }
        }
    }
}
