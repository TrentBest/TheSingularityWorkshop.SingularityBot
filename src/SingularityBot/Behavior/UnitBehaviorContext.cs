using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Mutable observation-derived context owned by one unit's behavior FSM.
/// The context deliberately contains no SC2 protocol types.
/// </summary>
public sealed class UnitBehaviorContext : IStateContext
{
    private readonly Action<BehaviorCommand> _emit;

    public UnitBehaviorContext(
        ulong unitTag,
        string name,
        string unitKind,
        bool isWorker,
        Action<BehaviorCommand> emit)
    {
        if (unitTag == 0) throw new ArgumentOutOfRangeException(nameof(unitTag));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A context name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(unitKind)) throw new ArgumentException("A unit kind is required.", nameof(unitKind));

        UnitTag = unitTag;
        Name = name;
        UnitKind = unitKind;
        IsWorker = isWorker;
        _emit = emit ?? throw new ArgumentNullException(nameof(emit));
        IsValid = true;
    }

    public ulong UnitTag { get; }
    public string Name { get; set; }
    public string UnitKind { get; }
    public bool IsWorker { get; }
    public bool IsValid { get; set; }

    // These facts are refreshed from the latest observation before each FSM tick.
    public bool HasCargo { get; set; }
    public bool HasAssignedWork { get; set; }
    public bool HasVisibleThreat { get; set; }
    public bool ShouldRetreat { get; set; }
    public bool HasObjective { get; set; }
    public bool IsAtObjective { get; set; }
    public bool ShouldRegroup { get; set; }
    public bool BuildOrderAvailable { get; set; }

    public void Emit(BehaviorCommand command)
    {
        if (command != BehaviorCommand.None)
            _emit(command);
    }
}
