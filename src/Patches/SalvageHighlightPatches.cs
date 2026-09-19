using System;
using BattleTech;
using BattleTech.UI;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>Shared salvage highlight logic: ID extraction, job matching,
    /// outline application. ProcessItem is the single per-item entry point;
    /// the H8c/H8e/H8f/H8b hooks below are thin wrappers over it, each
    /// covering a different point in the widget lifecycle.</summary>
    internal static class SalvageHighlight
    {
        internal static string GetDefId(InventoryItemElement_NotListView item)
        {
            var ctrl = item.controller;
            if (ctrl == null) return null;
            var salvageDef = ctrl.GetType().GetField("salvageDef")?.GetValue(ctrl) as SalvageDef;
            if (salvageDef == null) return null;
            return salvageDef.MechComponentDef?.Description?.Id
                   ?? salvageDef.Description?.Id;
        }

        internal static bool MatchesJob(string defId, CantinaJobDef def, JobInstance job)
        {
            if (def.ObjectiveType == ObjectiveType.CollectItems)
            {
                return string.Equals(defId, job.ResolvedTarget, StringComparison.OrdinalIgnoreCase);
            }
            if (def.ObjectiveType == ObjectiveType.CollectMech ||
                def.ObjectiveType == ObjectiveType.CollectMechParts)
            {
                return CheckChassisFamily(defId, job.ResolvedTarget);
            }
            return false;
        }

        /// <summary>True when outlining should be applied. The hooks are
        /// shared across screens (salvage, MechLab), so either flag enables
        /// it; per-item clearing always runs to keep pooled widgets
        /// clean.</summary>
        internal static bool ShouldHighlight =>
            Core.Settings.SalvageHighlight || Core.Settings.MechLabHighlight;

        internal static void ApplyOutline(InventoryItemElement_NotListView item)
        {
            var images = item.GetComponentsInChildren<UnityEngine.UI.Image>();
            UnityEngine.UI.Image target = null;
            foreach (var img in images)
            {
                if (img != item.iconMech && img.transform.parent == item.transform)
                {
                    target = img;
                    break;
                }
            }
            if (target == null && images.Length > 0) target = images[0];
            if (target == null) return;

            var outline = target.GetComponent<UnityEngine.UI.Outline>()
                          ?? target.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = UIColors.Color(UIColor.Blue);
            outline.effectDistance = new UnityEngine.Vector2(2f, -2f);
            outline.enabled = true;
        }

        internal static void ClearOutline(InventoryItemElement_NotListView item)
        {
            var outline = item.GetComponentInChildren<UnityEngine.UI.Outline>();
            if (outline != null && outline.enabled)
            {
                outline.enabled = false;
            }
        }

        /// <summary>Per-item matching: resolves the def ID, matches it against
        /// active jobs, applies or clears the outline. Salvage-screen widgets
        /// (ListElementController_Salvage*) resolve through their salvageDef
        /// only — CustomSalvage leaves stale ComponentRefs on pooled and
        /// reused widgets (a disassembled mech's cockpit card can still carry
        /// the previous salvage screen's cockpit ref), producing false
        /// matches. MechLab and other screens have no salvageDef and rely on
        /// ComponentRef. Returns true when outlined.</summary>
        internal static bool ProcessItem(InventoryItemElement_NotListView item)
        {
            if (Core.State?.ActiveJobs == null || Core.State.ActiveJobs.Count == 0)
            {
                ClearOutline(item);
                return false;
            }

            var salvageController = item.controller is ListElementController_SalvageFullMech_NotListView
                                 || item.controller is ListElementController_SalvageMechPart_NotListView
                                 || item.controller is ListElementController_SalvageGear_NotListView
                                 || item.controller is ListElementController_SalvageWeapon_NotListView;
            var defId = salvageController
                ? GetDefId(item)
                : item.ComponentRef?.ComponentDefID ?? GetDefId(item);
            if (string.IsNullOrEmpty(defId))
            {
                ClearOutline(item);
                return false;
            }

            foreach (var job in Core.State.ActiveJobs)
            {
                var def = JobCatalog.GetDef(job.DefId);
                if (def == null) continue;
                if (!MatchesJob(defId, def, job)) continue;

                if (ShouldHighlight)
                {
                    ApplyOutline(item);
                    Core.Debug($"[Salvage] Cantina highlight: {defId} (job: {job.ResolvedName})");
                }
                return true;
            }

            ClearOutline(item);
            return false;
        }

        private static bool CheckChassisFamily(string mechDefId, string targetFamily)
        {
            if (string.IsNullOrEmpty(targetFamily)) return false;
            // salvage items are mostly gear/weapon ids — skip the DataManager
            // round-trip for anything that can't be a mechdef
            if (!mechDefId.StartsWith("mechdef_", StringComparison.OrdinalIgnoreCase)) return false;
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (sim?.DataManager?.MechDefs == null ||
                !sim.DataManager.MechDefs.TryGet(mechDefId, out MechDef mechDef))
            {
                return false;
            }
            var family = ChassisFamilyResolver.GetFamily(mechDef);
            return string.Equals(family, targetFamily, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>H8c: fires for EVERY item on ANY screen that uses
    /// InventoryItemElement_NotListView (salvage, MechLab). It runs inside
    /// SetData, BEFORE the controller is handed its salvageDef — so on the
    /// salvage screen H8c alone can only CLEAR stale outlines; the actual
    /// highlighting there is completed by H8f/H8e/H8b once controller data is
    /// available. For MechLab items (ComponentRef set by the game) H8c is the
    /// primary and sufficient hook.</summary>
    [HarmonyPatch(typeof(InventoryItemElement_NotListView), nameof(InventoryItemElement_NotListView.SetTooltipData))]
    public static class SalvageItem_CantinaHighlight
    {
        public static void Postfix(InventoryItemElement_NotListView __instance)
        {
            if (UnityGameInstance.BattleTechGame?.Simulation == null) return;
            SalvageHighlight.ProcessItem(__instance);
        }
    }

    /// <summary>H8e: leftover items created AFTER salvage confirmation.
    /// SalvageConfirmed() builds fresh widgets for every leftover via
    /// AddNewSalvageLeftover → InitAndCreate, where SetData (and thus
    /// SetTooltipData/H8c) runs BEFORE the controller's salvageDef is
    /// assigned — H8c sees an empty controller and can't resolve the ID.
    /// AddLeftovers receives the exact widget after the controller is fully
    /// initialized.</summary>
    [HarmonyPatch(typeof(AAR_SalvageChosen), nameof(AAR_SalvageChosen.AddLeftovers))]
    public static class SalvageLeftover_CantinaHighlight
    {
        public static void Postfix(InventoryItemElement_NotListView item)
        {
            if (!Core.Settings.SalvageHighlight) return;
            if (UnityGameInstance.BattleTechGame?.Simulation == null) return;

            SalvageHighlight.ProcessItem(item);
        }
    }

    /// <summary>H8f: every item added to any MechLabInventoryWidget list —
    /// the salvage selection screen, MechLab inventory, and every mid-screen
    /// list rebuild (CustomSalvage mech disassembly rebuilds the whole
    /// salvage list through this). Unlike H8c this fires AFTER the
    /// controller is fully initialized (InitAndCreate completes before
    /// OnAddItem), so salvageDef is readable and GetDefId works for freshly
    /// created widgets.</summary>
    [HarmonyPatch(typeof(MechLabInventoryWidget), nameof(MechLabInventoryWidget.OnAddItem))]
    public static class SalvageListAdd_CantinaHighlight
    {
        public static void Postfix(IMechLabDraggableItem item)
        {
            if (UnityGameInstance.BattleTechGame?.Simulation == null) return;

            var widget = item as InventoryItemElement_NotListView;
            if (widget == null) return;

            SalvageHighlight.ProcessItem(widget);
        }
    }

    /// <summary>H8b: full rescan when the salvage screen opens — SetInitialText
    /// fires once after the initial list population, when all controllers are
    /// ready. It is the primary highlight pass for the selection screen (H8c
    /// fires too early there); it also re-applies highlights on reopen and
    /// clears non-matching items.</summary>
    [HarmonyPatch(typeof(AAR_SalvageChosen), nameof(AAR_SalvageChosen.SetInitialText))]
    public static class SalvageRescan_CantinaHighlight
    {
        public static void Postfix()
        {
            if (!Core.Settings.SalvageHighlight) return;
            if (UnityGameInstance.BattleTechGame?.Simulation == null) return;
            if (Core.State?.ActiveJobs == null || Core.State.ActiveJobs.Count == 0) return;

            var selection = UnityEngine.Object.FindObjectOfType<AAR_SalvageSelection>();
            if (selection == null) return;
            var items = selection.GetSalvageInventory();
            if (items == null || items.Count == 0) return;

            var highlighted = 0;
            foreach (var item in items)
            {
                if (SalvageHighlight.ProcessItem(item)) highlighted++;
            }

            Core.Debug($"[H8b] Rescan: {highlighted} outlined, {items.Count - highlighted} cleared");
        }
    }
}
