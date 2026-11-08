namespace SingularityBot.Behavior;

/// <summary>
/// Deterministic, per-observation command arbitration. Each unit receives at
/// most one command; capability priority, not FSM update order, decides which
/// intent survives.
/// </summary>
public sealed class BehaviorIntentBuffer
{
    private readonly SortedDictionary<ulong, BehaviorCommand> _commands = new();

    public int Count => _commands.Count;

    public void Add(ulong unitTag, BehaviorCommand command)
    {
        if (unitTag == 0 || command == BehaviorCommand.None)
            return;

        if (!_commands.TryGetValue(unitTag, out var current) ||
            Priority(command) > Priority(current))
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
        // Survival and high-value support actions supersede routine work.
        BehaviorCommand.RetreatToSafety => 100,
        BehaviorCommand.RepairAssignedTarget => 90,
        BehaviorCommand.BuildRequestedStructure => 80,

        // Finish the current economic cycle before issuing a new gather order.
        BehaviorCommand.ReturnCarriedResources => 70,
        BehaviorCommand.DefendWorkerLine => 60,
        BehaviorCommand.AttackVisibleThreat => 55,
        BehaviorCommand.RegroupWithArmy => 50,
        BehaviorCommand.AdvanceToObjective => 40,
        BehaviorCommand.AssignWorkerToMinerals => 10,
        _ => 0
    };
}
