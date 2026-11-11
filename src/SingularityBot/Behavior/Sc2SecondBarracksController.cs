using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>Scales the production line from one to four Barracks without stealing reserved opening workers.</summary>
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
    private int _targetBarracksCount = 2;
    private long? _buildJobId;
    private ulong? _preferredBuilderTag;
    private bool _supplyDepotGateSatisfied;
    public ulong? PreferredBuilderTag => _preferredBuilderTag;
    public bool RequiresSupplyDepot { get; private set; }
    // Retained for the orchestration boundary; production no longer waits on town-hall expansion.
    public bool RequiresCommandCenter => false;

    public void NotifyCommandCenterStarted() { }

    public ulong? BuilderTag => _builderTag;
    public bool IsComplete { get; private set; }
    public int CandidateCount => _candidates.Count;
    public int TargetBarracksCount => _targetBarracksCount;
    public MapPoint? Site => _site;
    public bool IsBuildSubmitted => _buildSubmitted;

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        SpatialMap map,
        Sc2GameDataCatalog gameData,
        WorkerResourceAssignmentPlanner assignments,
        IReadOnlySet<ulong> constructionCrewTags,
        BuildJobRuntime buildJobs = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(constructionCrewTags);
        buildJobs?.ReconcileBarracks(observation);

        if (IsComplete || map is null || gameData.BarracksBuildAbilityId is not > 0 ||
            gameData.BarracksMineralCost == 0 || gameData.MoveAbilityId is not > 0)
            return null;

        var barracks = observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
            !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();
        var completedCount = barracks.Count(unit => unit.BuildProgress >= 1f);
        if (completedCount >= _targetBarracksCount)
        {
            ReleaseBuilder(assignments);
            ResetPlacement();
            _buildJobId = null;
            if (_targetBarracksCount >= 4)
            {
                IsComplete = true;
                Console.WriteLine("[Opening] OBSERVED: four-Barracks production milestone completed.");
                return null;
            }

            _targetBarracksCount++;
            _supplyDepotGateSatisfied = false;
            Console.WriteLine($"[Opening] OBSERVED: {_targetBarracksCount - 1} Barracks completed; preparing Barracks {_targetBarracksCount}.");
            return null;
        }

        // Keep reconciling the in-progress Barracks while the Depot controller
        // runs. The Depot and next Barracks use separate reserved SCVs and may
        // progress concurrently; supply capacity is handled by the repeatable
        // Depot job rather than serializing production-facility construction.
        if (RequiresCommandCenter)
            return null;

        // As soon as Barracks two is observed under construction, use the
        // released first-Barracks SCV to build the next Depot in parallel.
        // Keep the second Barracks worker committed to its own structure and
        // allow Barracks three to start as soon as its own builder and minerals are available.
        if (_targetBarracksCount == 2 && !_supplyDepotGateSatisfied &&
            barracks.Length >= 2 && completedCount < 2)
        {
            RequiresSupplyDepot = true;
            Console.WriteLine("[Opening] OBSERVED: Barracks two is under construction; starting the follow-up Supply Depot in parallel before Barracks three.");
            return null;
        }

        // The first Barracks is owned by the opening controller; this controller
        // grows the production line sequentially to the four-Barracks milestone.
        if (barracks.Length >= _targetBarracksCount || barracks.Length == 0)
            return null;

        if (_builderTag is ulong current &&
            (observation.DeadUnitTags.Contains(current) ||
             !observation.Units.Any(unit => unit.Tag == current && IsLiveScv(unit))))
        {
            if (_buildJobId is long failedJob && buildJobs is not null && buildJobs.TryGet(failedJob, out var failed))
                failed.Fail("Assigned SCV died or was lost from live observation.", observation.GameLoop);
            _buildJobId = null;
            ReleaseBuilder(assignments);
            ResetPlacement();
        }

        if (_builderTag is null)
        {
            var townHall = observation.Units.FirstOrDefault(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible && unit.HasPosition &&
                IsTownHall(unit.UnitKind));
            if (townHall is null)
                return null;

            // Build-site candidates are known before selecting the worker. Pick
            // the unreserved SCV with the shortest trip to the candidate cluster,
            // rather than selecting by tag or pulling a distant construction-role
            // worker while a closer mineral-line worker is available.
            _candidates.AddRange(CreateCandidates(townHall, map, _targetBarracksCount));
            if (_candidates.Count == 0)
                return null;

            var eligibleWorkers = observation.Units
                .Where(IsLiveScv)
                .Where(unit => !unit.HasCargo && !assignments.IsReserved(unit.Tag))
                .ToArray();
            var builder = eligibleWorkers
                .Select(worker => new
                {
                    Worker = worker,
                    SiteDistanceSquared = _candidates.Count == 0
                        ? float.MaxValue
                        : _candidates.Min(point => DistanceSquared(
                            worker.PositionX, worker.PositionY, point.X, point.Y)),
                    IsConstructionCrew = constructionCrewTags.Contains(worker.Tag),
                    IsPreferred = worker.Tag == _preferredBuilderTag
                })
                .OrderBy(candidate => candidate.SiteDistanceSquared)
                .ThenBy(candidate => candidate.IsPreferred ? 0 : 1)
                .ThenBy(candidate => candidate.IsConstructionCrew ? 0 : 1)
                .ThenBy(candidate => candidate.Worker.Tag)
                .FirstOrDefault()?.Worker;
            if (builder is null)
                return null;

            _builderTag = builder.Tag;
            assignments.Reserve(builder.Tag);
            _buildJobId = buildJobs?.CreateBarracksConstruction(observation.GameLoop, builder.Tag, barracks.Select(unit => unit.Tag)).Id;
            _queryPending = _candidates.Count > 0;
            Console.WriteLine($"[Opening] SCV {_builderTag} reserved for Barracks {_targetBarracksCount}.");
            return null;
        }

        if (_queryPending || _site is null)
            return null;

        var worker = observation.Units.FirstOrDefault(unit =>
            unit.Tag == _builderTag.Value && IsLiveScv(unit) && !unit.HasCargo);
        if (worker is null)
            return null;

        // SC2's Build command pathfinds the SCV to the site. Use a wider
        // arrival envelope so a worker a couple of tiles away starts the job
        // instead of oscillating around an arbitrary Move threshold.
        if (DistanceSquared(worker.PositionX, worker.PositionY, _site.Value.X, _site.Value.Y) > 9f)
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
        Console.WriteLine($"[Opening] Requesting Barracks {_targetBarracksCount}: SCV={worker.Tag}, minerals={observation.Minerals}, site=({_site.Value.X:F1},{_site.Value.Y:F1}).");
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


    public void NotifySupplyDepotCompleted(ulong? recycledBuilderTag)
    {
        if (!RequiresSupplyDepot) return;
        _preferredBuilderTag = recycledBuilderTag;
        _supplyDepotGateSatisfied = true;
        RequiresSupplyDepot = false;
        Console.WriteLine($"[Opening] Supply expansion confirmed; Barracks three may proceed, preferred recycled SCV={_preferredBuilderTag?.ToString() ?? "none"}.");
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

    private static IReadOnlyList<MapPoint> CreateCandidates(UnitObservationFacts townHall, SpatialMap map, int targetBarracksCount)
    {
        var origin = new MapPoint(townHall.PositionX, townHall.PositionY);
        var forward = (map.Center - origin).Normalize();
        var lateral = new MapPoint(-forward.Y, forward.X);
        var ring = Math.Max(0, targetBarracksCount - 2);
        // Keep successive Barracks close to the production line. A 12-tile jump
        // made Barracks three much farther away than necessary and could leave
        // too few legal placement candidates near the base.
        var start = 9f + ring * 3f;
        var offsets = new (float Forward, float Lateral)[]
        {
            (start, 0), (start, 3), (start, -3),
            (start + 3, 0), (start + 3, 3), (start + 3, -3),
            (start + 6, 0), (start + 6, 3), (start + 6, -3),
            (start + 9, 0), (start + 9, 3), (start + 9, -3)
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
