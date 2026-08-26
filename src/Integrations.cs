using System;
using System.Collections.Generic;
using System.Reflection;

namespace BTCantinaMissions
{
    public static class Integrations
    {
        public static bool IsCS { get; internal set; } = false;
        public static bool IsSMA { get; internal set; } = false;

        public static void FinishedLoading(List<string> loadOrder)
        {
            foreach (string name in loadOrder)
            {
                if (name.Equals("CustomSalvage", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitCS();
                }

                if (name.Equals("BTSimpleMechAssembly", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitSMA();
                }
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