using System;
using System.Collections.Generic;
using BattleTech;

namespace BTCantinaMissions.Domain
{
    /// <summary>Live inventory of a chassis family (v0.7 design, ARCHITECTURE.md §10):
    /// parts and whole units the player currently owns.
    /// Stat keys: parts — "Item.MECHPART.{id}" (id = mechdef_ with CustomSalvage,
    /// chassisdef_ as the vanilla fallback, vehicledef_ for LewdableTanks fakes);
    /// stored units — "Item.MechDef.{id}" (mechdef from AddMech, chassisdef from
    /// UnreadyMech — both key styles occur). Active units live in ActiveMechs,
    /// ReadyingMechs count as owned but are NOT deliverable.</summary>
    internal static class FamilyInventory
    {
        private const string PartPrefix = "Item.MECHPART.";
        private const string StoredMechPrefix = "Item.MechDef.";

        private static readonly Dictionary<string, string> familyByStatId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal struct PartsEntry
        {
            public string Id;        // raw stat id (mechdef_/chassisdef_/vehicledef_)
            public string DisplayName;
            public int Undamaged;
            public int Damaged;
            public int Total => Undamaged + Damaged;
        }

        internal struct UnitEntry
        {
            public string Key;       // stable key: "bay:{n}" or "stat:{statId}"
            public string DisplayName;
            public bool Active;
            public int BaySlot;      // valid when Active
            public string StatId;    // valid when !Active
        }

        /// <summary>Family of a raw part/mech stat id, or null. Mirrors the H4a
        /// prefix mapping: vehiclechassisdef_ → vehicledef_, chassisdef_ →
        /// mechdef_, everything else as-is; then the standard resolver cascade.
        /// Caches misses too — unresolvable ids stay unresolvable.</summary>
        internal static string StatIdToFamily(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (familyByStatId.TryGetValue(id, out var cached)) return cached;

            string defId;
            if (id.StartsWith("vehiclechassisdef_", StringComparison.Ordinal))
                defId = "vehicledef_" + id.Substring("vehiclechassisdef_".Length);
            else if (id.StartsWith("chassisdef_", StringComparison.Ordinal))
                defId = "mechdef_" + id.Substring("chassisdef_".Length);
            else
                defId = id;

            string family = null;
            var dm = UnityGameInstance.BattleTechGame?.Simulation?.DataManager;
            if (dm?.MechDefs != null && dm.MechDefs.TryGet(defId, out MechDef mechDef))
                family = ChassisFamilyResolver.GetFamily(mechDef);

            familyByStatId[id] = family;
            return family;
        }

