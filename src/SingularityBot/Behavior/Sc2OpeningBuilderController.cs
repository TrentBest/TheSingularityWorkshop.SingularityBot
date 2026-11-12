using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Keeps starting SCVs mining until the Supply Depot is affordable. A selected
/// cargo-bearing worker completes its return to the Command Center before being
/// dispatched, preserving the mineral income that funds the opening.
/// </summary>
public sealed class Sc2OpeningBuilderController : IStateContext, IDisposable
{
    public const string MovementFsmName = "SingularityBot.OpeningBuilderMovement";
    public const string MovementProcessingGroup = "SingularityBot.OpeningBuilderMovement";
    private static long _nextMovementGroupId;
    private readonly string _movementProcessingGroup;
    private const string MovementIdle = "Idle";
    private const string MoveSubmitted = "MoveSubmitted";
    private const string Moving = "Moving";
    private const string Arrived = "Arrived";

    private readonly FSMHandle _movementHandle;
    private bool _moveSubmissionRequested;
    private bool _moveAccepted;
    private bool _moveRejected;
    private bool _builderLost;
    private bool _moveTargetChanged;
    private bool _moveRetryReady;
    private uint _currentGameLoop;
    private float _movementDistanceSquared;
    private bool _hasMovementTarget;

    public string Name { get; set; } = "Opening Supply Depot builder";
    public bool IsValid { get; set; } = true;
    public string MovementState => _movementHandle.CurrentState;

    private ulong? _builderTag;
    private IReadOnlyList<MapPoint> _candidatePoints = Array.Empty<MapPoint>();
    private bool _placementQueryPending;
    private MapPoint? _placement;
    private uint _moveRequestGameLoop;
    private uint _firstMoveRequestGameLoop;
    private MapPoint? _moveTarget;
    private int _placementQueryAttempts;
    private uint _buildRequestGameLoop;
    private readonly BuildJobRuntime _ownedBuildJobs = new();
    private BuildJobContext _buildJob;
    private HashSet<ulong> _preexistingDepotTags;
    private bool _builderWasReturningMinerals;
    private bool _builderReleasedOnCompletion;
    private uint? _lastObservedMinerals;

    public Sc2OpeningBuilderController()
    {
        _movementProcessingGroup = $"{MovementProcessingGroup}.{System.Threading.Interlocked.Increment(ref _nextMovementGroupId)}";
        EnsureMovementDefinition(_movementProcessingGroup);
        _movementHandle = FSM_API.Create.CreateInstance(MovementFsmName, this, _movementProcessingGroup);
    }

    public ulong? BuilderTag => _builderTag;
    public bool IsCompleted => _buildJob?.IsCompleted == true;
    public string BuildJobState => _buildJob?.CurrentState ?? "NoBuildJob";
    public string BuildJobFailureReason => _buildJob?.FailureReason ?? string.Empty;
    public bool IsDispatched => MovementState is Moving or Arrived;
    public MapPoint? Placement => _placement;
    public int CandidateCount => _candidatePoints.Count;
    public bool IsPlacementQueryPending => _placementQueryPending;
    public bool IsMoveSubmitted => MovementState is MoveSubmitted or Moving;
    public bool IsMoveRetryReady => _moveRetryReady;
    public uint CurrentMovementGameLoop => _currentGameLoop;
    public uint MoveRequestGameLoop => _firstMoveRequestGameLoop;
    public bool IsBuildSubmitted => _buildJob is not null && !_buildJob.IsFailed;

    private static void EnsureMovementDefinition(string processingGroup)
    {
        if (FSM_API.Interaction.Exists(MovementFsmName, processingGroup))
            return;

        FSM_API.Create.CreateFiniteStateMachine(
                MovementFsmName,
                processRate: -1,
                processingGroup: processingGroup)
            .State(MovementIdle, null, null, null)
            .State(MoveSubmitted, null, null, null)
            .State(Moving, null, null, null)
            .State(Arrived, null, null, null)
            .WithInitialState(MovementIdle)
            .Transition(MovementIdle, MoveSubmitted, HasMoveSubmission)
            .Transition(MovementIdle, Arrived, HasReachedPlacement)
            .Transition(MoveSubmitted, Moving, WasMoveAccepted)
            .Transition(MoveSubmitted, MovementIdle, WasMoveRejected)
            .Transition(MoveSubmitted, Arrived, HasReachedPlacement)
            .Transition(MoveSubmitted, MovementIdle, WasBuilderLost)
            .Transition(MoveSubmitted, MovementIdle, WasMoveTargetChanged)
            .Transition(Moving, Arrived, HasReachedPlacement)
            .Transition(Moving, MovementIdle, HasMoveRetryElapsed)
            .Transition(Moving, MovementIdle, WasBuilderLost)
            .Transition(Moving, MovementIdle, WasMoveTargetChanged)
            .Transition(Arrived, MovementIdle, HasLeftPlacement)
            .Transition(Arrived, MovementIdle, WasBuilderLost)
            .Transition(Arrived, MovementIdle, WasMoveTargetChanged)
            .BuildDefinition();
    }

