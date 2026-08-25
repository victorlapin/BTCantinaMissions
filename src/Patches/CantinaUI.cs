using BattleTech;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using BattleTech.UI.Tooltips;
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
}
