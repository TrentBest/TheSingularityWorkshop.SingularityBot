using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

public sealed class Sc2MarineRallyController
{
    private readonly Dictionary<ulong, uint> _lastRallyCommandLoops = new();
    private readonly Dictionary<ulong, MapPoint> _lastRallyTargets = new();
    private const uint RetryAfterGameLoops = 44;
    private const uint PatrolRotationGameLoops = 672;
    private const float ArrivalRadiusSquared = 4f;
    private const float ScoutThreatRadiusSquared = 144f;
    private const float RetreatThreatSeparationSquared = 196f;
    private bool _squadDispatched;
    private MapPoint? _rendezvousAnchor;
    private ulong? _scoutTag;
    private MapPoint? _scoutTarget;
    private RampScoutPlan? _scoutPlan;
    private MapPoint? _scoutRetreatTarget;
    private bool _scoutPlanAttempted;
    private string _scoutStatus = "Unassigned";
    private string _scoutDetails = string.Empty;
    private bool _scoutStatusChanged;

    public string ScoutStatus => _scoutStatus;
    public string ScoutDetails => _scoutDetails;
    public MapPoint? ScoutTarget => _scoutTarget;
    public ulong? ScoutTag => _scoutTag;

    public void Reset()
    {
        _lastRallyCommandLoops.Clear();
        _lastRallyTargets.Clear();
        _squadDispatched = false;
        _rendezvousAnchor = null;
        _scoutTag = null;
        _scoutTarget = null;
        _scoutPlan = null;
        _scoutRetreatTarget = null;
        _scoutPlanAttempted = false;
        _scoutStatus = "Unassigned";
        _scoutDetails = string.Empty;
        _scoutStatusChanged = false;
    }

    public ulong? GetScoutTag(NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var liveMarineTags = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .Select(unit => unit.Tag)
            .OrderBy(tag => tag)
            .ToArray();

        if (liveMarineTags.Length == 0)
        {
            if (_scoutTag is not null)
            {
                _scoutTag = null;
                _scoutTarget = null;
                _scoutPlan = null;
                _scoutRetreatTarget = null;
                _scoutPlanAttempted = false;
                _squadDispatched = false;
                _rendezvousAnchor = null;
                SetScoutStatus("NoLiveScout", "No live Marine is available for the dedicated scout role");
            }
            return null;
        }

        if (_scoutTag is null || !liveMarineTags.Contains(_scoutTag.Value))
        {
            _scoutTag = liveMarineTags[0];
            _scoutTarget = null;
            _scoutPlan = null;
            _scoutRetreatTarget = null;
            _scoutPlanAttempted = false;
            SetScoutStatus("ScoutAssigned", $"marine={_scoutTag.Value}");
        }

        return _scoutTag;
    }

    public bool TryTakeScoutStatusChange(out string status, out string details)
    {
        status = _scoutStatus;
        details = _scoutDetails;
        if (!_scoutStatusChanged)
            return false;
        _scoutStatusChanged = false;
        return true;
    }

    public IReadOnlyList<SC2APIProtocol.Action> TryCreateActions(
        NormalizedObservation observation,
        SpatialMap map,
        IReadOnlyList<MarineTacticalDecision> decisions,
        Sc2GameDataCatalog gameData,
        bool fourthMarineInProduction = false,
        SpatialTerrain terrain = null,
        SpatialTopology topology = null,
        SpatialGridSnapshot visibility = null,
        bool advanceToEnemyStart = false)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(gameData);
        if (map is null || decisions.Count == 0 || gameData.MoveAbilityId is not > 0)
            return Array.Empty<SC2APIProtocol.Action>();

        var direction = (map.Side.EnemyStart - map.Side.FriendlyStart).Normalize();
        var actions = new List<SC2APIProtocol.Action>();
        var liveMarineTags = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .Select(unit => unit.Tag)
            .ToHashSet();

        foreach (var retired in _lastRallyCommandLoops.Keys.Where(tag => !liveMarineTags.Contains(tag)).ToArray())
            _lastRallyCommandLoops.Remove(retired);
        foreach (var retired in _lastRallyTargets.Keys.Where(tag => !liveMarineTags.Contains(tag)).ToArray())
            _lastRallyTargets.Remove(retired);

