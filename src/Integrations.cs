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

                if (name.Equals("CustomComponents", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitCC();
                }

                if (name.Equals("MechAffinity", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitMA();
                }
            }
        }

        private static void InitCC()
        {
            Core.Log(" -- Checking for CustomComponents Integration -- ");

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.FullName.StartsWith("CustomComponents"))
                {
                    Core.Log("CustomComponents found");
                    Core.IsCC = true;
                    return;
                }
            }

            Core.Log("CustomComponents NOT found");
        }

        private static void InitMA()
        {
            Core.Log(" -- Checking for MechAffinity Integration -- ");

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.FullName.StartsWith("MechAffinity"))
                {
                    Core.Log("MechAffinity found");
                    Core.IsMA = true;
                    return;
                }
            }

            Core.Log("MechAffinity NOT found");
        }
    }
}