using System;
using System.Collections.Generic;
using System.Text;
using BattleTech;
using BTCantinaMissions.UI;

namespace BTCantinaMissions.Domain
{
    /// <summary>Issues rewards when a job is delivered (ARCHITECTURE.md section 10).
    /// Acquire: inventory untouched — the reward is paid purely for reaching the goal.
    /// Deliver (CollectItems only): the collected items are removed from inventory
    /// before payout; mech / mech-part jobs never remove anything — picking the exact
    /// unit or splitting family parts is not feasible.</summary>
    public static class RewardService
    {
        /// <summary>Full delivery flow: inventory check, state transition, item removal,
        /// payout. Returns false (with a toast) when the player no longer has the items.</summary>
        public static bool Deliver(string instanceId)
        {
            var job = Core.State.FindActive(instanceId);
            if (job == null || job.State != JobState.ReadyToDeliver)
            {
                Core.LogWarning($"[Reward] Job not ready: {instanceId}");
                return false;
            }

            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var def = JobCatalog.GetDef(job.DefId);
            var deliverItems = def?.ItemMode == ItemModeType.Deliver
                && def.ObjectiveType == ObjectiveType.CollectItems;

            if (def?.ItemMode == ItemModeType.Deliver && !deliverItems)
            {
                Core.LogWarning($"[Reward] Deliver is not supported for {def.ObjectiveType}, treating as Acquire ({def.Id})");
            }

            if (deliverItems && !HasEnoughItems(sim, job, def))
                return false;

            // Remove the job from the active list BEFORE taking the items, so the
            // removal tracker (H3a) does not fire on the job being delivered
            if (!Core.State.Deliver(instanceId))
                return false;

            if (deliverItems)
                RemoveItems(sim, job, def);

            Grant(sim, job, def);
            return true;
        }

        /// <summary>v0.7: parts delivery — the staged selection (stat id → count,
        /// from the CantinaPopup staging UI) is verified against the live
        /// inventory, then consumed undamaged-first (or intact-only, per the
        /// DamagedParts setting).</summary>
        public static bool DeliverParts(string instanceId, Dictionary<string, int> selection)
        {
            var job = Core.State.FindActive(instanceId);
            if (job == null || job.State != JobState.ReadyToDeliver) return false;

            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var def = JobCatalog.GetDef(job.DefId);

            var staged = 0;
            foreach (var kv in selection)
            {
                var parts = FindParts(sim, job, kv.Key);
                if (parts == null) return false;
                var available = parts.Value.Total;
                if (kv.Value < 0 || kv.Value > available)
                {
                    Core.LogWarning($"[Reward] Parts staging invalid: {kv.Key} x{kv.Value} (have {available})");
                    return false;
                }
                staged += kv.Value;
            }
            if (staged != job.TargetCount)
            {
                Core.LogWarning($"[Reward] Parts staging mismatch: {staged}/{job.TargetCount}");
                return false;
            }

            // same ordering as item delivery: the job leaves the list first, so
            // the MECHPART mirror (H3a) does not reverse the job being delivered
            if (!Core.State.Deliver(instanceId)) return false;

            foreach (var kv in selection)
            {
                var clean = Math.Min(FindParts(sim, job, kv.Key).Value.Undamaged, kv.Value);
                for (var i = 0; i < clean; i++)
                    sim.RemoveItemStat(kv.Key, "MECHPART", false);
                for (var i = clean; i < kv.Value; i++)
                    sim.RemoveItemStat(kv.Key, "MECHPART", true);
            }

            Grant(sim, job, def);
            return true;
        }

        /// <summary>v0.7: whole-unit delivery — active bays strip components back
        /// into the inventory (lootable gear returns, H3 keeps collect jobs
        /// honest), stored units are plain stat removal. Mirrors scrap without
        /// the payout; the buyer already pays via the job reward.</summary>
        public static bool DeliverMechUnit(string instanceId, string unitKey)
        {
            var job = Core.State.FindActive(instanceId);
            if (job == null || job.State != JobState.ReadyToDeliver) return false;

            var sim = UnityGameInstance.BattleTechGame.Simulation;

            FamilyInventory.UnitEntry? found = null;
            foreach (var unit in FamilyInventory.EnumerateDeliverableUnits(sim, job.ResolvedTarget))
            {
                if (unit.Key == unitKey) { found = unit; break; }
            }
            if (found == null)
            {
                Core.LogWarning($"[Reward] Deliverable unit not found: {unitKey}");
                return false;
            }
            var target = found.Value;

            if (!Core.State.Deliver(instanceId)) return false;

            if (target.Active)
                FamilyInventory.RemoveActiveUnit(sim, target.BaySlot);
            else
                FamilyInventory.RemoveStoredUnit(sim, target.StatId);

            Grant(sim, job, JobCatalog.GetDef(job.DefId));
            return true;
        }

        private static FamilyInventory.PartsEntry? FindParts(SimGameState sim, JobInstance job, string statId)
        {
            foreach (var parts in FamilyInventory.EnumerateParts(sim, job.ResolvedTarget))
            {
                if (parts.Id == statId) return parts;
            }
            return null;
        }

