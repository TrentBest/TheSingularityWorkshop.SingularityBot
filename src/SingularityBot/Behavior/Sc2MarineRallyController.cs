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

        // As soon as Marine four is training, the first three leave local scouting
        // and converge on a safe rendezvous near the friendly start. Once four are
        // alive, the squad moves in a persistent 2x2 box rather than four unrelated
        // point orders. Formation slots are derived from the travel direction.
        var squadTags = liveMarineTags.OrderBy(tag => tag).Take(4).ToArray();
        var rendezvousStarted = fourthMarineInProduction && squadTags.Length < 4;
        if (squadTags.Length < 4 && !rendezvousStarted)
            return Array.Empty<SC2APIProtocol.Action>();

        var rendezvousPoint = map.Side.FriendlyStart + direction * RendezvousOffset;
        var formationPoint = rallyPoint;
        var formationOffsets = MarineFormationPlanner.GetOffsets(MarineFormation.Box, squadTags.Length, direction.X, direction.Y);
        var targetsByTag = new Dictionary<ulong, (float X, float Y)>();
        if (rendezvousStarted)
        {
            foreach (var tag in squadTags)
                targetsByTag[tag] = (rendezvousPoint.X, rendezvousPoint.Y);
        }
        else
        {
            for (var index = 0; index < squadTags.Length; index++)
                targetsByTag[squadTags[index]] = (formationPoint.X + formationOffsets[index].X,
                    formationPoint.Y + formationOffsets[index].Y);
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
