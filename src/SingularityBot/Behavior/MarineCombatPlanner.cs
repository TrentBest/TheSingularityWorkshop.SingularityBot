#nullable enable annotations

using SingularityBot.Observation;

namespace SingularityBot.Behavior;

public enum MarineTacticalAction
{
    Hold,
    Fire,
    RepositionDuringCooldown,
    CloseToWeaponRange,
    Withdraw
}

public sealed record CombatWeaponProfile(
    float Range,
    float DamagePerSecond,
    bool TargetsGround = true,
    bool TargetsAir = true)
{
    public float GroundRange { get; init; } = Range;
    public float GroundDamagePerSecond { get; init; } = DamagePerSecond;
    public float AirRange { get; init; } = Range;
    public float AirDamagePerSecond { get; init; } = DamagePerSecond;

    public bool CanHit(bool targetIsFlying) => targetIsFlying ? TargetsAir : TargetsGround;
    public float GetRange(bool targetIsFlying) => targetIsFlying ? AirRange : GroundRange;
    public float GetDamagePerSecond(bool targetIsFlying) =>
        targetIsFlying ? AirDamagePerSecond : GroundDamagePerSecond;

    public static IReadOnlyDictionary<uint, CombatWeaponProfile> FromCatalog(Sc2GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var result = new Dictionary<uint, CombatWeaponProfile>();
        foreach (var entry in catalog.WeaponsByUnitType)
        {
            var ground = entry.Value.Where(weapon =>
                weapon.TargetType is SC2APIProtocol.Weapon.Types.TargetType.Ground or SC2APIProtocol.Weapon.Types.TargetType.Any).ToArray();
            var air = entry.Value.Where(weapon =>
                weapon.TargetType is SC2APIProtocol.Weapon.Types.TargetType.Air or SC2APIProtocol.Weapon.Types.TargetType.Any).ToArray();
            var groundRange = ground.Length == 0 ? 0 : ground.Max(weapon => weapon.Range);
            var airRange = air.Length == 0 ? 0 : air.Max(weapon => weapon.Range);
            var groundDps = ground.Sum(weapon => weapon.DamagePerSecond);
            var airDps = air.Sum(weapon => weapon.DamagePerSecond);
            result[entry.Key] = new CombatWeaponProfile(
                MathF.Max(groundRange, airRange),
                MathF.Max(groundDps, airDps),
                ground.Length > 0,
                air.Length > 0)
            {
                GroundRange = groundRange,
                GroundDamagePerSecond = groundDps,
                AirRange = airRange,
                AirDamagePerSecond = airDps
            };
        }
        return result;
    }

    public bool IsUsable => float.IsFinite(Range) && Range > 0 &&
                            float.IsFinite(DamagePerSecond) && DamagePerSecond > 0;
}
public sealed record MarineTacticalDecision(
    ulong MarineTag,
    ulong? TargetTag,
    MarineTacticalAction Action,
    float FriendlyDps,
    float ThreatDps,
    string Reason,
    bool IsOutsideLocalDefenseEnvelope = false);

/// <summary>
/// Deterministic, observation-only Marine decision kernel. Weapon profiles must
/// come from SC2 ResponseData; absent or invalid profiles fail closed.
/// It deliberately separates choosing a target from issuing a protocol action.
/// </summary>
public static class MarineCombatPlanner
{
    private const float SafetyMargin = 0.85f;
    private const float MaxPursuitBeyondWeaponRange = 6f;

