# BTCantinaMissions

A job board in the local cantina for HBS BattleTech (Unity + HarmonyX, ModTek).

Planets carrying the tag configured in `PlanetTag` (default: `planet_other_cantina`)
get a cantina. The store button in the location bar (the one next to Hiring hall) is 
**replaced** by a **Cantina** button: enabled (and opening the board) on cantina 
planets, disabled elsewhere. The store itself stays reachable through the left
navigation menu (provided by IRTweaks).
Inside the board: small jobs — destroy specific units, collect items, acquire
mechs or salvage parts. Take a job, do the work out in the field, come back and
deliver it for C-Bills (and bonus items). The board refreshes monthly.

## Requirements

| Mod | Status |
|---|---|
| [ModTek](https://github.com/BattletechModders/ModTek) | required |
| [JwTweaks](https://github.com/wmtorode/JwTweaks) (CustomSaveBlocks enabled) | **hard dependency** — mod state persists through its custom save blocks |
| [IRTweaks](https://github.com/BattletechModders/IRTweaks) | (StreamlinedMainMenu enabled) | **hard dependency** — provides the left navigation menu, the only remaining way into the store once the vanilla store button is repurposed |
| [CustomSalvage](https://github.com/BattletechModders/CustomSalvage) | optional — improves chassis-family resolution for mech/part jobs |
| [BTSimpleMechAssembly](https://github.com/mcb5637/BTSimpleMechAssembly) | optional — alternative chassis-family source for mech/part jobs |

Chassis-family resolution is a cascade: the `unit_chassis_*` unit tag first, then
CustomSalvage's AssemblyVariant, then BTSimpleMechAssembly's variant lookup,
finally the chassis display name. A missing mod simply skips its step; version
drift in modpacks degrades gracefully with a log warning instead of crashing.

## Gameplay

- The board is **global** (one per campaign), regenerated when you visit any cantina
  planet or at the start of each month. Taken jobs are never wiped by the refresh.
- Up to `MaxActiveJobs` jobs at once; duplicates of the same job/target are blocked at take time.
- Take / Deliver / Abandon straight from the board popup — it updates in place.
- Job types:
  - **DestroyUnits** — kill N units matching a tag set (`unit_vtol`, `unit_mech&unit_light`...).
    Counts any hostile destroyed in any contract, regardless of mission outcome.
  - **CollectItems** — obtain N of a specific component.
    `Acquire` mode: keep them, the reward pays for reaching the goal.
    `Deliver` mode: the items are removed from your inventory on delivery —
    and sold/installed items set your progress back.
  - **CollectMech** — bring in a mech of the given chassis family (e.g. any Locust).
  - **CollectMechParts** — collect N salvage parts of the family.
- Rewards: fixed C-Bills plus an optional item collection roll, shown in a reward
  popup with a full breakdown. Delivering items you no longer have is blocked.
- **Location-specific jobs**: some jobs only appear on cantinas located on planets
  with a certain tag — exotic hardware on `planet_other_blackmarket` worlds,
  electronics contracts on `planet_other_comstar` worlds.
- Toast notifications for progress, READY state and rewards (toggle in settings).

## Settings (`settings.json`)

```jsonc
{
  "PlanetTag": "planet_other_cantina", // which planets have a cantina
  "JobsPerBoard": 4,                  // jobs offered per board
  "MaxActiveJobs": 3,                 // concurrent active jobs
  "NotifyOnProgress": true,            // toasts on progress ticks
  "NotifyOnReady": true,               // green READY toast / red NOT READY toast
  "DebugLogging": false,               // verbose log to .modtek/battletech.log
  "DumpStateOnSave": false             // debug: state_dump.json next to the mod
}
```

## For modders: adding jobs

Jobs are `CantinaJobDef` JSON files in `jobs/` (registered as a ModTek
`CustomResourceTypes` entry in `mod.json`):

```jsonc
{
  "Description": {
    "Id": "cantinaJob_collectItems",
    "Name": "Collect {target}",     // {target} is substituted from the pool
    "Icon": ""
  },
  "ObjectiveType": "CollectItems",  // DestroyUnits | CollectItems | CollectMech | CollectMechParts

  // Target pool — the generator picks one random entry per board slot.
  // Use the pool matching the objective type; single-entry pool = fixed target.
  "UnitTagPool": ["unit_vtol", "unit_light&unit_mech"],        // DestroyUnits, & = all tags
  "ChassisPool": ["locust", "stinger"],                        // CollectMech / CollectMechParts
  "ItemPool": [                                                // CollectItems — explicit catalog:
    { "Id": "Weapon_Laser_Medium", "ItemType": "Weapon" },     //   the ID prefix alone is not
    { "Id": "Gear_Engine_XL", "ItemType": "HeatSink" }         //   reliable for modded items
  ],

  "MinTargetCount": 5,              // random count range per instance
  "MaxTargetCount": 10,
  "ItemMode": "Acquire",            // Acquire | Deliver (CollectItems only)

  "Reward": {
    "CBills": 150000,                        // fixed payout
    "ItemCollection": "CantinaReward_Tier1", // optional itemCollection CSV id
    "ItemCount": 1                           // weighted-random rolls from it (default 1)
  },

  "MinSystemDifficulty": 1,         // board eligibility per system
  "MaxSystemDifficulty": 20,
  "RequiredSystemTags": [],         // planet gate: the job only appears on cantinas
                                    // of systems carrying ALL of these tags
  "Weight": 8                       // weighted pick among eligible defs
}
```

`ItemType` is the vanilla `ComponentType` enum: `Weapon | AmmunitionBox | HeatSink |
JumpJet | Upgrade`.

`RequiredSystemTags` gates a job to specific worlds — all listed tags must be on
the star system for the job to enter its cantina board. Tags live in the star
system definitions (e.g. the InnerSphereMap data): examples from the shipped jobs
are `planet_other_blackmarket` (exotic hardware) and `planet_other_comstar`
(electronics contracts). Use it for regional/location flavor.

Destroy-units targets should be things the player can identify in combat by sight
(unit type, weight class, recognizable archetype like carriers) — internal spawn
markers such as `unit_indirectFire` are invisible to players and make frustrating
jobs.

Reward collections are vanilla `ItemCollectionDef` CSVs in `rewards/`
(`id, type, count, weight`). A roll grants one weighted-random entry per
`ItemCount`; missing or empty collections are not an error — the C-Bills still
pay out.

To give a planet a cantina, add the tag from `PlanetTag` (default `planet_other_cantina`) to its definition.

## Building from source

- .NET SDK, `dotnet build` (net472; targeting packs come from
  `Microsoft.NETFramework.ReferenceAssemblies`, so Visual Studio is not required).
- Copy `CHANGEME.Directory.Build.Props` to `Directory.Build.Props` and point it at
  your game/mod folders. The build deploys the DLL into the mod folder automatically.
- HarmonyX is referenced from `ModTek/lib`; private fields are accessed via
  [Krafs.Publicizer](https://github.com/pardeike/Publicizer) — `Assembly-CSharp`
  and `BTSimpleMechAssembly` are publicized (compile-time copies only; at runtime
  the original assemblies are used as-is).

## License

MIT — see [LICENSE](LICENSE).

## Status

v0.3 — functional core: board generation, all four job types, persistence,
rewards, notifications, paginated UI.
