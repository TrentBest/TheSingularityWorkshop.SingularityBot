namespace SingularityBot.Behavior;

/// <summary>
/// A domain-level intent emitted by an FSM. The SC2 adapter is responsible for
/// translating intents into protocol actions and validating them against the
/// current observation before submission.
/// </summary>
public enum BehaviorCommand
{
    None,
    AssignWorkerToMinerals,
    ReturnCarriedResources,
    BuildRequestedStructure,
    DefendWorkerLine,
    AdvanceToObjective,
    AttackVisibleThreat,
    RetreatToSafety,
    RegroupWithArmy
}
