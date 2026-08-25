using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using HBS.Collections;

namespace BTCantinaMissions.Domain
{
    /// <summary>Generates task board. Pure random — knows nothing about
    /// the player's active tasks.</summary>
    public static class BoardGenerator
    {
        private static readonly Random random = new Random();

        /// <summary>Refreshes the board (monthly or first visit).</summary>
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

            var selected = WeightedSample(eligible, Core.Settings.SlotsPerBoard);

            foreach (var def in selected)
            {
                var instance = CreateInstance(def, system.ID);
                if (instance == null) continue;
                Core.State.Board.Slots.Add(instance);
                Core.Debug($"[BoardGenerator]   + {instance.DisplayString(def)}");
            }

            Core.Log($"[BoardGenerator] Board for {system.Name}: {Core.State.Board.Slots.Count} slot(s)");
        }

        /// <summary>Filters defs by system difficulty and required tags.</summary>
        private static List<CantinaTaskDef> FilterEligible(StarSystem system)
        {
            var difficulty = system.Def.GetDifficulty(system.Sim.SimGameMode);
            var result = new List<CantinaTaskDef>();

            foreach (var def in TaskCatalog.AllDefs)
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

        /// <summary>Weighted random sample without replacement (same def can't appear twice on one board).</summary>
        private static List<CantinaTaskDef> WeightedSample(List<CantinaTaskDef> eligible, int count)
        {
            var pool = new List<CantinaTaskDef>(eligible);
            var selected = new List<CantinaTaskDef>();

            while (selected.Count < count && pool.Count > 0)
            {
                var totalWeight = pool.Sum(d => d.Weight);
                var roll = random.Next(totalWeight);
                var cumulative = 0;

                for (var i = 0; i < pool.Count; i++)
                {
                    cumulative += pool[i].Weight;
                    if (roll < cumulative)
                    {
                        selected.Add(pool[i]);
                        pool.RemoveAt(i);
                        break;
                    }
                }
            }

            return selected;
        }

        /// <summary>Creates a TaskInstance, resolving the target from pools if present.</summary>
        private static TaskInstance CreateInstance(CantinaTaskDef def, string systemId)
        {
            var pool = def.GetTargetPool();
            string target = null;

            if (pool != null && pool.Count > 0)
                target = pool[random.Next(pool.Count)];

            var name = def.Name;
            if (target != null && name.Contains("{target}"))
                name = name.Replace("{target}", ResolveDisplayName(def, target));

            return new TaskInstance(def.Id, target, name, systemId);
        }

        /// <summary>Resolves the human-readable display name for a pool target
        /// by looking up actual game data via DataManager. Falls back to
        /// simple humanization if lookup fails.</summary>
        private static string ResolveDisplayName(CantinaTaskDef def, string target)
        {
            var dm = Core.DM;
            if (dm == null) return FallbackHumanize(target);

            switch (def.ObjectiveType)
            {
                case ObjectiveType.CollectMech:
                case ObjectiveType.CollectMechParts:
                    {
                        // Find any chassisdef matching this family prefix
                        var prefix = $"chassisdef_{target}_";
                        foreach (var kvp in dm.ChassisDefs)
                        {
                            if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                var name = kvp.Value.Description?.Name;
                                if (!string.IsNullOrEmpty(name))
                                    return name;
                            }
                        }
                        break;
                    }
                case ObjectiveType.CollectItems:
                    {
                        var name = LookupItemName(dm, target);
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

        /// <summary>Looks up a component's display name by its ComponentDefID,
        /// searching across the relevant DataManager store.</summary>
        private static string LookupItemName(BattleTech.Data.DataManager dm, string id)
        {
            // Route by ID prefix to the right store
            if (id.StartsWith("Weapon_"))
            {
                if (dm.WeaponDefs.TryGet(id, out WeaponDef def))
                    return def?.Description?.Name;
            }
            else if (id.StartsWith("Ammo_"))
            {
                if (dm.AmmoBoxDefs.TryGet(id, out AmmunitionBoxDef def))
                    return def?.Description?.Name;
            }
            else if (id.StartsWith("Gear_HeatSink"))
            {
                if (dm.HeatSinkDefs.TryGet(id, out HeatSinkDef def))
                    return def?.Description?.Name;
            }
            else if (id.StartsWith("Gear_JumpJet"))
            {
                if (dm.JumpJetDefs.TryGet(id, out JumpJetDef def))
                    return def?.Description?.Name;
            }
            else if (id.StartsWith("Gear_"))
            {
                if (dm.UpgradeDefs.TryGet(id, out UpgradeDef def))
                    return def?.Description?.Name;
            }
            return null;
        }

        /// <summary>Display names for tags that need special casing.</summary>
        private static readonly Dictionary<string, string> tagOverrides = new Dictionary<string, string>
        {
            {"unit_vtol", "VTOL"},
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
