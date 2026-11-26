namespace SingularityBot.Behavior;

/// <summary>Domain intent emitted by an FSM capability and validated by the SC2 adapter.</summary>
public enum BehaviorCommand
{
    None,
    AssignWorkerToMinerals,
    ReturnCarriedResources,
    BuildRequestedStructure,
    RepairAssignedTarget,
    FollowTarget,
    PatrolRoute,
    MoveTowardsTarget,
    MoveAwayFromTarget,
    FlankLeftOfTarget,
    FlankRightOfTarget,
    AttackTarget,
    DefendWorkerLine,
    AdvanceToObjective,
    AttackVisibleThreat,
    RetreatToSafety,
    RegroupWithArmy,
    HoldRallyPoint,
    ScoutSector,
    ContainSector,
    EngageObjective,
    FallbackToRally,
    StutterStepMove
}
