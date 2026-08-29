using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using CustomComponents;
using HBS.Collections;
using LewdableTanks;

namespace BTCantinaMissions.Domain
{
    /// <summary>Generates job board. Pure random — knows nothing about
    /// the player's active jobs.</summary>
    public static class BoardGenerator
    {
        private static readonly Random random = new Random();

        /// <summary>Refreshes the board (monthly or first visit).
        /// Same def can appear multiple times with different resolved targets
        /// (e.g. "Collect Locust" and "Collect Cicada" from one collectMech def).</summary>
        public static void RefreshBoard(StarSystem system)
        {
            if (Core.State.Board == null)
            {
                Core.LogWarning("Board is null, nothing to refresh");
                return;
            }

            Core.State.Board.ClearSlots();

            var eligible = FilterEligible(system);
            if (eligible.Count == 0)
            {
                Core.Debug($"[BoardGenerator] No eligible defs for {system.Name}, board empty");
                return;
            }

            // Track used (defId, target) pairs to ensure variety on this board
            var usedOnBoard = new HashSet<string>();

            for (int slot = 0; slot < Core.Settings.JobsPerBoard; slot++)
            {
                var def = WeightedPick(eligible);
                if (def == null) break;

                var instance = CreateInstance(def, system.ID, usedOnBoard);
                if (instance == null)
                {
                    // All targets of this def exhausted on this board — try another def
                    var alternatives = eligible.Where(d => d != def).ToList();
                    if (alternatives.Count == 0) break;
                    def = WeightedPick(alternatives);
                    if (def == null) break;
                    instance = CreateInstance(def, system.ID, usedOnBoard);
                    if (instance == null) continue;
                }

                Core.State.Board.Slots.Add(instance);
                usedOnBoard.Add($"{def.Id}|{instance.ResolvedTarget}");
                Core.Debug($"[BoardGenerator]   + {instance.DisplayString()}");
            }

            Core.Log($"[BoardGenerator] Board for {system.Name}: {Core.State.Board.Slots.Count} slot(s)");
        }

        /// <summary>Filters defs by system difficulty and required tags.</summary>
        private static List<CantinaJobDef> FilterEligible(StarSystem system)
        {
            var difficulty = system.Def.GetDifficulty(system.Sim.SimGameMode);
            var result = new List<CantinaJobDef>();

            foreach (var def in JobCatalog.AllDefs)
            {
                if (def.MinSystemDifficulty > difficulty || def.MaxSystemDifficulty < difficulty)
                    continue;
                if (def.RequiredSystemTags != null && def.RequiredSystemTags.Count > 0)
                {
                    var hasAll = def.RequiredSystemTags.All(tag => system.Tags.Contains(tag));
                    if (!hasAll) continue;
                }
                result.Add(def);
            }

            return result;
        }

