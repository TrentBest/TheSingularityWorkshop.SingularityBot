#nullable enable annotations
using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Translates a verified structure-placement decision into one SC2 raw build
/// command. Placement search/query and worker reservation remain host-owned;
/// this helper refuses to invent an ability, worker, or legal location.
/// </summary>
public static class Sc2RaceBuildingActionTranslator
{
    public static Sc2RaceBuildingTranslationResult Translate(
        Sc2UnitFacts structure,
        Sc2UnitFacts workerType,
        Sc2AbilityFacts buildAbility,
        NormalizedObservation observation,
        ulong workerTag,
        float placementX,
        float placementY,
        IReadOnlySet<ulong> unavailableWorkerTags,
        IReadOnlySet<uint>? interruptibleOrderAbilityIds = null)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(workerType);
        ArgumentNullException.ThrowIfNull(buildAbility);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(unavailableWorkerTags);

        if (structure.UnitId == 0 || structure.AbilityId == 0 ||
            buildAbility.AbilityId != structure.AbilityId ||
            !buildAbility.IsBuilding)
            return Block("Structure build ability is missing, mismatched, or not identified as a building ability.");

        if (workerTag == 0 || !float.IsFinite(placementX) || !float.IsFinite(placementY))
            return Block("A valid worker tag and finite placement coordinates are required.");

        if (structure.MineralCost == 0)
            return Block("Authoritative structure mineral cost is unavailable.");

        var constructionPolicy = workerType.Name.ToUpperInvariant() switch
        {
            "SCV" => WorkerConstructionPolicy.For(Race.Terran),
            "PROBE" => WorkerConstructionPolicy.For(Race.Protoss),
            "DRONE" => WorkerConstructionPolicy.For(Race.Zerg),
            _ => null
        };
        if (constructionPolicy is null)
            return Block("The selected worker type has no registered race construction policy.");

        var worker = observation.Units.SingleOrDefault(unit => unit.Tag == workerTag);
        if (worker is null ||
            worker.Alliance != UnitAlliance.Self ||
            !worker.IsLiveVisible ||
            !worker.HasPosition ||
            observation.DeadUnitTags.Contains(workerTag) ||
            !worker.UnitKind.Equals(workerType.Name, StringComparison.OrdinalIgnoreCase))
            return Block("The selected worker is not a live, visible self unit of the resolved worker type.");

        var hasNonInterruptibleOrder = worker.HasOrders &&
            (interruptibleOrderAbilityIds is null || worker.Orders.Count == 0 ||
             worker.Orders.Any(order => !interruptibleOrderAbilityIds.Contains(order.AbilityId)));
        if (hasNonInterruptibleOrder ||
            !constructionPolicy.CanBeginConstruction(worker.HasCargo) ||
            unavailableWorkerTags.Contains(workerTag))
            return Block("The selected worker has a non-interruptible order, violates its race cargo policy, or is already reserved.");

        if (observation.Minerals < structure.MineralCost ||
            observation.Vespene < structure.VespeneCost)
            return Block("Current resources cannot cover the structure cost.");

        var command = new SC2APIProtocol.Action
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = checked((int)buildAbility.AbilityId),
                    UnitTags = { workerTag },
                    TargetWorldSpacePos = new Point2D { X = placementX, Y = placementY },
                    QueueCommand = false
                }
            }
        };

        return new Sc2RaceBuildingTranslationResult(
            new[] { command }, workerTag, structure.MineralCost,
            structure.VespeneCost, string.Empty)
        {
            WorkerDisposition = constructionPolicy.Disposition
        };
    }

    private static Sc2RaceBuildingTranslationResult Block(string reason) =>
        new(Array.Empty<SC2APIProtocol.Action>(), 0, 0, 0, reason);
}

/// <summary>One building command and the worker/resource reservation it requires.</summary>
public sealed record Sc2RaceBuildingTranslationResult(
    IReadOnlyList<SC2APIProtocol.Action> Actions,
    ulong WorkerTag,
    uint ReservedMinerals,
    uint ReservedVespene,
    string BlockReason)
{
    /// <summary>Race-specific lifecycle the host must apply after construction is accepted.</summary>
    public WorkerConstructionDisposition WorkerDisposition { get; init; } =
        WorkerConstructionDisposition.RemainsUntilComplete;

    public bool IsBlocked => Actions.Count == 0;
}
