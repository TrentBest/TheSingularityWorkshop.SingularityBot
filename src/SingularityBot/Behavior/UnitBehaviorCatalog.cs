using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>Reusable entity lifecycle and capability FSM definitions.</summary>
public static class UnitBehaviorCatalog
{
    public const string DataSyncProcessingGroup = "SingularityBot.DataSync";
    public const string MiningProcessingGroup = "SingularityBot.Mining";
    public const string ConstructionProcessingGroup = "SingularityBot.Construction";
    public const string RepairProcessingGroup = "SingularityBot.Repair";
    public const string CombatProcessingGroup = "SingularityBot.Combat";

    public const string EntityExistenceFsm = "SingularityBot.EntityExistence";
    public const string WorkerFsm = "SingularityBot.Worker";
    public const string ConstructionFsm = "SingularityBot.ConstructionCapability";
    public const string RepairFsm = "SingularityBot.RepairCapability";
    public const string CombatFsm = "SingularityBot.CombatUnit";

    // Ordered frame groups. Strategy and formation groups are introduced as
    // their contexts and intent contracts are implemented.
    public static IReadOnlyList<string> ProcessGroupsThisFrame { get; } =
    [
        DataSyncProcessingGroup,
        MiningProcessingGroup,
        ConstructionProcessingGroup,
        CombatProcessingGroup
    ];

    public static void Register()
    {
        foreach (var group in ProcessGroupsThisFrame.Append(RepairProcessingGroup).Distinct())
            FSM_API.Create.CreateProcessingGroup(group);
        RegisterEntityExistence();
        RegisterWorker();
        RegisterConstruction();
        RegisterRepair();
        RegisterCombatUnit();
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
        FSM_API.Create.CreateFiniteStateMachine(RepairFsm, processRate: 1, processingGroup: RepairProcessingGroup)
            .State("Waiting", null, ctx =>
            {
                if (ctx is UnitBehaviorContext unit && unit.IsCurrentlyVisible &&
                    unit.RepairTargetTag is not null && unit.IsRepairAssignmentSafe)
                    unit.Emit(BehaviorCommand.RepairAssignedTarget);
            }, null)
            .WithInitialState("Waiting")
            .BuildDefinition();
    }

    private static void RegisterCombatUnit()
    {
        FSM_API.Create.CreateFiniteStateMachine(CombatFsm, processRate: 1, processingGroup: CombatProcessingGroup)
            .State("Assess", null, ctx =>
            {
                if (ctx is not UnitBehaviorContext unit || !unit.IsCurrentlyVisible) return;
                if (unit.ShouldRetreat) unit.Emit(BehaviorCommand.RetreatToSafety);
                else if (unit.HasVisibleThreat) unit.Emit(BehaviorCommand.AttackVisibleThreat);
                else if (unit.ShouldRegroup) unit.Emit(BehaviorCommand.RegroupWithArmy);
                else if (unit.HasObjective) unit.Emit(BehaviorCommand.AdvanceToObjective);
            }, null)
            .WithInitialState("Assess")
            .BuildDefinition();
    }
}
