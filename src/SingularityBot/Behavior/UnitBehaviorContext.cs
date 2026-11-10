using SingularityBot.Observation;
using SingularityBot.Spatial;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Persistent, protocol-independent facts for one game entity. The context owns
/// all FSM handles attached to that entity and survives temporary observation loss.
/// </summary>
public sealed class UnitBehaviorContext : IStateContext
{
    private readonly Action<BehaviorCommand> _emit;
    private readonly Dictionary<string, FSMHandle> _ownedHandles = new(StringComparer.Ordinal);

    public UnitBehaviorContext(ulong unitTag, string name, string unitKind, bool isWorker, Action<BehaviorCommand> emit)
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
    public uint OwnerId { get; private set; }
    public UnitAlliance Alliance { get; private set; }
    public UnitVisibility CurrentVisibility { get; private set; } = UnitVisibility.Unknown;
    public uint LastObservedGameLoop { get; private set; }
    public uint LastSeenGameLoop { get; private set; }
    public bool IsCurrentlyVisible => CurrentVisibility == UnitVisibility.Visible;
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
    public bool HasCargo { get; private set; }
    public bool HasAssignedWork { get; private set; }
    public IReadOnlyList<UnitOrderObservationFacts> ObservedOrders { get; private set; } = Array.Empty<UnitOrderObservationFacts>();
    public UnitOrderObservationFacts? CurrentOrder => ObservedOrders.Count == 0 ? null : ObservedOrders[0];

    public ulong? AssignedMineralTag { get; private set; }
    public ulong? AssignedTownHallTag { get; private set; }
    public uint WorkerAssignmentGeneration { get; private set; }
    public WorkerRoutePhase WorkerRoutePhase { get; private set; }
    public uint WorkerRoutePhaseChangedAtGameLoop { get; private set; }

    // Behavior requests are caller-owned facts/assignments consumed by independent FSM capabilities.
    public ulong? MoveTowardsTargetTag { get; set; }
    public ulong? MoveAwayFromTargetTag { get; set; }
    public ulong? FollowTargetTag { get; set; }
    public bool HasPatrolRoute { get; set; }
    public ulong? FlankTargetTag { get; set; }
    public bool FlankLeftRequested { get; set; }
    public bool FlankRightRequested { get; set; }
    public ulong? AttackTargetTag { get; set; }
    public GoalActionRole GoalRole { get; set; } = GoalActionRole.None;
    public SectorId? AssignedSector { get; set; }
    public float SectorCenterX { get; set; }
    public float SectorCenterY { get; set; }
    public bool StutterStepEnabled { get; set; }
    public float WeaponCooldownRemaining { get; set; }

    public bool HasVisibleThreat { get; set; }
    public bool ShouldRetreat { get; set; }
    public bool HasObjective { get; set; }
    public bool IsAtObjective { get; set; }
    public bool ShouldRegroup { get; set; }
    public bool BuildOrderAvailable { get; set; }
    public ulong? RepairTargetTag { get; set; }
    public bool IsRepairAssignmentSafe { get; set; }
    public int RepairDistanceManhattan { get; set; }

    public IReadOnlyCollection<string> Capabilities => _ownedHandles.Keys.ToArray();
    internal bool HasCapability(string name) => _ownedHandles.ContainsKey(name);
    internal void OwnHandle(string name, FSMHandle handle) => _ownedHandles.Add(name, handle);

    internal bool TryGetFirstHandle(out FSMHandle handle)
    {
        handle = default;
        if (_ownedHandles.Count == 0) return false;
        handle = _ownedHandles.Values.First();
        return true;
    }

    internal void DestroyOwnedHandles()
    {
        foreach (var handle in _ownedHandles.Values)
            FSM_API.Interaction.DestroyInstance(handle);
        _ownedHandles.Clear();
    }

    internal void Invalidate()
    {
        IsValid = false;
        CurrentVisibility = UnitVisibility.Unknown;
    }

    internal void MarkNotObserved(uint gameLoop)
    {
        if (LastObservedGameLoop < gameLoop)
            CurrentVisibility = UnitVisibility.Unknown;
    }

    public void Apply(UnitObservationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Tag != UnitTag)
            throw new ArgumentException("Observation facts belong to a different unit tag.", nameof(facts));

        UnitTypeId = facts.UnitTypeId == 0 ? UnitTypeId : facts.UnitTypeId;
        OwnerId = checked((uint)facts.OwnerId);
        Alliance = facts.Alliance;
        CurrentVisibility = facts.Visibility;
        LastObservedGameLoop = facts.GameLoop;

        if (facts.IsLiveVisible)
        {
            LastSeenGameLoop = facts.GameLoop;
            Health = facts.Health;
            HealthMax = facts.HealthMax;
            Shield = facts.Shield;
            ShieldMax = facts.ShieldMax;
            BuildProgress = facts.BuildProgress;
            PositionX = facts.PositionX;
            PositionY = facts.PositionY;
            HasPosition = facts.HasPosition;
            HasCargo = facts.HasCargo;
            WeaponCooldownRemaining = facts.WeaponCooldownRemaining;
            HasAssignedWork = facts.HasOrders;
            ObservedOrders = facts.Orders;
            IsFlying = facts.IsFlying;
            IsBurrowed = facts.IsBurrowed;
            IsHallucination = facts.IsHallucination;
        }
        else
        {
            if (facts.HealthMax > 0)
            {
                Health = facts.Health;
                HealthMax = facts.HealthMax;
                Shield = facts.Shield;
                ShieldMax = facts.ShieldMax;
            }
            if (facts.HasPosition)
            {
                PositionX = facts.PositionX;
                PositionY = facts.PositionY;
                HasPosition = true;
            }
        }

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
        if (WorkerRoutePhase == phase && !forceTimestamp) return;
        WorkerRoutePhase = phase;
        WorkerRoutePhaseChangedAtGameLoop = gameLoop;
    }

    public void Emit(BehaviorCommand command)
    {
        if (command != BehaviorCommand.None)
            _emit(command);
    }
}

public enum WorkerRoutePhase
{
    Unassigned,
    Assigned,
    ExecutingOrder,
    CarryingResources,
    Outbound,
    Returning
}
