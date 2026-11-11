using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Starts one additional Command Center at the nearest observed friendly-side
/// mineral expansion. The selected expansion coordinate is retained for the
/// later Orbital flight/landing controller.
/// </summary>
public sealed class Sc2ExpansionCommandCenterController
{
    private enum PendingAction { None, Move, Build }
    private ulong? _builderTag;
    private IReadOnlyList<MapPoint> _candidates = Array.Empty<MapPoint>();
    private MapPoint? _site;
    private bool _queryPending;
    private bool _moveSubmitted;
    private uint _moveLoop;
    private bool _buildSubmitted;
    private uint _buildLoop;
    private PendingAction _pending;
    private BuildJobContext _job;
    private readonly HashSet<ulong> _preexistingCommandCenterTags = new();

    public ulong? BuilderTag => _builderTag;
    public MapPoint? ExpansionLocation => _site;
    public bool IsPlacementQueryPending => _queryPending;
    public bool IsStarted { get; private set; }
    public bool IsComplete { get; private set; }

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        SpatialMap map,
        Sc2GameDataCatalog gameData,
        WorkerResourceAssignmentPlanner assignments,
        IReadOnlySet<ulong> constructionCrewTags,
        BuildJobRuntime buildJobs = null,
        ulong? preferredBuilderTag = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(constructionCrewTags);

        if (IsComplete || map is null || gameData.CommandCenterBuildAbilityId is not > 0 ||
            gameData.CommandCenterMineralCost == 0 || gameData.MoveAbilityId is not > 0)
            return null;

