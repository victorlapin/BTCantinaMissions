using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JwTweaks.Features;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCantinaMissions
{
    public static class Integrations
    {
        public static bool IsCS { get; internal set; } = false;
        public static bool IsLT { get; internal set; } = false;
        public static bool IsSMA { get; internal set; } = false;

        public static void FinishedLoading(List<string> loadOrder)
        {
            foreach (string name in loadOrder)
            {
                if (name.Equals("CustomSalvage", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitCS();
                }

                if (name.Equals("LewdableTanks", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitLT();
                }

                if (name.Equals("BTSimpleMechAssembly", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitSMA();
                }
            }
        }

        /// <summary>Both hard dependencies carry feature toggles in their mod.json
        /// Settings; a switched-off toggle degrades the mod silently (state loss /
        /// unreachable store). The mod folder is derived from the loaded assembly's
        /// location — modpacks nest mod folders arbitrarily, so assuming a sibling
        /// path under the same Mods root would be wrong. mod.json is what the mods
        /// themselves load, so disk state is runtime state.</summary>
        public static void VerifyHardDependencies()
        {
            var irtweaks = Array.Find(AppDomain.CurrentDomain.GetAssemblies(),
                a => string.Equals(a.GetName().Name, "IRTweaks", StringComparison.OrdinalIgnoreCase));

            CheckDependencyToggle(
                ModJsonPathFor(typeof(SaveSerializationManager).Assembly, "JwTweaks"),
                s => s?["CustomSaveBlocks"]?.Value<bool>() != true,
                "JwTweaks 'CustomSaveBlocks' is DISABLED — cantina job state will not persist: " +
                "the board regenerates on every load and all job progress is lost");

            CheckDependencyToggle(
                ModJsonPathFor(irtweaks, "IRTweaks"),
                s => s?["Fixes"]?["StreamlinedMainMenu"]?.Value<bool>() != true,
                "IRTweaks 'StreamlinedMainMenu' is DISABLED — the store becomes unreachable " +
                "(the cantina button replaces the vanilla store button)");
        }

        /// <summary>mod.json path for a loaded mod assembly; falls back to a sibling
        /// folder under our own Mods root when the location is unavailable (in-memory
        /// load) or has no manifest beside it.</summary>
        private static string ModJsonPathFor(Assembly assembly, string modName)
        {
            var location = assembly?.Location;
            if (!string.IsNullOrEmpty(location))
            {
                var candidate = Path.Combine(Path.GetDirectoryName(location), "mod.json");
                if (File.Exists(candidate)) return candidate;
            }

            return Path.Combine(Directory.GetParent(Core.ModDir)?.FullName ?? "", modName, "mod.json");
        }

        private static void CheckDependencyToggle(string modJsonPath, Func<JObject, bool> isOff, string warning)
        {
            try
            {
                if (!File.Exists(modJsonPath)) return; // missing mod: ModTek DependsOn already failed loudly
                var settings = JsonConvert.DeserializeObject<JObject>(
                    File.ReadAllText(modJsonPath))?["Settings"] as JObject;
                if (isOff(settings)) Core.LogWarning($"[Init] {warning}");
            }
            catch (Exception e)
            {
                Core.Debug($"[Init] dependency toggle check skipped for {modJsonPath}: {e.Message}");
            }
        }

        private static void InitCS()
        {
            Core.Log(" -- Checking for CustomSalvage Integration -- ");

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.FullName.StartsWith("CustomSalvage"))
                {
                    Core.Log("CustomSalvage found");
                    IsCS = true;
                    return;
                }
            }

            Core.Log("CustomSalvage NOT found");
        }

        private static void InitLT()
        {
            Core.Log(" -- Checking for LewdableTanks Integration -- ");

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.FullName.StartsWith("LewdableTanks"))
                {
                    Core.Log("LewdableTanks found");
                    IsCS = true;
                    return;
                }
            }

            Core.Log("LewdableTanks NOT found");
        }

        private static void InitSMA()
        {
            Core.Log(" -- Checking for BTSimpleMechAssembly Integration -- ");

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.FullName.StartsWith("BTSimpleMechAssembly"))
                {
                    Core.Log("BTSimpleMechAssembly found");
                    IsSMA = true;
                    return;
                }
            }

            Core.Log("BTSimpleMechAssembly NOT found");
        }
    }
}