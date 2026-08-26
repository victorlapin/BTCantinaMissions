using System;
using System.Collections.Generic;
using System.Text;
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

        /// <summary>Max option buttons rendered at once — more would overflow the popup.</summary>
        private const int MAX_BUTTONS = 5;
        private static int currentPage = 0;
        private static string currentFlavor = "";

        private static readonly string[] boardFlavor =
{
            "The cantina's notice board is cluttered with scribbled job offers.",
            "A wiry bartender nods at the board by the door — fresh work, fresh pay.",
            "Holo-ads flicker over a board thick with mercenary job slips.",
            "The smell of fried synth-protein hangs over a wall of contracts.",
            "Half the board is pin-up holos; the other half is honest work."
        };

        private static readonly string[] rewardFlavor =
        {
            "The bartender slides a cred-chip across the counter.",
            "Payment lands in the company account with a satisfied chime.",
            "\"Pleasure doing business,\" the bartender grins.",
            "Somewhere in the back, a jukebox plays a victory tune."
        };

        private static readonly Random random = new Random();

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

            currentPage = 0;   // fresh open always starts at the first page
            currentFlavor = Flavor(boardFlavor);

            // ── Option stubs ──────────────────────────────
            // No more stubs than the popup can display: pagination renders at most MAX_BUTTONS
            var optionsCount = Math.Min(
                state.Board.Slots.Count + state.ActiveTasks.Count + 1,
                MAX_BUTTONS);
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

        /// <summary>Post-delivery reward popup: shows the full breakdown (CBills +
        /// rolled items). Queued behind the board popup — displayed after Leave.</summary>
        public static void ShowReward(string title, string body)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;

            var sbBody = new StringBuilder();
            sbBody.AppendLine(UIColors.Wrap($"<i>{Flavor(rewardFlavor)}</i>", UIColor.LightGray));
            sbBody.AppendLine();
            sbBody.AppendLine();
            sbBody.Append(body);

            var desc = new BaseDescriptionDef("cantina_reward", title, sbBody.ToString(), "uixTxrSpot_HiringHall");
            var options = new SimGameEventOption[1];
            options[0] = new SimGameEventOption
            {
                Description = new BaseDescriptionDef("cantina_reward_continue", "Continue", "", ""),
                RequirementList = null,
                ResultSets = null
            };

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

        private static string Flavor(string[] pool)
        {
            return pool[random.Next(pool.Length)];
        }

        /// <summary>Reward suffix for a board listing line: "— 150,000 C-Bills + items".</summary>
        private static string RewardSuffix(TaskInstance task)
        {
            var reward = TaskCatalog.GetDef(task.DefId)?.Reward;
            if (reward == null) return "";

            var parts = new StringBuilder();
            if (reward.CBills != 0)
            {
                parts.Append(UIColors.Wrap($"{reward.CBills:N0} C-Bills", UIColor.Gold));
            }
            if (!string.IsNullOrEmpty(reward.ItemCollection))
            {
                if (parts.Length > 0) parts.Append(UIColors.Wrap(" + ", UIColor.LightGray));
                parts.Append(UIColors.Wrap("items", UIColor.LightGray));
            }

            return parts.Length > 0 ? $" — {parts}" : "";
        }

        private static string BuildBody(CampaignState state)
        {
            var sb = new StringBuilder();

            sb.AppendLine(UIColors.Wrap($"<i>{currentFlavor}</i>", UIColor.LightGray));
            sb.AppendLine();
            sb.AppendLine();

            if (state.ActiveTasks.Count >= Core.Settings.MaxActiveTasks)
            {
                sb.AppendLine("<b>Available jobs (limit reached — deliver or abandon a job first):</b>");
            }
            else
            {
                sb.AppendLine("<b>Available jobs:</b>");
            }

            sb.AppendLine();

            if (state.Board?.Slots.Count > 0)
            {
                foreach (var task in state.Board.Slots)
                    sb.AppendLine($"  {task.DisplayString()}{RewardSuffix(task)}");
            }
            else
            {
                sb.AppendLine("  No jobs available.");
            }

            sb.AppendLine();
            sb.AppendLine();

            sb.AppendLine($"<b>Your active jobs (max. {Core.Settings.MaxActiveTasks})</b>:");
            sb.AppendLine();

            if (state.ActiveTasks.Count > 0)
            {
                foreach (var task in state.ActiveTasks)
                {
                    if (task.State == TaskState.ReadyToDeliver)
                        sb.AppendLine(UIColors.Wrap($"  {task.DisplayString()} — READY", UIColor.Green));
                    else
                        sb.AppendLine($"  {task.DisplayString()} — in progress");
                }
            }
            else
            {
                sb.AppendLine("  No active jobs.");
            }

            return sb.ToString();
        }

        public static void MakeOptions(SGEventPanel sgEventPanel)
        {
            var state = Core.State;
            var atLimit = state.ActiveTasks.Count >= Core.Settings.MaxActiveTasks;
            sgEventPanel.eventDescription.SetText(BuildBody(state));

            // Content entries in display order: board offers first, then active jobs
            var entries = new List<OptionEntry>();
            foreach (var task in state.Board.Slots)
            {
                entries.Add(new OptionEntry($"{UIColors.Wrap("[Take]", UIColor.Blue)} {task.ResolvedName}", !atLimit, arg =>
                {
                    var result = Core.State.TryTake(task.InstanceId);
                    Core.Log($"[Board] Take: {result}");
                    if (result == TakeResult.Success) MakeOptions(sgEventPanel);
                }));
            }
            foreach (var task in state.ActiveTasks)
            {
                if (task.State == TaskState.ReadyToDeliver)
                {
                    entries.Add(new OptionEntry($"{UIColors.Wrap("[Deliver]", UIColor.Green)} {task.DisplayString()}", true, arg =>
                    {
                        var ok = RewardService.Deliver(task.InstanceId);
                        Core.Log($"[Board] Deliver: {(ok ? "success" : "failed")}");
                        if (ok) MakeOptions(sgEventPanel);
                    }));
                }
                else
                {
                    entries.Add(new OptionEntry($"{UIColors.Wrap("[Abandon]", UIColor.Red)} {task.DisplayString()}", true, arg =>
                    {
                        var ok = Core.State.Abandon(task.InstanceId);
                        Core.Log($"[Board] Abandon: {(ok ? "success" : "failed")}");
                        if (ok) MakeOptions(sgEventPanel);
                    }));
                }
            }

            // Pagination: Leave is always the last button; on multi-page boards
            // the "Next >>" navigation takes one slot, leaving MAX_BUTTONS - 2 content buttons
            const int fullPage = MAX_BUTTONS - 2;
            int pageCount = entries.Count <= MAX_BUTTONS - 1
                ? 1
                : (entries.Count + fullPage - 1) / fullPage;
            if (currentPage >= pageCount) currentPage = 0;
            int pageSize = pageCount == 1 ? MAX_BUTTONS - 1 : fullPage;
            int start = currentPage * pageSize;

            var optionsList = sgEventPanel.optionsList;
            var index = 0;
            for (int i = start; i < entries.Count && i < start + pageSize; i++)
                SetOption(optionsList[index++], entries[i]);

            if (pageCount > 1)
            {
                SetOption(optionsList[index++], new OptionEntry(
                    $"Switch page ({currentPage + 1} of {pageCount})", true, arg =>
                {
                    currentPage = (currentPage + 1) % pageCount;
                    MakeOptions(sgEventPanel);
                }));
            }

            SetOption(optionsList[index++], new OptionEntry("Leave", true, arg => { sgEventPanel.Dismiss(); }));

            // Hide the leftover stub buttons — their empty frames still stretch the popup
            for (int i = index; i < optionsList.Count; i++)
                optionsList[i].gameObject.SetActive(false);
        }

        /// <summary>Wires the single Continue button of the reward popup.
        /// The body comes from the event's Description.Details (rendered by SetEvent).</summary>
        public static void MakeRewardOptions(SGEventPanel sgEventPanel)
        {
            var optionsList = sgEventPanel.optionsList;
            if (optionsList.Count == 0) return;
            SetOption(optionsList[0], "Continue", true, arg => { sgEventPanel.Dismiss(); });
            for (int i = 1; i < optionsList.Count; i++)
                optionsList[i].gameObject.SetActive(false);
        }

        private static void SetOption(SGEventOption option, OptionEntry entry)
        {
            // a button hidden as a page leftover must come back when a page shows it again
            option.gameObject.SetActive(true);
            SetOption(option, entry.Text, entry.Enabled, entry.Action);
        }

        private static void SetOption(SGEventOption option, string text, bool enabled, UnityAction<SimGameEventOption> action)
        {
            if (enabled)
            {
                option.description.SetText(text);
            }
            else
            {
                option.description.SetText(UIColors.Wrap(text, UIColor.MedGray));
            }
            option.button.enabled = enabled;
            option.OptionSelected.RemoveAllListeners();
            if (enabled) option.OptionSelected.AddListener(action);
        }

        private class OptionEntry
        {
            public readonly string Text;
            public readonly bool Enabled;
            public readonly UnityAction<SimGameEventOption> Action;

            public OptionEntry(string text, bool enabled, UnityAction<SimGameEventOption> action)
            {
                Text = text;
                Enabled = enabled;
                Action = action;
            }
        }
    }
}