        var commandCenters = observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag) &&
            (IsKind(unit, "CommandCenter") || IsKind(unit, "OrbitalCommand") || IsKind(unit, "PlanetaryFortress")))
            .ToArray();

        if (_site is not null)
        {
            var structure = commandCenters
                .Where(unit => unit.HasPosition && !_preexistingCommandCenterTags.Contains(unit.Tag))
                .Select(unit => new { Unit = unit, Distance = DistanceSquared(unit.PositionX, unit.PositionY, _site.Value.X, _site.Value.Y) })
                .Where(item => item.Distance <= 16f)
                .OrderBy(item => item.Distance)
                .FirstOrDefault();
            if (structure is not null)
            {
                IsStarted = true;
                _job?.ObserveResult(structure.Unit.Tag, structure.Unit.BuildProgress, observation.GameLoop);
                if (structure.Unit.BuildProgress >= 1f)
                {
                    IsComplete = true;
                    if (_builderTag is ulong finishedBuilder)
                        assignments.Unreserve(finishedBuilder);
                    Console.WriteLine($"[Expansion] Command Center {structure.Unit.Tag} completed at preserved expansion site ({_site.Value.X:F1},{_site.Value.Y:F1}).");
                    return null;
                }
            }
        }

        if (_builderTag is ulong current &&
            (observation.DeadUnitTags.Contains(current) ||
             !observation.Units.Any(unit => unit.Tag == current && IsLiveScv(unit))))
        {
            assignments.Unreserve(current);
            _job?.Fail("Expansion Command Center builder was lost.", observation.GameLoop);
            _builderTag = null;
            _job = null;
            _candidates = Array.Empty<MapPoint>();
            _site = null;
            _queryPending = false;
            _moveSubmitted = false;
            _buildSubmitted = false;
            _pending = PendingAction.None;
            IsStarted = false;
        }

        if (IsStarted || commandCenters.Length >= 2)
            return null;

        if (_builderTag is null)
        {
            if (observation.Minerals < gameData.CommandCenterMineralCost)
                return null;
            _preexistingCommandCenterTags.Clear();
            foreach (var existing in commandCenters) _preexistingCommandCenterTags.Add(existing.Tag);
            _candidates = ExpansionSitePlanner.FindCommandCenterCandidates(observation, map);
            if (_candidates.Count == 0)
            {
                Console.WriteLine("[Expansion] No unoccupied friendly-side mineral expansion could be derived; Command Center construction remains safely disabled.");
                return null;
            }

            var staging = _candidates[0];
            var eligibleWorkers = observation.Units
                .Where(unit => IsLiveScv(unit) && !assignments.IsReserved(unit.Tag))
                .ToArray();
            var builder = eligibleWorkers
                .Where(unit => constructionCrewTags.Contains(unit.Tag) || unit.Tag == preferredBuilderTag)
                .OrderBy(unit => unit.Tag == preferredBuilderTag ? 0 : 1)
                .ThenBy(unit => DistanceSquared(unit.PositionX, unit.PositionY, staging.X, staging.Y))
                .ThenBy(unit => unit.Tag)
                .FirstOrDefault();

            // A missing or stale Construction-capability assignment must not
            // prevent the expansion from starting. Fall back to the nearest
            // unreserved SCV; Reserve() removes its mining assignment atomically.
            builder ??= eligibleWorkers
                .OrderBy(unit => DistanceSquared(unit.PositionX, unit.PositionY, staging.X, staging.Y))
                .ThenBy(unit => unit.Tag)
                .FirstOrDefault();
            if (builder is null)
                return null;

            _builderTag = builder.Tag;
            assignments.Reserve(builder.Tag);
            _queryPending = true;
            Console.WriteLine($"[Expansion] SCV {builder.Tag} reserved for Command Center; minerals={observation.Minerals}, candidateSites={_candidates.Count}.");
            return null;
        }

        if (_queryPending || _site is null)
            return null;

        var worker = observation.Units.FirstOrDefault(unit =>
            unit.Tag == _builderTag.Value && IsLiveScv(unit));
        if (worker is null)
            return null;
        var canFundCommandCenterAndNextScv =
            observation.Minerals >= gameData.CommandCenterMineralCost + gameData.ScvMineralCost;
        if (!worker.HasCargo && IsGatheringMinerals(worker, observation) &&
            !canFundCommandCenterAndNextScv)
            return null;

        if (DistanceSquared(worker.PositionX, worker.PositionY, _site.Value.X, _site.Value.Y) > 2.25f)
        {
            if (_moveSubmitted && observation.GameLoop <= _moveLoop + 22)
                return null;
            _moveSubmitted = true;
            _moveLoop = observation.GameLoop;
            _pending = PendingAction.Move;
            return PositionCommand(worker.Tag, checked((int)gameData.MoveAbilityId.Value), _site.Value);
        }

        if (observation.Minerals < gameData.CommandCenterMineralCost ||
            (_buildSubmitted && observation.GameLoop <= _buildLoop + 8))
            return null;

        _buildSubmitted = true;
        _buildLoop = observation.GameLoop;
        _pending = PendingAction.Build;
        _job ??= buildJobs?.Create("CommandCenter", observation.GameLoop, workerTag: worker.Tag);
        Console.WriteLine($"[Expansion] Starting Command Center: SCV={worker.Tag}, minerals={observation.Minerals}, site=({_site.Value.X:F1},{_site.Value.Y:F1}).");
        return PositionCommand(worker.Tag, checked((int)gameData.CommandCenterBuildAbilityId.Value), _site.Value);
    }

    public RequestQuery TakePlacementQuery(Sc2GameDataCatalog gameData)
    {
        if (!_queryPending || _builderTag is null || gameData.CommandCenterBuildAbilityId is not > 0)
            return null;
        _queryPending = false;
        var query = new RequestQuery { IgnoreResourceRequirements = true };
        foreach (var point in _candidates)
            query.Placements.Add(new RequestQueryBuildingPlacement
            {
                AbilityId = checked((int)gameData.CommandCenterBuildAbilityId.Value),
                TargetPos = new Point2D { X = point.X, Y = point.Y },
                PlacingUnitTag = _builderTag.Value
            });
        return query;
    }

    public void OnPlacementQueryResult(ResponseQuery response)
    {
        if (_builderTag is null || _site is not null || _candidates.Count == 0)
            return;
        var count = Math.Min(response?.Placements.Count ?? 0, _candidates.Count);
        for (var index = 0; index < count; index++)
        {
            if (response.Placements[index].Result != ActionResult.Success)
                continue;
            _site = _candidates[index];
            Console.WriteLine($"[Expansion] SC2 accepted Command Center site ({_site.Value.X:F1},{_site.Value.Y:F1}); location retained for future Orbital routing.");
            return;
        }
        _queryPending = true;
        Console.WriteLine($"[Expansion] SC2 rejected all {_candidates.Count} Command Center sites; placement will be queried again.");
    }

    public void OnActionResult(ActionResult? result, uint gameLoop)
    {
        if (_pending == PendingAction.None)
            return;
        if (result is null || result != ActionResult.Success)
        {
            if (_pending == PendingAction.Move)
                _moveSubmitted = false;
            else if (_pending == PendingAction.Build)
            {
                _buildSubmitted = false;
                _job?.Fail("SC2 rejected the expansion Command Center build command.", gameLoop);
                _job = null;
            }
        }
        _pending = PendingAction.None;
    }

    private static bool IsLiveScv(UnitObservationFacts unit) =>
        unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
        unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase);

    private static bool IsKind(UnitObservationFacts unit, string kind) =>
        unit.UnitKind.Equals(kind, StringComparison.OrdinalIgnoreCase);

    private static bool IsGatheringMinerals(UnitObservationFacts worker, NormalizedObservation observation) =>
        worker.Orders.FirstOrDefault()?.TargetUnitTag is ulong tag &&
        observation.Units.Any(unit => unit.Tag == tag && unit.Alliance == UnitAlliance.Neutral &&
            unit.IsLiveVisible && unit.UnitKind.Contains("MineralField", StringComparison.OrdinalIgnoreCase));

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    private static SC2APIProtocol.Action PositionCommand(ulong tag, int abilityId, MapPoint target) =>
        new()
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = abilityId,
                    UnitTags = { tag },
                    TargetWorldSpacePos = new Point2D { X = target.X, Y = target.Y },
                    QueueCommand = false
                }
            }
        };
}
