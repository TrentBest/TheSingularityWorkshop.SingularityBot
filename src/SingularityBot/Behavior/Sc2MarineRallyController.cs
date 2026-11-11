using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Sends an assembled Marine force without a tactical target to a conservative
/// enemy-side observation waypoint, then streams later reinforcements into formation.
/// </summary>
public sealed class Sc2MarineRallyController
{
    private readonly Dictionary<ulong, uint> _lastRallyCommandLoops = new();
    private readonly Dictionary<ulong, MapPoint> _lastRallyTargets = new();
    private const uint RetryAfterGameLoops = 44;
    private const float RallyOffset = 30f;
    private const float ArrivalRadiusSquared = 4f;
    private const float RendezvousOffset = 5f;
    private bool _squadDispatched;
    private MapPoint? _rendezvousAnchor;
    private ulong? _scoutTag;
    private MapPoint? _scoutTarget;

    public IReadOnlyList<SC2APIProtocol.Action> TryCreateActions(
        NormalizedObservation observation,
        SpatialMap map,
        IReadOnlyList<MarineTacticalDecision> decisions,
        Sc2GameDataCatalog gameData,
        bool fourthMarineInProduction = false)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(gameData);
        if (map is null || decisions.Count == 0 || gameData.MoveAbilityId is not > 0)
            return Array.Empty<SC2APIProtocol.Action>();

        var direction = (map.Side.EnemyStart - map.Side.FriendlyStart).Normalize();
        var rallyPoint = map.Side.EnemyStart - direction * RallyOffset;

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

        if (liveMarineTags.Count == 0)
        {
            _squadDispatched = false;
            _rendezvousAnchor = null;
            _scoutTag = null;
            _scoutTarget = null;
        }

        if (_scoutTag is ulong existingScout && !liveMarineTags.Contains(existingScout))
        {
            _scoutTag = null;
            _scoutTarget = null;
        }

        _scoutTag ??= liveMarineTags.OrderBy(tag => tag).Select(tag => (ulong?)tag).FirstOrDefault();
        var scoutActions = new List<SC2APIProtocol.Action>();
        if (_scoutTag is ulong scoutTag)
        {
            _scoutTarget ??= FindFriendlyObservationSector(map);
            var hasScoutDecision = decisions.Any(item => item.MarineTag == scoutTag);
            var scoutDecision = decisions.FirstOrDefault(item => item.MarineTag == scoutTag);
            var scout = observation.Units.FirstOrDefault(unit =>
                unit.Tag == scoutTag && unit.IsLiveVisible && unit.HasPosition);
            if (_scoutTarget is MapPoint scoutTarget && scout is not null && hasScoutDecision &&
                scoutDecision.Action == MarineTacticalAction.Hold && scoutDecision.TargetTag is null)
            {
                var scoutDistanceSquared = DistanceSquared(
                    scout.PositionX, scout.PositionY, scoutTarget.X, scoutTarget.Y);
                if (scoutDistanceSquared > ArrivalRadiusSquared &&
                    (!_lastRallyCommandLoops.TryGetValue(scoutTag, out var scoutLastCommand) ||
                     observation.GameLoop >= scoutLastCommand + RetryAfterGameLoops))
                {
                    _lastRallyCommandLoops[scoutTag] = observation.GameLoop;
                    scoutActions.Add(MoveCommand(scoutTag, checked((int)gameData.MoveAbilityId.Value), scoutTarget));
                }
            }
        }

        // The first Marine is a dedicated early-warning observer. It moves to
        // the farthest non-enemy sector adjacent to the friendly start sector.
        // The remaining four Marines form the first mass-pressure squad.
        // Marine four entering production starts a real rendezvous at the
        // producing Barracks. Early Marines occupy distinct box slots around it;
        // the squad does not begin its cross-map advance until all four have
        // reached their assigned slots.
        if (!map.Bounds.Contains(rallyPoint) || map.Side.Assess(rallyPoint) != MapSide.Enemy)
            return scoutActions;

        var squadTags = liveMarineTags
            .Where(tag => tag != _scoutTag)
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
        var rendezvousTargets = squadTags.Select((tag, index) => (
            Tag: tag,
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
                if (marine is null || decision.Action != MarineTacticalAction.Hold || decision.TargetTag is not null)
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
        else
        {
            var formationOffsets = MarineFormationPlanner.GetOffsets(
                MarineFormation.Box, squadTags.Length, direction.X, direction.Y);
            for (var index = 0; index < squadTags.Length; index++)
                targetsByTag[squadTags[index]] = (
                    rallyPoint.X + formationOffsets[index].X,
                    rallyPoint.Y + formationOffsets[index].Y);
        }

        foreach (var decision in decisions.Where(item =>
                     targetsByTag.ContainsKey(item.MarineTag) &&
                     item.Action == MarineTacticalAction.Hold && item.TargetTag is null))
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

            // Formation members advance through short local waypoints rather than
            // asking SC2 to pathfind the entire cross-map route in one command.
            // This keeps each Marine close to its assigned slot and reduces the
            // tendency for trailing units to route around the Marine ahead.
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
            actions.Add(new SC2APIProtocol.Action
            {
                ActionRaw = new ActionRaw
                {
                    UnitCommand = new ActionRawUnitCommand
                    {
                        AbilityId = checked((int)gameData.MoveAbilityId.Value),
                        UnitTags = { marine.Tag },
                        TargetWorldSpacePos = new Point2D { X = waypoint.X, Y = waypoint.Y },
                        QueueCommand = false
                    }
                }
            });
        }

        scoutActions.AddRange(actions);
        return scoutActions;
    }

    private static MapPoint? FindFriendlyObservationSector(SpatialMap map)
    {
        var homeSector = map.GetSector(map.Side.FriendlyStart);
        return map.GetNeighbors(homeSector.Id)
            .Select(sector => sector.Center)
            .Where(map.Bounds.Contains)
            .Where(point => map.Side.Assess(point) != MapSide.Enemy)
            .OrderByDescending(point => point.DistanceSquaredTo(map.Side.FriendlyStart))
            .ThenBy(point => point.X)
            .ThenBy(point => point.Y)
            .Select(point => (MapPoint?)point)
            .FirstOrDefault();
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
