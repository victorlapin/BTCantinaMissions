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

                task.AddProgress(1, def.TargetCount);
                Core.Log($"[H3] CollectItems progress: {task.ResolvedName} ({task.Progress}/{def.TargetCount})");
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
                if (!ChassisMatchesFamily(mech, task.ResolvedTarget)) continue;

                task.AddProgress(1, def.TargetCount);
                Core.Log($"[H4] CollectMech progress: {task.ResolvedName} ({task.Progress}/{def.TargetCount})");
            }
        }

        private static bool ChassisMatchesFamily(MechDef mech, string family)
        {
            // Resolution: check unit_chassis tag → AssemblyVariant → PrefabIdentifier
            var tags = mech.MechTags;
            if (tags.Contains($"unit_chassis_{family}")) return true;

            var chassis = mech.Chassis;
            if (chassis == null) return false;

            // TODO: AssemblyVariant custom component lookup
            // For now: check PrefabBase
            var prefab = chassis.PrefabBase;
            if (!string.IsNullOrEmpty(prefab) && prefab.ToLower() == family.ToLower())
                return true;

            return false;
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
            var family = ExtractFamily(id);
            if (family == null) return;

            foreach (var task in Core.State.ActiveTasks)
            {
                if (task.DefId == null) continue;
                var def = TaskCatalog.GetDef(task.DefId);
                if (def?.ObjectiveType != ObjectiveType.CollectMechParts) continue;
                if (!string.Equals(task.ResolvedTarget, family, System.StringComparison.OrdinalIgnoreCase)) continue;

                task.AddProgress(1, def.TargetCount);
                Core.Log($"[H4a] CollectMechParts progress: {task.ResolvedName} ({task.Progress}/{def.TargetCount})");
            }
        }

        /// <summary>Extracts the chassis family from a chassisdef id like "chassisdef_locust_LCT-1V" → "locust".</summary>
        private static string ExtractFamily(string chassisDefId)
        {
            if (string.IsNullOrEmpty(chassisDefId) || !chassisDefId.StartsWith("chassisdef_")) return null;
            var rest = chassisDefId.Substring("chassisdef_".Length);
            var parts = rest.Split('_');
            return parts.Length > 0 ? parts[0] : null;
        }
    }
}
