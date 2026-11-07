using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Converts a deliberately small set of domain intents into SC2 raw actions.
/// All unit and target checks are performed against the current normalized frame.
/// </summary>
public static class Sc2ActionTranslator
{
    public static IReadOnlyList<SC2APIProtocol.Action> Translate(
        IReadOnlyList<KeyValuePair<ulong, BehaviorCommand>> intents,
        NormalizedObservation observation,
        uint? harvestGatherAbilityId)
    {
        ArgumentNullException.ThrowIfNull(intents);
        ArgumentNullException.ThrowIfNull(observation);

        if (harvestGatherAbilityId is null || harvestGatherAbilityId == 0)
            return Array.Empty<SC2APIProtocol.Action>();

        var visibleSelf = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                           unit.IsLiveVisible &&
                           unit.HasPosition &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .ToDictionary(unit => unit.Tag);

        var mineralFields = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Neutral &&
                           unit.IsLiveVisible &&
                           unit.HasPosition &&
                           unit.UnitKind.Contains("MineralField", StringComparison.OrdinalIgnoreCase) &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        var actions = new List<SC2APIProtocol.Action>();
        var commanded = new HashSet<ulong>();

        foreach (var intent in intents.OrderBy(item => item.Key))
        {
            if (intent.Value != BehaviorCommand.AssignWorkerToMinerals ||
                !commanded.Add(intent.Key) ||
                !visibleSelf.TryGetValue(intent.Key, out var worker) ||
                !worker.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) ||
                worker.HasCargo ||
                mineralFields.Length == 0)
                continue;

            var target = mineralFields
                .OrderBy(field => DistanceSquared(worker, field))
                .ThenBy(field => field.Tag)
                .First();

            actions.Add(new SC2APIProtocol.Action
            {
                ActionRaw = new ActionRaw
                {
                    UnitCommand = new ActionRawUnitCommand
                    {
                        AbilityId = harvestGatherAbilityId.Value,
                        TargetUnitTag = target.Tag,
                        UnitTags = { worker.Tag },
                        QueueCommand = false
                    }
                }
            });
        }

        return actions;
    }

    private static double DistanceSquared(UnitObservationFacts left, UnitObservationFacts right)
    {
        var dx = left.PositionX - right.PositionX;
        var dy = left.PositionY - right.PositionY;
        return (double)dx * dx + (double)dy * dy;
    }
}