    public static IReadOnlyList<MarineTacticalDecision> Plan(
        NormalizedObservation observation,
        IReadOnlyDictionary<uint, CombatWeaponProfile> weaponsByUnitType,
        IReadOnlySet<ulong>? destructibleNeutralTags = null,
        IReadOnlySet<ulong>? excludedUnitTags = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(weaponsByUnitType);

        var live = observation.Units
            .Where(unit => unit.IsLiveVisible && unit.HasPosition &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .ToArray();
        var marines = live.Where(unit => unit.Alliance == UnitAlliance.Self &&
            unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase)).ToArray();
        var combatMarines = marines
            .Where(unit => excludedUnitTags?.Contains(unit.Tag) != true)
            .ToArray();
        var enemyTargets = live.Where(unit => unit.Alliance == UnitAlliance.Enemy).ToArray();
        // Neutral destructibles are not combat targets by default. A separate,
        // observation-backed route-clearance policy must explicitly identify a
        // blocker before Marines are allowed to spend time attacking it.
        var targets = enemyTargets.Length > 0
            ? enemyTargets
            : live.Where(unit => unit.Alliance == UnitAlliance.Neutral &&
                destructibleNeutralTags?.Contains(unit.Tag) == true &&
                IsDestructible(unit.UnitKind) &&
                !IsNonCombatResource(unit.UnitKind)).ToArray();

        var anchorMarine = combatMarines.OrderBy(marine => marine.Tag).FirstOrDefault();
        var focusTarget = targets
            .Where(candidate => combatMarines.Any(marine =>
                weaponsByUnitType.TryGetValue(marine.UnitTypeId, out var profile) &&
                profile.IsUsable && profile.CanHit(candidate.IsFlying)))
            .OrderByDescending(candidate =>
                weaponsByUnitType.TryGetValue(candidate.UnitTypeId, out var threatProfile) &&
                threatProfile.IsUsable && threatProfile.CanHit(false)
                    ? threatProfile.GetDamagePerSecond(false) / MathF.Max(1f, candidate.Health + candidate.Shield)
                    : 0f)
            .ThenBy(candidate =>
            {
                var availableDps = marines
                    .Where(ally => weaponsByUnitType.TryGetValue(ally.UnitTypeId, out var profile) &&
                        profile.IsUsable && profile.CanHit(candidate.IsFlying) &&
                        DistanceSquared(ally, candidate) <= profile.GetRange(candidate.IsFlying) * profile.GetRange(candidate.IsFlying))
                    .Sum(ally => weaponsByUnitType[ally.UnitTypeId].GetDamagePerSecond(candidate.IsFlying));
                return (candidate.Health + candidate.Shield) / MathF.Max(0.1f, availableDps);
            })
            .ThenBy(candidate => anchorMarine is null ? 0 : DistanceSquared(candidate, anchorMarine))
            .ThenBy(candidate => candidate.Tag)
            .FirstOrDefault();

        var decisions = new List<MarineTacticalDecision>(marines.Length);
        foreach (var marine in marines.OrderBy(unit => unit.Tag))
        {
            if (excludedUnitTags?.Contains(marine.Tag) == true)
            {
                decisions.Add(new(marine.Tag, null, MarineTacticalAction.Hold, 0, 0,
                    "Dedicated scout excluded from combat targeting"));
                continue;
            }

            if (!weaponsByUnitType.TryGetValue(marine.UnitTypeId, out var ownWeapon) ||
                !ownWeapon.IsUsable)
            {
                decisions.Add(new(marine.Tag, null, MarineTacticalAction.Hold, 0, 0,
                    "Marine weapon profile unavailable; refusing to guess range or damage"));
                continue;
            }

            // All Marines share one focus target. This prevents split fire and
            // makes the kill-time estimate meaningful for the whole local force.
            var target = focusTarget is not null && ownWeapon.CanHit(focusTarget.IsFlying)
                ? focusTarget
                : null;
            if (target is null)
            {
                decisions.Add(new(marine.Tag, null, MarineTacticalAction.Hold, 0, 0,
                    "No visible attackable target"));
                continue;
            }

            var friendlyDps = combatMarines
                .Where(ally => weaponsByUnitType.TryGetValue(ally.UnitTypeId, out var profile) &&
                    profile.IsUsable && profile.CanHit(target.IsFlying) &&
                    DistanceSquared(ally, target) <= profile.GetRange(target.IsFlying) * profile.GetRange(target.IsFlying))
                .Sum(ally => weaponsByUnitType[ally.UnitTypeId].GetDamagePerSecond(target.IsFlying));

            var threatDps = live
                .Where(enemy => enemy.Alliance == UnitAlliance.Enemy &&
                    weaponsByUnitType.TryGetValue(enemy.UnitTypeId, out var profile) &&
                    profile.IsUsable && profile.CanHit(marine.IsFlying) &&
                    DistanceSquared(enemy, marine) <= profile.GetRange(marine.IsFlying) * profile.GetRange(marine.IsFlying))
                .Sum(enemy => weaponsByUnitType[enemy.UnitTypeId].GetDamagePerSecond(marine.IsFlying));

            var distance = MathF.Sqrt((float)DistanceSquared(marine, target));
            MarineTacticalAction action;
            string reason;
            var outsideLocalDefenseEnvelope =
                distance > ownWeapon.GetRange(target.IsFlying) + MaxPursuitBeyondWeaponRange;

            // Vision is not an instruction to invade. A contact far outside
            // the Marine's local defensive envelope is observed, not pursued.
            // Keep this fact typed so patrol logic can continue its security
            // route instead of freezing every Marine on a distant sighting.
            if (outsideLocalDefenseEnvelope)
            {
                action = MarineTacticalAction.Hold;
                reason = "Visible contact is outside the bounded local defense envelope";
            }
            else if (threatDps > 0 && friendlyDps < threatDps * SafetyMargin)
            {
                action = MarineTacticalAction.Withdraw;
                reason = "Local friendly damage output does not cover nearby threat";
            }
            else if (target.Alliance == UnitAlliance.Neutral && marine.WeaponCooldownRemaining > 0)
            {
                // Neutral destructibles are obstacles, not combat engagements.
                // After a shot, hand movement back to the squad/rally controller
                // instead of orbiting the blocker and stalling at the ramp.
                decisions.Add(new(marine.Tag, null, MarineTacticalAction.Hold,
                    friendlyDps, 0, "Neutral blocker hit; advance during weapon cooldown"));
                continue;
            }
            else if (distance <= ownWeapon.GetRange(target.IsFlying))
            {
                if (marine.WeaponCooldownRemaining <= 0)
                {
                    action = MarineTacticalAction.Fire;
                    reason = "Weapon ready and target inside authoritative weapon range";
                }
                else
                {
                    action = MarineTacticalAction.RepositionDuringCooldown;
                    reason = "Weapon cooling down; reposition before immediate next shot";
                }
            }
            else
            {
                action = MarineTacticalAction.CloseToWeaponRange;
                reason = "Target outside weapon range; approach while preserving local force";
            }

            decisions.Add(new(marine.Tag, target.Tag, action, friendlyDps, threatDps, reason,
                outsideLocalDefenseEnvelope));
        }

        return decisions;
    }

    private static bool IsDestructible(string kind) =>
        kind.Contains("Destructible", StringComparison.OrdinalIgnoreCase) ||
        kind.Contains("Debris", StringComparison.OrdinalIgnoreCase) ||
        kind.Contains("Rock", StringComparison.OrdinalIgnoreCase) ||
        kind.Contains("Blocker", StringComparison.OrdinalIgnoreCase);

    private static bool IsNonCombatResource(string kind) =>
        kind.Contains("MineralField", StringComparison.OrdinalIgnoreCase) ||
        kind.Contains("VespeneGeyser", StringComparison.OrdinalIgnoreCase) ||
        kind.Contains("RichVespeneGeyser", StringComparison.OrdinalIgnoreCase);

    private static double DistanceSquared(UnitObservationFacts left, UnitObservationFacts right)
    {
        var dx = left.PositionX - right.PositionX;
        var dy = left.PositionY - right.PositionY;
        return (double)dx * dx + (double)dy * dy;
    }
}
