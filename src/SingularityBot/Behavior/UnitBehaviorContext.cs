using SingularityBot.Observation;
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

    public uint UnitTypeId { get; private set; }
    public uint LastObservedGameLoop { get; private set; }
    public bool IsCurrentlyVisible { get; internal set; }
    public float Health { get; private set; }
    public float HealthMax { get; private set; }
    public float Shield { get; private set; }
    public float ShieldMax { get; private set; }
    public float BuildProgress { get; private set; }
    public float PositionX { get; private set; }
    public float PositionY { get; private set; }
    public bool HasPosition { get; private set; }
    public bool IsFlying { get; private set; }
    public bool IsBurrowed { get; private set; }
    public bool IsHallucination { get; private set; }

    // Strategic facts not yet derivable from the normalized observation remain
    // unset until their owning economy/army layer supplies them.
    public bool HasCargo { get; private set; }
    public bool HasAssignedWork { get; private set; }
    public bool HasVisibleThreat { get; set; }
    public bool ShouldRetreat { get; set; }
    public bool HasObjective { get; set; }
    public bool IsAtObjective { get; set; }
    public bool ShouldRegroup { get; set; }
    public bool BuildOrderAvailable { get; set; }

    public void Apply(UnitObservationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Tag != UnitTag)
            throw new ArgumentException("Observation facts belong to a different unit tag.", nameof(facts));
        if (!facts.IsLiveVisible)
            throw new ArgumentException("Only current visible facts may refresh a live unit context.", nameof(facts));

        UnitTypeId = facts.UnitTypeId;
        LastObservedGameLoop = facts.GameLoop;
        IsCurrentlyVisible = true;
        Health = facts.Health;
        HealthMax = facts.HealthMax;
        Shield = facts.Shield;
        ShieldMax = facts.ShieldMax;
        BuildProgress = facts.BuildProgress;
        PositionX = facts.PositionX;
        PositionY = facts.PositionY;
        HasPosition = facts.HasPosition;
        HasCargo = facts.HasCargo;
        HasAssignedWork = facts.HasOrders;
        IsFlying = facts.IsFlying;
        IsBurrowed = facts.IsBurrowed;
        IsHallucination = facts.IsHallucination;
        IsValid = true;
    }

    public void Emit(BehaviorCommand command)
    {
        if (command != BehaviorCommand.None)
            _emit(command);
    }
}
