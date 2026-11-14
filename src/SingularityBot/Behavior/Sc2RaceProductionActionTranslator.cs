using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Builds race-profile production commands from authoritative game data and the
/// current observation. It is deliberately stateless: the caller owns submission
/// latches, producer reservations, and action-result reconciliation.
/// </summary>
public static class Sc2RaceProductionActionTranslator
{
    public static Sc2RaceProductionTranslation Translate(
        Sc2RaceProductionProfile profile,
        Sc2UnitFacts target,
        Sc2UnitFacts producer,
        NormalizedObservation observation,
        int desiredRemainingUnits,
        IReadOnlySet<ulong>? unavailableProducerTags = null,
        Func<UnitObservationFacts, bool>? hasPositivePowerEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(unavailableProducerTags);

        if (desiredRemainingUnits <= 0 || profile.UnitsPerOrder <= 0 ||
            target.AbilityId == 0 || target.AbilityId > int.MaxValue ||
            target.MineralCost == 0 ||
            !string.Equals(target.Name, profile.TargetUnit, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(producer.Name, profile.ProducerUnit, StringComparison.OrdinalIgnoreCase))
            return Sc2RaceProductionTranslation.Blocked("Production profile or resolved game data is invalid.");

        if (!profile.RequiredCompletedUnits.All(required => observation.Units.Any(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                !observation.DeadUnitTags.Contains(unit.Tag) &&
                unit.BuildProgress >= 1f &&
                unit.UnitKind.Equals(required, StringComparison.OrdinalIgnoreCase))))
            return Sc2RaceProductionTranslation.Blocked("One or more required structures are not visibly completed.");

        if (profile.RequiresPoweredProducer && hasPositivePowerEvidence is null)
            return Sc2RaceProductionTranslation.Blocked("Powered producer evidence is unavailable; no command emitted.");

        var mineralCostPerOrder = checked((ulong)target.MineralCost * (uint)profile.UnitsPerOrder);
        var vespeneCostPerOrder = checked((ulong)target.VespeneCost * (uint)profile.UnitsPerOrder);
        var foodPerOrder = target.FoodRequired * profile.UnitsPerOrder;
        if (!float.IsFinite(foodPerOrder) || foodPerOrder < 0)
            return Sc2RaceProductionTranslation.Blocked("Resolved food cost is invalid.");

        var availableFood = observation.FoodCap >= observation.FoodUsed
            ? observation.FoodCap - observation.FoodUsed
            : 0;
        var maxByMinerals = mineralCostPerOrder == 0 ? int.MaxValue :
            (int)Math.Min(int.MaxValue, (ulong)observation.Minerals / mineralCostPerOrder);
        var maxByVespene = vespeneCostPerOrder == 0 ? int.MaxValue :
            (int)Math.Min(int.MaxValue, (ulong)observation.Vespene / vespeneCostPerOrder);
        var maxByFood = foodPerOrder <= 0 ? int.MaxValue :
            (int)Math.Min(int.MaxValue, (ulong)Math.Floor(availableFood / foodPerOrder + 0.0001f));
        var maxOrders = Math.Min(
            (desiredRemainingUnits + profile.UnitsPerOrder - 1) / profile.UnitsPerOrder,
            Math.Min(maxByMinerals, Math.Min(maxByVespene, maxByFood)));

        if (maxOrders <= 0)
            return Sc2RaceProductionTranslation.Blocked("Insufficient unreserved resources or supply for another production order.");

        var producers = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && !observation.DeadUnitTags.Contains(unit.Tag) &&
                unit.UnitKind.Equals(producer.Name, StringComparison.OrdinalIgnoreCase) &&
                !unit.HasOrders && !unit.HasCargo &&
                !unavailableProducerTags.Contains(unit.Tag) &&
                (!profile.RequiresPoweredProducer || hasPositivePowerEvidence!(unit)))
            .OrderBy(unit => unit.Tag)
            .Take(maxOrders)
            .ToArray();

        if (producers.Length == 0)
            return Sc2RaceProductionTranslation.Blocked(
                profile.RequiresPoweredProducer
                    ? "No idle producer has positive power evidence."
                    : "No idle producer is currently available.");

        var actions = producers.Select(unit => new SC2APIProtocol.Action
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = checked((int)target.AbilityId),
                    UnitTags = { unit.Tag },
                    QueueCommand = false
                }
            }
        }).ToArray();

        return new Sc2RaceProductionTranslation(
            actions,
            producers.Select(unit => unit.Tag).ToArray(),
            checked((uint)(mineralCostPerOrder * (uint)producers.Length)),
            checked((uint)(vespeneCostPerOrder * (uint)producers.Length)),
            foodPerOrder * producers.Length,
            profile.UnitsPerOrder * producers.Length,
            string.Empty);
    }
}

/// <summary>Translated production commands and their caller-owned reservation facts.</summary>
public sealed record Sc2RaceProductionTranslation(
    IReadOnlyList<SC2APIProtocol.Action> Actions,
    IReadOnlyList<ulong> ProducerTags,
    uint ReservedMinerals,
    uint ReservedVespene,
    float ReservedFood,
    int ExpectedOutputUnits,
    string BlockReason)
{
    public bool IsBlocked => Actions.Count == 0;

    public static Sc2RaceProductionTranslation Blocked(string reason) =>
        new(Array.Empty<SC2APIProtocol.Action>(), Array.Empty<ulong>(), 0, 0, 0, 0, reason);
}
