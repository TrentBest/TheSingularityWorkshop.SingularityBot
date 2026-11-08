using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>Registers independent worker capabilities and combat behavior definitions.</summary>
public static class UnitBehaviorCatalog
{
    public const string ProcessingGroup = "SingularityBot.Units";
    public const string MiningProcessingGroup = "SingularityBot.Mining";
    public const string ConstructionProcessingGroup = "SingularityBot.Construction";
    public const string RepairProcessingGroup = "SingularityBot.Repair";
    public const string CombatProcessingGroup = "SingularityBot.Combat";
    public const string WorkerFsm = "SingularityBot.Worker";
    public const string ConstructionFsm = "SingularityBot.ConstructionCapability";
    public const string RepairFsm = "SingularityBot.RepairCapability";
    public const string CombatFsm = "SingularityBot.CombatUnit";

    public static void Register()
    {
        FSM_API.Create.CreateProcessingGroup(ProcessingGroup);
        FSM_API.Create.CreateProcessingGroup(MiningProcessingGroup);
        FSM_API.Create.CreateProcessingGroup(ConstructionProcessingGroup);
        FSM_API.Create.CreateProcessingGroup(RepairProcessingGroup);
        FSM_API.Create.CreateProcessingGroup(CombatProcessingGroup);
        RegisterWorker();
        RegisterConstruction();
        RegisterRepair();
        RegisterCombatUnit();
    }

    // Baseline SCV capability: maintain its economy route. Cargo return takes
    // precedence over a fresh mineral assignment, without owning construction
    // or repair decisions.
    private static void RegisterWorker()
    {
        FSM_API.Create.CreateFiniteStateMachine(WorkerFsm, processRate: 1, processingGroup: MiningProcessingGroup)
            .State("Assess", null,
                ctx =>
                {
                    if (ctx is not UnitBehaviorContext unit) return;
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
            .State("Waiting", null,
                ctx =>
                {
                    if (ctx is UnitBehaviorContext unit && unit.BuildOrderAvailable)
                        unit.Emit(BehaviorCommand.BuildRequestedStructure);
                }, null)
            .WithInitialState("Waiting")
            .BuildDefinition();
    }

    // The repair FSM is registered in its own group. Its context is populated
    // by the repair coordinator; it is never polled by the normal frame update.
    private static void RegisterRepair()
    {
        FSM_API.Create.CreateFiniteStateMachine(RepairFsm, processRate: 1, processingGroup: RepairProcessingGroup)
            .State("Waiting", null,
                ctx =>
                {
                    if (ctx is UnitBehaviorContext unit && unit.RepairTargetTag is not null &&
                        unit.IsRepairAssignmentSafe)
                        unit.Emit(BehaviorCommand.RepairAssignedTarget);
                }, null)
            .WithInitialState("Waiting")
            .BuildDefinition();
    }

    private static void RegisterCombatUnit()
    {
        FSM_API.Create.CreateFiniteStateMachine(CombatFsm, processRate: 1, processingGroup: CombatProcessingGroup)
            .State("Assess", null,
                ctx =>
                {
                    if (ctx is not UnitBehaviorContext unit) return;
                    if (unit.ShouldRetreat) unit.Emit(BehaviorCommand.RetreatToSafety);
                    else if (unit.HasVisibleThreat) unit.Emit(BehaviorCommand.AttackVisibleThreat);
                    else if (unit.ShouldRegroup) unit.Emit(BehaviorCommand.RegroupWithArmy);
                    else if (unit.HasObjective) unit.Emit(BehaviorCommand.AdvanceToObjective);
                }, null)
            .WithInitialState("Assess")
            .BuildDefinition();
    }
}