    private static bool HasMoveSubmission(IStateContext context) =>
        context is Sc2OpeningBuilderController builder && builder._moveSubmissionRequested;

    private static bool WasMoveAccepted(IStateContext context) =>
        context is Sc2OpeningBuilderController builder && builder._moveAccepted;

    private static bool WasMoveRejected(IStateContext context) =>
        context is Sc2OpeningBuilderController builder && builder._moveRejected;

    private static bool WasBuilderLost(IStateContext context) =>
        context is Sc2OpeningBuilderController builder && builder._builderLost;

    private static bool WasMoveTargetChanged(IStateContext context) =>
        context is Sc2OpeningBuilderController builder && builder._moveTargetChanged;

    private static bool HasReachedPlacement(IStateContext context) =>
        context is Sc2OpeningBuilderController builder &&
        builder._hasMovementTarget && builder._movementDistanceSquared <= 2.25f;

    private static bool HasLeftPlacement(IStateContext context) =>
        context is Sc2OpeningBuilderController builder &&
        builder._hasMovementTarget && builder._movementDistanceSquared > 2.25f;

    private static bool HasMoveRetryElapsed(IStateContext context) =>
        context is Sc2OpeningBuilderController builder &&
        builder._currentGameLoop > builder._moveRequestGameLoop + 22;

    private void UpdateMovementFsm()
    {
        // The retry deadline is an explicit external observation boundary. Resolve
        // it directly through the FSM handle before the normal group tick so a
        // long-running move cannot retain the Moving state past its deadline.
        if (MovementState == Moving && HasMoveRetryElapsed(this))
            _movementHandle.TransitionTo(MovementIdle);

        FSM_API.Interaction.Update(_movementProcessingGroup);
        _moveSubmissionRequested = false;
        _moveAccepted = false;
        _moveRejected = false;
        _builderLost = false;
        _moveTargetChanged = false;
    }

    public void Dispose()
    {
        IsValid = false;
        FSM_API.Interaction.DestroyInstance(_movementHandle);
        _ownedBuildJobs.Clear();
    }

    /// <summary>
    /// Releases the opening Depot worker after the first Barracks builder has
    /// completed its handoff. If the same SCV built both structures this is
    /// harmless; if the Barracks used a different SCV it prevents the Depot
    /// worker from remaining reserved and idle for the rest of the match.
    /// </summary>
    /// <summary>
    /// Returns the original Depot builder to the worker pool as soon as its Depot
    /// is complete, unless that exact SCV is currently committed to the first Barracks.
    /// </summary>
    public void ReleaseBuilderIfNotAssignedToBarracks(
        WorkerResourceAssignmentPlanner assignments,
        ulong? barracksBuilderTag)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        if (!IsCompleted || _builderTag is not ulong tag || tag == barracksBuilderTag)
            return;

