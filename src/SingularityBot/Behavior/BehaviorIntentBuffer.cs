namespace SingularityBot.Behavior;

/// <summary>
/// Deterministic per-observation command arbitration. One command per unit is
/// retained; capability priority, not FSM update order, chooses the winner.
/// </summary>
public sealed class BehaviorIntentBuffer
{
    private readonly SortedDictionary<ulong, BehaviorCommand> _commands = new();
    public int Count => _commands.Count;

    public void Add(ulong unitTag, BehaviorCommand command)
    {
        if (unitTag == 0 || command == BehaviorCommand.None) return;
        if (!_commands.TryGetValue(unitTag, out var current) || Priority(command) > Priority(current))
            _commands[unitTag] = command;
    }

    public IReadOnlyList<KeyValuePair<ulong, BehaviorCommand>> Drain()
    {
        var result = _commands.ToArray();
        _commands.Clear();
        return result;
    }

    /// <summary>
    /// Produces typed, position-bearing intents from the winning commands and
    /// the same observation's persistent unit facts. This is an adapter boundary:
    /// it does not guess SC2 ability IDs or issue game actions.
    /// </summary>
    public IReadOnlyList<BehaviorIntent> DrainDetailed(UnitBehaviorRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var commands = Drain();
        var result = new List<BehaviorIntent>(commands.Count);
        foreach (var item in commands)
        {
            runtime.TryGetContext(item.Key, out var unit);
            var targetTag = item.Value switch
            {
                BehaviorCommand.AttackTarget or BehaviorCommand.StutterStepMove => unit?.AttackTargetTag,
                BehaviorCommand.FollowTarget => unit?.FollowTargetTag,
                BehaviorCommand.MoveTowardsTarget => unit?.MoveTowardsTargetTag,
                BehaviorCommand.MoveAwayFromTarget => unit?.MoveAwayFromTargetTag,
                BehaviorCommand.FlankLeftOfTarget or BehaviorCommand.FlankRightOfTarget => unit?.FlankTargetTag,
                BehaviorCommand.RepairAssignedTarget => unit?.RepairTargetTag,
                _ => null
            };
            var hasDestination = unit is not null &&
                (item.Value == BehaviorCommand.ScoutSector || item.Value == BehaviorCommand.ContainSector) &&
                unit.AssignedSector.HasValue;
            result.Add(new BehaviorIntent(
                item.Key, item.Value, targetTag,
                hasDestination ? unit.SectorCenterX : null,
                hasDestination ? unit.SectorCenterY : null,
                hasDestination ? unit.AssignedSector : null));
        }
        return result;
    }

    private static int Priority(BehaviorCommand command) => command switch
    {
        BehaviorCommand.RetreatToSafety => 100,
        BehaviorCommand.FallbackToRally => 95,
        BehaviorCommand.StutterStepMove => 85,
        BehaviorCommand.RepairAssignedTarget => 90,
        BehaviorCommand.BuildRequestedStructure => 80,
        BehaviorCommand.ReturnCarriedResources => 70,
        BehaviorCommand.DefendWorkerLine => 60,
        BehaviorCommand.AttackTarget => 58,
        BehaviorCommand.EngageObjective => 57,
        BehaviorCommand.AttackVisibleThreat => 56,
        BehaviorCommand.FlankLeftOfTarget or BehaviorCommand.FlankRightOfTarget => 54,
        BehaviorCommand.MoveAwayFromTarget => 53,
        BehaviorCommand.FollowTarget => 52,
        BehaviorCommand.PatrolRoute => 51,
        BehaviorCommand.MoveTowardsTarget => 50,
        BehaviorCommand.RegroupWithArmy => 49,
        BehaviorCommand.ScoutSector => 48,
        BehaviorCommand.ContainSector => 47,
        BehaviorCommand.HoldRallyPoint => 46,
        BehaviorCommand.AdvanceToObjective => 40,
        BehaviorCommand.AssignWorkerToMinerals => 10,
        _ => 0
    };
}
