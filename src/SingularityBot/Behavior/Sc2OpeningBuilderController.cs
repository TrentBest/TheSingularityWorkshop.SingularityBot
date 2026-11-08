using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Reserves the second SCV produced after the opening begins, stages it at a
/// queried Supply Depot site, and constructs when the Depot is affordable.
/// </summary>
public sealed class Sc2OpeningBuilderController
{

    private enum PendingAction
    {
        None,
        Move,
        Build
    }

    private readonly HashSet<ulong> _initialScvTags = new();
    private readonly SortedSet<ulong> _producedScvTags = new();
    private bool _initialScvPopulationCaptured;
    private ulong? _builderTag;
    private IReadOnlyList<MapPoint> _candidatePoints = Array.Empty<MapPoint>();
    private bool _placementQueryPending;
    private MapPoint? _placement;
    private bool _moveSubmitted;
    private uint _moveRequestGameLoop;
    private int _placementQueryAttempts;
    private bool _buildSubmitted;
    private uint _buildRequestGameLoop;
    private PendingAction _pendingAction;
    private bool _completed;

    public Sc2OpeningBuilderController() { }

    public ulong? BuilderTag => _builderTag;
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

        if (_completed || !initialScvProductionObserved ||
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

        if (!_initialScvPopulationCaptured)
        {
            foreach (var worker in observation.Units.Where(IsLiveScv))
                _initialScvTags.Add(worker.Tag);
            _initialScvPopulationCaptured = true;
            Console.WriteLine($"[Opening] Captured {_initialScvTags.Count} starting SCVs; the second newly produced SCV will build the first Depot.");
        }

        foreach (var worker in observation.Units.Where(IsLiveScv))
        {
            if (!_initialScvTags.Contains(worker.Tag))
                _producedScvTags.Add(worker.Tag);
        }

        if (_builderTag is null)
        {
            if (_producedScvTags.Count < 2)
                return null;

            // SC2 unit tags increase with creation order; retain the second new SCV
            // as the dedicated builder rather than interrupting a starting miner.
            _builderTag = _producedScvTags.ElementAt(1);
            assignments.Reserve(_builderTag.Value);
            var townHall = observation.Units.FirstOrDefault(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible && IsTownHall(unit.UnitKind));
            if (townHall is null)
                return null;

            _candidatePoints = CreatePlacementCandidates(townHall, spatialMap);
            _placementQueryPending = _candidatePoints.Count > 0;
            Console.WriteLine($"[Opening] Second produced SCV {_builderTag.Value} assigned as dedicated Depot builder; staging toward the ramp.");
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
            // An accepted Move is not proof that the SCV reached the site.
            // Retry after a bounded travel window so pathing interruptions cannot stall the opening.
            if (_moveSubmitted && observation.GameLoop <= _moveRequestGameLoop + 22)
                return null;

            if (_moveSubmitted)
                Console.WriteLine($"[Opening] Builder SCV {_builderTag} has not reached the Depot site; retrying Move (distance²={distanceSquared:F2}).");

            _moveSubmitted = true;
            _moveRequestGameLoop = observation.GameLoop;
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

    private static bool IsLiveScv(UnitObservationFacts unit) =>
        unit.Alliance == UnitAlliance.Self &&
        unit.IsLiveVisible &&
        unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase);


    private static IReadOnlyList<MapPoint> CreatePlacementCandidates(
        UnitObservationFacts townHall,
        SpatialMap map)
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
