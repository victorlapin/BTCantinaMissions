using System.Collections.Generic;
using System.Linq;
using BattleTech;

namespace BTCantinaMissions.Domain
{
    /// <summary>Type of objective a cantina job requires.</summary>
    public enum ObjectiveType
    {
        DestroyUnits,
        CollectItems,
        CollectMech,
        CollectMechParts
    }

    public enum ItemModeType
    {
        Acquire,
        Deliver
    }

    /// <summary>An item target: the modder explicitly names the catalog to search in —
    /// the ID prefix alone is not reliable for modded items (e.g. Gear_Engine_*
    /// living in the HeatSink store).</summary>
    public class TargetItemDef
    {
        public string Id;                 // ComponentDefID, e.g. "Weapon_Laser_Medium"
        public ComponentType ItemType;    // vanilla enum: Weapon | AmmunitionBox | HeatSink | JumpJet | Upgrade
    }

    /// <summary>Static definition of a cantina job, loaded from a CantinaJobDef JSON.</summary>
    public class CantinaJobDef
    {
        public DescriptionDef Description;

        public ObjectiveType ObjectiveType;

        // Target pool: generator picks a random entry.
        // Single-element pool = static target.
        public List<string> UnitTagPool;            // DestroyUnits: unit tags ("unit_vtol")
        public List<string> ChassisPool;            // CollectMech/Parts: chassis families ("locust")
        public List<TargetItemDef> ItemPool;        // CollectItems: explicit id + catalog

        public int MinTargetCount;
        public int MaxTargetCount;
        public ItemModeType ItemMode = ItemModeType.Deliver;            // Acquire | Deliver (CollectItems only)

        public RewardDef Reward;

        public int MinSystemDifficulty = 1;
        public int MaxSystemDifficulty = 10;
        public List<string> RequiredSystemTags;
        public int Weight = 10;

        public string Id => Description?.Id ?? "<no-id>";
        public string Name => Description?.Name ?? Id;

        /// <summary>Returns the target pool (as plain target strings) for this def's objective type.</summary>
        public List<string> GetTargetPool()
        {
            if (UnitTagPool != null && UnitTagPool.Count > 0) return UnitTagPool;
            if (ChassisPool != null && ChassisPool.Count > 0) return ChassisPool;
            if (ItemPool != null && ItemPool.Count > 0)
                return ItemPool.Select(t => t.Id).ToList();
            return null;
        }

        /// <summary>Finds the pool entry for an item target (Id match).</summary>
        public TargetItemDef FindItemTarget(string id)
        {
            return ItemPool?.FirstOrDefault(t => t.Id == id);
        }

        public class DescriptionDef
        {
            public string Id;
            public string Name;
            public string Icon;
        }

        public class RewardDef
        {
            public int CBills;                     // fixed payout
            public string ItemCollection;          // optional: itemCollection CSV id
        }
    }
}
