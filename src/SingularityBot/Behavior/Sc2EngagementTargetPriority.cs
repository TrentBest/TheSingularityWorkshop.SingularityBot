using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Gives opening pressure a small, evidence-based target hierarchy. It does not
/// infer construction from proximity alone: the worker must have an observed
/// order whose ability is identified by authoritative SC2 game data as a build
/// ability, and must be near a visibly unfinished production structure.
/// </summary>
public static class Sc2EngagementTargetPriority
{
    private static readonly HashSet<string> ProductionStructures = new(StringComparer.OrdinalIgnoreCase)
    {
        "Barracks", "Gateway", "SpawningPool", "RoachWarren",
        "Factory", "Starport", "CyberneticsCore", "Stargate",
        "RoboticsFacility", "HydraliskDen", "BanelingNest"
    };

    public static int Score(
        UnitObservationFacts candidate,
        IReadOnlyList<UnitObservationFacts> visibleEnemyUnits,
        IReadOnlySet<uint> constructionAbilityIds)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(visibleEnemyUnits);
        ArgumentNullException.ThrowIfNull(constructionAbilityIds);

        if (candidate.Alliance != UnitAlliance.Enemy || !candidate.IsLiveVisible)
            return 0;

        if (IsWorker(candidate) && IsObservedBuilder(candidate, visibleEnemyUnits, constructionAbilityIds))
            return 3;

        if (candidate.UnitKind.Equals("Probe", StringComparison.OrdinalIgnoreCase))
            return 2;

        return 1;
    }

    private static bool IsObservedBuilder(
        UnitObservationFacts worker,
        IReadOnlyList<UnitObservationFacts> visibleEnemyUnits,
        IReadOnlySet<uint> constructionAbilityIds)
    {
        if (!worker.HasPosition ||
            !worker.Orders.Any(order => constructionAbilityIds.Contains(order.AbilityId)))
            return false;

        return visibleEnemyUnits.Any(structure =>
            structure.Alliance == UnitAlliance.Enemy &&
            structure.IsLiveVisible &&
            structure.HasPosition &&
            structure.BuildProgress > 0f &&
            structure.BuildProgress < 1f &&
            ProductionStructures.Contains(structure.UnitKind) &&
            DistanceSquared(worker, structure) <= 36d);
    }

    private static bool IsWorker(UnitObservationFacts unit) =>
        unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) ||
        unit.UnitKind.Equals("Probe", StringComparison.OrdinalIgnoreCase) ||
        unit.UnitKind.Equals("Drone", StringComparison.OrdinalIgnoreCase);

    private static double DistanceSquared(UnitObservationFacts left, UnitObservationFacts right)
    {
        var dx = left.PositionX - right.PositionX;
        var dy = left.PositionY - right.PositionY;
        return (double)dx * dx + (double)dy * dy;
    }
}
