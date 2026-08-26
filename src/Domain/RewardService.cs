using System;
using System.Text;
using BattleTech;
using BattleTech.UI;
using BTCantinaMissions.UI;

namespace BTCantinaMissions.Domain
{
    /// <summary>Issues rewards when a task is delivered (ARCHITECTURE.md section 10).
    /// Acquire: inventory untouched — the reward is paid purely for reaching the goal.
    /// Deliver (CollectItems only): the collected items are removed from inventory
    /// before payout; mech / mech-part tasks never remove anything — picking the exact
    /// unit or splitting family parts is not feasible.</summary>
    public static class RewardService
    {
        /// <summary>Full delivery flow: inventory check, state transition, item removal,
        /// payout. Returns false (with a toast) when the player no longer has the items.</summary>
        public static bool Deliver(string instanceId)
        {
            var task = Core.State.FindActive(instanceId);
            if (task == null || task.State != TaskState.ReadyToDeliver)
            {
                Core.LogWarning($"[Reward] Task not ready: {instanceId}");
                return false;
            }

            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var def = TaskCatalog.GetDef(task.DefId);
            var deliverItems = def?.ItemMode == ItemModeType.Deliver
                && def.ObjectiveType == ObjectiveType.CollectItems;

            if (def?.ItemMode == ItemModeType.Deliver && !deliverItems)
            {
                Core.LogWarning($"[Reward] Deliver is not supported for {def.ObjectiveType}, treating as Acquire ({def.Id})");
            }

            if (deliverItems && !HasEnoughItems(sim, task))
                return false;

            // Remove the task from the active list BEFORE taking the items, so the
            // removal tracker (H3a) does not fire on the task being delivered
            if (!Core.State.Deliver(instanceId))
                return false;

            if (deliverItems)
                RemoveItems(sim, task);

            Grant(sim, task, def);
            return true;
        }

        private static void Grant(SimGameState sim, TaskInstance task, CantinaTaskDef def)
        {
            var reward = def?.Reward;
            if (reward == null)
            {
                Core.LogWarning($"[Reward] No reward defined for {def?.Id ?? task.DefId}");
                return;
            }

            if (reward.CBills != 0)
                sim.AddFunds(reward.CBills, task.ResolvedName);

            if (string.IsNullOrEmpty(reward.ItemCollection))
            {
                Announce(task, reward.CBills, null, 0);
                return;
            }

            if (!sim.DataManager.ItemCollectionDefs.TryGet(reward.ItemCollection, out var collection))
            {
                Core.LogWarning($"[Reward] ItemCollection '{reward.ItemCollection}' not found, items skipped ({def.Id})");
                Announce(task, reward.CBills, null, 0);
                return;
            }

            // The vanilla RewardsPopup roll, granted directly: QueueRewardsPopup is a
            // unique-typed queue entry and would silently drop when several tasks with
            // collections are delivered in one visit. The callback always fires —
            // synchronously for flat collections, async for nested Reference ones.
            sim.ItemCollectionResultGen.GenerateItemCollection(collection, 0, result =>
            {
                var items = new StringBuilder();
                var count = 0;
                foreach (var item in result.items)
                {
                    sim.AddFromShopDefItem(item, true, 0, SimGamePurchaseMessage.TransactionType.Add);
                    items.AppendLine($"  {RewardItemName(item)} ×{item.Count}");
                    count += item.Count;
                }
                Announce(task, reward.CBills, count > 0 ? items.ToString() : null, count);
            });
        }

        /// <summary>Instant gold toast (Notifications.OnReward) + a reward popup with
        /// the full breakdown — queued behind the board popup, shown after Leave.</summary>
        private static void Announce(TaskInstance task, int cbills, string itemsBlock, int itemCount)
        {
            var body = new StringBuilder();
            body.AppendLine("Job completed:");
            body.AppendLine($"  {task.ResolvedName}");
            body.AppendLine();
            body.AppendLine("You receive:");
            body.AppendLine($"  {UIColors.Wrap($"{cbills:N0} C-Bills", UIColor.Gold)}");
            if (!string.IsNullOrEmpty(itemsBlock))
                body.Append(itemsBlock);

            CantinaPopup.ShowReward("Cantina Reward", body.ToString());
            Notifications.OnReward(task, cbills, itemCount);
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

        private static bool HasEnoughItems(SimGameState sim, TaskInstance task)
        {
            var type = ComponentType(task.ResolvedTarget);
            if (type == null)
            {
                Core.LogWarning($"[Reward] Unknown component type for '{task.ResolvedTarget}' — cannot deliver");
                return false;
            }

            var total = sim.GetItemCount(task.ResolvedTarget, type, SimGameState.ItemCountType.ALL);
            if (total >= task.TargetCount) return true;

            Core.LogWarning($"[Reward] Not enough '{task.ResolvedTarget}': {total}/{task.TargetCount}");
            return false;
        }

        /// <summary>Removes TargetCount items (undamaged first). Precondition:
        /// HasEnoughItems passed and the task already left the active list.</summary>
        private static void RemoveItems(SimGameState sim, TaskInstance task)
        {
            var id = task.ResolvedTarget;
            var type = ComponentType(id);
            var undamaged = sim.GetItemCount(id, type, SimGameState.ItemCountType.UNDAMAGED_ONLY);

            var clean = Math.Min(undamaged, task.TargetCount);
            for (var i = 0; i < clean; i++)
                sim.RemoveItemStat(id, type, false);
            for (var i = clean; i < task.TargetCount; i++)
                sim.RemoveItemStat(id, type, true);
        }

        /// <summary>Maps a ComponentDefID prefix to the def type used by inventory stats.</summary>
        private static Type ComponentType(string id)
        {
            if (id.StartsWith("Weapon_")) return typeof(WeaponDef);
            if (id.StartsWith("Ammo_")) return typeof(AmmunitionBoxDef);
            if (id.StartsWith("Gear_HeatSink")) return typeof(HeatSinkDef);
            if (id.StartsWith("Gear_JumpJet")) return typeof(JumpJetDef);
            if (id.StartsWith("Gear_")) return typeof(UpgradeDef);
            return null;
        }
    }
}
