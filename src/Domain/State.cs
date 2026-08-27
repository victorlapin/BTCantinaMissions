using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BTCantinaMissions.Domain
{
    public enum JobState
    {
        Offered,          // on a board, not yet taken
        Taken,            // in player's active list, in progress
        ReadyToDeliver    // progress == target, waiting for delivery
    }

    /// <summary>Runtime instance of a cantina job.</summary>
    public class JobInstance
    {
        // [JsonProperty] is required on internal setters: JwTweaks LoadData uses
        // JsonConvert.PopulateObject, which skips non-public setters without it
        // (state silently resets to defaults on game load).
        [JsonProperty] public string InstanceId { get; internal set; }
        [JsonProperty] public string DefId { get; internal set; }
        [JsonProperty] public JobState State { get; internal set; }
        [JsonProperty] public int TargetCount { get; internal set; }
        [JsonProperty] public int Progress { get; internal set; }
        /// <summary>Target resolved from a pool (e.g. "locust", "MediumLaser", "unit_vtol").</summary>
        [JsonProperty] public string ResolvedTarget { get; internal set; }
        /// <summary>Def Name with {target} substituted from ResolvedTarget.</summary>
        [JsonProperty] public string ResolvedName { get; internal set; }
        /// <summary>System where this job was originally offered (flavor only).</summary>
        [JsonProperty] public string OriginSystemId { get; internal set; }

        public JobInstance() { }

        public JobInstance(string defId, string resolvedTarget, string resolvedName,
            int targetCount, string originSystemId)
        {
            InstanceId = Guid.NewGuid().ToString("N");
            DefId = defId;
            State = JobState.Offered;
            Progress = 0;
            ResolvedTarget = resolvedTarget;
            ResolvedName = resolvedName;
            TargetCount = targetCount;
            OriginSystemId = originSystemId;
        }

        public void Take()
        {
            if (State != JobState.Offered) return;
            State = JobState.Taken;
        }

        public void AddProgress(int amount)
        {
            if (State != JobState.Taken) return;
            Progress = Math.Min(Progress + amount, TargetCount);
            if (Progress >= TargetCount)
                State = JobState.ReadyToDeliver;
        }

        /// <summary>Syncs progress to an externally computed count (live inventory):
        /// sets the value and the state accordingly, both up and down.</summary>
        public void SyncProgress(int count)
        {
            if (State != JobState.Taken && State != JobState.ReadyToDeliver) return;
            Progress = Math.Min(Math.Max(count, 0), TargetCount);
            State = Progress >= TargetCount ? JobState.ReadyToDeliver : JobState.Taken;
        }

        /// <summary>Removes the job from the active list. Called by CampaignState.Deliver.</summary>
        public void Deliver()
        {
            if (State != JobState.ReadyToDeliver) return;
            // No state change — the job is removed from the list by the caller.
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
        /// <summary>Currently offered (Offered state) jobs on this board.</summary>
        [JsonProperty] public List<JobInstance> Slots { get; internal set; } = new List<JobInstance>();

        public SystemBoard() { }

        /// <summary>Removes all offered slots (monthly refresh).</summary>
        public void ClearSlots()
        {
            Slots.Clear();
        }

        /// <summary>Removes a specific job from the board (when taken by player).</summary>
        public bool RemoveSlot(string instanceId)
        {
            return Slots.RemoveAll(t => t.InstanceId == instanceId) > 0;
        }
    }

    /// <summary>Player's active job list + per-system boards.</summary>
    public class CampaignState
    {
        [JsonProperty] public int SchemaVersion { get; internal set; } = 1;

        /// <summary>Current global board.</summary>
        [JsonProperty] public SystemBoard Board { get; internal set; }

        /// <summary>Player's taken jobs (Taken / ReadyToDeliver). Removed on delivery.</summary>
        [JsonProperty] public List<JobInstance> ActiveJobs { get; internal set; } = new List<JobInstance>();

        [JsonIgnore]
        public IEnumerable<JobInstance> InProgress =>
            ActiveJobs.Where(t => t.State == JobState.Taken);

        [JsonIgnore]
        public IEnumerable<JobInstance> Deliverable =>
            ActiveJobs.Where(t => t.State == JobState.ReadyToDeliver);

        /// <summary>Moves a job from a board to the player's active list.
        /// Checks MaxActiveJobs limit and duplicate (defId, resolvedTarget).</summary>
        public TakeResult TryTake(string instanceId)
        {
            // find on a board
            if (Board == null)
                return TakeResult.NotFound;
            JobInstance job = Board.Slots.FirstOrDefault(t => t.InstanceId == instanceId);

            if (job == null)
                return TakeResult.NotFound;

            // check limit
            var activeCount = ActiveJobs.Count(t => t.State == JobState.Taken || t.State == JobState.ReadyToDeliver);
            if (activeCount >= Core.Settings.MaxActiveJobs)
                return TakeResult.LimitReached;

            // check duplicate
            if (ActiveJobs.Any(t => t.DefId == job.DefId && t.ResolvedTarget == job.ResolvedTarget))
                return TakeResult.Duplicate;

            // move: board → player
            Board.RemoveSlot(instanceId);
            job.Take();
            ActiveJobs.Add(job);
            Core.Log($"[Board] Job taken: {job.ResolvedName}");
            return TakeResult.Success;
        }

        /// <summary>Delivers a completed job and removes it from the active list.</summary>
        public bool Deliver(string instanceId)
        {
            var job = FindActive(instanceId);
            if (job == null || job.State != JobState.ReadyToDeliver) return false;
            Core.Log($"[Board] Job delivered: {job.ResolvedName}");
            ActiveJobs.Remove(job);
            return true;
        }

        /// <summary>[v2] Player abandons a taken job, freeing the slot.</summary>
        public bool Abandon(string instanceId)
        {
            var job = FindActive(instanceId);
            if (job == null) return false;
            ActiveJobs.Remove(job);
            Core.Log($"[Board] Job abandoned: {job.ResolvedName}");
            return true;
        }

        /// <summary>Finds a job by instanceId in the player's active list.</summary>
        public JobInstance FindActive(string instanceId)
        {
            return ActiveJobs.FirstOrDefault(t => t.InstanceId == instanceId);
        }

        /// <summary>Finds a job by instanceId (offered).</summary>
        public JobInstance FindOffered(string instanceId)
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
