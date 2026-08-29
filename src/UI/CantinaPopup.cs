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

        /// <summary>Mode of the popup currently being shown: full board on cantina
        /// worlds, ledger (active jobs only) everywhere else.</summary>
        private static bool currentIsCantina;

        private static readonly string[] boardFlavor =
{
            "The cantina's notice board is cluttered with scribbled job offers.",
            "A wiry bartender nods at the board by the door — fresh work, fresh pay.",
            "Holo-ads flicker over a board thick with mercenary job slips.",
            "The smell of fried synth-protein hangs over a wall of contracts.",
            "Half the board is pin-up holos; the other half is honest work."
        };

        private static readonly string[] ledgerFlavor =
        {
            "A dog-eared contract ledger, updated with every HPG ping.",
            "Your fixer's confirmation chits, filed by date and payoff.",
            "Merc work never sleeps — the ledger keeps the receipts.",
            "The company's open contracts, far from any cantina wall."
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

            var system = sim.CurSystem;
            currentIsCantina = system != null && system.Tags.Contains(Core.Settings.PlanetTag);

            if (Core.State.Board == null)
            {
                // could happen after loading a save without initialized board;
                // generate offers only on cantina worlds — the ledger works
                // with an empty board just fine
                Core.State.Board = new SystemBoard();
                if (currentIsCantina) BoardGenerator.RefreshBoard(system);
            }

            currentPage = 0;   // fresh open always starts at the first page
            currentFlavor = Flavor(currentIsCantina ? boardFlavor : ledgerFlavor);

            // ── Option stubs ──────────────────────────────
            // No more stubs than the popup can display: pagination renders at most MAX_BUTTONS
            var slots = currentIsCantina ? state.Board.Slots.Count : 0;
            var optionsCount = Math.Min(
                slots + state.ActiveJobs.Count + 1,
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
            var desc = new BaseDescriptionDef("cantina_board", "Cantina Jobs", "", "uixTxrSpot_HiringHall");

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
        public static void ShowReward(JobInstance job, int cbills, string itemsBlock)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;

            var body = new StringBuilder();
            body.AppendLine(UIColors.Wrap($"<i>{Flavor(rewardFlavor)}</i>", UIColor.LightGray));
            body.AppendLine();
            body.AppendLine();
            body.AppendLine("<b>Job completed:</b>");
            body.AppendLine();
            body.AppendLine($"  {job.ResolvedName}");
            body.AppendLine();
            body.AppendLine();
            body.AppendLine("<b>You receive:</b>");
            body.AppendLine();
            body.AppendLine($"  {UIColors.Wrap(SimGameState.GetCBillString(cbills), UIColor.Gold)}");
            if (!string.IsNullOrEmpty(itemsBlock))
                body.Append(itemsBlock);

            var desc = new BaseDescriptionDef("cantina_reward", "Cantina Reward", body.ToString(), "uixTxrSpot_HiringHall");
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

        /// <summary>Offered-job line: CollectItems shows the player's current inventory
        /// count instead of a dead 0/9 — what you see is what gets seeded on Take.</summary>
        private static string OfferedDisplayString(JobInstance job)
        {
            var def = JobCatalog.GetDef(job.DefId);
            if (def?.ObjectiveType == ObjectiveType.CollectItems)
            {
                var have = ItemCatalog.GetInventoryCount(
                    UnityGameInstance.BattleTechGame.Simulation, def, job);
                if (have > 0)
                    return $"{job.ResolvedName} ({Math.Min(have, job.TargetCount)}/{job.TargetCount})";
            }
            return job.DisplayString();
        }

        /// <summary>Deliver item jobs clamp progress at the target, hiding stock above it;
        /// surfaces the real count in the board body: "· 15 in stock".</summary>
        private static string StockSuffix(JobInstance job)
        {
            var def = JobCatalog.GetDef(job.DefId);
            if (def?.ObjectiveType != ObjectiveType.CollectItems || def.ItemMode != ItemModeType.Deliver)
                return "";

            var stock = ItemCatalog.GetInventoryCount(
                UnityGameInstance.BattleTechGame.Simulation, def, job);
            return stock > job.TargetCount
                ? UIColors.Wrap($" · {stock} in stock", UIColor.LightGray)
                : "";
        }

        /// <summary>Payment string for job lines: "¢320,000 + items" (gold c-bills,
        /// light-gray item note). Single source of reward formatting.</summary>
        private static string PaymentString(JobInstance job)
        {
            var reward = JobCatalog.GetDef(job.DefId)?.Reward;
            if (reward == null) return "";

            var scale = RewardService.EconomyScale(UnityGameInstance.BattleTechGame.Simulation);

            var sb = new StringBuilder();
            if (reward.CBills != 0)
            {
                var scaledCBills = UnityEngine.Mathf.RoundToInt(reward.CBills * scale);
                sb.Append(UIColors.Wrap(SimGameState.GetCBillString(scaledCBills), UIColor.Gold));
            }
            if (!string.IsNullOrEmpty(reward.ItemCollection))
            {
                if (sb.Length > 0) sb.Append(UIColors.Wrap(" + ", UIColor.LightGray));
                sb.Append(UIColors.Wrap("items", UIColor.LightGray));
            }
            return sb.ToString();
        }

        /// <summary>Reward suffix for a board listing line: "— ¢320,000 + items".</summary>
        private static string RewardSuffix(JobInstance job)
        {
            var payment = PaymentString(job);
            return payment.Length > 0 ? $" — {payment}" : "";
        }

        private static string BuildBody(CampaignState state)
        {
            var sb = new StringBuilder();

            sb.AppendLine(UIColors.Wrap($"<i>{currentFlavor}</i>", UIColor.LightGray));
            sb.AppendLine();
            sb.AppendLine();

            if (currentIsCantina)
            {
                var inTransit = UnityGameInstance.BattleTechGame.Simulation?.TravelState
                    != SimGameTravelStatus.IN_SYSTEM;

                if (state.ActiveJobs.Count >= Core.Settings.MaxActiveJobs)
                {
                    sb.AppendLine("<b>Available jobs (limit reached — deliver or abandon a job first):</b>");
                }
                else if (inTransit)
                {
                    sb.AppendLine("<b>Available jobs (in transit — land to take them):</b>");
                }
                else
                {
                    sb.AppendLine("<b>Available jobs:</b>");
                }

                sb.AppendLine();

                if (state.Board?.Slots.Count > 0)
                {
                    foreach (var job in state.Board.Slots)
                        sb.AppendLine($"  {OfferedDisplayString(job)}{RewardSuffix(job)}");
                }
                else
                {
                    sb.AppendLine("  No jobs available.");
                }

                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine($"<b>Your active jobs (max. {Core.Settings.MaxActiveJobs})</b>:");
            sb.AppendLine();

            if (state.ActiveJobs.Count > 0)
            {
                foreach (var job in state.ActiveJobs)
                {
                    if (job.State == JobState.ReadyToDeliver)
                        sb.AppendLine(UIColors.Wrap($"  {job.DisplayString()} — READY", UIColor.Green) + StockSuffix(job));
                    else
                        sb.AppendLine($"  {job.DisplayString()} — in progress{StockSuffix(job)}");
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
            var atLimit = state.ActiveJobs.Count >= Core.Settings.MaxActiveJobs;
            var inTransit = UnityGameInstance.BattleTechGame.Simulation?.TravelState
                != SimGameTravelStatus.IN_SYSTEM;
            sgEventPanel.eventDescription.SetText(BuildBody(state));

            // Content entries in display order: board offers first, then active jobs.
            // Ledger mode (non-cantina worlds): offers are not shown and cannot be
            // taken — the board physically hangs on a cantina wall
            var entries = new List<OptionEntry>();
            foreach (var job in currentIsCantina ? state.Board.Slots : new List<JobInstance>())
            {
                entries.Add(new OptionEntry($"{((atLimit || inTransit) ? "[Take]" : UIColors.Wrap("[Take]", UIColor.Blue))} {job.ResolvedName}", !atLimit && !inTransit, arg =>
                {
                    var result = Core.State.TryTake(job.InstanceId);
                    Core.Log($"[Board] Take: {result}");
                    if (result == TakeResult.Success)
                    {
                        // seed progress with what the player already holds — AddProgress
                        // clamps at the target and flips the job to READY when it is enough
                        var have = ItemCatalog.GetInventoryCount(
                            UnityGameInstance.BattleTechGame.Simulation,
                            JobCatalog.GetDef(job.DefId), job);
                        if (have > 0) job.AddProgress(have);
                        MakeOptions(sgEventPanel);
                    }
                }));
            }
            foreach (var job in state.ActiveJobs)
            {
                if (job.State == JobState.ReadyToDeliver)
                {
                    entries.Add(new OptionEntry($"{UIColors.Wrap("[Deliver]", UIColor.Green)} {job.DisplayString()}", true, arg =>
                        ConfirmDeliver(sgEventPanel, job)));
                }
                else
                {
                    entries.Add(new OptionEntry($"{UIColors.Wrap("[Abandon]", UIColor.Red)} {job.DisplayString()}", true, arg =>
                        ConfirmAbandon(sgEventPanel, job)));
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

        /// <summary>Delivery confirmation: Deliver-mode item jobs consume the collected
        /// items, so the player gets a blocking yes/no (with the deduction spelled out)
        /// before the transfer is final. Non-consuming jobs deliver directly — there is
        /// nothing to change one's mind about.</summary>
        private static void ConfirmDeliver(SGEventPanel sgEventPanel, JobInstance job)
        {
            var def = JobCatalog.GetDef(job.DefId);
            var consumes = def?.ObjectiveType == ObjectiveType.CollectItems && def.ItemMode == ItemModeType.Deliver;
            if (!consumes)
            {
                DeliverJob(sgEventPanel, job);
                return;
            }

            var itemName = job.ResolvedTarget;
            var entry = def.FindItemTarget(job.ResolvedTarget);
            if (entry != null)
            {
                var dm = UnityGameInstance.BattleTechGame.Simulation.DataManager;
                itemName = ItemCatalog.LookupName(dm, job.ResolvedTarget, entry.ItemType) ?? itemName;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Deliver \"{job.ResolvedName}\"?");
            sb.AppendLine();
            sb.AppendLine($"{job.TargetCount} × {itemName} will be removed from your inventory.");
            sb.AppendLine();
            sb.Append("Payment: ");
            sb.Append(PaymentString(job));

            // button order matters: Enter clicks the LAST button (HandleEnterKeypress),
            // so the destructive action goes last — Esc cancels, Enter confirms.
            // Built-in nested fader, NOT AddFader(): the shared PopupRoot fader slot
            // already holds the event's dim — AddFader/EndFader would clear it on close.
            GenericPopupBuilder.Create("Deliver job", sb.ToString())
                .AddButton("Cancel", null)
                .AddButton("Deliver", () => DeliverJob(sgEventPanel, job))
                .CancelOnEscape()
                .IsNestedPopupWithBuiltInFader()
                .Render();
        }

        /// <summary>Abandon confirmation: all progress is lost.</summary>
        private static void ConfirmAbandon(SGEventPanel sgEventPanel, JobInstance job)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Abandon \"{job.ResolvedName}\"?");
            sb.AppendLine();
            sb.AppendLine("All progress will be lost.");

            GenericPopupBuilder.Create("Abandon job", sb.ToString())
                .AddButton("Cancel", null)
                .AddButton("Abandon", () => AbandonJob(sgEventPanel, job))
                .CancelOnEscape()
                .IsNestedPopupWithBuiltInFader()
                .Render();
        }

        private static void DeliverJob(SGEventPanel sgEventPanel, JobInstance job)
        {
            var ok = RewardService.Deliver(job.InstanceId);
            Core.Log($"[Board] Deliver: {(ok ? "success" : "failed")}");
            if (ok) MakeOptions(sgEventPanel);
        }

        private static void AbandonJob(SGEventPanel sgEventPanel, JobInstance job)
        {
            var ok = Core.State.Abandon(job.InstanceId);
            Core.Log($"[Board] Abandon: {(ok ? "success" : "failed")}");
            if (ok) MakeOptions(sgEventPanel);
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
