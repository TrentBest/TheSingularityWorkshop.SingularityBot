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
    public const string GoalActionProcessingGroup = StrategyProcessingGroup;

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
    public const string GoalActionFsm = "SingularityBot.GoalAction";
    public const string ScoutSectorFsm = "SingularityBot.ScoutSector";
    public const string StutterStepFsm = "SingularityBot.StutterStep";

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

        AdvisorWorkScheduler.RegisterProcessingGroups();
        RegisterGameState();

        RegisterEntityExistence();
        RegisterWorker();
        RegisterConstruction();
        RegisterRepair();
        RegisterMovementCapabilities();
        RegisterAttackTarget();
        RegisterGoalActions();
        RegisterScoutSector();
        RegisterStutterStep();
        RegisterCombatUnit();
        ProductionBehaviorCatalog.Register();
        BuildJobRuntime.RegisterDefinition();
        Sc2RefineryRuntime.RegisterDefinition();
    }

    private static bool ShouldEnterMidGame(IStateContext context) =>
        context is GameStateContext state &&
        (state.HasAdvancedTech || state.FriendlyBaseCount >= 2);

    private static bool ShouldEnterLateGame(IStateContext context) =>
        context is GameStateContext state &&
        (state.HasLateGameTech || state.FriendlyBaseCount >= 3);

    private static bool IsUnitCurrentlyVisible(IStateContext context) =>
        context is UnitBehaviorContext unit && unit.IsCurrentlyVisible;

    private static bool IsUnitNotCurrentlyVisible(IStateContext context) =>
        context is UnitBehaviorContext unit && !unit.IsCurrentlyVisible;

    private static bool IsWorkerVisible(IStateContext context) =>
        context is UnitBehaviorContext unit && unit.IsWorker && unit.IsCurrentlyVisible;

    private static bool IsWorkerNotVisible(IStateContext context) =>
        context is UnitBehaviorContext unit && unit.IsWorker && !unit.IsCurrentlyVisible;

    private static bool IsWorkerVisibleAndCarryingResources(IStateContext context) =>
        context is UnitBehaviorContext unit && unit.IsWorker && unit.IsCurrentlyVisible && unit.HasCargo;

    private static bool IsWorkerVisibleAndAssignedToMinerals(IStateContext context) =>
        context is UnitBehaviorContext unit && unit.IsWorker && unit.IsCurrentlyVisible &&
        !unit.HasCargo && unit.AssignedMineralTag.HasValue;

    private static bool IsWorkerVisibleAndAwaitingAssignment(IStateContext context) =>
        context is UnitBehaviorContext unit && unit.IsWorker && unit.IsCurrentlyVisible &&
        !unit.HasCargo && !unit.AssignedMineralTag.HasValue;

    private static void RegisterGameState()
    {
        FSM_API.Create.CreateFiniteStateMachine("SingularityBot.GameState", processRate: -1, processingGroup: StrategyProcessingGroup)
            .State("EarlyGame", ctx => (ctx as GameStateContext)?.SetPhase(GamePhase.Early), null, null)
            .State("MidGame", ctx => (ctx as GameStateContext)?.SetPhase(GamePhase.Mid), null, null)
            .State("LateGame", ctx => (ctx as GameStateContext)?.SetPhase(GamePhase.Late), null, null)
            .Transition("EarlyGame", "MidGame", ShouldEnterMidGame)
            .Transition("MidGame", "LateGame", ShouldEnterLateGame)
            .WithInitialState("EarlyGame")
            .BuildDefinition();
    }

    private static void RegisterEntityExistence()
    {
        FSM_API.Create.CreateFiniteStateMachine(EntityExistenceFsm, processRate: -1, processingGroup: DataSyncProcessingGroup)
            .State("FirstSeen", null, null, null)
            .State("AliveVisible", null, null, null)
            .State("AliveRemembered", null, null, null)
            .Transition("FirstSeen", "AliveVisible", IsUnitCurrentlyVisible)
            .Transition("FirstSeen", "AliveRemembered", IsUnitNotCurrentlyVisible)
            .Transition("AliveVisible", "AliveRemembered", IsUnitNotCurrentlyVisible)
            .Transition("AliveRemembered", "AliveVisible", IsUnitCurrentlyVisible)
            .WithInitialState("FirstSeen")
            .BuildDefinition();
    }

    private static void RegisterWorker()
    {
        FSM_API.Create.CreateFiniteStateMachine(WorkerFsm, processRate: -1, processingGroup: MiningProcessingGroup)
            .State("Assess", null, null, null)
            .State("NotVisible", null, null, null)
            .State("Visible", null, null, null)
            .State("ReturningResources", ctx => Emit(ctx, BehaviorCommand.ReturnCarriedResources), null, null)
            .State("Mining", ctx => Emit(ctx, BehaviorCommand.AssignWorkerToMinerals), null, null)
            .State("AwaitingAssignment", null, null, null)
            .Transition("Assess", "NotVisible", IsWorkerNotVisible)
            .Transition("Assess", "ReturningResources", IsWorkerVisibleAndCarryingResources)
            .Transition("Assess", "Mining", IsWorkerVisibleAndAssignedToMinerals)
            .Transition("Assess", "AwaitingAssignment", IsWorkerVisibleAndAwaitingAssignment)
            .Transition("NotVisible", "Visible", IsWorkerVisible)
            .Transition("Visible", "NotVisible", IsWorkerNotVisible)
            .Transition("Visible", "ReturningResources", IsWorkerVisibleAndCarryingResources)
            .Transition("Visible", "Mining", IsWorkerVisibleAndAssignedToMinerals)
            .Transition("Visible", "AwaitingAssignment", IsWorkerVisibleAndAwaitingAssignment)
            .Transition("ReturningResources", "NotVisible", IsWorkerNotVisible)
            .Transition("ReturningResources", "Mining", IsWorkerVisibleAndAssignedToMinerals)
            .Transition("ReturningResources", "AwaitingAssignment", IsWorkerVisibleAndAwaitingAssignment)
            .Transition("Mining", "NotVisible", IsWorkerNotVisible)
            .Transition("Mining", "ReturningResources", IsWorkerVisibleAndCarryingResources)
            .Transition("Mining", "AwaitingAssignment", IsWorkerVisibleAndAwaitingAssignment)
            .Transition("AwaitingAssignment", "NotVisible", IsWorkerNotVisible)
            .Transition("AwaitingAssignment", "ReturningResources", IsWorkerVisibleAndCarryingResources)
            .Transition("AwaitingAssignment", "Mining", IsWorkerVisibleAndAssignedToMinerals)
            .WithInitialState("Assess")
            .BuildDefinition();
    }

    private static void RegisterConstruction()
    {
        FSM_API.Create.CreateFiniteStateMachine(ConstructionFsm, processRate: -1, processingGroup: ConstructionProcessingGroup)
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
        FSM_API.Create.CreateFiniteStateMachine(fsmName, processRate: -1, processingGroup: processingGroup)
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


    private static void RegisterGoalActions()
    {
        var graph = FSM_API.Create.CreateFiniteStateMachine(GoalActionFsm, processRate: -1, processingGroup: GoalActionProcessingGroup)
            .State("Unassigned", null, null, null)
            .State("Rallying", ctx => Emit(ctx, BehaviorCommand.HoldRallyPoint), null, null)
            .State("Scouting", null, null, null)
            .State("Containing", ctx => Emit(ctx, BehaviorCommand.ContainSector), null, null)
            .State("Engaging", ctx => Emit(ctx, BehaviorCommand.EngageObjective), null, null)
            .State("FallingBack", ctx => Emit(ctx, BehaviorCommand.FallbackToRally), null, null);
        foreach (var from in new[] { "Unassigned", "Rallying", "Scouting", "Containing", "Engaging", "FallingBack" })
        foreach (var role in Enum.GetValues<GoalActionRole>())
        {
            if (role == GoalActionRole.None) continue;
            var to = role switch { GoalActionRole.Rally => "Rallying", GoalActionRole.Scout => "Scouting", GoalActionRole.Contain => "Containing", GoalActionRole.Engage => "Engaging", _ => "FallingBack" };
            if (from != to) graph.Transition(from, to, ctx => HasRole(ctx, role));
        }
        graph.WithInitialState("Unassigned").BuildDefinition();
    }

    private static void RegisterScoutSector() =>
        RegisterToggleCapability(ScoutSectorFsm, LowDpsProcessingGroup,
            unit => unit.IsCurrentlyVisible && unit.GoalRole == GoalActionRole.Scout && unit.AssignedSector.HasValue,
            BehaviorCommand.ScoutSector);

    private static void RegisterStutterStep()
    {
        FSM_API.Create.CreateFiniteStateMachine(StutterStepFsm, processRate: -1, processingGroup: HighDpsProcessingGroup)
            .State("Disabled", null, null, null)
            .State("WeaponReady", ctx => Emit(ctx, BehaviorCommand.AttackTarget), null, null)
            .State("WeaponCooldown", ctx => Emit(ctx, BehaviorCommand.StutterStepMove), null, null)
            .Transition("Disabled", "WeaponReady", IsStutterReady)
            .Transition("Disabled", "WeaponCooldown", IsStutterCooling)
            .Transition("WeaponReady", "WeaponCooldown", IsStutterCooling)
            .Transition("WeaponReady", "Disabled", ctx => !IsStutterEnabled(ctx))
            .Transition("WeaponCooldown", "WeaponReady", IsStutterReady)
            .Transition("WeaponCooldown", "Disabled", ctx => !IsStutterEnabled(ctx))
            .WithInitialState("Disabled").BuildDefinition();
    }

    private static bool HasRole(IStateContext ctx, GoalActionRole role) =>
        ctx is UnitBehaviorContext unit && unit.IsCurrentlyVisible && unit.GoalRole == role;
    private static bool IsStutterEnabled(IStateContext ctx) =>
        ctx is UnitBehaviorContext unit && unit.IsCurrentlyVisible && unit.StutterStepEnabled && unit.GoalRole != GoalActionRole.Fallback && unit.AttackTargetTag.HasValue;
    private static bool IsStutterReady(IStateContext ctx) =>
        IsStutterEnabled(ctx) && ctx is UnitBehaviorContext unit && unit.WeaponCooldownRemaining <= 0;
    private static bool IsStutterCooling(IStateContext ctx) =>
        IsStutterEnabled(ctx) && ctx is UnitBehaviorContext unit && unit.WeaponCooldownRemaining > 0;

    /// <summary>Tactical selection is represented by guarded FSM transitions, not an if/else chain.</summary>
    private static void RegisterCombatUnit()
    {
        FSM_API.Create.CreateFiniteStateMachine(CombatFsm, processRate: -1, processingGroup: CombatProcessingGroup)
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
