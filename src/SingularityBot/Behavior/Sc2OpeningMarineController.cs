#nullable enable annotations

using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Builds one Barracks with an available SCV and trains the first Marine.
/// The Marine remains on the friendly side; combat control reacts to nearby contacts.
/// </summary>
public sealed class Sc2OpeningMarineController
{
    private BuildJobContext _barracksBuildJob;
    private BuildJobContext _marineBuildJob;
    private ulong? _marineTag;

    private enum PendingAction { None, MoveBuilder, BuildBarracks, TrainMarine }

    private readonly List<MapPoint> _candidatePoints = new();
    private ulong? _builderTag;
    private MapPoint? _placement;
    private bool _placementQueryPending;
    private uint _builderMoveRequestGameLoop;
    private bool _builderMoveSubmitted;
    private PendingAction _pendingAction;

    public ulong? BuilderTag => _builderTag;
    public ulong? ReleasedBuilderTag { get; private set; }
    public bool BarracksComplete => _barracksBuildJob?.IsCompleted == true;
    public bool MarineObserved => _marineTag.HasValue;
    public bool MarineTrainingStarted => MarineObserved ||
        _marineBuildJob?.HasExecutionEvidence == true || _marineBuildJob?.IsExecuting == true;
    public bool MarineTrainingOrderObserved => MarineObserved ||
        _marineBuildJob?.HasExecutionEvidence == true;
    public bool IsOpeningMarineTrainingPending => _marineBuildJob is not null &&
        !_marineBuildJob.IsFailed && !_marineBuildJob.HasFailureEvidence &&
        !_marineBuildJob.HasExecutionEvidence && !MarineObserved;
    public bool MarineDeployed => MarineObserved;
    public bool IsBuildSubmitted => _barracksBuildJob is not null &&
        !_barracksBuildJob.IsFailed && !_barracksBuildJob.HasFailureEvidence;
    public int CandidateCount => _candidatePoints.Count;

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        SpatialMap map,
        Sc2GameDataCatalog gameData,
        WorkerResourceAssignmentPlanner assignments,
        BuildJobRuntime buildJobs,
        ulong? preferredBuilderTag = null,
        uint reservedMinerals = 0,
        ulong? excludedBuilderTag = null,
        bool allowConstruction = true,
        bool allowMarineTraining = true)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(buildJobs);

        if (map is null || gameData.BarracksBuildAbilityId is not > 0 ||
            gameData.BarracksMineralCost == 0 || gameData.MarineTrainAbilityId is not > 0 ||
            gameData.MarineMineralCost == 0 || gameData.MoveAbilityId is not > 0)
            return null;

        BuildJobRuntime.RegisterDefinition();
        buildJobs.ReconcileBarracks(observation);
        buildJobs.ReconcileMarineProduction(observation, gameData.MarineTrainAbilityId.Value);

        var barracks = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .FirstOrDefault();

        if (barracks is null)
        {
            var inProgress = observation.Units.Any(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                unit.BuildProgress < 1f && !observation.DeadUnitTags.Contains(unit.Tag));
            if (inProgress)
                return null;

            // Do not reserve, stage, or move a Barracks SCV until the Supply Depot
            // is observed complete. This preserves the opening worker economy.
            if (!allowConstruction)
                return null;

            // Do not pull the Barracks SCV as soon as the Depot completes.
            // Keep that worker mining until the 150-mineral build is fundable
            // after strategic reservations; then its travel time overlaps the
            // final resource accumulation instead of leaving it idle at site.
            if (observation.Minerals < gameData.BarracksMineralCost ||
                observation.Minerals - gameData.BarracksMineralCost < reservedMinerals)
                return null;

            if (_builderTag is ulong existing &&
                (observation.DeadUnitTags.Contains(existing) ||
                 !observation.Units.Any(unit => unit.Tag == existing && IsLiveScv(unit))))
            {
                assignments.Unreserve(existing);
                _builderTag = null;
                _placement = null;
                _candidatePoints.Clear();
                _placementQueryPending = false;
                _builderMoveSubmitted = false;
                _barracksBuildJob = null;
                _pendingAction = PendingAction.None;
            }

            if (_builderTag is null)
            {
                UnitObservationFacts? builder = null;
                if (preferredBuilderTag is ulong preferredTag)
                {
                    var preferredWorker = observation.Units.FirstOrDefault(unit =>
                        unit.Tag == preferredTag && unit.Tag != excludedBuilderTag && IsLiveScv(unit));
                    if (preferredWorker is not null && !preferredWorker.HasCargo)
                    {
                        // Reuse the Depot builder when it is immediately available.
                        // A cargo-bearing preferred worker must not block the next job:
                        // it remains reserved for its existing route while the Advisor
                        // assigns another eligible SCV if one is available.
                        builder = preferredWorker;
                    }
                    else if (preferredWorker is not null)
                    {
                        Console.WriteLine($"[Opening] Preferred Depot builder SCV {preferredTag} is still carrying resources; selecting another available SCV for Barracks construction.");
                    }
                }

                builder ??= observation.Units
                    .Where(IsLiveScv)
                    .Where(unit => unit.Tag != excludedBuilderTag)
                    .Where(unit => !unit.HasCargo && !assignments.IsReserved(unit.Tag))
                    .OrderBy(unit => unit.Tag)
                    .FirstOrDefault();
                if (builder is null)
                    return null;

                var townHall = observation.Units.FirstOrDefault(unit =>
                    unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                    IsTownHall(unit.UnitKind) && unit.HasPosition);
                if (townHall is null)
                    return null;

                _builderTag = builder.Tag;
                assignments.Reserve(builder.Tag);
                _candidatePoints.AddRange(CreateCandidates(townHall, map));
                _placementQueryPending = _candidatePoints.Count > 0;
                Console.WriteLine(preferredBuilderTag == builder.Tag
                    ? $"[Opening] Construction-crew SCV {_builderTag} continues from the Supply Depot to the Barracks."
                    : $"[Opening] SCV {_builderTag} reserved to construct the first Barracks.");
            }

            if (_placement is null || _builderTag is null)
                return null;

            var worker = observation.Units.FirstOrDefault(unit =>
                unit.Tag == _builderTag.Value && IsLiveScv(unit) && !unit.HasCargo);
            if (worker is null)
                return null;

            var distanceSquared = DistanceSquared(worker.PositionX, worker.PositionY,
                _placement.Value.X, _placement.Value.Y);
            if (distanceSquared > 2.25f)
            {
                if (_builderMoveSubmitted && observation.GameLoop <= _builderMoveRequestGameLoop + 22)
                    return null;
                _builderMoveSubmitted = true;
                _builderMoveRequestGameLoop = observation.GameLoop;
                _pendingAction = PendingAction.MoveBuilder;
                return PositionCommand(worker.Tag, checked((int)gameData.MoveAbilityId.Value), _placement.Value);
            }

            // The Supply Depot completion gate was checked before selecting or staging this worker.
            if (!allowConstruction ||
                observation.Minerals < gameData.BarracksMineralCost ||
                observation.Minerals - gameData.BarracksMineralCost < reservedMinerals)
                return null;
            if (IsBuildSubmitted && observation.GameLoop <= _barracksBuildJob.CreatedAtGameLoop + 8)
                return null;
            if (IsBuildSubmitted)
                _barracksBuildJob.Fail("Opening Barracks structure was not observed before build retry.", observation.GameLoop);

            _barracksBuildJob = buildJobs.CreateBarracksConstruction(observation.GameLoop, worker.Tag,
                observation.Units.Where(unit => unit.Alliance == UnitAlliance.Self &&
                    unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase))
                    .Select(unit => unit.Tag));
            Console.WriteLine($"[Opening] Requesting Barracks: SCV={worker.Tag}, ability={gameData.BarracksBuildAbilityId}, minerals={observation.Minerals}, site=({_placement.Value.X:F1},{_placement.Value.Y:F1}).");
            _pendingAction = PendingAction.BuildBarracks;
            return PositionCommand(worker.Tag,
                checked((int)gameData.BarracksBuildAbilityId.Value), _placement.Value);
        }

        if (barracks.BuildProgress < 1f)
            return null;

        if (!BarracksComplete)
            Console.WriteLine($"[Opening] OBSERVED: Barracks {barracks.Tag} completed.");
        if (_builderTag is ulong builderTag)
        {
            assignments.Unreserve(builderTag);
            ReleasedBuilderTag = builderTag;
            _builderTag = null;
        }

        if (!allowMarineTraining)
            return null;

        var marines = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        if (marines.Length > 0)
        {
            if (_marineTag is null)
                Console.WriteLine($"[Opening] OBSERVED: Marine {marines[0].Tag} produced.");
            _marineTag = marines[0].Tag;

            // Production is the opening objective. Do not send a lone Marine
            // across the map or up an unknown enemy ramp. Hand it to the
            // defensive combat controller immediately; it will engage only
            // contacts that enter its bounded local pursuit envelope.
            Console.WriteLine($"[Opening] Marine {marines[0].Tag} remains on the friendly side; defensive control is active.");
            // Do not return here: the Barracks must keep producing Marines while
            // the existing force is being controlled by the combat planner.
        }

        var trainingOrderObserved = barracks.Orders.Any(order =>
            order.AbilityId == gameData.MarineTrainAbilityId.Value);
        if (trainingOrderObserved)
            return null;
        if (MarineTrainingStarted && !MarineObserved)
            return null;

        // A submitted request is not enough to unlock the next opening phase.
        // Keep the gate closed until the Barracks order is actually observed.
        if (IsOpeningMarineTrainingPending &&
            observation.GameLoop <= _marineBuildJob.CreatedAtGameLoop + 8)
            return null;
        if (IsOpeningMarineTrainingPending)
            _marineBuildJob.Fail("Opening Barracks training order was not observed before retry timeout.", observation.GameLoop);

        if (observation.Minerals < gameData.MarineMineralCost ||
            observation.Minerals - gameData.MarineMineralCost < reservedMinerals ||
            observation.FoodCap < observation.FoodUsed ||
            observation.FoodCap - observation.FoodUsed < gameData.MarineFoodRequired ||
            barracks.HasOrders)
            return null;

        _marineBuildJob = buildJobs.CreateMarineProduction(observation.GameLoop, barracks.Tag,
            observation.Units.Where(unit => unit.Alliance == UnitAlliance.Self && unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase)).Select(unit => unit.Tag));
        Console.WriteLine($"[Opening] Requesting first Marine: Barracks={barracks.Tag}, ability={gameData.MarineTrainAbilityId}, minerals={observation.Minerals}, supply={observation.FoodUsed}/{observation.FoodCap}.");
        _pendingAction = PendingAction.TrainMarine;
        return UnitCommand(barracks.Tag, checked((int)gameData.MarineTrainAbilityId.Value));
    }

    public RequestQuery TakePlacementQuery(Sc2GameDataCatalog gameData)
    {
        if (!_placementQueryPending || _builderTag is null ||
            gameData.BarracksBuildAbilityId is not > 0)
            return null;

        _placementQueryPending = false;
        var query = new RequestQuery { IgnoreResourceRequirements = true };
        foreach (var point in _candidatePoints)
        {
            query.Placements.Add(new RequestQueryBuildingPlacement
            {
                AbilityId = checked((int)gameData.BarracksBuildAbilityId.Value),
                TargetPos = new Point2D { X = point.X, Y = point.Y },
                PlacingUnitTag = _builderTag.Value
            });
        }
        Console.WriteLine($"[Opening] Querying SC2 for a Barracks site ({_candidatePoints.Count} candidates).");
        return query;
    }

    public void OnPlacementQueryResult(ResponseQuery response)
    {
        if (_builderTag is null || _candidatePoints.Count == 0 || _placement is not null)
            return;

        var count = Math.Min(response?.Placements.Count ?? 0, _candidatePoints.Count);
        for (var index = 0; index < count; index++)
        {
            if (response.Placements[index].Result != ActionResult.Success)
                continue;
            _placement = _candidatePoints[index];
            Console.WriteLine($"[Opening] SC2 accepted Barracks site ({_placement.Value.X:F1},{_placement.Value.Y:F1}).");
            return;
        }

        _placementQueryPending = true;
        Console.WriteLine("[Opening] SC2 did not accept a Barracks placement candidate; retrying.");
    }

    public void OnActionResult(ActionResult? result)
    {
        if (_pendingAction == PendingAction.None)
            return;

        if (result is null || result != ActionResult.Success)
        {
            switch (_pendingAction)
            {
                case PendingAction.MoveBuilder:
                    _builderMoveSubmitted = false;
                    break;
                case PendingAction.BuildBarracks:
                    if (_barracksBuildJob is not null && !_barracksBuildJob.IsCompleted && !_barracksBuildJob.IsFailed)
                        _barracksBuildJob.Fail("SC2 rejected the opening Barracks construction command.", _barracksBuildJob.LastObservedGameLoop);
                    _barracksBuildJob = null;
                    break;
                case PendingAction.TrainMarine:
                    if (_marineBuildJob is not null && !_marineBuildJob.IsCompleted && !_marineBuildJob.IsFailed)
                        _marineBuildJob.Fail("SC2 rejected the opening Marine training command.", _marineBuildJob.LastObservedGameLoop);
                    _marineBuildJob = null;
                    break;
            }
        }

        _pendingAction = PendingAction.None;
    }

    public string BarracksBuildJobState => _barracksBuildJob?.CurrentState ?? "NoBuildJob";
    public string MarineProductionJobState => _marineBuildJob?.CurrentState ?? "NoProductionJob";

    private static bool IsLiveScv(UnitObservationFacts unit) =>
        unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
        unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase);

    private static bool IsTownHall(string kind) =>
        kind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<MapPoint> CreateCandidates(UnitObservationFacts townHall, SpatialMap map)
    {
        var origin = new MapPoint(townHall.PositionX, townHall.PositionY);
        var forward = (map.Center - origin).Normalize();
        var lateral = new MapPoint(-forward.Y, forward.X);
        var offsets = new (float Forward, float Lateral)[]
        {
            (4.5f, 0), (4.5f, 2), (4.5f, -2), (4.5f, 4), (4.5f, -4),
            (6.5f, 0), (6.5f, 2), (6.5f, -2), (6.5f, 4), (6.5f, -4),
            (8.5f, 0), (8.5f, 2), (8.5f, -2), (8.5f, 4), (8.5f, -4)
        };
        return offsets.Select(offset => origin + forward * offset.Forward + lateral * offset.Lateral)
            .Where(map.Bounds.Contains).ToArray();
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

    private static SC2APIProtocol.Action UnitCommand(ulong tag, int abilityId) =>
        new()
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = abilityId,
                    UnitTags = { tag },
                    QueueCommand = false
                }
            }
        };

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
