using System.Collections.Generic;
using BattleTech;
using BTCantinaMissions.Domain;

namespace BTCantinaMissions.UI
{
    /// <summary>Cantina UI via SGEventPanel: vertical options, H8 intercept for Take/Deliver.</summary>
    public static class CantinaPopup
    {
        public static void Show()
        {
            var state = Core.State;
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var options = new List<SimGameEventOption>();

            // ── Board: Take options ────────────────────────
            var atLimit = state.ActiveTasks.Count >= Core.Settings.MaxActiveTasks;
            if (state.Board?.Slots.Count > 0 && !atLimit)
            {
                foreach (var task in state.Board.Slots)
                {
                    options.Add(CreateOption(
                        $"cantina_take_{task.InstanceId}",
                        $"Take: {task.DisplayString()}"));
                }
            }

            // ── Active: Deliver / Abandon options ─────────
            foreach (var task in state.ActiveTasks)
            {
                if (task.State == TaskState.ReadyToDeliver)
                    options.Add(CreateOption(
                        $"cantina_deliver_{task.InstanceId}",
                        $"Deliver: {task.DisplayString()}"));
                else
                    options.Add(CreateOption(
                        $"cantina_abandon_{task.InstanceId}",
                        $"Abandon: {task.DisplayString()}"));
            }

            // ── Leave ─────────────────────────────────────
            options.Add(CreateOption("cantina_leave", "Leave"));

            // ── Build event ───────────────────────────────
            var body = BuildBody(state);
            var desc = new BaseDescriptionDef("cantina_board", "Cantina", body, "uixTxrSpot_HiringHall");

            var evt = new SimGameEventDef(
                SimGameEventDef.EventPublishState.PUBLISHED,
                SimGameEventDef.SimEventType.NORMAL,
                EventScope.Company,
                desc,
                default,
                new RequirementDef[0],
                new SimGameEventObject[0],
                options.ToArray(),
                0, true, null);

            var tracker = new SimGameEventTracker();
            tracker.Init(
                new EventScope[] { EventScope.Company },
                0f, 0f,
                SimGameEventDef.SimEventType.NORMAL,
                sim);
            sim.OnEventTriggered(evt, EventScope.Company, tracker);
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

        private static SimGameEventOption CreateOption(string id, string name)
        {
            return new SimGameEventOption
            {
                Description = new BaseDescriptionDef(id, name, "", ""),
                RequirementList = new RequirementDef[0],
                ResultSets = new SimGameEventResultSet[]
                {
                    new SimGameEventResultSet
                    {
                        Description = new BaseDescriptionDef(id + "_result", name, "", ""),
                        Weight = 1,
                        Results = new SimGameEventResult[0]
                    }
                }
            };
        }
    }
}