        var scoutTag = GetScoutTag(observation);
        var scout = scoutTag is ulong assignedScoutTag
            ? observation.Units.FirstOrDefault(unit =>
                unit.Tag == assignedScoutTag && unit.IsLiveVisible && unit.HasPosition)
            : null;
        var patrolOrigin = scout is null
            ? (MapPoint?)null
            : new MapPoint(scout.PositionX, scout.PositionY);
        var patrolPoints = GetFriendlyPatrolWaypoints(map, terrain, topology, patrolOrigin);

        var scoutActions = new List<SC2APIProtocol.Action>();
        if (scoutTag is ulong tag && scout is not null)
        {
            EnsureScoutPlan(map, terrain, topology, scout);
            var hasScoutDecision = decisions.Any(item => item.MarineTag == tag);
            var scoutDecision = decisions.FirstOrDefault(item => item.MarineTag == tag);
            if (hasScoutDecision && scoutDecision.Action == MarineTacticalAction.Hold &&
                (scoutDecision.TargetTag is null || scoutDecision.IsOutsideLocalDefenseEnvelope))
            {
                var threats = observation.Units
                    .Where(unit => unit.Alliance == UnitAlliance.Enemy && unit.IsLiveVisible && unit.HasPosition &&
                        DistanceSquared(scout.PositionX, scout.PositionY, unit.PositionX, unit.PositionY) <=
                        ScoutThreatRadiusSquared)
                    .ToArray();

                if (threats.Length > 0)
                {
                    if (_scoutRetreatTarget is not MapPoint currentRetreat ||
                        threats.Any(threat => DistanceSquared(currentRetreat.X, currentRetreat.Y,
                            threat.PositionX, threat.PositionY) < RetreatThreatSeparationSquared))
                    {
                        _scoutRetreatTarget = FindSafeRetreatPoint(
                            patrolPoints, map, terrain, topology,
                            new MapPoint(scout.PositionX, scout.PositionY), threats);
                        if (_scoutRetreatTarget is MapPoint safePoint)
                            SetScoutStatus("ThreatRetreat",
                                $"marine={tag};threats={threats.Length};retreat={safePoint.X:F1},{safePoint.Y:F1}");
                        else
                            SetScoutStatus("ThreatNoSafeRetreat",
                                $"marine={tag};threats={threats.Length};no connected friendly retreat waypoint");
                    }
                }
                else if (_scoutRetreatTarget is not null)
                {
                    _scoutRetreatTarget = null;
                    SetScoutStatus("ResumingRampScout", $"marine={tag}");
                }

                var destination = _scoutRetreatTarget ?? _scoutTarget;
                if (destination is MapPoint target)
                {
                    var distanceSquared = DistanceSquared(
                        scout.PositionX, scout.PositionY, target.X, target.Y);
                    if (distanceSquared > ArrivalRadiusSquared &&
                        (_scoutRetreatTarget is not null ||
                         !_lastRallyCommandLoops.TryGetValue(tag, out var scoutLastCommand) ||
                         observation.GameLoop >= scoutLastCommand + RetryAfterGameLoops))
                    {
                        _lastRallyCommandLoops[tag] = observation.GameLoop;
                        scoutActions.Add(MoveCommand(
                            tag, checked((int)gameData.MoveAbilityId.Value), target));
                        var planDetails = _scoutPlan is RampScoutPlan plan
                            ? $";terrain={plan.TerrainHeight};rampEdge={plan.RampEdgePoint.X:F1},{plan.RampEdgePoint.Y:F1}"
                            : string.Empty;
                        SetScoutStatus(_scoutRetreatTarget is null ? "MovingToRampFoot" : "Retreating",
                            $"marine={tag};target={target.X:F1},{target.Y:F1}{planDetails}");
                    }
                    else if (distanceSquared <= ArrivalRadiusSquared)
                    {
                        _lastRallyCommandLoops.Remove(tag);
                        if (_scoutRetreatTarget is not null)
                        {
                            SetScoutStatus("HoldingSafeRetreat",
                                $"marine={tag};retreat={target.X:F1},{target.Y:F1};threats={threats.Length}");
                        }
                        else if (_scoutPlan is RampScoutPlan plan)
                        {
                            var rampVisibility = visibility is not null &&
                                visibility.TryGetValue(plan.RampEdgePoint, out var visibilityValue)
                                    ? visibilityValue switch { 2 => "visible", 1 => "fogged", _ => "hidden" }
                                    : "unavailable";
                            var townHallVisible = observation.Units.Any(unit =>
                                unit.Alliance == UnitAlliance.Enemy && unit.IsLiveVisible &&
                                IsEnemyTownHall(unit.UnitKind));
                            SetScoutStatus("AtRampFoot",
                                $"marine={tag};rampEntranceVisibility={rampVisibility};enemyTownHallVisible={townHallVisible};" +
                                $"target={plan.ObservationPoint.X:F1},{plan.ObservationPoint.Y:F1};" +
                                $"rampEdge={plan.RampEdgePoint.X:F1},{plan.RampEdgePoint.Y:F1};terrain={plan.TerrainHeight}");
                        }
                        else
                        {
                            SetScoutStatus("AtFriendlyObservation",
                                $"marine={tag};target={target.X:F1},{target.Y:F1}");
                        }
                    }
                }
            }
        }

