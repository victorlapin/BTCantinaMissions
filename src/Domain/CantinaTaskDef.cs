using System.Collections.Generic;

namespace BTCantinaMissions.Domain
{
    /// <summary>Type of objective a cantina task requires.</summary>
    public enum ObjectiveType
    {
        DestroyUnits,
        CollectItems,
        CollectMech,
        CollectMechParts
    }

    /// <summary>Static definition of a cantina task, loaded from a CantinaTaskDef JSON.</summary>
    public class CantinaTaskDef
    {
        public DescriptionDef Description;

        public ObjectiveType ObjectiveType;

        // Target pool: generator picks a random entry.
        // Single-element pool = static target.
        public List<string> UnitTagPool;   // DestroyUnits: unit tags ("unit_vtol")
        public List<string> ChassisPool;   // CollectMech/Parts: chassis families ("locust")
        public List<string> ItemPool;      // CollectItems: ComponentDefID ("Weapon_Laser_Medium")

        public int MinTargetCount;
        public int MaxTargetCount;
        public string ItemMode;            // Acquire | Deliver (CollectItems only)

        public RewardDef Reward;

        public int MinSystemDifficulty = 1;
        public int MaxSystemDifficulty = 10;
        public List<string> RequiredSystemTags;
        public int Weight = 10;

        public string Id => Description?.Id ?? "<no-id>";
        public string Name => Description?.Name ?? Id;

        /// <summary>Returns the target pool for this def's objective type.</summary>
        public List<string> GetTargetPool()
        {
            if (UnitTagPool != null && UnitTagPool.Count > 0) return UnitTagPool;
            if (ChassisPool != null && ChassisPool.Count > 0) return ChassisPool;
            if (ItemPool != null && ItemPool.Count > 0) return ItemPool;
            return null;
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
