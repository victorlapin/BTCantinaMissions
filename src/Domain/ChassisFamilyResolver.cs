using System.Collections.Generic;
using BattleTech;
using BTSimpleMechAssembly;
using CustomComponents;
using CustomSalvage;
using LewdableTanks;

namespace BTCantinaMissions.Domain
{
    /// <summary>Resolves a chassis (mech or fake-vehicle) to its family name, with caching.
    /// Mechanized path: unit_chassis_* tag. Vehicle path (LewdableTanks fake-mechs):
    /// VAssemblyVariant.PrefabID on the real VehicleChassisDef. Mech paths: AssemblyVariant
    /// via CustomSalvage, then BTSimpleMechAssembly. Fallback: chassis display name.</summary>
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

            // LewdableTanks vehicles: enter our hooks as fake MechDefs — the canonical
            // family lives in VAssemblyVariant on the REAL VehicleChassisDef.
            if (family == null && Integrations.IsLT)
                family = GetVehicleFamily(mechDef);

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

        /// <summary>LewdableTanks fake-vehicle family: mirrors the mod's own
        /// ChassisHandler.get_variant patch — IsVehicle() on the fake MechDef, the real
        /// VehicleDef fetched by the SAME id (LT preserves vehicledef_* ids), then
        /// VAssemblyVariant.PrefabID from the chassis via CustomComponents.
        /// Isolated in its own method: the body references LT types and is only JITted
        /// when the integration is active (soft dependency, like the CS/SMA paths).
        /// Fail-soft against LT version drift in foreign modpacks.</summary>
        private static string GetVehicleFamily(MechDef mechDef)
        {
            try
            {
                // qualified: BTSimpleMechAssembly ships an IsVehicle too — LT's is the
                // authoritative fake-vehicle check (fake_vehicle / fake_vehicle_chassis)
                if (!LewdableTanks.Extensions.IsVehicle(mechDef)) return null;

                var dm = UnityGameInstance.BattleTechGame?.Simulation?.DataManager;
                if (dm?.VehicleDefs == null ||
                    !dm.VehicleDefs.TryGet(mechDef.Description.Id, out VehicleDef vehicle) ||
                    vehicle?.Chassis == null)
                    return null;

                var prefab = vehicle.Chassis.GetComponent<VAssemblyVariant>()?.PrefabID;
                return string.IsNullOrEmpty(prefab) ? null : prefab;
            }
            catch (System.Exception e)
            {
                Core.LogWarning($"[ChassisFamilyResolver] LewdableTanks API drift: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        /// <summary>AssemblyVariant lookup via CustomComponents (requires CC + CS loaded).
        /// Fail-soft: a foreign modpack may ship a different CustomComponents version
        /// where the API drifted — fall back to the chassis name path.</summary>
        private static string GetAssemblyVariantFamily(ChassisDef chassisDef)
        {
            try
            {
                if (chassisDef.Is<AssemblyVariant>(out var assemblyVariant))
                {
                    return assemblyVariant.PrefabID;
                }
            }
            catch (System.Exception e)
            {
                Core.LogWarning($"[ChassisFamilyResolver] CustomComponents API drift: {e.GetType().Name}: {e.Message}");
            }

            return null;
        }

        /// <summary>AssemblyVariant lookup via BTSimpleMechAssembly (requires SMA loaded).
        /// Fail-soft against SMA version drift in foreign modpacks.</summary>
        private static string GetSMAFamily(ChassisDef chassisDef)
        {
            try
            {
                return CCIntegration.GetVariant(chassisDef, false);
            }
            catch (System.Exception e)
            {
                Core.LogWarning($"[ChassisFamilyResolver] BTSimpleMechAssembly API drift: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }
    }
}