        // The first Marine is a dedicated, non-combat scout. With map layers it
        // stages at a connected low-ground foot of the enemy ramp; without them
        // it uses only the conservative friendly observation post. The remaining
        // four Marines assemble before taking up the distributed patrol screen.
        if (patrolPoints.Count == 0 && !advanceToEnemyStart)
            return scoutActions;

        var squadTags = liveMarineTags
            .Where(tag => tag != scoutTag)
            .OrderBy(tag => tag)
            .ToArray();
        if (!_squadDispatched && squadTags.Length < 4)
            return scoutActions;

        var producingBarracks = observation.Units.FirstOrDefault(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible && unit.HasPosition &&
            unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
            unit.Orders.Any(order => order.AbilityId == gameData.MarineTrainAbilityId));
        _rendezvousAnchor ??= producingBarracks is null
            ? map.Side.FriendlyStart
            : new MapPoint(producingBarracks.PositionX, producingBarracks.PositionY);
        var rendezvousAnchor = _rendezvousAnchor.Value;
        var rendezvousOffsets = MarineFormationPlanner.GetOffsets(
            MarineFormation.Box, squadTags.Length, direction.X, direction.Y);
        var rendezvousTargets = squadTags.Select((squadTag, index) => (
            Tag: squadTag,
            X: rendezvousAnchor.X + rendezvousOffsets[index].X,
            Y: rendezvousAnchor.Y + rendezvousOffsets[index].Y)).ToArray();

        var squadDecisions = decisions
            .Where(decision => squadTags.Contains(decision.MarineTag))
            .ToArray();
        var allFourAssembled = _squadDispatched || (squadTags.Length == 4 &&
            squadDecisions.Length == 4 && squadDecisions.All(decision =>
            {
                var marine = observation.Units.FirstOrDefault(unit =>
                    unit.Tag == decision.MarineTag && unit.IsLiveVisible && unit.HasPosition);
                var slot = rendezvousTargets.FirstOrDefault(target => target.Tag == decision.MarineTag);
                if (marine is null || decision.Action != MarineTacticalAction.Hold ||
                    (decision.TargetTag is not null && !decision.IsOutsideLocalDefenseEnvelope))
                    return false;
                var dx = marine.PositionX - slot.X;
                var dy = marine.PositionY - slot.Y;
                return dx * dx + dy * dy <= ArrivalRadiusSquared;
            }));
        if (allFourAssembled)
            _squadDispatched = true;

        var targetsByTag = new Dictionary<ulong, (float X, float Y)>();
        if (!allFourAssembled)
        {
            foreach (var slot in rendezvousTargets)
                targetsByTag[slot.Tag] = (slot.X, slot.Y);
        }
        else if (advanceToEnemyStart)
        {
            // An authored Engage posture turns the assembled squad into an
            // offensive force. SC2 pathfinding owns the route; the combat
            // planner still validates every attack against live observations.
            for (var index = 0; index < squadTags.Length; index++)
                targetsByTag[squadTags[index]] = (map.Side.EnemyStart.X, map.Side.EnemyStart.Y);
        }
        else
        {
            var patrolPhase = (int)((observation.GameLoop / PatrolRotationGameLoops) % patrolPoints.Count);
            for (var index = 0; index < squadTags.Length; index++)
            {
                var patrolPoint = patrolPoints[(index + patrolPhase) % patrolPoints.Count];
                targetsByTag[squadTags[index]] = (patrolPoint.X, patrolPoint.Y);
            }
        }

