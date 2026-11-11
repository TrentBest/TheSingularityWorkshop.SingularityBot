using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Sends Marines without a tactical target across the map to an enemy-side
/// reconnaissance waypoint near the opponent's natural approach.
/// </summary>
public sealed class Sc2MarineRallyController
{
    private readonly Dictionary<ulong, uint> _lastRallyCommandLoops = new();
    private const uint RetryAfterGameLoops = 44;
    private const float RallyOffset = 8f;
    private const float ArrivalRadiusSquared = 4f;
    private const float RendezvousOffset = 5f;
    private bool _squadDispatched;

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
        if (!map.Bounds.Contains(rallyPoint) || map.Side.Assess(rallyPoint) != MapSide.Enemy)
            return Array.Empty<SC2APIProtocol.Action>();

        var actions = new List<SC2APIProtocol.Action>();
        var liveMarineTags = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .Select(unit => unit.Tag)
            .ToHashSet();

        foreach (var retired in _lastRallyCommandLoops.Keys.Where(tag => !liveMarineTags.Contains(tag)).ToArray())
            _lastRallyCommandLoops.Remove(retired);

        if (liveMarineTags.Count == 0)
            _squadDispatched = false;

        // Marine four entering production starts a real rendezvous at the
        // producing Barracks. Early Marines occupy distinct box slots around it;
        // the squad does not begin its cross-map advance until all four have
        // reached their assigned slots.
        var squadTags = liveMarineTags.OrderBy(tag => tag).Take(4).ToArray();
        if (squadTags.Length < 4 && !fourthMarineInProduction)
            return Array.Empty<SC2APIProtocol.Action>();

        var producingBarracks = observation.Units.FirstOrDefault(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible && unit.HasPosition &&
            unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
            unit.Orders.Any(order => order.AbilityId == gameData.MarineTrainAbilityId));
        var rendezvousAnchor = producingBarracks is null
            ? map.Side.FriendlyStart
            : new MapPoint(producingBarracks.PositionX, producingBarracks.PositionY);
        var rendezvousOffsets = MarineFormationPlanner.GetOffsets(
            MarineFormation.Box, squadTags.Length, direction.X, direction.Y);
        var rendezvousTargets = squadTags.Select((tag, index) => (
            Tag: tag,
            X: rendezvousAnchor.X + rendezvousOffsets[index].X,
            Y: rendezvousAnchor.Y + rendezvousOffsets[index].Y)).ToArray();

        var allFourAssembled = _squadDispatched || (squadTags.Length == 4 && decisions
            .Where(decision => squadTags.Contains(decision.MarineTag))
            .All(decision =>
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
            var dx = marine.PositionX - destination.X;
            var dy = marine.PositionY - destination.Y;
            if (dx * dx + dy * dy <= ArrivalRadiusSquared)
            {
                _lastRallyCommandLoops.Remove(marine.Tag);
                continue;
            }

            if (_lastRallyCommandLoops.TryGetValue(marine.Tag, out var lastCommand) &&
                observation.GameLoop < lastCommand + RetryAfterGameLoops)
                continue;

            _lastRallyCommandLoops[marine.Tag] = observation.GameLoop;
            actions.Add(new SC2APIProtocol.Action
            {
                ActionRaw = new ActionRaw
                {
                    UnitCommand = new ActionRawUnitCommand
                    {
                        AbilityId = checked((int)gameData.MoveAbilityId.Value),
                        UnitTags = { marine.Tag },
                        TargetWorldSpacePos = new Point2D { X = destination.X, Y = destination.Y },
                        QueueCommand = false
                    }
                }
            });
        }

        return actions;
    }
}
