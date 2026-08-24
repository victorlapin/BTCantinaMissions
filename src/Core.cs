using System;
using System.IO;
using System.Reflection;
using BattleTech.Data;
using BTCantinaMissions.Domain;
using HarmonyLib;
using HBS.Logging;
using Newtonsoft.Json;

[assembly: AssemblyVersion("0.1.0")]

namespace BTCantinaMissions
{
    public static class Core
    {
        public const string ModName = "BTCantinaMissions";
        private static readonly ILog log = Logger.GetLogger(ModName, LogLevel.Debug);

        public static string ModDir { get; private set; }
        public static Settings Settings { get; private set; }
        public static CampaignState State { get; internal set; } = new CampaignState();

        public static DataManager DM { get; internal set; }

        public static bool IsCS { get; internal set; } = false;

        public static void Init(string modDir, string settingsJson)
        {
            ModDir = modDir;
            Settings = LoadSettings();

            var harmony = new Harmony(ModName);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log($"[Init] {ModName} loaded, modDir: {modDir}, harmony patches applied");
            Log($"[Init] Settings: tag={Settings.PlanetTag}, slots={Settings.SlotsPerBoard}, " +
                    $"maxActive={Settings.MaxActiveTasks}, refresh={Settings.BoardRefresh}, " +
                    $"lazy={Settings.LazyBoardGeneration}");
        }

        private static Settings LoadSettings()
        {
            var path = Path.Combine(ModDir, "settings.json");
            try
            {
                if (!File.Exists(path))
                {
                    LogWarning($"[Init] settings.json not found at {path}, using defaults");
                    return new Settings();
                }
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path)) ?? new Settings();
            }
            catch (Exception e)
            {
                LogError($"[Init] Failed to parse settings.json, using defaults: {e.Message}");
                return new Settings();
            }
        }

        #region Logging helpers

        public static void Log(string message) => log.Log(message);
        public static void LogWarning(string message) => log.LogWarning(message);
        public static void LogError(string message) => log.LogError(message);

        public static void LogException(string message, Exception exception) =>
            log.LogException(message, exception);

        public static void Debug(string message)
        {
            if (Settings?.DebugLogging != true) return;
            log.LogDebug(message);
        }

        #endregion
    }
}
