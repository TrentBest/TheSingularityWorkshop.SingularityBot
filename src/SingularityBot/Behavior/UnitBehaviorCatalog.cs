using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Registers reusable worker and combat behavior definitions. Definitions are
/// shared; every live unit receives its own context and FSM instance.
/// </summary>
public static class UnitBehaviorCatalog
{
    public const string ProcessingGroup = "SingularityBot.Units";
    public const string WorkerFsm = "SingularityBot.Worker";
    public const string CombatFsm = "SingularityBot.CombatUnit";

    public static void Register()
    {
        FSM_API.Create.CreateProcessingGroup(ProcessingGroup);
        RegisterWorker();
        RegisterCombatUnit();
    }

    private static void RegisterWorker()
    {
        FSM_API.Create.CreateFiniteStateMachine(WorkerFsm, processRate: 1, processingGroup: ProcessingGroup)
            .State("Assess",
                onEnter: null,
                onUpdate: ctx =>
                {
                    if (ctx is not UnitBehaviorContext unit) return;
                    if (unit.HasCargo) unit.Emit(BehaviorCommand.ReturnCarriedResources);
                    else if (unit.BuildOrderAvailable) unit.Emit(BehaviorCommand.BuildRequestedStructure);
                    else if (unit.HasVisibleThreat) unit.Emit(BehaviorCommand.DefendWorkerLine);
                    else if (!unit.HasAssignedWork) unit.Emit(BehaviorCommand.AssignWorkerToMinerals);
                },
                onExit: null)
            .State("Returning",
                onEnter: ctx => (ctx as UnitBehaviorContext)?.Emit(BehaviorCommand.ReturnCarriedResources),
                onUpdate: null,
                onExit: null)
            .State("Working",
                onEnter: ctx => (ctx as UnitBehaviorContext)?.Emit(BehaviorCommand.AssignWorkerToMinerals),
                onUpdate: null,
                onExit: null)
            .State("Building",
                onEnter: ctx => (ctx as UnitBehaviorContext)?.Emit(BehaviorCommand.BuildRequestedStructure),
                onUpdate: null,
                onExit: null)
            .State("Defending",
                onEnter: ctx => (ctx as UnitBehaviorContext)?.Emit(BehaviorCommand.DefendWorkerLine),
                onUpdate: null,
                onExit: null)
            .WithInitialState("Assess")
            .Transition("Assess", "Returning", ctx => ctx is UnitBehaviorContext unit && unit.HasCargo)
            .Transition("Assess", "Building", ctx => ctx is UnitBehaviorContext unit && !unit.HasCargo && unit.BuildOrderAvailable)
            .Transition("Assess", "Defending", ctx => ctx is UnitBehaviorContext unit && !unit.HasCargo && !unit.BuildOrderAvailable && unit.HasVisibleThreat)
            .Transition("Assess", "Working", ctx => ctx is UnitBehaviorContext unit && !unit.HasCargo && !unit.BuildOrderAvailable && !unit.HasVisibleThreat && !unit.HasAssignedWork)
            .Transition("Returning", "Assess", ctx => ctx is UnitBehaviorContext unit && !unit.HasCargo)
            .Transition("Working", "Assess", ctx => ctx is UnitBehaviorContext unit && (unit.HasCargo || unit.BuildOrderAvailable || unit.HasVisibleThreat))
            .Transition("Building", "Assess", ctx => ctx is UnitBehaviorContext unit && !unit.BuildOrderAvailable)
            .Transition("Defending", "Assess", ctx => ctx is UnitBehaviorContext unit && !unit.HasVisibleThreat)
            .BuildDefinition();
    }

    private static void RegisterCombatUnit()
    {
        FSM_API.Create.CreateFiniteStateMachine(CombatFsm, processRate: 1, processingGroup: ProcessingGroup)
            .State("Assess",
                onEnter: null,
                onUpdate: ctx =>
                {
                    if (ctx is not UnitBehaviorContext unit) return;
                    if (unit.ShouldRetreat) unit.Emit(BehaviorCommand.RetreatToSafety);
                    else if (unit.HasVisibleThreat) unit.Emit(BehaviorCommand.AttackVisibleThreat);
                    else if (unit.ShouldRegroup) unit.Emit(BehaviorCommand.RegroupWithArmy);
                    else if (unit.HasObjective) unit.Emit(BehaviorCommand.AdvanceToObjective);
                },
                onExit: null)
            .State("Engage",
                onEnter: ctx => (ctx as UnitBehaviorContext)?.Emit(BehaviorCommand.AttackVisibleThreat),
                onUpdate: null,
                onExit: null)
            .State("Retreat",
                onEnter: ctx => (ctx as UnitBehaviorContext)?.Emit(BehaviorCommand.RetreatToSafety),
                onUpdate: null,
                onExit: null)
            .State("Regroup",
                onEnter: ctx => (ctx as UnitBehaviorContext)?.Emit(BehaviorCommand.RegroupWithArmy),
                onUpdate: null,
                onExit: null)
            .State("Advance",
                onEnter: ctx => (ctx as UnitBehaviorContext)?.Emit(BehaviorCommand.AdvanceToObjective),
                onUpdate: null,
                onExit: null)
            .WithInitialState("Assess")
            .Transition("Assess", "Retreat", ctx => ctx is UnitBehaviorContext unit && unit.ShouldRetreat)
            .Transition("Assess", "Engage", ctx => ctx is UnitBehaviorContext unit && !unit.ShouldRetreat && unit.HasVisibleThreat)
            .Transition("Assess", "Regroup", ctx => ctx is UnitBehaviorContext unit && !unit.ShouldRetreat && !unit.HasVisibleThreat && unit.ShouldRegroup)
            .Transition("Assess", "Advance", ctx => ctx is UnitBehaviorContext unit && !unit.ShouldRetreat && !unit.HasVisibleThreat && !unit.ShouldRegroup && unit.HasObjective)
            .Transition("Engage", "Retreat", ctx => ctx is UnitBehaviorContext unit && unit.ShouldRetreat)
            .Transition("Engage", "Assess", ctx => ctx is UnitBehaviorContext unit && !unit.HasVisibleThreat && !unit.ShouldRetreat)
            .Transition("Retreat", "Assess", ctx => ctx is UnitBehaviorContext unit && !unit.ShouldRetreat)
            .Transition("Regroup", "Assess", ctx => ctx is UnitBehaviorContext unit && !unit.ShouldRegroup)
            .Transition("Advance", "Assess", ctx => ctx is UnitBehaviorContext unit && (unit.IsAtObjective || unit.ShouldRegroup || unit.HasVisibleThreat))
            .BuildDefinition();
    }
}
