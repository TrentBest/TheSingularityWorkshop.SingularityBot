using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Reconciles completed units and visible in-progress production against a
/// race-specific profile. Producer orders are counted as output commitments;
/// Zerg Egg orders are counted on their Egg container rather than on Larva.
/// </summary>
public static class Sc2RaceProductionProgressReconciler
{
    public static Sc2RaceProductionProgress Reconcile(
        Sc2RaceProductionProfile profile,
        Sc2UnitFacts target,
        NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(observation);

        if (profile.UnitsPerOrder <= 0 ||
            target.AbilityId == 0 ||
            !string.Equals(profile.TargetUnit, target.Name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The profile and resolved target game data do not agree.", nameof(profile));

        var selfUnits = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .ToArray();

        var completed = selfUnits.Count(unit =>
            unit.UnitKind.Equals(target.Name, StringComparison.OrdinalIgnoreCase) &&
            unit.BuildProgress >= 1f);

        var observedInProgressUnits = selfUnits.Count(unit =>
            unit.UnitKind.Equals(target.Name, StringComparison.OrdinalIgnoreCase) &&
            unit.BuildProgress < 1f);

        // Training orders live on their producer (Barracks, Gateway, Hatchery,
        // etc.). Zerg morph orders instead live on an Egg. Count the order once,
        // then compare its expected output with visible target units so a unit
        // represented by both observations is not double-counted.
        var producerOrders = selfUnits
            .Where(unit => unit.UnitKind.Equals(profile.ProducerUnit, StringComparison.OrdinalIgnoreCase))
            .Sum(unit => unit.Orders.Count(order => order.AbilityId == target.AbilityId));
        var eggOrders = selfUnits
            .Where(unit => unit.UnitKind.Equals("Egg", StringComparison.OrdinalIgnoreCase))
            .Sum(unit => unit.Orders.Count(order => order.AbilityId == target.AbilityId));

        // A visible incomplete unit may be the output of a producer order, so
        // use the larger count for that lane. An Egg is itself the Zerg morph
        // container: its paired output is additional to any separately visible
        // incomplete target unit.
        var producerOrderOutput = checked(producerOrders * profile.UnitsPerOrder);
        var eggOrderOutput = checked(eggOrders * profile.UnitsPerOrder);
        var inProgress = checked(eggOrderOutput + Math.Max(observedInProgressUnits, producerOrderOutput));
        var inProgressOrders = checked(producerOrders + eggOrders);
        return new Sc2RaceProductionProgress(completed, inProgress, inProgressOrders);
    }
}

/// <summary>Observed production progress expressed in output units and source orders.</summary>
public sealed record Sc2RaceProductionProgress(
    int CompletedUnits,
    int InProgressUnits,
    int InProgressOrders)
{
    public int AccountedUnits => checked(CompletedUnits + InProgressUnits);
}
