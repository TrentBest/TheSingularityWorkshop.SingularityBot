using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>Reusable entity lifecycle and capability FSM definitions.</summary>
public static class UnitBehaviorCatalog
{
    public const string DataSyncProcessingGroup = "SingularityBot.DataSync";
    public const string StrategyProcessingGroup = "SingularityBot.Strategy";
    public const string SubStrategyProcessingGroup = "SingularityBot.SubStrategy";
    public const string HighDpsProcessingGroup = "SingularityBot.HighDPS";
    public const string LowDpsProcessingGroup = "SingularityBot.LowDPS";
    public const string ActionResolutionProcessingGroup = "SingularityBot.ActionResolution";
    public const string ProductionProcessingGroup = "SingularityBot.Production";

    public const string MiningProcessingGroup = LowDpsProcessingGroup;
    public const string ConstructionProcessingGroup = LowDpsProcessingGroup;
    public const string RepairProcessingGroup = HighDpsProcessingGroup;
    public const string CombatProcessingGroup = HighDpsProcessingGroup;

    public const string EntityExistenceFsm = "SingularityBot.EntityExistence";
    public const string WorkerFsm = "SingularityBot.Worker";
    public const string ConstructionFsm = "SingularityBot.ConstructionCapability";
    public const string RepairFsm = "SingularityBot.RepairCapability";
    public const string CombatFsm = "SingularityBot.CombatUnit";
    public const string MoveTowardsFsm = "SingularityBot.MoveTowards";
    public const string MoveAwayFsm = "SingularityBot.MoveAwayFrom";
    public const string FollowFsm = "SingularityBot.Follow";
    public const string PatrolFsm = "SingularityBot.Patrol";
    public const string FlankLeftFsm = "SingularityBot.FlankLeft";
    public const string FlankRightFsm = "SingularityBot.FlankRight";
    public const string AttackTargetFsm = "SingularityBot.AttackTarget";

    public static IReadOnlyList<string> ProcessGroupsThisFrame { get; } =
    [
        DataSyncProcessingGroup, StrategyProcessingGroup, SubStrategyProcessingGroup,
        HighDpsProcessingGroup, LowDpsProcessingGroup, ActionResolutionProcessingGroup,
        ProductionProcessingGroup
    ];

    public static void Register()
    {
        foreach (var group in ProcessGroupsThisFrame.Distinct())
            FSM_API.Create.CreateProcessingGroup(group);

        RegisterEntityExistence();
        RegisterWorker();
        RegisterConstruction();
        RegisterRepair();
        RegisterMovementCapabilities();
        RegisterAttackTarget();
        RegisterCombatUnit();
        ProductionBehaviorCatalog.Register();
    }

    private static void RegisterEntityExistence()
    {
        FSM_API.Create.CreateFiniteStateMachine(EntityExistenceFsm, processRate: 1, processingGroup: DataSyncProcessingGroup)
            .State("FirstSeen", null, null, null)
            .State("AliveVisible", null, null, null)
            .State("AliveRemembered", null, null, null)
            .Transition("FirstSeen", "AliveVisible", ctx => ctx is UnitBehaviorContext unit && unit.IsCurrentlyVisible)
            .Transition("FirstSeen", "AliveRemembered", ctx => ctx is UnitBehaviorContext unit && !unit.IsCurrentlyVisible)
            .Transition("AliveVisible", "AliveRemembered", ctx => ctx is UnitBehaviorContext unit && !unit.IsCurrentlyVisible)
            .Transition("AliveRemembered", "AliveVisible", ctx => ctx is UnitBehaviorContext unit && unit.IsCurrentlyVisible)
            .WithInitialState("FirstSeen")
            .BuildDefinition();
    }

    private static void RegisterWorker()
    {
        FSM_API.Create.CreateFiniteStateMachine(WorkerFsm, processRate: 1, processingGroup: MiningProcessingGroup)
            .State("Assess", null, ctx =>
            {
                if (ctx is not UnitBehaviorContext unit || !unit.IsCurrentlyVisible) return;
                if (unit.HasCargo) unit.Emit(BehaviorCommand.ReturnCarriedResources);
                else if (unit.AssignedMineralTag is not null)
                    unit.Emit(BehaviorCommand.AssignWorkerToMinerals);
            }, null)
            .WithInitialState("Assess")
            .BuildDefinition();
    }

    private static void RegisterConstruction()
    {
        FSM_API.Create.CreateFiniteStateMachine(ConstructionFsm, processRate: 1, processingGroup: ConstructionProcessingGroup)
            .State("Waiting", null, ctx =>
            {
                if (ctx is UnitBehaviorContext unit && unit.IsCurrentlyVisible && unit.BuildOrderAvailable)
                    unit.Emit(BehaviorCommand.BuildRequestedStructure);
            }, null)
            .WithInitialState("Waiting")
            .BuildDefinition();
    }

