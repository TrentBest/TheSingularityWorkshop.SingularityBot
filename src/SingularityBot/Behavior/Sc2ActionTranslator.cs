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
        uint? smartAbilityId,
        IReadOnlyDictionary<ulong, WorkerResourceAssignment> workerAssignments = null)
    {
        ArgumentNullException.ThrowIfNull(intents);
        ArgumentNullException.ThrowIfNull(observation);

        var visibleSelf = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                           unit.IsLiveVisible &&
                           unit.HasPosition &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .ToDictionary(unit => unit.Tag);

        var townHalls = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                           unit.IsLiveVisible &&
                           unit.HasPosition &&
                           IsTownHall(unit.UnitKind) &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        var mineralFields = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Neutral &&
                           unit.IsLiveVisible &&
                           unit.HasPosition &&
                           unit.UnitKind.Contains("MineralField", StringComparison.OrdinalIgnoreCase) &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .ToDictionary(unit => unit.Tag);

        var actions = new List<SC2APIProtocol.Action>();
        var commanded = new HashSet<ulong>();

        foreach (var intent in intents.OrderBy(item => item.Key))
        {
            if (!commanded.Add(intent.Key) ||
                !visibleSelf.TryGetValue(intent.Key, out var worker) ||
                !IsWorker(worker.UnitKind))
                continue;

            switch (intent.Value)
            {
                case BehaviorCommand.AssignWorkerToMinerals:
                {
                    if (!TryGetAbilityId(smartAbilityId, out var smartId) ||
                        worker.HasCargo ||
                        workerAssignments is null ||
                        !workerAssignments.TryGetValue(worker.Tag, out var assignment) ||
                        !mineralFields.ContainsKey(assignment.MineralTag))
                        continue;

                    actions.Add(CreateUnitCommand(worker.Tag, smartId, assignment.MineralTag));
                    break;
                }
                case BehaviorCommand.ReturnCarriedResources:
                {
                    if (!TryGetAbilityId(smartAbilityId, out var smartId) ||
                        !worker.HasCargo || townHalls.Length == 0)
                        continue;

                    var townHall = townHalls
                        .OrderBy(field => DistanceSquared(worker, field))
                        .ThenBy(field => field.Tag)
                        .First();
                    actions.Add(CreateUnitCommand(worker.Tag, smartId, townHall.Tag));
                    break;
                }
            }
        }

        return actions;
    }

    private static bool TryGetAbilityId(uint? value, out int abilityId)
    {
        if (value is > 0 and <= int.MaxValue)
        {
            abilityId = (int)value.Value;
            return true;
        }

        abilityId = 0;
        return false;
    }

    private static SC2APIProtocol.Action CreateUnitCommand(ulong workerTag, int abilityId, ulong targetTag) =>
        new()
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = abilityId,
                    TargetUnitTag = targetTag,
                    UnitTags = { workerTag },
                    QueueCommand = false
                }
            }
        };

    private static bool IsWorker(string unitKind) =>
        unitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Probe", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Drone", StringComparison.OrdinalIgnoreCase);

    private static double DistanceSquared(UnitObservationFacts left, UnitObservationFacts right)
    {
        var dx = left.PositionX - right.PositionX;
        var dy = left.PositionY - right.PositionY;
        return (double)dx * dx + (double)dy * dy;
    }

    private static bool IsTownHall(string unitKind) =>
        unitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Nexus", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Hatchery", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Lair", StringComparison.OrdinalIgnoreCase) ||
        unitKind.Equals("Hive", StringComparison.OrdinalIgnoreCase);

}
