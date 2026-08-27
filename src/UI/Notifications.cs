using System.Collections.Generic;
using System.Text;
using BattleTech;
using BattleTech.UI;
using BTCantinaMissions.Domain;

namespace BTCantinaMissions.UI
{
    /// <summary>Job progress/readiness toasts via the ship room's native floaty toast
    /// queue (SGTimePlayPause → SGTimeFloatyStack). During combat the sim room UI is
    /// torn down, so toasts fired at contract completion are held in a pending queue
    /// and flushed when the room becomes ready again (SGRoomManager.OnSimGameReady).</summary>
    public static class Notifications
    {
        private const int MaxPending = 20;
        private static readonly Queue<string> pending = new Queue<string>();

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
            Show(UIColors.Wrap($"{job.DisplayString()} — NOT READY", UIColor.Red));
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
            var shipRoom = TryGetShipRoom();
            if (shipRoom == null)
            {
                // hold instead of drop — flushed when the sim room is back (post-combat)
                pending.Enqueue(message);
                while (pending.Count > MaxPending) pending.Dequeue();
                Core.Debug($"[Notifications] ship room not ready, toast held: {message}");
                return;
            }
            shipRoom.AddEventToast(new Localize.Text(message));
        }

        /// <summary>Drains toasts held while the sim room was unavailable.</summary>
        public static void Flush()
        {
            if (pending.Count == 0) return;
            var shipRoom = TryGetShipRoom();
            while (pending.Count > 0 && shipRoom != null)
                shipRoom.AddEventToast(new Localize.Text(pending.Dequeue()));
        }

        private static SGRoomController_Ship TryGetShipRoom()
        {
            var sim = UnityGameInstance.BattleTechGame?.Simulation;
            var shipRoom = sim?.RoomManager?.ShipRoom;
            return shipRoom?.TimePlayPause != null ? shipRoom : null;
        }
    }
}