        foreach (var decision in decisions.Where(item =>
                     targetsByTag.ContainsKey(item.MarineTag) &&
                     item.Action == MarineTacticalAction.Hold &&
                     (item.TargetTag is null || item.IsOutsideLocalDefenseEnvelope)))
        {
            var marine = observation.Units.FirstOrDefault(unit =>
                unit.Tag == decision.MarineTag && liveMarineTags.Contains(unit.Tag) && unit.HasPosition);
            if (marine is null)
                continue;

            var destination = targetsByTag[marine.Tag];
            var finalDx = marine.PositionX - destination.X;
            var finalDy = marine.PositionY - destination.Y;
            if (finalDx * finalDx + finalDy * finalDy <= ArrivalRadiusSquared)
            {
                _lastRallyCommandLoops.Remove(marine.Tag);
                _lastRallyTargets.Remove(marine.Tag);
                continue;
            }

            if (_lastRallyTargets.TryGetValue(marine.Tag, out var priorTarget) &&
                DistanceSquared(marine.PositionX, marine.PositionY, priorTarget.X, priorTarget.Y) <= ArrivalRadiusSquared)
            {
                _lastRallyCommandLoops.Remove(marine.Tag);
                _lastRallyTargets.Remove(marine.Tag);
            }

            if (_lastRallyCommandLoops.TryGetValue(marine.Tag, out var lastCommand) &&
                observation.GameLoop < lastCommand + RetryAfterGameLoops)
                continue;

            var waypoint = StepToward(
                marine.PositionX, marine.PositionY, destination.X, destination.Y, 4f);
            _lastRallyCommandLoops[marine.Tag] = observation.GameLoop;
            _lastRallyTargets[marine.Tag] = waypoint;
            actions.Add(MoveCommand(marine.Tag, checked((int)gameData.MoveAbilityId.Value), waypoint));
        }

