using System;
using System.Collections.Generic;
using BattleTech;
using BTCantinaMissions.Domain;
using BTCantinaMissions.UI;
using HarmonyLib;
using HBS.Collections;

namespace BTCantinaMissions.Patches
{
    /// <summary>Victims destroyed by the player's own team during the current combat.
    /// Attribution matters: MissionControl can drop allied lances and employer forces
    /// fight alongside the player — only the player's own work advances DestroyUnits
    /// jobs. Stored as actor references on purpose: encounter GUIDs repeat across runs,
    /// object identity cannot; entries from other combats are filtered out by the
    /// actor.Combat check at counting time. Never persisted.</summary>
    internal static class PlayerKillTracker
    {
        internal static readonly List<AbstractActor> Kills = new List<AbstractActor>();

        /// <summary>Actor whose activation is currently resolving. PanicSystem batches
        /// panic/ejection rolls to the END of the attacker's activation and then ejects
        /// the victim with the victim's own GUID as the source — the real attacker is
        /// only recoverable as "whoever's activation is ending".</summary>
        internal static AbstractActor CurrentActivator;

        /// <summary>Drops all references (kills + the activator): a stray actor
        /// reference pins the whole combat object graph against GC between missions.</summary>
        internal static void Reset()
        {
            Kills.Clear();
            CurrentActivator = null;
        }
    }

    /// <summary>Feeds PlayerKillTracker.CurrentActivator — the panic-ejection
    /// attribution fallback.</summary>
    [HarmonyPatch(typeof(AbstractActor), "OnActivationBegin")]
    public static class OnActivationBeginPatch
    {
        public static void Prefix(AbstractActor __instance)
        {
            PlayerKillTracker.CurrentActivator = __instance;
        }
    }

    /// <summary>H5: records every hostile destroyed by the player's team (any cause —
    /// gunfire, collisions, crashing after our knockdown — as long as the game
    /// attributes the death to a player-team attacker). Skirmish deaths never enter
    /// the list (no SimGameState).</summary>
    [HarmonyPatch(typeof(AbstractActor), nameof(AbstractActor.HandleDeath))]
    public static class HandleDeathPatch
    {
        public static void Prefix(AbstractActor __instance, string attackerGUID)
        {
            // same gate as the game's own publish path: fires exactly once per actor
            if (!__instance.IsFlaggedForDeath || __instance.HasHandledDeath) return;
            // despawned = withdrew / escaped the field, not destroyed
            if (__instance.WasDespawned) return;

            // skirmish has no campaign state — its kills must not leak anywhere
            if (UnityGameInstance.BattleTechGame?.Simulation == null) return;

            var combat = __instance.Combat;
            var playerTeam = combat?.LocalPlayerTeam;
            if (playerTeam == null) return;

            // environmental/self-inflicted deaths carry no attacker — not our work either
            var killer = string.IsNullOrEmpty(attackerGUID) ? null : combat.FindActorByGUID(attackerGUID);
            if (killer == null || killer.team != playerTeam) return;
            if (!playerTeam.IsEnemy(__instance.team)) return;

            PlayerKillTracker.Kills.Add(__instance);
            Core.Debug($"[H5] Player kill: {__instance.Description.Name} by {killer.Description.Name}");
        }
    }

    /// <summary>H5a: forced ejections credit the attacker who caused them. The base
    /// EjectPilot discards the damage source and reports the death as self-inflicted
    /// (HandleDeath(this.GUID)), so a pilot bailing out from our shot would never
    /// register in H5 — record it here instead. Voluntary ejections (AI morale panic
    /// via EjectSequence, the player's own eject order) carry a self or non-enemy
    /// source and stay uncounted.</summary>
    [HarmonyPatch(typeof(AbstractActor), nameof(AbstractActor.EjectPilot))]
    public static class EjectPilotPatch
    {
        public static void Postfix(AbstractActor __instance, string sourceID, int stackItemID,
            DeathMethod deathMethod, bool isSilent)
        {
            // only completed ejections: with no pilot aboard the base method did nothing
            // (FlagForDeath/HandleDeath never ran)
            if (!__instance.IsFlaggedForDeath || !__instance.HasHandledDeath) return;

            // skirmish has no campaign state — its kills must not leak anywhere
            if (UnityGameInstance.BattleTechGame?.Simulation == null) return;

            var combat = __instance.Combat;
            var playerTeam = combat?.LocalPlayerTeam;
            if (playerTeam == null) return;

            var killer = string.IsNullOrEmpty(sourceID) ? null : combat.FindActorByGUID(sourceID);
            if (killer == __instance)
            {
                // self-attributed ejection: PanicSystem discards the attacker and passes
                // the victim's own GUID — the real attacker is whoever's activation is
                // resolving (panic rolls are batched to its end). Voluntary ejections
                // happen in the victim's own activation, so this stays uncounted for them.
                killer = PlayerKillTracker.CurrentActivator;
            }
            if (killer == null || killer.team != playerTeam) return;
            if (!playerTeam.IsEnemy(__instance.team)) return;

            // a real kill path may have credited this actor already — never twice
            if (PlayerKillTracker.Kills.Contains(__instance)) return;

            PlayerKillTracker.Kills.Add(__instance);
            Core.Debug($"[H5] Ejection credited: {__instance.Description.Name} bailed out from {killer.Description.Name}");
        }
    }

