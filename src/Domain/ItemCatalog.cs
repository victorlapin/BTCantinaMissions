using System;
using BattleTech;

namespace BTCantinaMissions.Domain
{
    /// <summary>Resolves the vanilla ComponentType to the def type used by the sim
    /// inventory stats, and to the DataManager store for display names.</summary>
    public static class ItemCatalog
    {
        /// <summary>Weapon | AmmunitionBox | HeatSink | JumpJet | Upgrade map to inventory
        /// stores; other enum values (NotSet, Special, MechPart) do not.</summary>
        public static bool TryResolveType(ComponentType itemType, out Type type)
        {
            switch (itemType)
            {
                case ComponentType.Weapon:
                    type = typeof(WeaponDef); return true;
                case ComponentType.AmmunitionBox:
                    type = typeof(AmmunitionBoxDef); return true;
                case ComponentType.HeatSink:
                    type = typeof(HeatSinkDef); return true;
                case ComponentType.JumpJet:
                    type = typeof(JumpJetDef); return true;
                case ComponentType.Upgrade:
                    type = typeof(UpgradeDef); return true;
            }

            type = null;
            return false;
        }

        /// <summary>How many of the job's item target the player currently holds.
        /// Deliver mode only: Acquire jobs pay without consuming the items, so seeding
        /// them from stock would be an infinitely farmable reward.</summary>
        public static int GetInventoryCount(SimGameState sim, CantinaJobDef def, JobInstance job)
        {
            if (def?.ObjectiveType != ObjectiveType.CollectItems) return 0;
            if (def.ItemMode != ItemModeType.Deliver) return 0;
            var entry = def.FindItemTarget(job.ResolvedTarget);
            if (entry == null || !TryResolveType(entry.ItemType, out var type)) return 0;
            return sim.GetItemCount(job.ResolvedTarget, type, SimGameState.ItemCountType.ALL);
        }

        /// <summary>Display name for an item from the DataManager store named by its type.</summary>
        public static string LookupName(BattleTech.Data.DataManager dm, string id, ComponentType itemType)
        {
            if (!TryResolveType(itemType, out var type))
                return null;

            if (type == typeof(WeaponDef))
                return dm.WeaponDefs.TryGet(id, out WeaponDef def) ? def?.Description?.UIName : null;
            if (type == typeof(AmmunitionBoxDef))
                return dm.AmmoBoxDefs.TryGet(id, out AmmunitionBoxDef def) ? def?.Description?.UIName : null;
            if (type == typeof(HeatSinkDef))
                return dm.HeatSinkDefs.TryGet(id, out HeatSinkDef def) ? def?.Description?.UIName : null;
            if (type == typeof(JumpJetDef))
                return dm.JumpJetDefs.TryGet(id, out JumpJetDef def) ? def?.Description?.UIName : null;
            if (type == typeof(UpgradeDef))
                return dm.UpgradeDefs.TryGet(id, out UpgradeDef def) ? def?.Description?.UIName : null;

            return null;
        }
    }
}
