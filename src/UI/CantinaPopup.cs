using BattleTech;
using BattleTech.UI;
using BTCantinaMissions.Domain;
using UnityEngine.Events;

namespace BTCantinaMissions.UI
{
    /// <summary>Cantina UI via SGEventPanel: vertical options, H8 intercept for Take/Deliver.</summary>
    public static class CantinaPopup
    {
        private static readonly SimGameEventTracker eventTracker = new SimGameEventTracker();
        private static bool isTrackerReady = false;

        /// <summary>Event popup without the SIM_GAME_EVENT_RESOLVED autosave that
        /// QueueEventPopup attaches to every event (the board is not a real event).</summary>
        private class BoardPopupEntry : SimGameInterruptManager.EventPopupEntry
        {
            private static readonly SimGameInterruptManager.Entry[] NoSideEffects =
                new SimGameInterruptManager.Entry[0];

            public BoardPopupEntry(SimGameEventDef evt, EventScope scope, SimGameEventTracker tracker)
                : base(evt, scope, tracker) { }

            public override SimGameInterruptManager.Entry[] SideEffectEntries => NoSideEffects;
        }

        public static void Show()
        {
            var state = Core.State;
            var sim = UnityGameInstance.BattleTechGame.Simulation;

            if (Core.State.Board == null)
            {
                // could happen after loading a save without initialized board
                Core.State.Board = new SystemBoard();
                BoardGenerator.RefreshBoard(sim.CurSystem);
            }

            // ── Option stubs ──────────────────────────────
            var optionsCount = state.Board.Slots.Count + state.ActiveTasks.Count + 1;
            var options = new SimGameEventOption[optionsCount];
            for (int i = 0; i < optionsCount; i++)
            {
                options[i] = new SimGameEventOption
                {
                    Description = new BaseDescriptionDef($"cantina_option_{i}", $"cantina_option_{i}", "", ""),
                    RequirementList = null,
                    ResultSets = null
                };
            }

            // ── Build event ───────────────────────────────
            var desc = new BaseDescriptionDef("cantina_board", "Cantina", "", "uixTxrSpot_HiringHall");

            var evt = new SimGameEventDef(
                SimGameEventDef.EventPublishState.PUBLISHED,
                SimGameEventDef.SimEventType.UNSELECTABLE,
                EventScope.Company,
                desc,
                new RequirementDef { Scope = EventScope.Company },
                new RequirementDef[0],
                new SimGameEventObject[0],
                options,
                0, true, null);

            if (!isTrackerReady)
            {
                eventTracker.Init(
                    new EventScope[] { EventScope.Company },
                    0f, 0f,
                    SimGameEventDef.SimEventType.NORMAL,
                    sim);
                isTrackerReady = true;
            }

            sim.InterruptQueue.AddInterrupt(new BoardPopupEntry(evt, EventScope.Company, eventTracker), true);
        }

        private static string BuildBody(CampaignState state)
        {
            var sb = new System.Text.StringBuilder();

            if (state.ActiveTasks.Count > 0)
            {
                sb.AppendLine("Your active jobs:");
                sb.AppendLine();
                foreach (var task in state.ActiveTasks)
                {
                    if (task.State == TaskState.ReadyToDeliver)
                        sb.AppendLine($"<color=green>  {task.DisplayString()} — READY</color>");
                    else
                        sb.AppendLine($"  {task.DisplayString()} — in progress");
                }
                sb.AppendLine();
                sb.AppendLine($"({state.ActiveTasks.Count}/{Core.Settings.MaxActiveTasks} slots used)");
            }

            if (state.Board?.Slots.Count > 0 && sb.Length > 0)
                sb.AppendLine();

            if (state.Board?.Slots.Count > 0)
            {
                if (state.ActiveTasks.Count >= Core.Settings.MaxActiveTasks)
                {
                    sb.AppendLine("Available jobs (limit reached — deliver a job first):");
                }
                else
                {
                    sb.AppendLine("Available jobs:");
                    sb.AppendLine();
                }
                foreach (var task in state.Board.Slots)
                    sb.AppendLine($"  {task.DisplayString()}");
            }
            else
            {
                sb.AppendLine("No jobs available. The board refreshes monthly.");
            }

            return sb.ToString();
        }

        public static void MakeOptions(SGEventPanel sgEventPanel)
        {
            var state = Core.State;
            var atLimit = state.ActiveTasks.Count >= Core.Settings.MaxActiveTasks;
            sgEventPanel.eventDescription.SetText(BuildBody(state));

            var optionsList = sgEventPanel.optionsList;
            var optionsCount = optionsList.Count;
            var index = 0;

            foreach (var task in state.Board.Slots)
            {
                SetOption(optionsList[index], $"Take: {task.DisplayString()}", !atLimit, arg =>
                {
                    var result = Core.State.TryTake(task.InstanceId);
                    Core.Log($"[H8] Take: {result}");
                    if (result == TakeResult.Success) MakeOptions(sgEventPanel);
                });
                index++;
            }

            foreach (var task in state.ActiveTasks)
            {
                if (task.State == TaskState.ReadyToDeliver)
                {
                    SetOption(optionsList[index], $"Deliver: {task.DisplayString()}", true, arg =>
                    {
                        var ok = Core.State.Deliver(task.InstanceId);
                        Core.Log($"[H8] Deliver: {(ok ? "success" : "failed")}");
                        if (ok) MakeOptions(sgEventPanel);
                    });
                    index++;
                }
                else
                {
                    SetOption(optionsList[index], $"Abandon: {task.DisplayString()}", true, arg =>
                    {
                        var ok = Core.State.Abandon(task.InstanceId);
                        Core.Log($"[H8] Abandon: {(ok ? "success" : "failed")}");
                        if (ok) MakeOptions(sgEventPanel);
                    });
                    index++;
                }
            }

            for (int i = index; i < optionsCount; i++)
            {
                if (i == optionsCount - 1)
                {
                    SetOption(optionsList[i], "Leave", true, arg => { sgEventPanel.Dismiss(); });
                }
                else
                {
                    SetOption(optionsList[i], "---", false, arg => { });
                }
            }
        }

        private static void SetOption(SGEventOption option, string text, bool enabled, UnityAction<SimGameEventOption> action)
        {
            option.description.SetText(text);
            option.button.enabled = enabled;
            option.OptionSelected.RemoveAllListeners();
            if (enabled) option.OptionSelected.AddListener(action);
        }
    }
}
