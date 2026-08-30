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
    }
}
