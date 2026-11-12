using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>Translates stable scouting assignments into individual move orders.</summary>
public static class Sc2ScoutingActionTranslator
{
    public static IReadOnlyList<SC2APIProtocol.Action> Translate(
        IReadOnlyDictionary<ulong, MapPoint> assignments,
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        if (gameData.MoveAbilityId is not > 0)
            return Array.Empty<SC2APIProtocol.Action>();

        var units = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.HasPosition && !observation.DeadUnitTags.Contains(unit.Tag))
            .ToDictionary(unit => unit.Tag);
        var actions = new List<SC2APIProtocol.Action>();
        var moveId = checked((int)gameData.MoveAbilityId.Value);
        foreach (var assignment in assignments.OrderBy(item => item.Key))
        {
            if (!units.TryGetValue(assignment.Key, out var unit) ||
                unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase))
                continue;

            var target = assignment.Value;
            if (unit.Orders.Any(order => order.AbilityId == moveId &&
                order.TargetPositionX.HasValue && order.TargetPositionY.HasValue &&
                DistanceSquared(order.TargetPositionX.Value, order.TargetPositionY.Value,
                    target.X, target.Y) <= 9f))
                continue;

            actions.Add(new SC2APIProtocol.Action
            {
                ActionRaw = new ActionRaw
                {
                    UnitCommand = new ActionRawUnitCommand
                    {
                        AbilityId = moveId,
                        UnitTags = { unit.Tag },
                        TargetWorldSpacePos = new Point2D { X = target.X, Y = target.Y },
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
