namespace SingularityBot.Behavior;

/// <summary>Deterministic, per-observation collection of commands emitted by unit FSMs.</summary>
public sealed class BehaviorIntentBuffer
{
    private readonly SortedDictionary<ulong, BehaviorCommand> _commands = new();

    public int Count => _commands.Count;

    public void Add(ulong unitTag, BehaviorCommand command)
    {
        if (unitTag == 0 || command == BehaviorCommand.None)
            return;

        // One authoritative command per unit per observation. A later FSM emission
        // replaces an earlier one, preserving the FSM's transition priority.
        _commands[unitTag] = command;
    }

    public IReadOnlyList<KeyValuePair<ulong, BehaviorCommand>> Drain()
    {
        var result = _commands.ToArray();
        _commands.Clear();
        return result;
    }
}
