using BattleTech;
using BattleTech.UI;
using BTCantinaMissions.Domain;

namespace BTCantinaMissions.UI
{
    /// <summary>Task progress/readiness toasts via the ship room's native floaty toast
    /// queue (SGTimePlayPause → SGTimeFloatyStack). The queue is drained in Update —
    /// toasts fired during combat or while the room is hidden show up when the player
    /// is back at the ship, so no own buffering is needed.</summary>
    public static class Notifications
    {
        /// <summary>Called after every AddProgress: shows a READY toast when the task
        /// just completed, otherwise a progress toast. Gated by NotifyOnReady /
        /// NotifyOnProgress settings.</summary>
        public static void OnProgress(TaskInstance task)
        {
            if (task.State == TaskState.ReadyToDeliver)
            {
                if (Core.Settings.NotifyOnReady)
                    Show(UIColors.Wrap($"{task.DisplayString()} — READY", UIColor.Green));
            }
            else if (Core.Settings.NotifyOnProgress)
            {
                Show(task.DisplayString());
            }
        }

        public static void OnNewQuarterBegin()
        {
            var message = "New cantina jobs available";
            Show(message);
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
