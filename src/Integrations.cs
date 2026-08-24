using System;
using System.Collections.Generic;
using System.Reflection;

namespace BTCantinaMissions
{
    public static class Integrations
    {
        public static void FinishedLoading(List<string> loadOrder)
        {
            foreach (string name in loadOrder)
            {
                if (name.Equals("CustomSalvage", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitCS();
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
                    Core.IsCS = true;
                    return;
                }
            }

            Core.Log("CustomSalvage NOT found");
        }
    }
}