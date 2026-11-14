using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Reconciles completed units and visible in-progress production against a
/// race-specific profile. Zerg Egg orders count as the profile's full output
/// batch (two Zerglings for one Larva order).
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

        var inProgressUnits = selfUnits.Count(unit =>
            unit.UnitKind.Equals(target.Name, StringComparison.OrdinalIgnoreCase) &&
            unit.BuildProgress < 1f);

        // A Zerg Egg is a single visible container for the full result of the
        // morph order. Count its target ability once, then expand by UnitsPerOrder.
        var inProgressOrders = selfUnits.Count(unit =>
            unit.UnitKind.Equals("Egg", StringComparison.OrdinalIgnoreCase) &&
            unit.Orders.Any(order => order.AbilityId == target.AbilityId));

        var eggOutput = checked(inProgressOrders * profile.UnitsPerOrder);
        return new Sc2RaceProductionProgress(
            completed,
            checked(inProgressUnits + eggOutput),
            inProgressOrders);
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