        assignments.Unreserve(tag);
        Console.WriteLine($"[Opening] Released original Depot builder SCV {tag} to resume mining; first Barracks uses {(barracksBuilderTag?.ToString() ?? "a different worker")}.");
        _builderTag = null;
    }

    public void ReleaseBuilderAfterBarracksHandoff(WorkerResourceAssignmentPlanner assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        if (!IsCompleted || _builderTag is not ulong tag)
            return;
        assignments.Unreserve(tag);
        Console.WriteLine($"[Opening] Released original Depot builder SCV {tag} after the first Barracks handoff.");
        _builderTag = null;
    }

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        SpatialMap spatialMap,
        Sc2GameDataCatalog gameData,
        WorkerResourceAssignmentPlanner assignments,
        IReadOnlySet<ulong> constructionCrewTags,
        BuildJobRuntime buildJobs = null,
        ulong? preferredBuilderTag = null,
        bool releaseBuilderOnCompletion = false,
        uint workerPullMinerals = 80)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(assignments);

        _currentGameLoop = observation.GameLoop;
        _moveRetryReady = false;
        var previousMineralBank = _lastObservedMinerals;
        _lastObservedMinerals = observation.Minerals;

        BuildJobRuntime.RegisterDefinition();
        buildJobs ??= _ownedBuildJobs;
        buildJobs.ReconcileSupplyDepotConstruction(observation);
        if (ReferenceEquals(buildJobs, _ownedBuildJobs))
            buildJobs.Update();
        _preexistingDepotTags ??= observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)))
            .Select(unit => unit.Tag)
            .ToHashSet();

        if (IsCompleted)
        {
            if (releaseBuilderOnCompletion && !_builderReleasedOnCompletion &&
                _builderTag is ulong completedBuilder)
            {
                assignments.Unreserve(completedBuilder);
                _builderReleasedOnCompletion = true;
                Console.WriteLine($"[Opening] Supply Depot completed; recycled SCV {completedBuilder} back to the worker pool.");
            }
            return null;
        }

        if (gameData.SupplyDepotBuildAbilityId is not > 0 ||
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
            _builderLost = true;
            UpdateMovementFsm();
            _moveTarget = null;
            _hasMovementTarget = false;
            Console.WriteLine($"[Opening] Reserved builder SCV {selectedTag} was lost or morphed; selecting a replacement.");
        }

        // Pull the builder before the structure is affordable so it can travel
        // while the bank accumulates. The threshold is policy data; the authoritative
        // game-data cost still gates the actual Build command below.
        var builderPullThreshold = Math.Min(workerPullMinerals, gameData.SupplyDepotMineralCost);
        if (_builderTag is null && observation.Minerals < builderPullThreshold)
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
                .Select(worker => new
                {
                    Worker = worker,
                    EstimatedArrivalDistance = Math.Sqrt(DistanceSquared(
                        worker.PositionX, worker.PositionY,
                        stagingPoint.X, stagingPoint.Y)),
                    IsConstructionCrew = constructionCrewTags.Contains(worker.Tag),
                    IsReturningWithMinerals = worker.HasCargo && IsReturningToTownHall(worker, observation),
                    IsCarryingOtherCargo = worker.HasCargo && !IsReturningToTownHall(worker, observation)
                })
                // Preserve a usable handoff builder. Otherwise prefer a confirmed
                // mineral returner, not merely any SCV with cargo: gas cargo must not
                // be mistaken for minerals once Refineries are operating.
                .OrderBy(candidate => candidate.Worker.Tag == preferredBuilderTag && !candidate.IsCarryingOtherCargo ? 0 : 1)
                .ThenBy(candidate => candidate.IsReturningWithMinerals ? 0 : 1)
                .ThenBy(candidate => candidate.IsCarryingOtherCargo ? 1 : 0)
                .ThenBy(candidate => candidate.IsConstructionCrew ? 0 : 1)
                .ThenBy(candidate => candidate.EstimatedArrivalDistance)
                .ThenBy(candidate => candidate.Worker.Tag)
                .FirstOrDefault();

            if (selected is null)
                return null;

            _builderTag = selected.Worker.Tag;
            _firstMoveRequestGameLoop = 0;
            _builderReleasedOnCompletion = false;
            assignments.Reserve(_builderTag.Value);
            _placementQueryPending = true;
            Console.WriteLine(
                $"[Opening] Construction-crew SCV {_builderTag.Value} reserved for the Supply Depot; " +
                $"estimated staging distance={selected.EstimatedArrivalDistance:F2}. " +
                "The worker completes its harvest and deposits its load before leaving the mineral line.");
        }

        var builder = observation.Units.FirstOrDefault(unit =>
            unit.Tag == _builderTag.Value &&
            unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag));
        if (builder is null)
            return null;

        // Never sacrifice a carried resource load for construction. The worker
        // FSM owns the return-to-town-hall command while this builder reservation
        // is active; wait until a later observation confirms the cargo was
        // deposited before dispatching the SCV to the accepted Depot site.
        if (builder.HasCargo)
        {
            if (IsReturningToTownHall(builder, observation))
                _builderWasReturningMinerals = true;
            return null;
        }

        var builderJustDepositedMinerals = _builderWasReturningMinerals;
        if (builderJustDepositedMinerals)
        {
            var bankDelta = previousMineralBank is uint previous
                ? (int)observation.Minerals - (int)previous
                : 0;
            Console.WriteLine(
                $"[Economy] OBSERVED mineral deposit: Depot builder SCV {builder.Tag} returned its load to a town hall; " +
                $"bank={observation.Minerals}; net bank delta since prior controller observation={bankDelta}.");
            _builderWasReturningMinerals = false;
        }

        // A worker FSM can immediately queue another mineral gather after the
        // return completes. That new trip must not strand a reserved builder:
        // preserve an existing trip only when the builder has not just deposited.
        if (!builderJustDepositedMinerals && IsGatheringMinerals(builder, observation))
            return null;

        // Do not send the builder to a speculative staging point. SC2's placement
        // response is authoritative; move once, directly to the accepted site.
        if (_placement is null)
            return null;
        var movementTarget = _placement.Value;
        if (_moveTarget is null ||
            DistanceSquared(_moveTarget.Value.X, _moveTarget.Value.Y, movementTarget.X, movementTarget.Y) > 0.01f)
        {
            if (_moveTarget is not null)
            {
                _moveTargetChanged = true;
                UpdateMovementFsm();
            }
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
                if (depot.BuildProgress >= 1f)
                {
                    if (releaseBuilderOnCompletion && _builderTag is ulong completedBuilder)
                    {
                        assignments.Unreserve(completedBuilder);
                        _builderReleasedOnCompletion = true;
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
        _movementDistanceSquared = distanceSquared;
        _hasMovementTarget = true;
        var stateBeforeMovementUpdate = MovementState;
        UpdateMovementFsm();

        if (distanceSquared > 2.25f)
        {
            // An accepted Move is not proof that the SCV reached the site.
            // Retry after a bounded travel window so pathing interruptions cannot stall the opening.
            if (IsMoveSubmitted)
                return null;

            if (stateBeforeMovementUpdate == Moving)
            {
                _moveRetryReady = true;
                Console.WriteLine($"[Opening] Builder SCV {_builderTag} has not reached the Depot site; retrying Move (distance²={distanceSquared:F2}).");
            }

            _moveTarget = movementTarget;
            if (_firstMoveRequestGameLoop == 0)
                _firstMoveRequestGameLoop = observation.GameLoop;
            _moveRequestGameLoop = observation.GameLoop;
            _moveAccepted = false;
            _moveSubmissionRequested = true;
            UpdateMovementFsm();
            return CreatePositionCommand(
                _builderTag.Value,
                checked((int)gameData.MoveAbilityId.Value),
                movementTarget);
        }

        if (observation.Minerals < gameData.SupplyDepotMineralCost)
            return null;

        if (_buildJob is not null && !_buildJob.IsCompleted && !_buildJob.IsFailed)
        {
            if (observation.GameLoop > _buildRequestGameLoop + 8)
            {
                if (_buildJob is not null && !_buildJob.IsCompleted && !_buildJob.IsFailed)
                    _buildJob.Fail("Opening Supply Depot structure was not observed before build retry.", observation.GameLoop);
                _buildJob = null;
            }
            else
            {
                return null;
            }
        }
        _buildRequestGameLoop = observation.GameLoop;
        _buildJob = buildJobs.CreateSupplyDepotConstruction(observation.GameLoop, builder.Tag,
            observation.Units.Where(unit => unit.Alliance == UnitAlliance.Self &&
                (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)))
                .Select(unit => unit.Tag));
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
        if (MovementState == MoveSubmitted)
        {
            if (result == ActionResult.Success)
                _moveAccepted = true;
            else
                _moveRejected = true;
            UpdateMovementFsm();
            return;
        }

        if (IsBuildSubmitted && (result is null || result != ActionResult.Success))
        {
            if (_buildJob is not null && !_buildJob.IsCompleted && !_buildJob.IsFailed)
                _buildJob.Fail("SC2 rejected the opening Supply Depot construction command.", _buildJob.LastObservedGameLoop);
            _buildJob = null;
        }
    }

    private static bool IsGatheringMinerals(
        UnitObservationFacts worker,
        NormalizedObservation observation)
    {
        var targetTag = worker.Orders.FirstOrDefault()?.TargetUnitTag;
        return targetTag is ulong tag && observation.Units.Any(unit =>
            unit.Tag == tag && unit.IsLiveVisible &&
            unit.Alliance == UnitAlliance.Neutral &&
            (unit.UnitKind.Contains("MineralField", StringComparison.OrdinalIgnoreCase) ||
             unit.UnitKind.Contains("RichMineralField", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsReturningToTownHall(
        UnitObservationFacts worker,
        NormalizedObservation observation)
    {
        var targetTag = worker.Orders.FirstOrDefault()?.TargetUnitTag;
        return targetTag is ulong tag && observation.Units.Any(unit =>
            unit.Tag == tag && unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible && IsTownHall(unit.UnitKind));
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
