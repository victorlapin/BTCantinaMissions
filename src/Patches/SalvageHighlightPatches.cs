using System;
using BattleTech;
using BattleTech.UI;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>H8: adds a gold outline to salvage items matching active cantina
    /// collect jobs. Hooks AddNewSalvageEntryToWidget — fires for EVERY item
    /// added to the salvage list (initial population AND CustomSalvage
    /// disassembly). SalvageDef is passed directly, no controller navigation.</summary>
    [HarmonyPatch(typeof(AAR_SalvageScreen), nameof(AAR_SalvageScreen.AddNewSalvageEntryToWidget))]
    public static class SalvageItem_CantinaHighlight
    {
        public static void Postfix(AAR_SalvageScreen __instance, SalvageDef salvageDef)
        {
            if (salvageDef == null) return;
            if (UnityGameInstance.BattleTechGame?.Simulation == null) return;
            if (Core.State?.ActiveJobs == null || Core.State.ActiveJobs.Count == 0) return;

            var defId = salvageDef.MechComponentDef?.Description?.Id
                        ?? salvageDef.Description?.Id;
            if (string.IsNullOrEmpty(defId)) return;

            // find the UI item just created for this salvageDef (last in the list)
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

                var isMatch = false;
                if (def.ObjectiveType == ObjectiveType.CollectItems)
                {
                    isMatch = string.Equals(defId, job.ResolvedTarget, StringComparison.OrdinalIgnoreCase);
                }
                else if (def.ObjectiveType == ObjectiveType.CollectMech ||
                         def.ObjectiveType == ObjectiveType.CollectMechParts)
                {
                    isMatch = CheckChassisFamily(defId, job.ResolvedTarget);
                }

                if (!isMatch) continue;

                ApplyOutline(item);
                Core.Debug($"[H8] Cantina highlight: {defId} (job: {job.ResolvedName})");
                return;
            }
        }

        private static void ApplyOutline(InventoryItemElement_NotListView item)
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
}
