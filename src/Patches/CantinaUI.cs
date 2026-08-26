using BattleTech;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using BattleTech.UI.Tooltips;
using BTCantinaMissions.UI;
using HarmonyLib;
using UnityEngine;

namespace BTCantinaMissions.Patches
{
    /// <summary>Right-corner button: always "Cantina".
    /// Enabled on cantina planets (if reputation allows), disabled elsewhere.</summary>
    [HarmonyPatch(typeof(SGLocationWidget), nameof(SGLocationWidget.ManageShopButtonState))]
    public static class SGLocationWidget_ManageShopButtonState
    {
        public static void Postfix(SGLocationWidget __instance, StarSystem currSystem)
        {
            var button = __instance.storeButton;
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
            Core.Debug($"[UI] Button pressed: {button}");
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
            if (evt?.Description?.Id != "cantina_board") return;
            if (evt?.Options == null) return;

            var avail = __instance.availableOptionButtons;
            if (avail == null || avail.Count == 0) return;

            var created = 0;
            while (avail.Count < evt.Options.Length)
            {
                var template = avail[0];
                var clone = Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = template.gameObject.name;
                avail.Add(clone.GetComponent<SGEventOption>());
                created++;
            }
            if (created > 0)
                Core.Debug($"[UI] Extended SGEventPanel option pool by {created} for {evt.Options.Length} options");
        }

        public static void Postfix(SGEventPanel __instance, SimGameEventDef evt)
        {
            if (evt?.Description?.Id != "cantina_board") return;

            Core.Debug($"[UI] Event started");
            CantinaPopup.MakeOptions(__instance);
        }
    }
}
