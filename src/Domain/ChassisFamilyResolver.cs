using System.Collections.Generic;
using BattleTech;
using BTSimpleMechAssembly;
using CustomComponents;
using CustomSalvage;

namespace BTCantinaMissions.Domain
{
    /// <summary>Resolves a ChassisDef to its family name, with caching.
    /// Uses AssemblyVariant (via CustomComponents) when available,
    /// falls back to chassis name matching.</summary>
    public static class ChassisFamilyResolver
    {
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        /// <summary>Returns the chassis family for a ChassisDef, or null if unresolvable.</summary>
        public static string GetFamily(MechDef mechDef)
        {
            if (mechDef?.Description?.Id == null) return null;

            var id = mechDef.Chassis.Description.Id;
            if (cache.TryGetValue(id, out var cached)) return cached;

            string family = null;

            // Fast path: unit_chassis_* tag
            foreach (var tag in mechDef.MechTags)
            {
                if (tag.StartsWith("unit_chassis_"))
                {
                    family = tag.Substring("unit_chassis_".Length);
                    break;
                }
            }

            // Main path: AssemblyVariant via CustomComponents + CustomSalvage
            if (family == null && Integrations.IsCS)
                family = GetAssemblyVariantFamily(mechDef.Chassis);

            if (family == null && Integrations.IsSMA)
                family = GetSMAFamily(mechDef.Chassis);

            // Fallback: use chassis name
            if (family == null)
                family = mechDef.Chassis.Description.Name;

            cache[id] = family;
            return family;
        }

        /// <summary>Checks if a MechDef belongs to the given chassis family.</summary>
        public static bool MatchesFamily(MechDef mechDef, string family)
        {
            if (mechDef?.Chassis == null || string.IsNullOrEmpty(family)) return false;
            var resolved = GetFamily(mechDef);
            return string.Equals(resolved, family, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>AssemblyVariant lookup via CustomComponents (requires CC + CS loaded).</summary>
        private static string GetAssemblyVariantFamily(ChassisDef chassisDef)
        {
            if (chassisDef.Is<AssemblyVariant>(out var assemblyVariant))
            {
                return assemblyVariant.PrefabID;
            }

            return null;
        }

        /// <summary>AssemblyVariant lookup via BTSimpleMechAssembly.</summary>
        private static string GetSMAFamily(ChassisDef chassisDef)
        {
            return CCIntegration.GetVariant(chassisDef, false);
        }
    }
}
