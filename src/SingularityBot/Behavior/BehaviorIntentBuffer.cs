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

    private static int Priority(BehaviorCommand command) => command switch
    {
        BehaviorCommand.RetreatToSafety => 100,
        BehaviorCommand.RepairAssignedTarget => 90,
        BehaviorCommand.BuildRequestedStructure => 80,
        BehaviorCommand.ReturnCarriedResources => 70,
        BehaviorCommand.DefendWorkerLine => 60,
        BehaviorCommand.AttackTarget => 58,
        BehaviorCommand.AttackVisibleThreat => 56,
        BehaviorCommand.FlankLeftOfTarget or BehaviorCommand.FlankRightOfTarget => 54,
        BehaviorCommand.MoveAwayFromTarget => 53,
        BehaviorCommand.FollowTarget => 52,
        BehaviorCommand.PatrolRoute => 51,
        BehaviorCommand.MoveTowardsTarget => 50,
        BehaviorCommand.RegroupWithArmy => 49,
        BehaviorCommand.AdvanceToObjective => 40,
        BehaviorCommand.AssignWorkerToMinerals => 10,
        _ => 0
    };
}
