using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BTCantinaMissions.Domain
{
    public enum TaskState
    {
        Offered,          // on a board, not yet taken
        Taken,            // in player's active list, in progress
        ReadyToDeliver    // progress == target, waiting for delivery
    }

    /// <summary>Runtime instance of a cantina task.</summary>
    public class TaskInstance
    {
        // [JsonProperty] is required on internal setters: JwTweaks LoadData uses
        // JsonConvert.PopulateObject, which skips non-public setters without it
        // (state silently resets to defaults on game load).
        [JsonProperty] public string InstanceId { get; internal set; }
        [JsonProperty] public string DefId { get; internal set; }
        [JsonProperty] public TaskState State { get; internal set; }
        [JsonProperty] public int TargetCount { get; internal set; }
        [JsonProperty] public int Progress { get; internal set; }
        /// <summary>Target resolved from a pool (e.g. "locust", "MediumLaser", "unit_vtol").</summary>
        [JsonProperty] public string ResolvedTarget { get; internal set; }
        /// <summary>Def Name with {target} substituted from ResolvedTarget.</summary>
        [JsonProperty] public string ResolvedName { get; internal set; }
        /// <summary>System where this task was originally offered (flavor only).</summary>
        [JsonProperty] public string OriginSystemId { get; internal set; }

        public TaskInstance() { }

        public TaskInstance(string defId, string resolvedTarget, string resolvedName,
            int targetCount, string originSystemId)
        {
            InstanceId = Guid.NewGuid().ToString("N");
            DefId = defId;
            State = TaskState.Offered;
            Progress = 0;
            ResolvedTarget = resolvedTarget;
            ResolvedName = resolvedName;
            TargetCount = targetCount;
            OriginSystemId = originSystemId;
        }

        public void Take()
        {
            if (State != TaskState.Offered) return;
            State = TaskState.Taken;
        }

        public void AddProgress(int amount)
        {
            if (State != TaskState.Taken) return;
            Progress = Math.Min(Progress + amount, TargetCount);
            if (Progress >= TargetCount)
                State = TaskState.ReadyToDeliver;
        }

        /// <summary>Removes the task from the active list. Called by CampaignState.Deliver.</summary>
        public void Deliver()
        {
            if (State != TaskState.ReadyToDeliver) return;
            // No state change — the task is removed from the list by the caller.
        }

        /// <summary>Display string: "Kill VTOLs (3/5)".</summary>
        public string DisplayString()
        {
            return $"{ResolvedName} ({Progress}/{TargetCount})";
        }
    }

    /// <summary>Per-system board: what's currently offered at this cantina.</summary>
    public class SystemBoard
    {
        /// <summary>Currently offered (Offered state) tasks on this board.</summary>
        [JsonProperty] public List<TaskInstance> Slots { get; internal set; } = new List<TaskInstance>();

        public SystemBoard() { }

        /// <summary>Removes all offered slots (monthly refresh).</summary>
        public void ClearSlots()
        {
            Slots.Clear();
        }

        /// <summary>Removes a specific task from the board (when taken by player).</summary>
        public bool RemoveSlot(string instanceId)
        {
            return Slots.RemoveAll(t => t.InstanceId == instanceId) > 0;
        }
    }

    /// <summary>Player's active task list + per-system boards.</summary>
    public class CampaignState
    {
        [JsonProperty] public int SchemaVersion { get; internal set; } = 1;

        /// <summary>Current global board.</summary>
        [JsonProperty] public SystemBoard Board { get; internal set; }

        /// <summary>Player's taken tasks (Taken / ReadyToDeliver). Removed on delivery.</summary>
        [JsonProperty] public List<TaskInstance> ActiveTasks { get; internal set; } = new List<TaskInstance>();

        [JsonIgnore]
        public IEnumerable<TaskInstance> InProgress =>
            ActiveTasks.Where(t => t.State == TaskState.Taken);

        [JsonIgnore]
        public IEnumerable<TaskInstance> Deliverable =>
            ActiveTasks.Where(t => t.State == TaskState.ReadyToDeliver);

        /// <summary>Moves a task from a board to the player's active list.
        /// Checks MaxActiveTasks limit and duplicate (defId, resolvedTarget).</summary>
        public TakeResult TryTake(string instanceId)
        {
            // find on a board
            if (Board == null)
                return TakeResult.NotFound;
            TaskInstance task = Board.Slots.FirstOrDefault(t => t.InstanceId == instanceId);

            if (task == null)
                return TakeResult.NotFound;

            // check limit
            var activeCount = ActiveTasks.Count(t => t.State == TaskState.Taken || t.State == TaskState.ReadyToDeliver);
            if (activeCount >= Core.Settings.MaxActiveTasks)
                return TakeResult.LimitReached;

            // check duplicate
            if (ActiveTasks.Any(t => t.DefId == task.DefId && t.ResolvedTarget == task.ResolvedTarget))
                return TakeResult.Duplicate;

            // move: board → player
            Board.RemoveSlot(instanceId);
            task.Take();
            ActiveTasks.Add(task);
            return TakeResult.Success;
        }

        /// <summary>Delivers a completed task and removes it from the active list.</summary>
        public bool Deliver(string instanceId)
        {
            var task = FindActive(instanceId);
            if (task == null || task.State != TaskState.ReadyToDeliver) return false;
            ActiveTasks.Remove(task);
            return true;
        }

        /// <summary>[v2] Player abandons a taken task, freeing the slot.</summary>
        public bool Abandon(string instanceId)
        {
            var task = FindActive(instanceId);
            if (task == null) return false;
            ActiveTasks.Remove(task);
            Core.Log($"[CampaignState] Task abandoned: {task.ResolvedName}");
            return true;
        }

        /// <summary>Finds a task by instanceId in the player's active list.</summary>
        public TaskInstance FindActive(string instanceId)
        {
            return ActiveTasks.FirstOrDefault(t => t.InstanceId == instanceId);
        }

        /// <summary>Finds a task by instanceId (offered).</summary>
        public TaskInstance FindOffered(string instanceId)
        {
            if (Board == null)
                return null;

            return Board.Slots.FirstOrDefault(t => t.InstanceId == instanceId);
        }
    }

    /// <summary>Result of a Take attempt.</summary>
    public enum TakeResult
    {
        Success,
        NotFound,
        LimitReached,
        Duplicate
    }
}
