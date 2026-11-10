using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Converts deterministic Marine decisions into validated raw SC2 commands.
/// It emits no guessed ability IDs and suppresses duplicate active orders.
/// </summary>
public static class Sc2MarineCombatActionTranslator
{
    public static IReadOnlyList<SC2APIProtocol.Action> Translate(
        IReadOnlyList<MarineTacticalDecision> decisions,
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        IReadOnlyDictionary<uint, CombatWeaponProfile> weapons)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(weapons);

        if (gameData.AttackAbilityId is not > 0 || gameData.MoveAbilityId is not > 0)
            return Array.Empty<SC2APIProtocol.Action>();

        var units = observation.Units
            .Where(unit => unit.IsLiveVisible && unit.HasPosition &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .ToDictionary(unit => unit.Tag);
        var marines = units.Values
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var actions = new List<SC2APIProtocol.Action>();

        foreach (var decision in decisions.OrderBy(item => item.MarineTag))
        {
            if (!units.TryGetValue(decision.MarineTag, out var marine) ||
                marine.Alliance != UnitAlliance.Self ||
                !string.Equals(marine.UnitKind, "Marine", StringComparison.OrdinalIgnoreCase) ||
                !decision.TargetTag.HasValue ||
                !units.TryGetValue(decision.TargetTag.Value, out var target))
                continue;

            if (decision.Action == MarineTacticalAction.Fire)
            {
                var attackId = (int)gameData.AttackAbilityId.Value;
                if (HasUnitTargetOrder(marine, attackId, target.Tag))
                    continue;

                actions.Add(CreateUnitCommand(marine.Tag, attackId, target.Tag));
                continue;
            }

            if (decision.Action is not (MarineTacticalAction.RepositionDuringCooldown or
                MarineTacticalAction.CloseToWeaponRange or MarineTacticalAction.Withdraw))
                continue;

            if (!weapons.TryGetValue(marine.UnitTypeId, out var ownWeapon) || !ownWeapon.IsUsable)
                continue;

            var destination = decision.Action switch
            {
                MarineTacticalAction.Withdraw => RetreatPoint(
                    marine,
                    units.Values.Where(unit => unit.Alliance == UnitAlliance.Enemy)
                        .OrderBy(unit => DistanceSquared(marine.PositionX, marine.PositionY, unit.PositionX, unit.PositionY))
                        .FirstOrDefault() ?? target,
                    marines),
                MarineTacticalAction.CloseToWeaponRange => ApproachPoint(marine, target, ownWeapon.GetRange(target.IsFlying)),
                _ => CooldownPoint(marine, target, ownWeapon.GetRange(target.IsFlying), weapons)
            };

            if (!float.IsFinite(destination.X) || !float.IsFinite(destination.Y))
                continue;

            var moveId = (int)gameData.MoveAbilityId.Value;
            if (HasPositionOrderNear(marine, moveId, destination.X, destination.Y))
                continue;

            actions.Add(CreatePositionCommand(marine.Tag, moveId, destination.X, destination.Y));
        }

        return actions;
    }

    private static (float X, float Y) ApproachPoint(
        UnitObservationFacts marine, UnitObservationFacts target, float weaponRange)
    {
        var (dx, dy, distance) = Direction(marine, target);
        var desiredDistance = MathF.Max(0.5f, weaponRange - 0.5f);
        var travel = MathF.Max(0, distance - desiredDistance);
        return (marine.PositionX - dx * travel, marine.PositionY - dy * travel);
    }

    private static (float X, float Y) CooldownPoint(
        UnitObservationFacts marine,
        UnitObservationFacts target,
        float ownWeaponRange,
        IReadOnlyDictionary<uint, CombatWeaponProfile> weapons)
    {
        var (dx, dy, distance) = Direction(marine, target);
        if (target.Alliance == UnitAlliance.Enemy &&
            weapons.TryGetValue(target.UnitTypeId, out var enemyWeapon) &&
            enemyWeapon.IsUsable && enemyWeapon.GetRange(marine.IsFlying) >= ownWeaponRange)
        {
            // Step beyond the enemy envelope. On the next observation the
            // approach decision naturally brings the Marine back to firing range.
            var desiredDistance = enemyWeapon.GetRange(marine.IsFlying) + 0.5f;
            var travel = MathF.Max(0.75f, desiredDistance - distance);
            return (marine.PositionX + dx * travel, marine.PositionY + dy * travel);
        }

        // Otherwise orbit the target during cooldown instead of standing idle.
        var sign = (marine.Tag & 1) == 0 ? 1f : -1f;
        const float orbitStep = 1.25f;
        return (marine.PositionX - dy * orbitStep * sign,
                marine.PositionY + dx * orbitStep * sign);
    }

    private static (float X, float Y) RetreatPoint(
        UnitObservationFacts marine,
        UnitObservationFacts nearestThreat,
        IReadOnlyList<UnitObservationFacts> marines)
    {
        var supporters = marines.Where(unit => unit.Tag != marine.Tag).ToArray();
        if (supporters.Length > 0)
        {
            var centerX = supporters.Average(unit => unit.PositionX);
            var centerY = supporters.Average(unit => unit.PositionY);
            var dx = centerX - marine.PositionX;
            var dy = centerY - marine.PositionY;
            var length = MathF.Sqrt(dx * dx + dy * dy);
            if (length > 0.01f)
                return (marine.PositionX + dx / length * 3f,
                        marine.PositionY + dy / length * 3f);
        }

        var (awayX, awayY, _) = Direction(marine, nearestThreat);
        return (marine.PositionX + awayX * 5f, marine.PositionY + awayY * 5f);
    }

    private static (float X, float Y, float Distance) Direction(
        UnitObservationFacts marine, UnitObservationFacts target)
    {
        var dx = marine.PositionX - target.PositionX;
        var dy = marine.PositionY - target.PositionY;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance <= 0.001f)
            return (1f, 0f, 0f);
        return (dx / distance, dy / distance, distance);
    }

    private static bool HasUnitTargetOrder(UnitObservationFacts unit, int abilityId, ulong targetTag) =>
        unit.Orders.Any(order => order.AbilityId == abilityId && order.TargetUnitTag == targetTag);

    private static bool HasPositionOrderNear(
        UnitObservationFacts unit, int abilityId, float x, float y) =>
        unit.Orders.Any(order => order.AbilityId == abilityId &&
            order.TargetPositionX.HasValue && order.TargetPositionY.HasValue &&
            DistanceSquared(order.TargetPositionX.Value, order.TargetPositionY.Value, x, y) <= 9f);

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    private static SC2APIProtocol.Action CreateUnitCommand(ulong unitTag, int abilityId, ulong targetTag) =>
        new()
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = abilityId,
                    TargetUnitTag = targetTag,
                    UnitTags = { unitTag },
                    QueueCommand = false
                }
            }
        };

    private static SC2APIProtocol.Action CreatePositionCommand(
        ulong unitTag, int abilityId, float x, float y) =>
        new()
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = abilityId,
                    TargetWorldSpacePos = new Point2D { X = x, Y = y },
                    UnitTags = { unitTag },
                    QueueCommand = false
                }
            }
        };
}
