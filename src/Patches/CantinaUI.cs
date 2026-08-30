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
                : "Review your active cantina jobs");

            if (isCantina)
            {
                // Same rule as the store: low rep = no service
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var rep = sim.GetReputation(currSystem.OwnerValue);
                button.SetState(rep > SimGameReputation.LOATHED ? ButtonState.Enabled : ButtonState.Disabled, false);
            }
            else
            {
                // Ledger mode: your own contracts are reviewable (and deliverable)
                // anywhere — only the job board itself needs a cantina
                button.SetState(ButtonState.Enabled, false);
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


    /// <summary>Intercepts the Store button click everywhere: cantina planets open the
    /// full board, other worlds open the ledger (active jobs only).</summary>
    [HarmonyPatch(typeof(SGLocationWidget), nameof(SGLocationWidget.ReceiveButtonPress))]
    public static class SGLocationWidget_ReceiveButtonPress
    {
        public static bool Prefix(string button)
        {
            Core.Debug($"[UI] Button pressed: {button}");
            if (button != "Store") return true;

            CantinaPopup.Show();
            return false;
        }
    }

    /// <summary>Esc on our popups: vanilla HandleEscapeKeypress dismisses only the RESULT
    /// screen — in the options state it swallows the key. Prefix dismisses cantina events
    /// directly (same path as the Leave/Continue buttons) and reports the key handled.</summary>
    [HarmonyPatch(typeof(SGEventPanel), nameof(SGEventPanel.HandleEscapeKeypress))]
    public static class SGEventPanel_HandleEscapeKeypress
    {
        public static bool Prefix(SGEventPanel __instance, ref bool __result)
        {
            // the current event def sits in the interrupt entry's parameters (see
            // EventPopupEntry ctor); SetEvent itself does not store the def anywhere
            var entry = __instance.thisEntry;
            var def = (entry?.parameters?.Count > 0) ? entry.parameters[0] as SimGameEventDef : null;
            var id = def?.Description?.Id;
            if (id != "cantina_board" && id != "cantina_reward") return true; // vanilla behavior

            Core.Debug($"[UI] Esc dismissed {id}");
            __instance.Dismiss();
            __result = true;
            return false;
        }
    }

    /// <summary>Body line spacing for cantina popups. The panel is shared with
    /// vanilla events, so the bump must be applied for ours and restored for
    /// everything else — the original value is captured once on first use.</summary>
    internal static class EventBodySpacing
    {
        internal const float Factor = 2f;
        private static float? vanilla;

        internal static void Apply(SGEventPanel panel, bool ours)
        {
            var text = panel?.eventDescription;
            if (text == null) return;
            if (vanilla == null) vanilla = text.lineSpacing;
            text.lineSpacing = ours ? vanilla.Value * Factor : vanilla.Value;
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
            var evtId = evt?.Description?.Id;
            EventBodySpacing.Apply(__instance,
                evtId == "cantina_board" || evtId == "cantina_reward");

            // Restore vanilla options buttons state
            if (__instance.optionsList != null)
                foreach (var btn in __instance.optionsList)
                    if (btn != null && btn.button != null)
                        btn.button.enabled = true;

            if (evtId != "cantina_board") return;
            if (evt?.Options == null) return;

            var avail = __instance.availableOptionButtons;
            var inUse = __instance.optionsList;
            if (avail == null || inUse == null) return;

            // SetEvent returns the in-use buttons to the pool (ClearOptions) before taking
            // them again — count both. At prefix time the pool can be empty while all
            // buttons of the previous show are still in optionsList (Dismiss does not
            // call ClearOptions), so cloning from avail alone is not enough.
            var effective = avail.Count + inUse.Count;
            if (effective == 0) return; // no button to clone a template from

            var created = 0;
            while (effective < evt.Options.Length)
            {
                var template = (avail.Count > 0 ? avail[0] : inUse[0]).gameObject;
                var clone = Object.Instantiate(template, template.transform.parent);
                clone.name = template.name;
                avail.Add(clone.GetComponent<SGEventOption>());
                effective++;
                created++;
            }
            if (created > 0)
                Core.Debug($"[UI] Extended SGEventPanel option pool by {created} for {evt.Options.Length} options");
        }

        public static void Postfix(SGEventPanel __instance, SimGameEventDef evt)
        {
            var id = evt?.Description?.Id;
            if (id == "cantina_board")
            {
                Core.Debug("[UI] Board event started");
                CantinaPopup.MakeOptions(__instance);
            }
            else if (id == "cantina_reward")
            {
                Core.Debug("[UI] Reward event started");
                CantinaPopup.MakeRewardOptions(__instance);
            }
        }
    }

    /// <summary>Flushes toasts held while the sim room UI was torn down
    /// (combat / loading) once the room is ready again.</summary>
    [HarmonyPatch(typeof(SGRoomManager), nameof(SGRoomManager.OnSimGameReady))]
    public static class SGRoomManager_OnSimGameReady
    {
        public static void Postfix()
        {
            Notifications.Flush();
        }
    }
}
