using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>Builds the second Barracks without stealing the first Barracks builder.</summary>
public sealed class Sc2SecondBarracksController
{
    private enum Pending { None, Move, Build }
    private readonly List<MapPoint> _candidates = new();
    private ulong? _builderTag;
    private MapPoint? _site;
    private bool _queryPending;
    private bool _moveSubmitted;
    private uint _moveLoop;
    private bool _buildSubmitted;
    private uint _buildLoop;
    private Pending _pending;

    public ulong? BuilderTag => _builderTag;
    public bool IsComplete { get; private set; }

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        SpatialMap map,
        Sc2GameDataCatalog gameData,
        WorkerResourceAssignmentPlanner assignments,
        IReadOnlySet<ulong> constructionCrewTags)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(constructionCrewTags);

        if (IsComplete || map is null || gameData.BarracksBuildAbilityId is not > 0 ||
            gameData.BarracksMineralCost == 0 || gameData.MoveAbilityId is not > 0)
            return null;

        var barracks = observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
            !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();
        if (barracks.Length >= 2)
        {
            IsComplete = true;
            ReleaseBuilder(assignments);
            Console.WriteLine("[Opening] OBSERVED: second Barracks completed; construction SCV released.");
            return null;
        }

        if (barracks.Length == 0)
            return null;

        if (_builderTag is ulong current &&
            (observation.DeadUnitTags.Contains(current) ||
             !observation.Units.Any(unit => unit.Tag == current && IsLiveScv(unit))))
        {
            ReleaseBuilder(assignments);
            ResetPlacement();
        }

        if (_builderTag is null)
        {
            var builder = observation.Units
                .Where(IsLiveScv)
                .Where(unit => !unit.HasCargo && constructionCrewTags.Contains(unit.Tag) &&
                    !assignments.IsReserved(unit.Tag))
                .OrderBy(unit => unit.Tag)
                .FirstOrDefault();
            if (builder is null)
                return null;

            var townHall = observation.Units.FirstOrDefault(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible && unit.HasPosition &&
                IsTownHall(unit.UnitKind));
            if (townHall is null)
                return null;

            _builderTag = builder.Tag;
            assignments.Reserve(builder.Tag);
            _candidates.AddRange(CreateCandidates(townHall, map));
            _queryPending = _candidates.Count > 0;
            Console.WriteLine($"[Opening] SCV {_builderTag} reserved for the second Barracks.");
            return null;
        }

        if (_queryPending || _site is null)
            return null;

        var worker = observation.Units.FirstOrDefault(unit =>
            unit.Tag == _builderTag.Value && IsLiveScv(unit) && !unit.HasCargo);
        if (worker is null)
            return null;

        if (DistanceSquared(worker.PositionX, worker.PositionY, _site.Value.X, _site.Value.Y) > 2.25f)
        {
            if (_moveSubmitted && observation.GameLoop <= _moveLoop + 22)
                return null;
            _moveSubmitted = true;
            _moveLoop = observation.GameLoop;
            _pending = Pending.Move;
            return PositionCommand(worker.Tag, checked((int)gameData.MoveAbilityId.Value), _site.Value);
        }

        if (observation.Minerals < gameData.BarracksMineralCost ||
            (_buildSubmitted && observation.GameLoop <= _buildLoop + 8))
            return null;

        _buildSubmitted = true;
        _buildLoop = observation.GameLoop;
        _pending = Pending.Build;
        Console.WriteLine($"[Opening] Requesting second Barracks: SCV={worker.Tag}, minerals={observation.Minerals}, site=({_site.Value.X:F1},{_site.Value.Y:F1}).");
        return PositionCommand(worker.Tag, checked((int)gameData.BarracksBuildAbilityId.Value), _site.Value);
    }

    public RequestQuery TakePlacementQuery(Sc2GameDataCatalog gameData)
    {
        if (!_queryPending || _builderTag is null || gameData.BarracksBuildAbilityId is not > 0)
            return null;
        _queryPending = false;
        var query = new RequestQuery { IgnoreResourceRequirements = true };
        foreach (var point in _candidates)
            query.Placements.Add(new RequestQueryBuildingPlacement
            {
                AbilityId = checked((int)gameData.BarracksBuildAbilityId.Value),
                TargetPos = new Point2D { X = point.X, Y = point.Y },
                PlacingUnitTag = _builderTag.Value
            });
        return query;
    }

    public void OnPlacementQueryResult(ResponseQuery response)
    {
        if (_builderTag is null || _candidates.Count == 0 || _site is not null)
            return;
        var count = Math.Min(response?.Placements.Count ?? 0, _candidates.Count);
        for (var index = 0; index < count; index++)
        {
            if (response.Placements[index].Result != ActionResult.Success)
                continue;
            _site = _candidates[index];
            return;
        }
        _queryPending = true;
    }

    public void OnActionResult(ActionResult? result)
    {
        if (_pending == Pending.None)
            return;
        if (result is null || result != ActionResult.Success)
        {
            if (_pending == Pending.Move) _moveSubmitted = false;
            if (_pending == Pending.Build) _buildSubmitted = false;
        }
        _pending = Pending.None;
    }

    private void ReleaseBuilder(WorkerResourceAssignmentPlanner assignments)
    {
        if (_builderTag is ulong tag)
            assignments.Unreserve(tag);
        _builderTag = null;
    }

    private void ResetPlacement()
    {
        _candidates.Clear();
        _site = null;
        _queryPending = false;
        _moveSubmitted = false;
        _buildSubmitted = false;
        _pending = Pending.None;
    }

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
            (9, 0), (9, 3), (9, -3), (12, 0), (12, 3), (12, -3),
            (15, 0), (15, 3), (15, -3), (18, 0), (18, 3), (18, -3)
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

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