    /// <summary>H6: applies recorded player-team kills to active DestroyUnits jobs at
    /// contract completion. Works regardless of mission result.</summary>
    [HarmonyPatch(typeof(Contract), nameof(Contract.CompleteContract))]
    public static class CompleteContractPatch
    {
        public static void Postfix(Contract __instance, MissionResult result, bool isGoodFaithEffort)
        {
            // Skirmish has no SimGameState — campaign jobs must not count its kills
            // (static Core.State survives exit to the main menu)
            if (UnityGameInstance.BattleTechGame?.Simulation == null)
            {
                PlayerKillTracker.Reset();
                return;
            }

            var combat = UnityGameInstance.BattleTechGame?.Combat;
            if (combat == null) return;

            var deadTags = new List<TagSet>();
            var deadFamilies = new List<string>();
            foreach (var actor in PlayerKillTracker.Kills)
            {
                // only kills from THIS combat: the tracker is static and may still
                // hold references from abandoned/restarted runs of earlier missions
                if (actor == null || actor.Combat != combat) continue;

                var mech = actor as Mech;
                var vehicle = actor as Vehicle;
                var turret = actor as Turret;

                if (mech?.MechDef?.Chassis != null)
                {
                    Core.Debug($"[H6] Destroyed by player: {mech.MechDef.Chassis.Description.Name} ({mech.MechDef.Description.Id})");
                    var tags = new TagSet();
                    foreach (var t in mech.MechDef.Chassis.ChassisTags) tags.Add(t);
                    foreach (var t in mech.MechDef.MechTags) tags.Add(t);
                    deadTags.Add(tags);
                    // chassis family for DestroyChassis (fake vehicles resolve here too)
                    deadFamilies.Add(ChassisFamilyResolver.GetFamily(mech.MechDef));
                }
                else if (vehicle?.VehicleDef != null)
                {
                    Core.Debug($"[H6] Destroyed by player: {vehicle.VehicleDef.Chassis.Description.Name} ({vehicle.VehicleDef.Description.Id})");
                    var tags = new TagSet();
                    foreach (var t in vehicle.VehicleDef.VehicleTags) tags.Add(t);
                    deadTags.Add(tags);
                    deadFamilies.Add(null); // no MechDef → no family (safety branch, LT vehicles are fake mechs)
                }
                else if (turret?.TurretDef != null)
                {
                    Core.Debug($"[H6] Destroyed by player: {turret.TurretDef.Description.Name} ({turret.TurretDef.Description.Id})");
                    var tags = new TagSet();
                    foreach (var t in turret.TurretDef.TurretTags) tags.Add(t);
                    deadTags.Add(tags);
                    deadFamilies.Add(null); // turrets have no chassis family
                }
            }

            // consume: the list must be empty for the next mission whatever happens below
            PlayerKillTracker.Reset();

            if (deadTags.Count == 0) return;
            Core.Log($"[H6] Mission ended ({result}), {deadTags.Count} targets destroyed by the player");

            foreach (var job in Core.State.ActiveJobs)
            {
                var def = JobCatalog.GetDef(job.DefId);
                var count = 0;

                if (def?.ObjectiveType == ObjectiveType.DestroyUnits)
                {
                    foreach (var tagSet in deadTags)
                    {
                        if (BoardGenerator.MatchesTarget(tagSet, job.ResolvedTarget))
                            count++;
                    }
                }
                else if (def?.ObjectiveType == ObjectiveType.DestroyChassis)
                {
                    // forced ejections are in the kill list already (H5a) — a chassis
                    // target forced to bail out counts as destroyed
                    foreach (var family in deadFamilies)
                    {
                        if (string.Equals(family, job.ResolvedTarget, StringComparison.OrdinalIgnoreCase))
                            count++;
                    }
                }
                else
                {
                    continue;
                }

                if (count > 0)
                {
                    job.AddProgress(count);
                    Core.Log($"[H6] {job.ResolvedName}: +{count} → {job.Progress}/{job.TargetCount}");
                    Notifications.OnProgress(job);
                }
            }
        }
    }
}