        /// <summary>Single weighted random pick (with replacement — same def can be picked again).</summary>
        private static CantinaJobDef WeightedPick(List<CantinaJobDef> pool)
        {
            if (pool.Count == 0) return null;
            var totalWeight = pool.Sum(d => d.Weight);
            var roll = random.Next(totalWeight);
            var cumulative = 0;
            for (var i = 0; i < pool.Count; i++)
            {
                cumulative += pool[i].Weight;
                if (roll < cumulative) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        /// <summary>Creates a JobInstance, resolving a target from pools.
        /// Skips targets already used on this board to ensure variety.</summary>
        private static JobInstance CreateInstance(CantinaJobDef def, string systemId, HashSet<string> usedOnBoard)
        {
            var pool = def.GetTargetPool();
            string target = null;

            if (pool != null && pool.Count > 0)
            {
                var available = pool.Where(t => !usedOnBoard.Contains($"{def.Id}|{t}")).ToList();
                if (available.Count == 0) return null;
                target = available[random.Next(available.Count)];
            }
            else if (usedOnBoard.Contains($"{def.Id}|"))
            {
                // Fixed-mode def already on this board
                return null;
            }
            var name = def.Name;
            if (target != null && name.Contains("{target}"))
                name = name.Replace("{target}", ResolveDisplayName(def, target));

            var targetCount = random.Next(def.MinTargetCount, def.MaxTargetCount + 1);

            return new JobInstance(def.Id, target, name, targetCount, systemId);
        }

        /// <summary>Resolves the human-readable display name for a pool target
        /// by looking up actual game data via DataManager. Falls back to
        /// simple humanization if lookup fails.</summary>
        private static string ResolveDisplayName(CantinaJobDef def, string target)
        {
            var dm = UnityGameInstance.BattleTechGame.Simulation.DataManager;
            if (dm == null) return FallbackHumanize(target);

            switch (def.ObjectiveType)
            {
                case ObjectiveType.CollectMech:
                case ObjectiveType.CollectMechParts:
                case ObjectiveType.DestroyChassis:
                    {
                        var name = FindChassisName(dm, target);
                        if (name != null) return name;
                        break;
                    }
                case ObjectiveType.CollectItems:
                    {
                        // explicit catalog from the def — the ID prefix is not reliable
                        // for modded items (e.g. Gear_Engine_* in the HeatSink store)
                        var entry = def.FindItemTarget(target);
                        var name = entry != null ? ItemCatalog.LookupName(dm, target, entry.ItemType) : null;
                        if (name != null) return name;
                        break;
                    }
                case ObjectiveType.DestroyUnits:
                    {
                        // "unit_vtol" → strip prefix, capitalize
                        if (target.StartsWith("unit_"))
                            return FallbackHumanize(target);
                        break;
                    }
            }

            return FallbackHumanize(target);
        }

        /// <summary>Display-name cache per chassis family — FindChassisName scans the
        /// whole ChassisDefs store (thousands of entries in modded games) twice per call.</summary>
        private static readonly Dictionary<string, string> chassisNameCache = new Dictionary<string, string>();

        /// <summary>Finds a chassis display name by family. Two passes:
        /// 1) exact prefix match with sub-family exclusion (wasp ≠ wasp_lam)
        /// 2) normalized match: strip _ and - from both sides (hermesii → hermes_ii)</summary>
        private static string FindChassisName(BattleTech.Data.DataManager dm, string target)
        {
            if (chassisNameCache.TryGetValue(target, out var cached))
                return cached;

            var name = FindChassisNameUncached(dm, target);
            chassisNameCache[target] = name;
            return name;
        }

        private static string FindChassisNameUncached(BattleTech.Data.DataManager dm, string target)
        {
            // Pass 1: exact prefix, skip sub-families (variant code starts uppercase)
            var prefix = $"chassisdef_{target}_";
            foreach (var kvp in dm.ChassisDefs)
            {
                if (!kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                var rest = kvp.Key.Substring(prefix.Length);
                if (string.IsNullOrEmpty(rest) || char.IsLower(rest[0]))
                    continue; // sub-family like "lam_", "iic_"
                var name = kvp.Value.Description?.Name;
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            // Pass 2: normalized — strip separators from both sides
            var normalizedTarget = NormalizeChassisKey(target);
            foreach (var kvp in dm.ChassisDefs)
            {
                if (!kvp.Key.StartsWith("chassisdef_", StringComparison.OrdinalIgnoreCase))
                    continue;
                var rest = kvp.Key.Substring("chassisdef_".Length);
                // Find variant code start (first uppercase char)
                int variantStart = -1;
                for (int i = 0; i < rest.Length; i++)
                {
                    if (char.IsUpper(rest[i])) { variantStart = i; break; }
                }
                if (variantStart <= 0) continue;
                var familyPart = rest.Substring(0, variantStart).TrimEnd('_');
                if (NormalizeChassisKey(familyPart) == normalizedTarget)
                {
                    var name = kvp.Value.Description?.Name;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }

            // Pass 3: vehicles (LewdableTanks) — vehicle families are PrefabIDs, not
            // chassisdef id prefixes; match via VAssemblyVariant and take the real
            // vehicle chassis display name
            if (Integrations.IsLT)
            {
                var vehicleName = FindVehicleChassisName(dm, target);
                if (!string.IsNullOrEmpty(vehicleName))
                    return vehicleName;
            }

            return null;
        }

        /// <summary>Vehicle display name by family (PrefabID): scans VehicleDefs and
        /// matches VAssemblyVariant. LT types are isolated here for soft-dependency JIT;
        /// fail-soft against LT version drift. Runs once per family (chassisNameCache).</summary>
        private static string FindVehicleChassisName(BattleTech.Data.DataManager dm, string target)
        {
            try
            {
                foreach (var kvp in dm.VehicleDefs)
                {
                    var chassis = kvp.Value?.Chassis;
                    if (chassis == null) continue;

                    var prefab = chassis.GetComponent<VAssemblyVariant>()?.PrefabID;
                    if (!string.Equals(prefab, target, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = chassis.Description?.Name;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }
            catch (Exception e)
            {
                Core.LogWarning($"[BoardGenerator] LewdableTanks API drift: {e.GetType().Name}: {e.Message}");
            }

            return null;
        }

        /// <summary>Normalizes a chassis key fragment: lowercase, strip _ and -.</summary>
        private static string NormalizeChassisKey(string s)
        {
            return s?.Replace("_", "").Replace("-", "").ToLowerInvariant() ?? "";
        }

        /// <summary>Display names for tags that need special casing.</summary>
        private static readonly Dictionary<string, string> tagOverrides = new Dictionary<string, string>
        {
            {"unit_vtol", "VTOL"},
            {"unit_legendary", "Legendary unit"},
            {"unit_primitive", "Primitive units"},
        };

        /// <summary>Splits a composite target ("unit_mech&unit_light") into individual tags.</summary>
        public static string[] SplitTarget(string target)
        {
            return target?.Split('&') ?? new string[0];
        }

        /// <summary>Checks if a unit's tags match a target (all tags in a composite entry must be present).</summary>
        public static bool MatchesTarget(TagSet unitTags, string target)
        {
            var parts = SplitTarget(target);
            foreach (var tag in parts)
                if (!unitTags.Contains(tag.Trim()))
                    return false;
            return true;
        }

        /// <summary>Humanizes a target by stripping unit_ prefixes, capitalizing, joining with spaces.
        /// "unit_legendary&unit_assault&unit_mech" → "Legendary Assault Mech".</summary>
        private static string FallbackHumanize(string target)
        {
            if (string.IsNullOrEmpty(target)) return target;

            var parts = SplitTarget(target);
            var words = new List<string>();

            foreach (var part in parts)
            {
                var tag = part.Trim();
                if (tagOverrides.TryGetValue(tag, out var known))
                {
                    words.Add(known);
                    continue;
                }
                var s = tag;
                if (s.StartsWith("unit_")) s = s.Substring(5);
                if (string.IsNullOrEmpty(s)) continue;
                words.Add(char.ToUpper(s[0]) + s.Substring(1));
            }

            return string.Join(" ", words);
        }
    }
}
