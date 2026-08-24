using System;
using System.Collections.Generic;
using System.Linq;

namespace BTCantinaMissions.Domain
{
    public enum TaskState
    {
        Offered,          // on a board, not yet taken
        Taken,            // in player's active list, in progress
        ReadyToDeliver    // progress == target, waiting for delivery
    }

    /// <summary>Runtime instance of a cantina task (ARCHITECTURE.md section 5).</summary>
    public class TaskInstance
    {
        public string InstanceId { get; internal set; }
        public string DefId { get; internal set; }
        public TaskState State { get; internal set; }
        public int Progress { get; internal set; }
        /// <summary>Target resolved from a pool (e.g. "locust", "MediumLaser", "unit_vtol").</summary>
        public string ResolvedTarget { get; internal set; }
        /// <summary>Def Name with {target} substituted from ResolvedTarget.</summary>
        public string ResolvedName { get; internal set; }
        /// <summary>System where this task was originally offered (flavor only).</summary>
        public string OriginSystemId { get; internal set; }
        public int CreatedMonth { get; internal set; }
        public int? TakenMonth { get; internal set; }

        public TaskInstance() { }

        public TaskInstance(string defId, string resolvedTarget, string resolvedName,
            string originSystemId, int month)
        {
            InstanceId = Guid.NewGuid().ToString("N");
            DefId = defId;
            State = TaskState.Offered;
            Progress = 0;
            ResolvedTarget = resolvedTarget;
            ResolvedName = resolvedName;
            OriginSystemId = originSystemId;
            CreatedMonth = month;
        }

        public void Take(int month)
        {
            if (State != TaskState.Offered) return;
            State = TaskState.Taken;
            TakenMonth = month;
        }

        public void AddProgress(int amount, int targetCount)
        {
            if (State != TaskState.Taken) return;
            Progress = Math.Min(Progress + amount, targetCount);
            if (Progress >= targetCount)
                State = TaskState.ReadyToDeliver;
        }

        /// <summary>Removes the task from the active list. Called by CampaignState.Deliver.</summary>
        public void Deliver()
        {
            if (State != TaskState.ReadyToDeliver) return;
            // No state change — the task is removed from the list by the caller.
        }

        /// <summary>Display string: "Kill VTOLs (3/5)".</summary>
        public string DisplayString(CantinaTaskDef def)
        {
            return $"{ResolvedName} ({Progress}/{def?.TargetCount ?? 0})";
        }
    }

    /// <summary>Per-system board: what's currently offered at this cantina.</summary>
    public class SystemBoard
    {
        public string SystemId { get; internal set; }
        public int LastRefreshMonth { get; internal set; }
        /// <summary>Currently offered (Offered state) tasks on this board.</summary>
        public List<TaskInstance> Slots { get; internal set; } = new List<TaskInstance>();

        public SystemBoard() { }

        public SystemBoard(string systemId, int month)
        {
            SystemId = systemId;
            LastRefreshMonth = month;
        }

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

    /// <summary>Player's active task list + per-system boards (ARCHITECTURE.md section 5).
    /// ActiveTasks is the player's "backpack" — independent of any board.</summary>
    public class CampaignState
    {
        public int SchemaVersion { get; internal set; } = 1;

        /// <summary>All boards, keyed by system ID. Only contains Offered tasks.</summary>
        public Dictionary<string, SystemBoard> Boards { get; internal set; } = new Dictionary<string, SystemBoard>();

        /// <summary>Player's taken tasks (Taken / ReadyToDeliver). Removed on delivery.</summary>
        public List<TaskInstance> ActiveTasks { get; internal set; } = new List<TaskInstance>();

        public IEnumerable<TaskInstance> InProgress =>
            ActiveTasks.Where(t => t.State == TaskState.Taken);

        public IEnumerable<TaskInstance> Deliverable =>
            ActiveTasks.Where(t => t.State == TaskState.ReadyToDeliver);

        /// <summary>Moves a task from a board to the player's active list.
        /// Checks MaxActiveTasks limit and duplicate (defId, resolvedTarget).</summary>
        public TakeResult TryTake(string instanceId, int currentMonth)
        {
            // find on a board
            TaskInstance task = null;
            SystemBoard sourceBoard = null;
            foreach (var board in Boards.Values)
            {
                task = board.Slots.FirstOrDefault(t => t.InstanceId == instanceId);
                if (task != null) { sourceBoard = board; break; }
            }
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
            sourceBoard.RemoveSlot(instanceId);
            task.Take(currentMonth);
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

        /// <summary>Finds a task by instanceId on any board (offered).</summary>
        public TaskInstance FindOffered(string instanceId)
        {
            foreach (var board in Boards.Values)
            {
                var task = board.Slots.FirstOrDefault(t => t.InstanceId == instanceId);
                if (task != null) return task;
            }
            return null;
        }

        public SystemBoard GetOrCreateBoard(string systemId, int month)
        {
            if (!Boards.TryGetValue(systemId, out var board))
            {
                // LastRefreshMonth = 0 ensures RefreshBoard will process it immediately
                board = new SystemBoard(systemId, 0);
                Boards[systemId] = board;
            }
            return board;
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
