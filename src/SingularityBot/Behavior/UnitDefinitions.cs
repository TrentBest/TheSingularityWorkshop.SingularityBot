namespace SingularityBot.Behavior;

/// <summary>A reusable FSM capability and its scheduled processing group.</summary>
public sealed record UnitCapabilityDefinition(string FsmName, string ProcessingGroup);

/// <summary>Compiled composition data, not a unit-specific runtime class.</summary>
public sealed record UnitDefinition(string UnitKind, IReadOnlyList<UnitCapabilityDefinition> Capabilities);

/// <summary>
/// Initial compiled unit compositions. Each capability definition is shared;
/// each unit receives its own FSM_API instance bound to its own context.
/// </summary>
public static class UnitDefinitions
{
    private static UnitCapabilityDefinition Capability(string fsm, string group) => new(fsm, group);

    private static UnitCapabilityDefinition[] MobileCore() =>
    [
        Capability(UnitBehaviorCatalog.MoveTowardsFsm, UnitBehaviorCatalog.LowDpsProcessingGroup),
        Capability(UnitBehaviorCatalog.MoveAwayFsm, UnitBehaviorCatalog.LowDpsProcessingGroup),
        Capability(UnitBehaviorCatalog.FollowFsm, UnitBehaviorCatalog.LowDpsProcessingGroup),
        Capability(UnitBehaviorCatalog.PatrolFsm, UnitBehaviorCatalog.LowDpsProcessingGroup),
        Capability(UnitBehaviorCatalog.FlankLeftFsm, UnitBehaviorCatalog.LowDpsProcessingGroup),
        Capability(UnitBehaviorCatalog.FlankRightFsm, UnitBehaviorCatalog.LowDpsProcessingGroup),
        Capability(UnitBehaviorCatalog.AttackTargetFsm, UnitBehaviorCatalog.HighDpsProcessingGroup),
        Capability(UnitBehaviorCatalog.GoalActionFsm, UnitBehaviorCatalog.GoalActionProcessingGroup),
        Capability(UnitBehaviorCatalog.ScoutSectorFsm, UnitBehaviorCatalog.LowDpsProcessingGroup),
        Capability(UnitBehaviorCatalog.StutterStepFsm, UnitBehaviorCatalog.HighDpsProcessingGroup),
        Capability(UnitBehaviorCatalog.CombatFsm, UnitBehaviorCatalog.HighDpsProcessingGroup)
    ];

    public static readonly UnitDefinition SCV = new(
        "SCV",
        [.. MobileCore(),
            Capability(UnitBehaviorCatalog.WorkerFsm, UnitBehaviorCatalog.MiningProcessingGroup),
            Capability(UnitBehaviorCatalog.ConstructionFsm, UnitBehaviorCatalog.ConstructionProcessingGroup),
            Capability(UnitBehaviorCatalog.RepairFsm, UnitBehaviorCatalog.RepairProcessingGroup)]);

    public static readonly UnitDefinition Probe = new(
        "Probe",
        [.. MobileCore(), Capability(UnitBehaviorCatalog.WorkerFsm, UnitBehaviorCatalog.MiningProcessingGroup)]);

    public static readonly UnitDefinition Drone = new(
        "Drone",
        [.. MobileCore(), Capability(UnitBehaviorCatalog.WorkerFsm, UnitBehaviorCatalog.MiningProcessingGroup)]);

    public static readonly UnitDefinition Zealot = new("Zealot", MobileCore());
    public static readonly UnitDefinition Marine = new("Marine", MobileCore());
    public static readonly UnitDefinition Marauder = new("Marauder", MobileCore());
    public static readonly UnitDefinition Zergling = new("Zergling", MobileCore());

    public static IReadOnlyList<UnitDefinition> InitialSet => [SCV, Probe, Drone, Zealot, Marine, Marauder, Zergling];

    private static readonly IReadOnlyDictionary<string, UnitDefinition> ByKind =
        InitialSet.ToDictionary(definition => definition.UnitKind, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string unitKind, out UnitDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(unitKind))
        {
            definition = null!;
            return false;
        }

        return ByKind.TryGetValue(unitKind, out definition!);
    }
}
