using BattleTech;
using BattleTech.Save;
using HarmonyLib;

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
}