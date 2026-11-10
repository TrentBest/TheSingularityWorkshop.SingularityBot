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
    public bool CanHit(bool targetIsFlying) => targetIsFlying ? TargetsAir : TargetsGround;

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
            var applicable = entry.Value;
            var range = applicable.Max(weapon => weapon.Range);
            var dps = applicable.Sum(weapon => weapon.DamagePerSecond);
            result[entry.Key] = new CombatWeaponProfile(range, dps, ground.Length > 0, air.Length > 0);
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
    string Reason);

/// <summary>
/// Deterministic, observation-only Marine decision kernel. Weapon profiles must
/// come from SC2 ResponseData; absent or invalid profiles fail closed.
/// It deliberately separates choosing a target from issuing a protocol action.
/// </summary>
public static class MarineCombatPlanner
{
    private const float SupportRadius = 9f;
    private const float SafetyMargin = 1.15f;

    public static IReadOnlyList<MarineTacticalDecision> Plan(
        NormalizedObservation observation,
        IReadOnlyDictionary<uint, CombatWeaponProfile> weaponsByUnitType,
        IReadOnlySet<ulong>? destructibleNeutralTags = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(weaponsByUnitType);

        var live = observation.Units
            .Where(unit => unit.IsLiveVisible && unit.HasPosition &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .ToArray();
        var marines = live.Where(unit => unit.Alliance == UnitAlliance.Self &&
            unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase)).ToArray();
        var targets = live.Where(unit =>
            (unit.Alliance == UnitAlliance.Enemy ||
             (unit.Alliance == UnitAlliance.Neutral &&
              destructibleNeutralTags?.Contains(unit.Tag) == true)) &&
            !IsNonCombatResource(unit.UnitKind)).ToArray();

        var decisions = new List<MarineTacticalDecision>(marines.Length);
        foreach (var marine in marines.OrderBy(unit => unit.Tag))
        {
            if (!weaponsByUnitType.TryGetValue(marine.UnitTypeId, out var ownWeapon) ||
                !ownWeapon.IsUsable)
            {
                decisions.Add(new(marine.Tag, null, MarineTacticalAction.Hold, 0, 0,
                    "Marine weapon profile unavailable; refusing to guess range or damage"));
                continue;
            }

            // A destructible or structure may have no weapon profile of its own;
            // it remains a valid opportunistic target. Only friendly/enemy DPS
            // calculations require weapon metadata.
            var target = targets
                .Where(candidate => ownWeapon.CanHit(candidate.IsFlying))
                .OrderBy(candidate => DistanceSquared(marine, candidate))
                .ThenBy(candidate => candidate.Tag)
                .FirstOrDefault();
            if (target is null)
            {
                decisions.Add(new(marine.Tag, null, MarineTacticalAction.Hold, 0, 0,
                    "No visible attackable target"));
                continue;
            }

            var friendlyDps = marines
                .Where(ally => DistanceSquared(ally, target) <= SupportRadius * SupportRadius &&
                    weaponsByUnitType.TryGetValue(ally.UnitTypeId, out var profile) && profile.IsUsable && profile.CanHit(target.IsFlying))
                .Sum(ally => weaponsByUnitType[ally.UnitTypeId].DamagePerSecond);

            var threatDps = live
                .Where(enemy => enemy.Alliance == UnitAlliance.Enemy &&
                    DistanceSquared(enemy, marine) <= SupportRadius * SupportRadius &&
                    weaponsByUnitType.TryGetValue(enemy.UnitTypeId, out var profile) && profile.IsUsable && profile.CanHit(marine.IsFlying))
                .Sum(enemy => weaponsByUnitType[enemy.UnitTypeId].DamagePerSecond);

            var distance = MathF.Sqrt((float)DistanceSquared(marine, target));
            MarineTacticalAction action;
            string reason;

            if (threatDps > 0 && friendlyDps < threatDps * SafetyMargin)
            {
                action = MarineTacticalAction.Withdraw;
                reason = "Local friendly damage output does not cover nearby threat";
            }
            else if (distance <= ownWeapon.Range)
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

            decisions.Add(new(marine.Tag, target.Tag, action, friendlyDps, threatDps, reason));
        }

        return decisions;
    }

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
