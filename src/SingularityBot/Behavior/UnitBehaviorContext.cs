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
    public IReadOnlyList<UnitOrderObservationFacts> ObservedOrders { get; private set; } = Array.Empty<UnitOrderObservationFacts>();
    public UnitOrderObservationFacts? CurrentOrder => ObservedOrders.Count == 0 ? null : ObservedOrders[0];

    // The economy planner owns reservation identity; the worker FSM context
    // owns the observed execution phase for that reservation.
    public ulong? AssignedMineralTag { get; private set; }
    public ulong? AssignedTownHallTag { get; private set; }
    public uint WorkerAssignmentGeneration { get; private set; }
    public WorkerRoutePhase WorkerRoutePhase { get; private set; }
    public uint WorkerRoutePhaseChangedAtGameLoop { get; private set; }
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
        ObservedOrders = facts.Orders;
        IsFlying = facts.IsFlying;
        IsBurrowed = facts.IsBurrowed;
        IsHallucination = facts.IsHallucination;
        IsValid = true;
    }

    public void ApplyWorkerAssignment(WorkerResourceAssignment? assignment, uint gameLoop)
    {
        if (assignment is null)
        {
            AssignedMineralTag = null;
            AssignedTownHallTag = null;
            WorkerAssignmentGeneration = 0;
            SetRoutePhase(WorkerRoutePhase.Unassigned, gameLoop);
            return;
        }

        var changed = WorkerAssignmentGeneration != assignment.Generation ||
                      AssignedMineralTag != assignment.MineralTag ||
                      AssignedTownHallTag != assignment.TownHallTag;

        AssignedMineralTag = assignment.MineralTag;
        AssignedTownHallTag = assignment.TownHallTag;
        WorkerAssignmentGeneration = assignment.Generation;

        // Phase is observation-derived, never inferred from an issued command.
        var orderTarget = CurrentOrder?.TargetUnitTag;
        var observedPhase = HasCargo
            ? orderTarget == AssignedTownHallTag ? WorkerRoutePhase.Returning : WorkerRoutePhase.CarryingResources
            : orderTarget == AssignedMineralTag ? WorkerRoutePhase.Outbound
            : HasAssignedWork ? WorkerRoutePhase.ExecutingOrder
            : WorkerRoutePhase.Assigned;

        SetRoutePhase(changed ? WorkerRoutePhase.Assigned : observedPhase, gameLoop, forceTimestamp: changed);
    }

    private void SetRoutePhase(WorkerRoutePhase phase, uint gameLoop, bool forceTimestamp = false)
    {
        if (WorkerRoutePhase == phase && !forceTimestamp)
            return;

        WorkerRoutePhase = phase;
        WorkerRoutePhaseChangedAtGameLoop = gameLoop;
    }

    public void Emit(BehaviorCommand command)
    {
        if (command != BehaviorCommand.None)
            _emit(command);
    }
}


/// <summary>Observed lifecycle phase of a worker's persistent resource route.</summary>
public enum WorkerRoutePhase
{
    Unassigned,
    Assigned,
    ExecutingOrder,
    CarryingResources,
    Outbound,
    Returning
}