        scoutActions.AddRange(actions);
        return scoutActions;
    }

    private void EnsureScoutPlan(
        SpatialMap map,
        SpatialTerrain terrain,
        SpatialTopology topology,
        UnitObservationFacts scout)
    {
        if (_scoutPlanAttempted)
            return;

        _scoutPlanAttempted = true;
        var origin = new MapPoint(scout.PositionX, scout.PositionY);
        if (terrain is not null && topology is not null &&
            Sc2RampScoutPlanner.TryFindObservationPoint(map, terrain, topology, origin, out var plan))
        {
            _scoutPlan = plan;
            _scoutTarget = plan.ObservationPoint;
            SetScoutStatus("RampFootPlanned",
                $"target={plan.ObservationPoint.X:F1},{plan.ObservationPoint.Y:F1};" +
                $"edge={plan.RampEdgePoint.X:F1},{plan.RampEdgePoint.Y:F1};" +
                $"terrain={plan.TerrainHeight};enemyDistance={plan.DistanceFromEnemyStart:F1};" +
                $"crossTrack={plan.CrossTrackDistance:F1};connected=true");
            return;
        }

        if (terrain is null || topology is null)
        {
            _scoutTarget = FindFriendlyObservationSector(map, terrain);
            SetScoutStatus("FriendlyObservationFallback",
                _scoutTarget is MapPoint fallback
                    ? $"terrainOrTopologyUnavailable;target={fallback.X:F1},{fallback.Y:F1}"
                    : "terrainOrTopologyUnavailable;noFriendlyObservationSector");
            return;
        }

        SetScoutStatus("RampFootUnavailable",
            "No pathable low-ground ramp foot is connected to the scout's current pathing region");
    }

    private static MapPoint? FindSafeRetreatPoint(
        IReadOnlyList<MapPoint> patrolPoints,
        SpatialMap map,
        SpatialTerrain terrain,
        SpatialTopology topology,
        MapPoint origin,
        IReadOnlyList<UnitObservationFacts> threats) =>
        patrolPoints
            .Where(point => map.Side.Assess(point) == MapSide.Friendly)
            .Where(point => IsPathable(point, terrain))
            .Where(point => topology is null || topology.AreConnected(origin, point))
            .Where(point => threats.All(threat =>
                DistanceSquared(point.X, point.Y, threat.PositionX, threat.PositionY) >=
                RetreatThreatSeparationSquared))
            .OrderBy(point => DistanceSquared(origin.X, origin.Y, point.X, point.Y))
            .ThenBy(point => point.DistanceSquaredTo(map.Side.FriendlyStart))
            .Select(point => (MapPoint?)point)
            .FirstOrDefault();

    private void SetScoutStatus(string status, string details)
    {
        if (_scoutStatus == status && _scoutDetails == details)
            return;
        _scoutStatus = status;
        _scoutDetails = details;
        _scoutStatusChanged = true;
    }

    private static bool IsEnemyTownHall(string unitKind) =>
        unitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Nexus", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Hatchery", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Lair", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Hive", StringComparison.OrdinalIgnoreCase);

    private static MapPoint? FindFriendlyObservationSector(SpatialMap map, SpatialTerrain terrain)
    {
        var homeSector = map.GetSector(map.Side.FriendlyStart);
        return map.GetNeighbors(homeSector.Id)
            .Select(sector => sector.Center)
            .Where(map.Bounds.Contains)
            .Where(point => map.Side.Assess(point) != MapSide.Enemy)
            .Where(point => IsPathable(point, terrain))
            .OrderByDescending(point => point.DistanceSquaredTo(map.Side.FriendlyStart))
            .ThenBy(point => point.X)
            .ThenBy(point => point.Y)
            .Select(point => (MapPoint?)point)
            .FirstOrDefault();
    }

    public static IReadOnlyList<MapPoint> GetFriendlyPatrolWaypoints(
        SpatialMap map,
        SpatialTerrain terrain = null,
        SpatialTopology topology = null,
        MapPoint? origin = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        var scoutPoint = FindFriendlyObservationSector(map, terrain);
        var candidates = new List<MapPoint>();

        foreach (var sector in map.Sectors)
        {
            var insetX = MathF.Min(4f, sector.Bounds.Width * 0.15f);
            var insetY = MathF.Min(4f, sector.Bounds.Height * 0.15f);
            var minX = sector.Bounds.MinX + insetX;
            var maxX = sector.Bounds.MaxX - insetX;
            var minY = sector.Bounds.MinY + insetY;
            var maxY = sector.Bounds.MaxY - insetY;
            var xSamples = new[] { minX, (minX + maxX) * 0.5f, maxX };
            var ySamples = new[] { minY, (minY + maxY) * 0.5f, maxY };

            foreach (var y in ySamples)
            foreach (var x in xSamples)
            {
                var point = new MapPoint(x, y);
                if (!map.Bounds.Contains(point) || map.Side.Assess(point) != MapSide.Friendly ||
                    !IsPathable(point, terrain))
                    continue;
                if (topology is not null && origin is MapPoint originPoint &&
                    !topology.AreConnected(originPoint, point))
                    continue;
                if (point.DistanceSquaredTo(map.Side.FriendlyStart) < 100f)
                    continue;
                if (scoutPoint is MapPoint reservedPoint && point.DistanceSquaredTo(reservedPoint) < 64f)
                    continue;
                if (candidates.Any(existing => existing.DistanceSquaredTo(point) < 64f))
                    continue;

                candidates.Add(point);
            }
        }

        return candidates
            .OrderBy(point => MathF.Atan2(
                point.Y - map.Side.FriendlyStart.Y,
                point.X - map.Side.FriendlyStart.X))
            .ThenBy(point => point.DistanceSquaredTo(map.Side.FriendlyStart))
            .ToArray();
    }

    private static bool IsPathable(MapPoint point, SpatialTerrain terrain)
    {
        if (terrain is null)
            return true;
        return terrain.TryIsPathable(point, out var pathable) && pathable;
    }

    private static SC2APIProtocol.Action MoveCommand(ulong tag, int abilityId, MapPoint target) =>
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

    private static MapPoint StepToward(float x, float y, float targetX, float targetY, float maxDistance)
    {
        var dx = targetX - x;
        var dy = targetY - y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance <= maxDistance || distance <= 0.001f)
            return new MapPoint(targetX, targetY);
        var scale = maxDistance / distance;
        return new MapPoint(x + dx * scale, y + dy * scale);
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