        /// <summary>Career economy scaling: the "Contract Payment" slider at career
        /// start writes Finances.ContractPricePerDifficulty (RT: 100k/134k/200k/300k
        /// for Cheapskate..Generous) and the game scales all contract payouts by it.
        /// Cantina payouts follow the same slider, normalized to the Normal (200k)
        /// baseline the reward numbers were balanced against. Guarded: no constants,
        /// no scaling (skirmish / edge cases).</summary>
        public static float EconomyScale(SimGameState sim)
        {
            var perDifficulty = sim?.Constants?.Finances?.ContractPricePerDifficulty ?? 0;
            if (perDifficulty <= 0) return 1f;
            return perDifficulty / 200000f;
        }

        private static void Grant(SimGameState sim, JobInstance job, CantinaJobDef def)
        {
            var reward = def?.Reward;
            if (reward == null)
            {
                Core.LogWarning($"[Reward] No reward defined for {def?.Id ?? job.DefId}");
                return;
            }

            var cbills = UnityEngine.Mathf.RoundToInt(reward.CBills * EconomyScale(sim));
            if (cbills != 0)
                sim.AddFunds(cbills, job.ResolvedName);

            if (string.IsNullOrEmpty(reward.ItemCollection))
            {
                Announce(job, cbills, null, 0);
                return;
            }

            if (!sim.DataManager.ItemCollectionDefs.TryGet(reward.ItemCollection, out var collection))
            {
                Core.LogWarning($"[Reward] ItemCollection '{reward.ItemCollection}' not found, items skipped ({def.Id})");
                Announce(job, cbills, null, 0);
                return;
            }

            // ItemCount = number of weighted-random rolls (0/unset = 1). Granted directly:
            // QueueRewardsPopup is a unique-typed queue entry and would silently drop
            // when several jobs with collections are delivered in one visit. Note that
            // count 0 (the vanilla RewardsPopup convention) means "grant the whole list".
            var rolls = reward.ItemCount > 0 ? reward.ItemCount : 1;
            sim.ItemCollectionResultGen.GenerateItemCollection(collection, rolls, result =>
            {
                var items = new StringBuilder();
                var count = 0;
                foreach (var item in result.items)
                {
                    sim.AddFromShopDefItem(item, true, 0, SimGamePurchaseMessage.TransactionType.Add);
                    items.AppendLine($"  {RewardItemName(item)} ×{item.Count}");
                    count += item.Count;
                }
                Announce(job, cbills, count > 0 ? items.ToString() : null, count);
            });
        }

        /// <summary>Instant gold toast (Notifications.OnReward) + a reward popup with
        /// the full breakdown — queued behind the board popup, shown after Leave.</summary>
        private static void Announce(JobInstance job, int cbills, string itemsBlock, int itemCount)
        {
            CantinaPopup.ShowReward(job, cbills, itemsBlock);
            Notifications.OnReward(job, cbills, itemCount);
        }

        /// <summary>Display name for a rolled reward item via the vanilla
        /// ShopDefItem → SalvageDef DM lookup; falls back to the raw ID.</summary>
        private static string RewardItemName(ShopDefItem item)
        {
            try
            {
                var salvage = new SalvageDef();
                item.ToSalvageDef(ref salvage);
                var name = salvage.Description?.Name;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch (Exception e)
            {
                Core.Debug($"[Reward] Name lookup failed for {item.ID}: {e.Message}");
            }
            return item.ID;
        }

        /// <summary>The stat type for a job's item target — taken from the def's
        /// explicit pool entry (FindItemTarget), not from the ID prefix: modded items
        /// do not follow the vanilla prefix conventions.</summary>
        private static bool TryGetStatType(JobInstance job, CantinaJobDef def, out Type type)
        {
            var entry = def?.FindItemTarget(job.ResolvedTarget);
            if (entry == null)
            {
                type = null;
                return false;
            }
            return ItemCatalog.TryResolveType(entry.ItemType, out type);
        }

        private static bool HasEnoughItems(SimGameState sim, JobInstance job, CantinaJobDef def)
        {
            if (!TryGetStatType(job, def, out var type))
            {
                Core.LogWarning($"[Reward] Unknown item type for '{job.ResolvedTarget}' — cannot deliver");
                return false;
            }

            var total = sim.GetItemCount(job.ResolvedTarget, type, SimGameState.ItemCountType.ALL);
            if (total >= job.TargetCount) return true;

            Core.LogWarning($"[Reward] Not enough '{job.ResolvedTarget}': {total}/{job.TargetCount}");
            return false;
        }

        /// <summary>Removes TargetCount items (undamaged first). Precondition:
        /// HasEnoughItems passed and the job already left the active list.</summary>
        private static void RemoveItems(SimGameState sim, JobInstance job, CantinaJobDef def)
        {
            var id = job.ResolvedTarget;
            TryGetStatType(job, def, out var type);
            var undamaged = sim.GetItemCount(id, type, SimGameState.ItemCountType.UNDAMAGED_ONLY);

            var clean = Math.Min(undamaged, job.TargetCount);
            for (var i = 0; i < clean; i++)
                sim.RemoveItemStat(id, type, false);
            for (var i = clean; i < job.TargetCount; i++)
                sim.RemoveItemStat(id, type, true);
        }
    }
}
