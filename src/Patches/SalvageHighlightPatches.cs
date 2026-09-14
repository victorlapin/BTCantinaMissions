using System;
using System.Collections.Generic;
using BattleTech;
using BattleTech.UI;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>Shared salvage highlight logic: ID extraction, matching, outline
    /// application. Used by both H8 (per-item) and H8b (rescan) hooks.</summary>
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

        /// <summary>Applies the Blue outline border. Cleaned by H8c when the
        /// pooled widget is reused in MechLab/stores (RefreshInfo path).</summary>
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

        private static bool CheckChassisFamily(string mechDefId, string targetFamily)
        {
            if (string.IsNullOrEmpty(targetFamily)) return false;
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var mechDef = sim?.DataManager?.MechDefs?.Get(mechDefId);
            if (mechDef == null) return false;
            var family = ChassisFamilyResolver.GetFamily(mechDef);
            return string.Equals(family, targetFamily, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>H8: fires for EVERY item added to the salvage list — initial
    /// population, CustomSalvage disassembly, leftover additions. Highlights
    /// matching items as they appear.</summary>
    [HarmonyPatch(typeof(AAR_SalvageScreen), nameof(AAR_SalvageScreen.AddNewSalvageEntryToWidget))]
    public static class SalvageItem_CantinaHighlight
    {
        public static void Postfix(AAR_SalvageScreen __instance, SalvageDef salvageDef)
        {
            if (!Core.Settings.SalvageHighlight) return;
            if (salvageDef == null) return;
            if (UnityGameInstance.BattleTechGame?.Simulation == null) return;
            if (Core.State?.ActiveJobs == null || Core.State.ActiveJobs.Count == 0) return;

            var defId = salvageDef.MechComponentDef?.Description?.Id
                        ?? salvageDef.Description?.Id;
            if (string.IsNullOrEmpty(defId)) return;

            var selection = __instance.salvageSelection;
            if (selection == null) return;
            var items = selection.GetSalvageInventory();
            if (items == null || items.Count == 0) return;
            var item = items[items.Count - 1];
            if (item == null) return;

            foreach (var job in Core.State.ActiveJobs)
            {
                var def = JobCatalog.GetDef(job.DefId);
                if (def == null) continue;
                if (!SalvageHighlight.MatchesJob(defId, def, job)) continue;

                SalvageHighlight.ApplyOutline(item);
                Core.Debug($"[H8] Cantina highlight: {defId} (job: {job.ResolvedName})");
                return;
            }
        }
    }

    /// <summary>H8b: re-scan after screen transitions (confirmation, leftover
    /// addition). Re-parenting resets Outline on child Image; postfix restores
    /// highlights so quick-sell doesn't catch cantina targets by accident.</summary>
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
                var defId = SalvageHighlight.GetDefId(item);
                if (string.IsNullOrEmpty(defId)) continue;

                foreach (var job in Core.State.ActiveJobs)
                {
                    var def = JobCatalog.GetDef(job.DefId);
                    if (def == null) continue;
                    if (!SalvageHighlight.MatchesJob(defId, def, job)) continue;

                    SalvageHighlight.ApplyOutline(item);
                    highlighted++;
                    break;
                }
            }

            if (highlighted > 0)
                Core.Debug($"[H8b] Rescan: re-applied {highlighted} outlines");
        }
    }

    /// <summary>H8c: dual-purpose hook on RefreshInfo (fires in MechLab/stores
    /// via the MechComponentRef SetData path, not on salvage's controller path).
    /// With MechLabHighlight off: cleans up outlines from pooled salvage widgets.
    /// With MechLabHighlight on: highlights cantina targets in MechLab/stores —
    /// ComponentRef is available here, no controller navigation needed.</summary>
    [HarmonyPatch(typeof(InventoryItemElement_NotListView), nameof(InventoryItemElement_NotListView.RefreshInfo))]
    public static class SalvageHighlightCleanup
    {
        public static void Postfix(InventoryItemElement_NotListView __instance)
        {
            if (!Core.Settings.MechLabHighlight)
            {
                // cleanup mode: disable outline from pooled salvage reuse
                var outline = __instance.GetComponentInChildren<UnityEngine.UI.Outline>();
                if (outline != null && outline.enabled)
                {
                    outline.enabled = false;
                    Core.Debug("[H8c] Outline cleaned on widget reuse");
                }
                return;
            }

            // highlight mode: check item against cantina jobs
            if (UnityGameInstance.BattleTechGame?.Simulation == null) return;
            if (Core.State?.ActiveJobs == null || Core.State.ActiveJobs.Count == 0) return;

            var defId = __instance.ComponentRef?.ComponentDefID;
            if (string.IsNullOrEmpty(defId))
            {
                // no ID — clean any leftover outline
                var o = __instance.GetComponentInChildren<UnityEngine.UI.Outline>();
                if (o != null && o.enabled) o.enabled = false;
                return;
            }

            foreach (var job in Core.State.ActiveJobs)
            {
                var def = JobCatalog.GetDef(job.DefId);
                if (def == null) continue;
                if (!SalvageHighlight.MatchesJob(defId, def, job)) continue;

                SalvageHighlight.ApplyOutline(__instance);
                return;
            }

            // no match — clean any leftover outline
            var outline2 = __instance.GetComponentInChildren<UnityEngine.UI.Outline>();
            if (outline2 != null && outline2.enabled)
            {
                outline2.enabled = false;
            }
        }

    }
}
