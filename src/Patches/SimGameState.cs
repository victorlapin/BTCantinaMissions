using System.IO;
using BattleTech;
using BattleTech.Save;
using HarmonyLib;
using Newtonsoft.Json;

namespace BTCantinaMissions.Patches
{
    [HarmonyPatch(typeof(SimGameState), "Init")]
    public static class SimGameState_InitPatch
    {
        public static void Postfix(SimGameState __instance)
        {
            Core.Debug("SimGameState Init");
            Core.DM = __instance.DataManager;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
    public static class SimGameState_RehydratePatch
    {
        public static void Postfix(SimGameState __instance, GameInstanceSave gameInstanceSave)
        {
            Core.Debug("SimGameState Rehydrate");
            Core.DM = __instance.DataManager;
        }
    }

    /// <summary>Dumps CampaignState to a readable JSON file when the game saves.
    /// JwTweaks writes the actual CustomSaveBlock; this is a parallel debug dump.</summary>
    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.Dehydrate))]
    public static class SimGameState_DehydratePatch
    {
        public static bool Prepare() => Core.Settings.DumpStateOnSave;

        // Parameter types matched by name; the third parameter's type is in
        // BattleTech.Save.Test namespace which is awkward to reference.
        public static void Postfix(SimGameState __instance)
        {
            try
            {
                var path = Path.Combine(Core.ModDir, "state_dump.json");
                File.WriteAllText(path,
                    JsonConvert.SerializeObject(Core.State, Formatting.Indented));
                Core.Debug($"[DumpState] State dumped to {path}");
            }
            catch (System.Exception e)
            {
                Core.LogWarning($"[DumpState] Failed to dump state: {e.Message}");
            }
        }
    }
}
