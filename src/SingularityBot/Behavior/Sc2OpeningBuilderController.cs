using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Keeps starting SCVs mining until the Supply Depot is affordable, then reserves
/// a worker and lets an active mineral trip finish before dispatching it to build.
/// </summary>
public sealed class Sc2OpeningBuilderController
{

    private enum PendingAction
    {
        None,
        Move,
        Build
    }

    private ulong? _builderTag;
    private IReadOnlyList<MapPoint> _candidatePoints = Array.Empty<MapPoint>();
    private bool _placementQueryPending;
    private MapPoint? _placement;
    private bool _moveSubmitted;
    private bool _dispatched;
    private uint _moveRequestGameLoop;
    private MapPoint? _moveTarget;
    private int _placementQueryAttempts;
    private bool _buildSubmitted;
    private uint _buildRequestGameLoop;
    private PendingAction _pendingAction;
    private bool _completed;
    private long? _buildJobId;
    private HashSet<ulong> _preexistingDepotTags;

    public Sc2OpeningBuilderController() { }

    public ulong? BuilderTag => _builderTag;
    public bool IsCompleted => _completed;
    public bool IsDispatched => _dispatched;
    public MapPoint? Placement => _placement;
    public int CandidateCount => _candidatePoints.Count;
    public bool IsPlacementQueryPending => _placementQueryPending;
    public bool IsMoveSubmitted => _moveSubmitted;
    public bool IsBuildSubmitted => _buildSubmitted;

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        SpatialMap spatialMap,
        Sc2GameDataCatalog gameData,
        WorkerResourceAssignmentPlanner assignments,
        IReadOnlySet<ulong> constructionCrewTags,
        BuildJobRuntime buildJobs = null,
        ulong? preferredBuilderTag = null,
        bool releaseBuilderOnCompletion = false)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(assignments);

        buildJobs?.ReconcileSupplyDepotConstruction(observation);
        _preexistingDepotTags ??= observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)))
            .Select(unit => unit.Tag)
            .ToHashSet();

        if (_completed ||
            gameData.SupplyDepotBuildAbilityId is not > 0 ||
            gameData.SupplyDepotMineralCost == 0 ||
            gameData.MoveAbilityId is not > 0 ||
            spatialMap is null ||
            constructionCrewTags is null)
            return null;

        if (_builderTag is ulong selectedTag &&
            (observation.DeadUnitTags.Contains(selectedTag) ||
             observation.Units.Any(unit => unit.Tag == selectedTag &&
                 unit.IsLiveVisible &&
                 !unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase))))
        {
            assignments.Unreserve(selectedTag);
            _builderTag = null;
            _placement = null;
            _candidatePoints = Array.Empty<MapPoint>();
            _placementQueryPending = false;
            _moveSubmitted = false;
            _moveTarget = null;
            _dispatched = false;
            _buildSubmitted = false;
            _pendingAction = PendingAction.None;
            Console.WriteLine($"[Opening] Reserved builder SCV {selectedTag} was lost or morphed; selecting a replacement.");
        }

        // Never pull an SCV merely because the opening has started. Keep the
        // whole worker line gathering until the actual structure cost is available.
        if (_builderTag is null && observation.Minerals < gameData.SupplyDepotMineralCost)
            return null;

        if (_builderTag is null)
        {
            var townHall = observation.Units.FirstOrDefault(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.HasPosition && IsTownHall(unit.UnitKind));
            if (townHall is null)
                return null;

            _candidatePoints = CreatePlacementCandidates(townHall, observation, spatialMap);
            if (_candidatePoints.Count == 0)
                return null;

            var stagingPoint = _candidatePoints[0];
            var eligibleWorkers = observation.Units
                .Where(unit => IsLiveScv(unit) && !assignments.IsReserved(unit.Tag))
                .ToArray();
            var selected = eligibleWorkers
                .Where(unit => constructionCrewTags.Contains(unit.Tag) || unit.Tag == preferredBuilderTag)
                .Select(worker => new
                {
                    Worker = worker,
                    EstimatedArrivalDistance = Math.Sqrt(DistanceSquared(
                        worker.PositionX, worker.PositionY,
                        stagingPoint.X, stagingPoint.Y))
                })
                .OrderBy(candidate => candidate.Worker.Tag == preferredBuilderTag ? 0 : 1)
                // Prefer a worker that has already completed a mineral trip:
                // construction consumes the cargo trip as an intentional opening cost.
                .ThenBy(candidate => candidate.Worker.HasCargo ? 0 : 1)
                .ThenBy(candidate => candidate.EstimatedArrivalDistance)
                .ThenBy(candidate => candidate.Worker.Tag)
                .FirstOrDefault();
            // A missing construction capability must never stall the opening.
            // Fall back to the nearest unreserved SCV and let the reservation
            // prevent its mineral assignment from being reissued.
            selected ??= eligibleWorkers
                .Select(worker => new
                {
                    Worker = worker,
                    EstimatedArrivalDistance = Math.Sqrt(DistanceSquared(
                        worker.PositionX, worker.PositionY,
                        stagingPoint.X, stagingPoint.Y))
                })
                .OrderBy(candidate => candidate.EstimatedArrivalDistance)
                .ThenBy(candidate => candidate.Worker.Tag)
                .FirstOrDefault();

            if (selected is null)
                return null;

            _builderTag = selected.Worker.Tag;
            assignments.Reserve(_builderTag.Value);
            _placementQueryPending = true;
            Console.WriteLine(
                $"[Opening] Construction-crew SCV {_builderTag.Value} reserved for the Supply Depot; " +
                $"estimated staging distance={selected.EstimatedArrivalDistance:F2}. " +
                "The worker stays on its current harvest until it has a load.");
        }

        var builder = observation.Units.FirstOrDefault(unit =>
            unit.Tag == _builderTag.Value &&
            unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag));
        if (builder is null)
            return null;

        // If the selected worker is already mining and has not collected its load,
        // let that trip finish. Once it is carrying minerals, dispatch it directly
        // to construction: do not waste the trip returning to the Command Center.
        // The first Depot has priority over the next SCV: start it as soon as
        // its 100-mineral cost is available. Waiting for a 150-mineral combined
        // reserve caused the opening to hit 15/15 supply before construction.
        if (!builder.HasCargo && IsGatheringMinerals(builder, observation))
            return null;

        // Replace the worker's mining order only after its current harvest is
        // complete (or immediately when it was idle).
        var movementTarget = _placement ?? _candidatePoints[0];
        if (_moveTarget is null ||
            DistanceSquared(_moveTarget.Value.X, _moveTarget.Value.Y, movementTarget.X, movementTarget.Y) > 0.01f)
        {
            _moveSubmitted = false;
            _moveTarget = movementTarget;
        }

        if (_placement is not null)
        {
            var depot = observation.Units
                .Where(unit => unit.Alliance == UnitAlliance.Self &&
                               unit.IsLiveVisible &&
                               (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
                                unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)) &&
                               !_preexistingDepotTags.Contains(unit.Tag) &&
                               unit.HasPosition)
                .OrderBy(unit => DistanceSquared(unit.PositionX, unit.PositionY, _placement.Value.X, _placement.Value.Y))
                .FirstOrDefault();

            if (depot is not null &&
                DistanceSquared(depot.PositionX, depot.PositionY, _placement.Value.X, _placement.Value.Y) <= 9)
            {
                _buildSubmitted = true;
                if (depot.BuildProgress >= 1f)
                {
                    _completed = true;
                    if (releaseBuilderOnCompletion && _builderTag is ulong completedBuilder)
                    {
                        assignments.Unreserve(completedBuilder);
                        Console.WriteLine($"[Opening] Supply Depot {depot.Tag} completed; recycled SCV {completedBuilder} back to the worker pool.");
                    }
                    else
                    {
                        Console.WriteLine($"[Opening] Supply Depot {depot.Tag} completed; construction-crew SCV {_builderTag.Value} remains reserved for the queued Barracks build.");
                    }
                }
                return null;
            }
        }

        var distanceSquared = DistanceSquared(
            builder.PositionX, builder.PositionY, movementTarget.X, movementTarget.Y);

        if (distanceSquared > 2.25f)
        {
            // An accepted Move is not proof that the SCV reached the site.
            // Retry after a bounded travel window so pathing interruptions cannot stall the opening.
            if (_moveSubmitted && observation.GameLoop <= _moveRequestGameLoop + 22)
                return null;

            if (_moveSubmitted)
                Console.WriteLine($"[Opening] Builder SCV {_builderTag} has not reached the Depot site; retrying Move (distance²={distanceSquared:F2}).");

            _moveSubmitted = true;
            _moveTarget = movementTarget;
            _moveRequestGameLoop = observation.GameLoop;
            _pendingAction = PendingAction.Move;
            return CreatePositionCommand(
                _builderTag.Value,
                checked((int)gameData.MoveAbilityId.Value),
                movementTarget);
        }

        // The placement query can complete after the staging Move. If the accepted
        // build site differs from staging, the next pass issues a replacement Move.
        if (_placement is null)
            return null;

        if (observation.Minerals < gameData.SupplyDepotMineralCost)
            return null;

        if (_buildSubmitted)
        {
            if (observation.GameLoop > _buildRequestGameLoop + 8)
            {
                if (_buildJobId is long staleId && buildJobs is not null &&
                    buildJobs.TryGet(staleId, out var staleJob) && !staleJob.IsCompleted && !staleJob.IsFailed)
                    staleJob.Fail("Opening Supply Depot structure was not observed before build retry.", observation.GameLoop);
                _buildSubmitted = false;
            }
            else
            {
                return null;
            }
        }

        _buildSubmitted = true;
        _buildRequestGameLoop = observation.GameLoop;
        _buildJobId = buildJobs?.CreateSupplyDepotConstruction(observation.GameLoop, builder.Tag,
            observation.Units.Where(unit => unit.Alliance == UnitAlliance.Self &&
                (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)))
                .Select(unit => unit.Tag)).Id;
        _pendingAction = PendingAction.Build;
        return CreatePositionCommand(
            _builderTag.Value,
            checked((int)gameData.SupplyDepotBuildAbilityId.Value),
            _placement.Value);
    }

    /// <summary>Returns the one-shot placement query prepared after builder selection.</summary>
    public RequestQuery TakePlacementQuery(Sc2GameDataCatalog gameData)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        if (!_placementQueryPending || _builderTag is null ||
            gameData.SupplyDepotBuildAbilityId is not > 0)
            return null;

        _placementQueryPending = false;
        _placementQueryAttempts++;
        Console.WriteLine($"[Opening] Querying SC2 for Supply Depot placement (attempt {_placementQueryAttempts}, candidates={_candidatePoints.Count}, builder={_builderTag}).");
        var query = new RequestQuery { IgnoreResourceRequirements = true };
        foreach (var point in _candidatePoints)
        {
            query.Placements.Add(new RequestQueryBuildingPlacement
            {
                AbilityId = checked((int)gameData.SupplyDepotBuildAbilityId.Value),
                TargetPos = new Point2D { X = point.X, Y = point.Y },
                PlacingUnitTag = _builderTag.Value
            });
        }
        return query;
    }

    public void OnPlacementQueryResult(ResponseQuery response)
    {
        if (_builderTag is null || _candidatePoints.Count == 0 || _placement is not null)
            return;

        if (response is null || response.Placements.Count == 0)
        {
            _placementQueryPending = true;
            Console.WriteLine($"[Opening] Supply Depot placement query returned no results; retrying for builder {_builderTag}.");
            return;
        }

        var count = Math.Min(response.Placements.Count, _candidatePoints.Count);
        for (var index = 0; index < count; index++)
        {
            if (response.Placements[index].Result != ActionResult.Success)
                continue;

            _placement = _candidatePoints[index];
            Console.WriteLine($"[Opening] SC2 placement query accepted Supply Depot site ({_placement.Value.X:F1}, {_placement.Value.Y:F1}) for builder {_builderTag}.");
            return;
        }

        _placementQueryPending = true;
        Console.WriteLine($"[Opening] SC2 rejected all {_candidatePoints.Count} candidate Supply Depot sites (attempt {_placementQueryAttempts}); retrying placement query for builder {_builderTag}.");
    }

    public void OnActionResult(ActionResult? result, BuildJobRuntime buildJobs = null)
    {
        if (_pendingAction == PendingAction.None)
            return;

        if (_pendingAction == PendingAction.Move && result == ActionResult.Success)
            _dispatched = true;

        if (result is null || result != ActionResult.Success)
        {
            if (_pendingAction == PendingAction.Move)
            {
                _moveSubmitted = false;
                _dispatched = false;
            }
            else if (_pendingAction == PendingAction.Build)
            {
                _buildSubmitted = false;
                if (_buildJobId is long id && buildJobs is not null && buildJobs.TryGet(id, out var job))
                    job.Fail("SC2 rejected the opening Supply Depot construction command.", job.LastObservedGameLoop);
                _buildJobId = null;
            }
        }

        _pendingAction = PendingAction.None;
    }

    private static bool IsGatheringMinerals(
        UnitObservationFacts worker,
        NormalizedObservation observation)
    {
        var targetTag = worker.Orders.FirstOrDefault()?.TargetUnitTag;
        return targetTag is ulong mineralTag && observation.Units.Any(unit =>
            unit.Tag == mineralTag &&
            unit.Alliance == UnitAlliance.Neutral &&
            unit.IsLiveVisible &&
            (unit.UnitKind.Contains("MineralField", StringComparison.OrdinalIgnoreCase) ||
             unit.UnitKind.Contains("RichMineralField", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsLiveScv(UnitObservationFacts unit) =>
        unit.Alliance == UnitAlliance.Self &&
        unit.IsLiveVisible &&
        unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase);


    private static IReadOnlyList<MapPoint> CreatePlacementCandidates(
        UnitObservationFacts townHall,
        NormalizedObservation observation,
        SpatialMap map)
    {
        var origin = new MapPoint(townHall.PositionX, townHall.PositionY);
        var mineralLine = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Neutral && unit.IsLiveVisible &&
                unit.HasPosition && !observation.DeadUnitTags.Contains(unit.Tag) &&
                (unit.UnitKind.Contains("MineralField", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Contains("RichMineralField", StringComparison.OrdinalIgnoreCase)))
            .Select(unit => new
            {
                Unit = unit,
                Distance = DistanceSquared(unit.PositionX, unit.PositionY, origin.X, origin.Y)
            })
            .Where(item => item.Distance <= 30f * 30f)
            .ToArray();

        // Use the observed mineral line as the geometric anchor. The Depot sits
        // beyond the patches on the side away from the Command Center, as requested.
        // SC2 still decides which candidate is actually legal.
        var mineralAnchor = mineralLine.Length >= 4
            ? new MapPoint(
                mineralLine.Average(item => item.Unit.PositionX),
                mineralLine.Average(item => item.Unit.PositionY))
            : (MapPoint?)null;
        var forward = mineralAnchor is MapPoint minerals
            ? (minerals - origin).Normalize()
            : (map.Center - origin).Normalize();
        var lateral = new MapPoint(-forward.Y, forward.X);
        var anchor = mineralAnchor is MapPoint line
            ? line + forward * 4.5f
            : origin + forward * 9f;
        var offsets = new (float Forward, float Lateral)[]
        {
            (0, 0), (0, 2), (0, -2), (0, 4), (0, -4),
            (-1.5f, 1), (-1.5f, -1), (1.5f, 1), (1.5f, -1),
            (-3f, 0), (-3f, 2), (-3f, -2),
            (3f, 0), (3f, 2), (3f, -2)
        };

        return offsets
            .Select(offset => anchor + forward * offset.Forward + lateral * offset.Lateral)
            .Where(map.Bounds.Contains)
            .ToArray();
    }

    private static SC2APIProtocol.Action CreatePositionCommand(
        ulong unitTag,
        int abilityId,
        MapPoint target) =>
        new()
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = abilityId,
                    UnitTags = { unitTag },
                    TargetWorldSpacePos = new Point2D { X = target.X, Y = target.Y },
                    QueueCommand = false
                }
            }
        };

    private static bool IsTownHall(string kind) =>
        kind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase);

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
