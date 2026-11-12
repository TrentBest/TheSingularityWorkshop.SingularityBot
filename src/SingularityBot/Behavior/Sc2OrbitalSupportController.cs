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
    /// Legacy first-base reservation query retained for isolated controller callers.
    /// The match loop now reserves the exact Town Hall selected by TownHallUpgradeFsm.
    /// Do not wait until the 150-mineral threshold: doing so lets continuous SCV
    /// production spend each new 50-mineral increment and starve the morph forever.
    /// The reservation survives an existing SCV order so production cannot refill
    /// the queue when it completes before the morph can be issued.
    /// </summary>
    public ulong? GetCommandCenterReservedForUpgrade(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData)
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
                unit.IsLiveVisible && IsSupplyDepot(unit) && unit.BuildProgress >= 1f) &&
            observation.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && IsKind(unit, "Barracks") && unit.BuildProgress >= 1f);

        if (firstBaseTownHall is null || !IsKind(firstBaseTownHall, "CommandCenter") ||
            !orbitalTechReady || gameData.OrbitalUpgradeMineralCost == 0)
            return null;

        return firstBaseTownHall.Tag;
    }

    /// <summary>
    /// Indicates that the starting Command Center can accept its Orbital morph
    /// this frame. The opening uses this to prevent a new SCV order from being
    /// submitted to the same producer in the same action batch.
    /// </summary>
    public bool IsUpgradeReadyToSubmit(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        ulong? permittedCommandCenterTag = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);

        if (permittedCommandCenterTag is not ulong permittedTag ||
            gameData.OrbitalUpgradeAbilityId is not > 0 ||
            gameData.OrbitalUpgradeMineralCost == 0 ||
            observation.Minerals < gameData.OrbitalUpgradeMineralCost)
            return false;

        var commandCenter = observation.Units.FirstOrDefault(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag) &&
            unit.Tag == permittedTag &&
            IsKind(unit, "CommandCenter"));
        if (commandCenter is null)
            return false;

        var orbitalTechReady =
            observation.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && IsSupplyDepot(unit) && unit.BuildProgress >= 1f) &&
            observation.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && IsKind(unit, "Barracks") && unit.BuildProgress >= 1f);
        if (!orbitalTechReady)
            return false;

        return gameData.ScvTrainAbilityId is > 0
            ? !commandCenter.Orders.Any(order => order.AbilityId == gameData.ScvTrainAbilityId.Value)
            : !commandCenter.HasOrders;
    }

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        uint reservedMinerals = 0,
        bool allowOrbitalUpgrade = true,
        ulong? permittedCommandCenterTag = null,
        IReadOnlySet<ulong>? reservedOrbitalTags = null)
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
                unit.IsLiveVisible && IsSupplyDepot(unit) && unit.BuildProgress >= 1f) &&
            observation.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && IsKind(unit, "Barracks") && unit.BuildProgress >= 1f);

        // The match loop supplies the exact Town Hall admitted by its Orbital
        // upgrade FSM. Without an explicit tag, retain first-base behavior for
        // direct controller callers and legacy tests.
        var firstBaseTownHall = permittedCommandCenterTag is ulong permittedTag
            ? townHalls.FirstOrDefault(unit => unit.Tag == permittedTag)
            : townHalls.FirstOrDefault();
        // A Command Center's persistent worker-rally order is not a production
        // queue and must not block its Orbital morph. Only an observed SCV-train
        // order makes the town hall busy for this transition. If SCV ability data
        // is unavailable, retain the conservative HasOrders guard.
        // Once SC2 reports the morph order, it remains authoritative even
        // after the local submission-retry window expires. Do not reissue the
        // upgrade every frame while the unit is still represented as a
        // CommandCenter during the transition.
        var orbitalUpgradeAlreadyQueued = firstBaseTownHall is not null &&
            gameData.OrbitalUpgradeAbilityId is > 0 &&
            firstBaseTownHall.Orders.Any(order =>
                order.AbilityId == gameData.OrbitalUpgradeAbilityId.Value);
        if (orbitalUpgradeAlreadyQueued)
            return null;

        var hasScvTrainingOrder = gameData.ScvTrainAbilityId is > 0
            ? firstBaseTownHall?.Orders.Any(order => order.AbilityId == gameData.ScvTrainAbilityId.Value) == true
            : firstBaseTownHall?.HasOrders == true;

        if (allowOrbitalUpgrade && firstBaseTownHall is not null && IsKind(firstBaseTownHall, "CommandCenter") &&
            orbitalTechReady && gameData.OrbitalUpgradeAbilityId is > 0 &&
            gameData.OrbitalUpgradeMineralCost > 0 && !hasScvTrainingOrder &&
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
                !(reservedOrbitalTags?.Contains(unit.Tag) ?? false) &&
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

    private static bool IsSupplyDepot(UnitObservationFacts unit) =>
        IsKind(unit, "SupplyDepot") || IsKind(unit, "SupplyDepotLowered");

    private static bool IsKind(UnitObservationFacts unit, string kind) =>
        unit.UnitKind.Equals(kind, StringComparison.OrdinalIgnoreCase);

    private static float DistanceSquared(UnitObservationFacts left, UnitObservationFacts right)
    {
        var dx = left.PositionX - right.PositionX;
        var dy = left.PositionY - right.PositionY;
        return dx * dx + dy * dy;
    }
}
