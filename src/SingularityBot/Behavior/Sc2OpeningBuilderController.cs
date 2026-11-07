using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Selects the first Supply Depot builder when the mineral bank crosses the
/// configured threshold, then stages that returning SCV and constructs at a
/// placement accepted by the SC2 query API.
/// </summary>
public sealed class Sc2OpeningBuilderController
{
    public const uint DefaultBuilderPullThreshold = 10;

    private enum PendingAction
    {
        None,
        Move,
        Build
    }

    private readonly uint _builderPullThreshold;
    private uint _previousMinerals;
    private bool _hasMineralSample;
    private bool _thresholdReached;
    private ulong? _builderTag;
    private ulong? _returnTownHallTag;
    private IReadOnlyList<MapPoint> _candidatePoints = Array.Empty<MapPoint>();
    private bool _placementQueryPending;
    private MapPoint? _placement;
    private bool _moveSubmitted;
    private bool _buildSubmitted;
    private uint _buildRequestGameLoop;
    private PendingAction _pendingAction;
    private bool _completed;

    public Sc2OpeningBuilderController(uint builderPullThreshold = DefaultBuilderPullThreshold)
    {
        if (builderPullThreshold == 0)
            throw new ArgumentOutOfRangeException(nameof(builderPullThreshold));
        _builderPullThreshold = builderPullThreshold;
    }

