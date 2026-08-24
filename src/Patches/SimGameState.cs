using BattleTech;
using HarmonyLib;

namespace BTCantinaMissions.Patches
{
    [HarmonyPatch(typeof(SimGameState), "Init")]
    public static class SimGameState_InitPatch
    {
        public static void Postfix(SimGameState __instance)
        {
            Core.DM = __instance.DataManager;
        }
    }
}