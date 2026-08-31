using System;
using System.IO;
using System.Reflection;
using BTCantinaMissions.Domain;
using HarmonyLib;
using HBS.Logging;
using JwTweaks.Data;
using JwTweaks.Features;
using Newtonsoft.Json;

[assembly: AssemblyVersion("0.5.0")]

namespace BTCantinaMissions
{
    public static class Core
    {
        public const string ModName = "BTCantinaMissions";
        private static readonly ILog log = Logger.GetLogger(ModName, LogLevel.Debug);

        public static string ModDir { get; private set; }
        public static Settings Settings { get; private set; }
        public static CampaignState State { get; internal set; } = new CampaignState();

        public static void Init(string modDir, string settingsJson)
        {
            ModDir = modDir;
            Settings = LoadSettings();

            var harmony = new Harmony(ModName);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log($"[Init] {ModName} loaded, modDir: {modDir}, harmony patches applied");
            Log($"[Init] Settings: tag={Settings.PlanetTag}, jobsPerBoard={Settings.JobsPerBoard}, " +
                    $"maxActive={Settings.MaxActiveJobs}");

            Integrations.VerifyHardDependencies();

            JsonSaveBlock<CampaignState> saveBlock = new JsonSaveBlock<CampaignState>
            {
                Data = State
            };
            SaveSerializationManager.RegisterCustomSaveBlock(saveBlock, ModName);
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

        /// <summary>Clears the campaign state in place. The State instance cannot be
        /// replaced — JwTweaks holds it in JsonSaveBlock.Data — so save loads and new
        /// campaign starts must reset fields instead.</summary>
        public static void ResetState()
        {
            State.Board = null;
            State.ActiveJobs.Clear();
            State.SchemaVersion = 1;
            Log("[Core] Campaign state reset");
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
