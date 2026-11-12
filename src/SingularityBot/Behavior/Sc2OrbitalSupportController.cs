using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Converts idle Command Centers into Orbitals and spends available Orbital
/// energy on MULEs. Ability identifiers and upgrade costs come only from
/// the match's ResponseData; unsupported metadata disables the action.
/// </summary>
public sealed class Sc2OrbitalSupportController
{
    private ulong? _pendingUnitTag;
    private uint _pendingAbilityId;
    private uint _pendingGameLoop;

    /// <summary>
    /// Reserves the first-base Command Center for its one-time Orbital morph as soon
    /// as the tech and mineral gates are satisfied. The reservation intentionally
    /// survives an existing SCV order so production cannot refill the queue when it
    /// completes before the morph can be issued.
    /// </summary>
    public ulong? GetCommandCenterReservedForUpgrade(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        uint reservedMinerals = 0)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);

        var townHalls = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                !observation.DeadUnitTags.Contains(unit.Tag) &&
                (IsKind(unit, "CommandCenter") || IsKind(unit, "OrbitalCommand") ||
                 IsKind(unit, "PlanetaryFortress")))
            .OrderBy(unit => unit.Tag)
            .ToArray();
        // SC2 unit tags are stable and the starting town hall predates later
        // expansions. Keep the first-base upgrade target stable as the economy grows.
        var firstBaseTownHall = townHalls.FirstOrDefault();

        var orbitalTechReady =
            observation.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && IsKind(unit, "SupplyDepot") && unit.BuildProgress >= 1f) &&
            observation.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && IsKind(unit, "Barracks") && unit.BuildProgress >= 1f);

        if (firstBaseTownHall is null || !IsKind(firstBaseTownHall, "CommandCenter") ||
            !orbitalTechReady || gameData.OrbitalUpgradeAbilityId is not > 0 ||
            gameData.OrbitalUpgradeMineralCost == 0 ||
            observation.Minerals < gameData.OrbitalUpgradeMineralCost + reservedMinerals)
            return null;

        return firstBaseTownHall.Tag;
    }

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        uint reservedMinerals = 0)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);

        var townHalls = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                !observation.DeadUnitTags.Contains(unit.Tag) &&
                (IsKind(unit, "CommandCenter") || IsKind(unit, "OrbitalCommand") ||
                 IsKind(unit, "PlanetaryFortress")))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        if (_pendingUnitTag is ulong pendingTag)
        {
            var pendingUnit = townHalls.FirstOrDefault(unit => unit.Tag == pendingTag);
            if (pendingUnit is null)
            {
                ClearPending();
            }
            else if (pendingUnit.Orders.Any(order => order.AbilityId == _pendingAbilityId))
            {
                ClearPending();
            }
            else if (observation.GameLoop <= _pendingGameLoop + 8)
            {
                return null;
            }
            else
            {
                ClearPending();
            }
        }

        // Orbital capability unlocks with the first completed Barracks. Upgrade
        // immediately when the Depot prerequisite and resource reservations permit;
        // waiting for a second Barracks delays the first energy/MULE utility.
        var orbitalTechReady =
            observation.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && IsKind(unit, "SupplyDepot") && unit.BuildProgress >= 1f) &&
            observation.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && IsKind(unit, "Barracks") && unit.BuildProgress >= 1f);

        // Convert only the original starting Command Center. Later expansion
        // town halls must not steal the one-time opening upgrade, and their
        // existence must not make the starting base permanently ineligible.
        var firstBaseTownHall = townHalls.FirstOrDefault();
        if (firstBaseTownHall is not null && IsKind(firstBaseTownHall, "CommandCenter") &&
            orbitalTechReady && gameData.OrbitalUpgradeAbilityId is > 0 &&
            gameData.OrbitalUpgradeMineralCost > 0 && !firstBaseTownHall.HasOrders &&
            observation.Minerals >= gameData.OrbitalUpgradeMineralCost + reservedMinerals)
        {
            return Submit(firstBaseTownHall.Tag, gameData.OrbitalUpgradeAbilityId.Value,
                observation.GameLoop);
        }

        if (gameData.MuleCallDownAbilityId is not > 0 ||
            !gameData.TryGetAbility(gameData.MuleCallDownAbilityId.Value, out var muleAbility) ||
            !float.IsFinite(muleAbility.CastRange) || muleAbility.CastRange <= 0)
            return null;

        var mineralFields = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Neutral && unit.IsLiveVisible &&
                unit.HasPosition && unit.UnitKind.StartsWith("MineralField", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .ToArray();
        if (mineralFields.Length == 0)
            return null;

        var orbital = townHalls
            .Where(unit => IsKind(unit, "OrbitalCommand") && unit.Energy >= 50 &&
                !unit.Orders.Any(order => order.AbilityId == gameData.MuleCallDownAbilityId.Value))
            .OrderByDescending(unit => unit.Energy)
            .ThenBy(unit => unit.Tag)
            .FirstOrDefault();
        if (orbital is null)
            return null;

        var maximumRangeSquared = muleAbility.CastRange * muleAbility.CastRange;
        var mineral = mineralFields
            .Where(unit => DistanceSquared(orbital, unit) <= maximumRangeSquared)
            .OrderBy(unit => DistanceSquared(orbital, unit))
            .ThenBy(unit => unit.Tag)
            .FirstOrDefault();
        return mineral is null
            ? null
            : Submit(orbital.Tag, gameData.MuleCallDownAbilityId.Value,
                observation.GameLoop, mineral.Tag);
    }

    public void OnActionResult(ActionResult? result)
    {
        if (result is null || result != ActionResult.Success)
            ClearPending();
    }

    private SC2APIProtocol.Action Submit(
        ulong unitTag,
        uint abilityId,
        uint gameLoop,
        ulong? targetTag = null)
    {
        _pendingUnitTag = unitTag;
        _pendingAbilityId = abilityId;
        _pendingGameLoop = gameLoop;
        var command = new ActionRawUnitCommand
        {
            AbilityId = checked((int)abilityId),
            QueueCommand = false
        };
        command.UnitTags.Add(unitTag);
        if (targetTag is ulong target)
            command.TargetUnitTag = target;
        return new SC2APIProtocol.Action { ActionRaw = new ActionRaw { UnitCommand = command } };
    }

    private void ClearPending()
    {
        _pendingUnitTag = null;
        _pendingAbilityId = 0;
        _pendingGameLoop = 0;
    }

    private static bool IsKind(UnitObservationFacts unit, string kind) =>
        unit.UnitKind.Equals(kind, StringComparison.OrdinalIgnoreCase);

    private static float DistanceSquared(UnitObservationFacts left, UnitObservationFacts right)
    {
        var dx = left.PositionX - right.PositionX;
        var dy = left.PositionY - right.PositionY;
        return dx * dx + dy * dy;
    }
}