        /// <summary>All part entries of the family, sorted by display name.</summary>
        internal static List<PartsEntry> EnumerateParts(SimGameState sim, string family)
        {
            var result = new List<PartsEntry>();
            if (sim == null || string.IsNullOrEmpty(family)) return result;

            foreach (var statId in sim.GetAllInventoryStrings())
            {
                if (statId == null || !statId.StartsWith(PartPrefix, StringComparison.Ordinal)) continue;
                var rawId = statId.Substring(PartPrefix.Length);
                if (!IsFamily(rawId, family)) continue;

                var entry = new PartsEntry
                {
                    Id = rawId,
                    DisplayName = PartDisplayName(rawId),
                    Undamaged = sim.GetItemCount(rawId, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY),
                    Damaged = sim.GetItemCount(rawId, "MECHPART", SimGameState.ItemCountType.DAMAGED_ONLY)
                };
                if (entry.Total > 0) result.Add(entry);
            }
            result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        internal static int CountParts(SimGameState sim, string family)
        {
            var total = 0;
            foreach (var e in EnumerateParts(sim, family)) total += e.Total;
            return total;
        }

        /// <summary>Owned unit count for the Acquire mirror: active + readying +
        /// stored. ReadyingMechs count as the player's property (v0.7 design
        /// table) even though they cannot be delivered.</summary>
        internal static int CountUnits(SimGameState sim, string family)
        {
            if (sim == null || string.IsNullOrEmpty(family)) return 0;
            var count = 0;

            foreach (var mech in sim.ActiveMechs.Values)
                if (mech != null && ChassisFamilyResolver.MatchesFamily(mech, family)) count++;
            foreach (var mech in sim.ReadyingMechs.Values)
                if (mech != null && ChassisFamilyResolver.MatchesFamily(mech, family)) count++;

            foreach (var statId in sim.GetAllInventoryStrings())
            {
                if (statId == null || !statId.StartsWith(StoredMechPrefix, StringComparison.Ordinal)) continue;
                var rawId = statId.Substring(StoredMechPrefix.Length);
                if (sim.GetItemCount(rawId, typeof(MechDef), SimGameState.ItemCountType.ALL) <= 0) continue;
                if (IsFamily(rawId, family)) count++;
            }
            return count;
        }

        /// <summary>Deliverable units of the family: active bays + stored item
        /// stats. ReadyingMechs are excluded by design (mid-refit, not removable
        /// from the work queue).</summary>
        internal static List<UnitEntry> EnumerateDeliverableUnits(SimGameState sim, string family)
        {
            var result = new List<UnitEntry>();
            if (sim == null || string.IsNullOrEmpty(family)) return result;

            foreach (var bay in sim.ActiveMechs)
            {
                var mech = bay.Value;
                if (mech == null || !ChassisFamilyResolver.MatchesFamily(mech, family)) continue;
                result.Add(new UnitEntry
                {
                    Key = "bay:" + bay.Key,
                    DisplayName = UnitName(mech.Description.UIName, bay.Key, true),
                    Active = true,
                    BaySlot = bay.Key
                });
            }

            foreach (var statId in sim.GetAllInventoryStrings())
            {
                if (statId == null || !statId.StartsWith(StoredMechPrefix, StringComparison.Ordinal)) continue;
                var rawId = statId.Substring(StoredMechPrefix.Length);
                if (sim.GetItemCount(rawId, typeof(MechDef), SimGameState.ItemCountType.ALL) <= 0) continue;
                if (!IsFamily(rawId, family)) continue;

                result.Add(new UnitEntry
                {
                    Key = "stat:" + statId,
                    DisplayName = UnitName(StoredMechName(rawId), 0, false),
                    Active = false,
                    StatId = rawId
                });
            }
            return result;
        }

        /// <summary>Removes a stored unit: plain stat removal, mirroring
        /// ScrapInactiveMech without the scrap payout.</summary>
        internal static void RemoveStoredUnit(SimGameState sim, string rawId)
        {
            sim.RemoveItemStat(rawId, typeof(MechDef), false);
        }

        /// <summary>Removes an active unit: StripMech — components return to the
        /// inventory (a lootable cockpit installed on the delivered mech comes
        /// back and H3 keeps collect jobs honest), the bay slot frees. Mirrors
        /// ScrapActiveMech without the scrap payout.</summary>
        internal static void RemoveActiveUnit(SimGameState sim, int baySlot)
        {
            if (!sim.ActiveMechs.TryGetValue(baySlot, out MechDef mech) || mech == null) return;
            sim.StripMech(baySlot, mech);
        }

        private static bool IsFamily(string rawId, string family)
        {
            var resolved = StatIdToFamily(rawId);
            return string.Equals(resolved, family, StringComparison.OrdinalIgnoreCase);
        }

        private static string PartDisplayName(string rawId)
        {
            var dm = UnityGameInstance.BattleTechGame?.Simulation?.DataManager;
            string defId;
            if (rawId.StartsWith("chassisdef_", StringComparison.Ordinal))
                defId = "mechdef_" + rawId.Substring("chassisdef_".Length);
            else if (rawId.StartsWith("vehiclechassisdef_", StringComparison.Ordinal))
                defId = "vehicledef_" + rawId.Substring("vehiclechassisdef_".Length);
            else
                defId = rawId;

            if (dm?.MechDefs != null && dm.MechDefs.TryGet(defId, out MechDef mechDef))
            {
                var name = mechDef.Description.UIName;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return rawId;
        }

        private static string StoredMechName(string rawId)
        {
            var dm = UnityGameInstance.BattleTechGame?.Simulation?.DataManager;
            if (dm?.MechDefs != null && dm.MechDefs.TryGet(rawId, out MechDef mechDef))
            {
                var name = mechDef.Description.UIName;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            // UnreadyMech stores under the chassisdef id — no unique mechdef,
            // but the chassis display name is exactly what the mech bay shows
            if (rawId.StartsWith("chassisdef_", StringComparison.Ordinal) &&
                dm?.ChassisDefs != null && dm.ChassisDefs.TryGet(rawId, out ChassisDef chassis))
            {
                var name = chassis.Description.UIName;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return rawId;
        }

        private static string UnitName(string name, int bay, bool active)
        {
            return active ? $"{name} (bay {bay + 1})" : $"{name} (storage)";
        }
    }
}
