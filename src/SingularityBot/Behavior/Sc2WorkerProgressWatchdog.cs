using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Detects SCVs whose active Move orders stop producing positional progress.
/// An order is not proof of movement: stalled workers are re-issued their
/// observed destination, while gathering and construction orders are untouched.
/// </summary>
public sealed class Sc2WorkerProgressWatchdog
{
    private const uint StalledMoveGameLoops = 22;
    private const float ProgressDistanceSquared = 0.5625f;
    private const float ArrivalDistanceSquared = 2.25f;

    private sealed record ProgressState(
        float X,
        float Y,
        float TargetX,
        float TargetY,
        uint LastProgressGameLoop);

    private readonly Dictionary<ulong, ProgressState> _states = new();

    public IReadOnlyList<SC2APIProtocol.Action> TryCreateActions(
        NormalizedObservation observation,
        uint? moveAbilityId,
        IReadOnlySet<ulong>? alreadyCommandedTags = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (moveAbilityId is not uint moveId || moveId == 0 || moveId > int.MaxValue)
            return Array.Empty<SC2APIProtocol.Action>();

        var liveScvs = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && unit.HasPosition &&
                unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .ToArray();
        var liveTags = liveScvs.Select(unit => unit.Tag).ToHashSet();
        foreach (var retired in _states.Keys.Where(tag => !liveTags.Contains(tag)).ToArray())
            _states.Remove(retired);

        var actions = new List<SC2APIProtocol.Action>();
        foreach (var scv in liveScvs.OrderBy(unit => unit.Tag))
        {
            if (alreadyCommandedTags?.Contains(scv.Tag) == true)
                continue;

            var moveOrder = scv.Orders.FirstOrDefault(order =>
                order.AbilityId == moveId &&
                order.TargetPositionX.HasValue &&
                order.TargetPositionY.HasValue);
            if (moveOrder is null)
            {
                _states.Remove(scv.Tag);
                continue;
            }

            var targetX = moveOrder.TargetPositionX!.Value;
            var targetY = moveOrder.TargetPositionY!.Value;
            var remainingDistanceSquared = DistanceSquared(
                scv.PositionX, scv.PositionY, targetX, targetY);
            if (remainingDistanceSquared <= ArrivalDistanceSquared)
            {
                _states.Remove(scv.Tag);
                continue;
            }

            if (!_states.TryGetValue(scv.Tag, out var state) ||
                DistanceSquared(state.TargetX, state.TargetY, targetX, targetY) > 0.01f)
            {
                _states[scv.Tag] = new ProgressState(
                    scv.PositionX, scv.PositionY, targetX, targetY, observation.GameLoop);
                continue;
            }

            var movedSquared = DistanceSquared(
                scv.PositionX, scv.PositionY, state.X, state.Y);
            if (movedSquared >= ProgressDistanceSquared)
            {
                _states[scv.Tag] = state with
                {
                    X = scv.PositionX,
                    Y = scv.PositionY,
                    LastProgressGameLoop = observation.GameLoop
                };
                continue;
            }

            if (observation.GameLoop < state.LastProgressGameLoop + StalledMoveGameLoops)
                continue;

            Console.WriteLine(
                $"[WorkerWatchdog] SCV {scv.Tag} made no positional progress toward " +
                $"({targetX:F1},{targetY:F1}) for {StalledMoveGameLoops} game loops; reissuing Move.");

            _states[scv.Tag] = state with
            {
                X = scv.PositionX,
                Y = scv.PositionY,
                LastProgressGameLoop = observation.GameLoop
            };
            actions.Add(new SC2APIProtocol.Action
            {
                ActionRaw = new ActionRaw
                {
                    UnitCommand = new ActionRawUnitCommand
                    {
                        AbilityId = checked((int)moveId),
                        UnitTags = { scv.Tag },
                        TargetWorldSpacePos = new Point2D { X = targetX, Y = targetY },
                        QueueCommand = false
                    }
                }
            });
        }

        return actions;
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
