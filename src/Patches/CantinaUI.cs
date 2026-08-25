using System.Collections.Generic;
using BattleTech;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using BattleTech.UI.Tooltips;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    /// <summary>Right-corner button: always "Cantina".
    /// Enabled on cantina planets (if reputation allows), disabled elsewhere.</summary>
    [HarmonyPatch(typeof(SGLocationWidget), nameof(SGLocationWidget.ManageShopButtonState))]
    public static class SGLocationWidget_ManageShopButtonState
    {
        public static void Postfix(SGLocationWidget __instance, StarSystem currSystem)
        {
            var button = AccessTools.Field(typeof(SGLocationWidget), "storeButton")
                .GetValue(__instance) as HBSDOTweenButton;
            if (button == null) return;

            SetButtonText(button, "Cantina");

            var isCantina = currSystem != null && currSystem.Tags.Contains(Core.Settings.PlanetTag);
            SetTooltip(button, isCantina
                ? "Browse the cantina job board"
                : "No cantina on this planet");

            if (isCantina)
            {
                // Same rule as the store: low rep = no service
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var rep = sim.GetReputation(currSystem.OwnerValue);
                button.SetState(rep > SimGameReputation.LOATHED ? ButtonState.Enabled : ButtonState.Disabled, false);
            }
            else
            {
                button.SetState(ButtonState.Disabled, false);
            }
        }

        private static void SetButtonText(HBSDOTweenButton button, string text)
        {
            var label = button.GetComponentInChildren<LocalizableText>();
            label?.SetText(text);
        }

        private static void SetTooltip(HBSDOTweenButton button, string text)
        {
            var tooltip = button.GetComponentInChildren<HBSTooltip>();
            if (tooltip == null) return;
            var data = new HBSTooltipStateData();
            data.SetString(text);
            tooltip.SetDefaultStateData(data);
        }
    }


    /// <summary>Intercepts the Store button click on cantina planets to show the cantina event popup.</summary>
    [HarmonyPatch(typeof(SGLocationWidget), nameof(SGLocationWidget.ReceiveButtonPress))]
    public static class SGLocationWidget_ReceiveButtonPress
    {
        public static bool Prefix(string button)
        {
            Core.Debug($"[Cantina UI] Button pressed: {button}");
            if (button != "Store") return true;
            if (!IsCantinaPlanetEnabled) return true;

            CantinaPopup.Show();
            return false;
        }

        private static bool IsCantinaPlanetEnabled
        {
            get
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var system = sim?.CurSystem;
                if (system == null) return false;
                if (!system.Tags.Contains(Core.Settings.PlanetTag)) return false;
                return sim.GetReputation(system.OwnerValue) > SimGameReputation.LOATHED;
            }
        }
    }

    /// <summary>Extends SGEventPanel's prebuilt option-button pool (cloning the first
    /// button) when an event has more options than the pool holds. Keeps the panel in
    /// its vanilla reuse mode — buttons are Init'ed and deactivated by ClearOptions,
    /// which avoids orphaned placeholder buttons from the dynamic PrefabCache path.</summary>
    [HarmonyPatch(typeof(SGEventPanel), nameof(SGEventPanel.SetEvent))]
    public static class SGEventPanel_SetEvent
    {
        public static void Prefix(SGEventPanel __instance, SimGameEventDef evt)
        {
            if (evt?.Options == null) return;

            if (!(AccessTools.Field(typeof(SGEventPanel), "availableOptionButtons")
                .GetValue(__instance) is List<SGEventOption> avail) || avail.Count == 0) return;

            var created = 0;
            while (avail.Count < evt.Options.Length)
            {
                var template = avail[0];
                var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = template.gameObject.name;
                avail.Add(clone.GetComponent<SGEventOption>());
                created++;
            }
            if (created > 0)
                Core.Debug($"[UI] Extended SGEventPanel option pool by {created} for {evt.Options.Length} options");
        }
    }

    /// <summary>H8: handles cantina option selection (Take / Deliver / Leave) from event popup.</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.OnEventOptionSelected))]
    public static class SimGameState_OnEventOptionSelected
    {
        public static void Postfix(SimGameEventOption option)
        {
            var id = option?.Description?.Id;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("cantina_")) return;

            Core.Debug($"[H8] Cantina option: {id}");

            if (id == "cantina_leave") return;

            if (id.StartsWith("cantina_take_"))
            {
                var instanceId = id.Substring("cantina_take_".Length);
                var result = Core.State.TryTake(instanceId);
                Core.Log($"[H8] Take: {result}");
                // Re-show only on success — a second press (double-click) must not queue another popup
                if (result == TakeResult.Success) CantinaPopup.Show();
            }
            else if (id.StartsWith("cantina_deliver_"))
            {
                var instanceId = id.Substring("cantina_deliver_".Length);
                var ok = Core.State.Deliver(instanceId);
                Core.Log($"[H8] Deliver: {(ok ? "success" : "failed")}");
                if (ok) CantinaPopup.Show();
            }
            else if (id.StartsWith("cantina_abandon_"))
            {
                var instanceId = id.Substring("cantina_abandon_".Length);
                var ok = Core.State.Abandon(instanceId);
                Core.Log($"[H8] Abandon: {(ok ? "success" : "failed")}");
                if (ok) CantinaPopup.Show();
            }
        }
    }
}
