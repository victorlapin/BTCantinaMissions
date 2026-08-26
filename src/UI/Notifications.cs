using System.Text;
using BattleTech;
using BattleTech.UI;
using BTCantinaMissions.Domain;

namespace BTCantinaMissions.UI
{
    /// <summary>Job progress/readiness toasts via the ship room's native floaty toast
    /// queue (SGTimePlayPause → SGTimeFloatyStack). The queue is drained in Update —
    /// toasts fired during combat or while the room is hidden show up when the player
    /// is back at the ship, so no own buffering is needed.</summary>
    public static class Notifications
    {
        /// <summary>Called after every AddProgress: shows a READY toast when the job
        /// just completed, otherwise a progress toast. Gated by NotifyOnReady /
        /// NotifyOnProgress settings.</summary>
        public static void OnProgress(JobInstance job)
        {
            if (job.State == JobState.ReadyToDeliver)
            {
                if (Core.Settings.NotifyOnReady)
                    Show(UIColors.Wrap($"{job.DisplayString()} — READY", UIColor.Green));
            }
            else if (Core.Settings.NotifyOnProgress)
            {
                Show(job.DisplayString());
            }
        }

        public static void OnNewQuarterBegin()
        {
            var message = "New cantina jobs available";
            Show(message);
        }

        /// <summary>Toast for a job that dropped back from READY because its items
        /// left the inventory (sold / installed) — Deliver-mode tracking.</summary>
        public static void OnReverted(JobInstance job)
        {
            Show(UIColors.Wrap($"{job.ResolvedName}: items left the inventory — job no longer ready", UIColor.Red));
        }

        public static void OnReward(JobInstance job, int cbills, int items)
        {
            var summary = new StringBuilder();
            summary.Append($"Reward: {SimGameState.GetCBillString(cbills)}");
            if (items > 0) summary.Append($" + {items} item(s)");

            Core.Log($"[Reward] {job.ResolvedName}: {summary}");
            Show(UIColors.Wrap(summary.ToString(), UIColor.Gold));
        }

        private static void Show(string message)
        {
            var sim = UnityGameInstance.BattleTechGame?.Simulation;
            var shipRoom = sim?.RoomManager?.ShipRoom;
            if (shipRoom?.TimePlayPause == null)
            {
                Core.Debug($"[Notifications] ship room not ready, toast dropped: {message}");
                return;
            }
            shipRoom.AddEventToast(new Localize.Text(message));
        }
    }
}
