using System.Collections.Generic;

namespace BTCantinaMissions
{
    public class Settings
    {
        public string PlanetTag = "planet_pop_large";
        public int JobsPerBoard = 4;
        public int MaxActiveJobs = 3;
        public bool NotifyOnProgress = true;
        public bool NotifyOnReady = true;

        /// <summary>Show combat floaties over killed cantina targets ("Cantina:
        /// Destroy VTOLs 3/5"). false = silent tracking, progress still applies.</summary>
        public bool CombatFloaties = true;

        /// <summary>Blue outline on cantina collectible items in the salvage
        /// screen. false = no highlighting during salvage selection.</summary>
        public bool SalvageHighlight = true;

        /// <summary>Blue outline on cantina collectibles in the MechLab
        /// inventory — helps avoid selling/installing cantina targets.</summary>
        public bool MechLabHighlight = false;
        public bool DebugLogging = false;
        public bool DumpStateOnSave = false;

        /// <summary>Keyboard shortcut opening the cantina board/ledger from the ship
        /// room (a UnityEngine KeyCode name, e.g. "F7"). Empty = disabled. Primary
        /// entry for modpacks where the store button must stay vanilla.</summary>
        public string CantinaHotkey = "";

        /// <summary>Replace the location-bar store button with the Cantina button
        /// (RT pack default: the store stays reachable via IRTweaks' left menu).
        /// false leaves the store button vanilla — pair with CantinaHotkey.</summary>
        public bool InterceptStoreButton = true;

        /// <summary>Display names for unit tags (DestroyTagged targets). A value in
        /// settings.json replaces the whole dictionary — include the defaults you
        /// want to keep. Tags not listed fall back to humanization (strip "unit_",
        /// capitalize).</summary>
        public Dictionary<string, string> DisplayNameTagOverrides = new Dictionary<string, string>
        {
            {"unit_vtol", "VTOL"},
            {"unit_legendary", "Legendary unit"},
            {"unit_primitive", "Primitive unit"}
        };

        /// <summary>Units carrying any of these tags never count toward cantina
        /// kill jobs (DestroyTagged and DestroyChassis alike) — no progress, no
        /// floaties, no AAR lines. The RT pack ships ["unit_uav", "unit_battlearmor"]
        /// to keep StrategicOperations drones and battle-armor swarms from farming
        /// jobs; BTX ships an empty list. Don't target a listed tag in a job —
        /// the exclusion always wins.</summary>
        public List<string> ExcludedKillTags = new List<string>();
    }
}