    public ulong? BuilderTag => _builderTag;
    public bool ThresholdReached => _thresholdReached;
    public bool IsCompleted => _completed;
    public MapPoint? Placement => _placement;

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        SpatialMap spatialMap,
        Sc2GameDataCatalog gameData,
        bool initialScvProductionObserved,
        WorkerResourceAssignmentPlanner assignments)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(assignments);

        UpdateMineralThreshold(observation.Minerals);
        if (_completed || !initialScvProductionObserved || !_thresholdReached ||
            spatialMap is null ||
            gameData.SupplyDepotBuildAbilityId is not > 0 ||
            gameData.SupplyDepotMineralCost == 0 ||
            gameData.MoveAbilityId is not > 0)
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
            _buildSubmitted = false;
            _pendingAction = PendingAction.None;
            Console.WriteLine($"[Opening] Reserved builder SCV {selectedTag} was lost or morphed; selecting a replacement.");
        }

        if (_builderTag is null)
        {
            var returner = FindReturningScv(observation, assignments);
            if (returner is null)
                return null;

            _builderTag = returner.Value.Worker.Tag;
            Console.WriteLine($"[Opening] 10-mineral threshold reached; pulling returning SCV {_builderTag.Value} from its mineral return cycle.");
            _returnTownHallTag = returner.Value.TownHallTag;
            assignments.Reserve(returner.Tag);
            var townHall = observation.Units.FirstOrDefault(unit => unit.Tag == _returnTownHallTag.Value && unit.IsLiveVisible);
            if (townHall is null)
                return null;
            _candidatePoints = CreatePlacementCandidates(townHall, spatialMap);
            _placementQueryPending = _candidatePoints.Count > 0;
            return null;
        }

        if (_placement is null)
            return null;

        var builder = observation.Units.FirstOrDefault(unit =>
            unit.Tag == _builderTag.Value &&
            unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag));
        if (builder is null || builder.HasCargo)
            return null;

        var depot = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                           unit.IsLiveVisible &&
                           (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
                            unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)) &&
                           unit.HasPosition)
            .OrderBy(unit => DistanceSquared(unit.PositionX, unit.PositionY, _placement.Value.X, _placement.Value.Y))
            .FirstOrDefault();

        if (depot is not null &&
            DistanceSquared(depot.PositionX, depot.PositionY, _placement.Value.X, _placement.Value.Y) <= 9)
        {
            _buildSubmitted = true;
            if (depot.BuildProgress >= 1f)
            {
                assignments.Unreserve(_builderTag.Value);
                _completed = true;
                Console.WriteLine($"[Opening] Supply Depot {depot.Tag} completed; SCV {_builderTag.Value} released to the economy.");
            }
            return null;
        }

        var distanceSquared = DistanceSquared(
            builder.PositionX, builder.PositionY, _placement.Value.X, _placement.Value.Y);

        if (distanceSquared > 2.25f)
        {
            if (_moveSubmitted)
                return null;

            _moveSubmitted = true;
            _pendingAction = PendingAction.Move;
            return CreatePositionCommand(
                _builderTag.Value,
                checked((int)gameData.MoveAbilityId.Value),
                _placement.Value);
        }

        if (observation.Minerals < gameData.SupplyDepotMineralCost)
            return null;

        if (_buildSubmitted)
        {
            if (observation.GameLoop > _buildRequestGameLoop + 8)
                _buildSubmitted = false;
            else
                return null;
        }

        _buildSubmitted = true;
        _buildRequestGameLoop = observation.GameLoop;
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
        if (response is null)
            return;

        var count = Math.Min(response.Placements.Count, _candidatePoints.Count);
        for (var index = 0; index < count; index++)
        {
            if (response.Placements[index].Result != ActionResult.Success)
                continue;

            _placement = _candidatePoints[index];
            Console.WriteLine($"[Opening] SC2 placement query accepted Supply Depot site ({_placement.Value.X:F1}, {_placement.Value.Y:F1}) for builder {_builderTag}.");
            return;
        }

        Console.WriteLine($"[Opening] SC2 rejected all {_candidatePoints.Count} candidate Supply Depot sites; builder {_builderTag} remains reserved.");
    }

    public void OnActionResult(ActionResult? result)
    {
        if (_pendingAction == PendingAction.None)
            return;

        if (result is null || result != ActionResult.Success)
        {
            if (_pendingAction == PendingAction.Move)
                _moveSubmitted = false;
            else if (_pendingAction == PendingAction.Build)
                _buildSubmitted = false;
        }

        _pendingAction = PendingAction.None;
    }

    private void UpdateMineralThreshold(uint minerals)
    {
        if (!_hasMineralSample)
        {
            _hasMineralSample = true;
            _previousMinerals = minerals;
            return;
        }

        if (!_thresholdReached &&
            _previousMinerals < _builderPullThreshold &&
            minerals >= _builderPullThreshold)
            _thresholdReached = true;

        _previousMinerals = minerals;
    }

    private static (UnitObservationFacts Worker, ulong TownHallTag)? FindReturningScv(
        NormalizedObservation observation,
        WorkerResourceAssignmentPlanner assignments)
    {
        var townHallTags = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                           unit.IsLiveVisible &&
                           IsTownHall(unit.UnitKind) &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .Select(unit => unit.Tag)
            .ToHashSet();

        foreach (var worker in observation.Units
                     .Where(unit => unit.Alliance == UnitAlliance.Self &&
                                    unit.IsLiveVisible &&
                                    unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) &&
                                    unit.HasCargo &&
                                    unit.Orders.Count > 0 &&
                                    !observation.DeadUnitTags.Contains(unit.Tag))
                     .OrderBy(unit => unit.Tag))
        {
            var targetTag = worker.Orders[0].TargetUnitTag;
            if (targetTag is not ulong target)
                continue;

            if (townHallTags.Contains(target))
                return (worker, target);

            if (assignments.TryGet(worker.Tag, out var assignment) &&
                assignment.MineralTag == target &&
                townHallTags.Contains(assignment.TownHallTag))
                return (worker, assignment.TownHallTag);
        }

        return null;
    }

    private static IReadOnlyList<MapPoint> CreatePlacementCandidates(
        UnitObservationFacts townHall,
        SpatialMap map)
    {
        var origin = new MapPoint(townHall.PositionX, townHall.PositionY);
        var forward = (map.Side.EnemyStart - origin).Normalize();
        var lateral = new MapPoint(-forward.Y, forward.X);
        var offsets = new (float Forward, float Lateral)[]
        {
            (4.5f, 0), (4.5f, 2), (4.5f, -2), (4.5f, 4), (4.5f, -4),
            (6.5f, 0), (6.5f, 2), (6.5f, -2), (6.5f, 4), (6.5f, -4),
            (8.5f, 0), (8.5f, 2), (8.5f, -2), (8.5f, 4), (8.5f, -4)
        };

        return offsets
            .Select(offset => origin + forward * offset.Forward + lateral * offset.Lateral)
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