    private static void RegisterRepair()
    {
        RegisterToggleCapability(RepairFsm, RepairProcessingGroup,
            unit => unit.IsCurrentlyVisible && unit.RepairTargetTag is not null && unit.IsRepairAssignmentSafe,
            BehaviorCommand.RepairAssignedTarget);
    }

    private static void RegisterMovementCapabilities()
    {
        RegisterToggleCapability(MoveTowardsFsm, LowDpsProcessingGroup,
            unit => unit.IsCurrentlyVisible && unit.MoveTowardsTargetTag is not null,
            BehaviorCommand.MoveTowardsTarget);
        RegisterToggleCapability(MoveAwayFsm, LowDpsProcessingGroup,
            unit => unit.IsCurrentlyVisible && unit.MoveAwayFromTargetTag is not null,
            BehaviorCommand.MoveAwayFromTarget);
        RegisterToggleCapability(FollowFsm, LowDpsProcessingGroup,
            unit => unit.IsCurrentlyVisible && unit.FollowTargetTag is not null,
            BehaviorCommand.FollowTarget);
        RegisterToggleCapability(PatrolFsm, LowDpsProcessingGroup,
            unit => unit.IsCurrentlyVisible && unit.HasPatrolRoute,
            BehaviorCommand.PatrolRoute);
        RegisterToggleCapability(FlankLeftFsm, LowDpsProcessingGroup,
            unit => unit.IsCurrentlyVisible && unit.FlankTargetTag is not null && unit.FlankLeftRequested,
            BehaviorCommand.FlankLeftOfTarget);
        RegisterToggleCapability(FlankRightFsm, LowDpsProcessingGroup,
            unit => unit.IsCurrentlyVisible && unit.FlankTargetTag is not null && unit.FlankRightRequested,
            BehaviorCommand.FlankRightOfTarget);
    }

    private static void RegisterAttackTarget()
    {
        RegisterToggleCapability(AttackTargetFsm, HighDpsProcessingGroup,
            unit => unit.IsCurrentlyVisible && unit.AttackTargetTag is not null,
            BehaviorCommand.AttackTarget);
    }

    /// <summary>
    /// Each capability is a real FSM_API graph. Entry into Executing emits one
    /// semantic intent; clearing its guard returns the instance to Dormant.
    /// </summary>
    private static void RegisterToggleCapability(
        string fsmName,
        string processingGroup,
        Func<UnitBehaviorContext, bool> requested,
        BehaviorCommand command)
    {
        FSM_API.Create.CreateFiniteStateMachine(fsmName, processRate: 1, processingGroup: processingGroup)
            .State("Dormant", null, null, null)
            .State("Executing", ctx =>
            {
                if (ctx is UnitBehaviorContext unit)
                    unit.Emit(command);
            }, null, null)
            .Transition("Dormant", "Executing",
                ctx => ctx is UnitBehaviorContext unit && requested(unit))
            .Transition("Executing", "Dormant",
                ctx => ctx is UnitBehaviorContext unit && !requested(unit))
            .WithInitialState("Dormant")
            .BuildDefinition();
    }

    /// <summary>Tactical selection is represented by guarded FSM transitions, not an if/else chain.</summary>
    private static void RegisterCombatUnit()
    {
        FSM_API.Create.CreateFiniteStateMachine(CombatFsm, processRate: 1, processingGroup: CombatProcessingGroup)
            .State("Assess", null, null, null)
            .State("Retreating", ctx => Emit(ctx, BehaviorCommand.RetreatToSafety), null, null)
            .State("Regrouping", ctx => Emit(ctx, BehaviorCommand.RegroupWithArmy), null, null)
            .State("Advancing", ctx => Emit(ctx, BehaviorCommand.AdvanceToObjective), null, null)
            .Transition("Assess", "Retreating", ctx => ctx is UnitBehaviorContext unit && unit.ShouldRetreat)
            .Transition("Assess", "Regrouping", ctx => ctx is UnitBehaviorContext unit && !unit.ShouldRetreat && unit.ShouldRegroup)
            .Transition("Assess", "Advancing", ctx => ctx is UnitBehaviorContext unit && !unit.ShouldRetreat && !unit.ShouldRegroup && unit.HasObjective)
            .Transition("Retreating", "Assess", ctx => ctx is UnitBehaviorContext unit && !unit.ShouldRetreat)
            .Transition("Regrouping", "Assess", ctx => ctx is UnitBehaviorContext unit && !unit.ShouldRegroup && !unit.ShouldRetreat)
            .Transition("Advancing", "Assess", ctx => ctx is UnitBehaviorContext unit && (!unit.HasObjective || unit.ShouldRetreat || unit.ShouldRegroup))
            .WithInitialState("Assess")
            .BuildDefinition();
    }

    private static void Emit(IStateContext context, BehaviorCommand command)
    {
        if (context is UnitBehaviorContext unit && unit.IsCurrentlyVisible)
            unit.Emit(command);
    }
}
