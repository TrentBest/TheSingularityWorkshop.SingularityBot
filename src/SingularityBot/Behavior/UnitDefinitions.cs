namespace SingularityBot.Behavior;

/// <summary>
/// A reusable capability resource that a unit definition composes into an
/// entity context. The FSM itself is registered once; each entity receives
/// its own FSM instance in the declared processing group.
/// </summary>
public sealed record UnitCapabilityDefinition(
    string FsmName,
    string ProcessingGroup);

/// <summary>
/// Declarative composition recipe for a unit kind. This is data consumed by
/// the generic unit factory, not a runtime entity type.
/// </summary>
public sealed record UnitDefinition(
    string UnitKind,
    IReadOnlyList<UnitCapabilityDefinition> Capabilities);

/// <summary>
/// Initial unit composition resources. GUI-authored definitions can replace
/// or extend this catalog without changing UnitBehaviorRuntime.
/// </summary>
public static class UnitDefinitions
{
    public static readonly UnitDefinition SCV = new(
        "SCV",
        [
            new(UnitBehaviorCatalog.WorkerFsm, UnitBehaviorCatalog.MiningProcessingGroup)
        ]);

    public static readonly UnitDefinition Probe = new(
        "Probe",
        [
            new(UnitBehaviorCatalog.WorkerFsm, UnitBehaviorCatalog.MiningProcessingGroup)
        ]);

    public static readonly UnitDefinition Drone = new(
        "Drone",
        [
            new(UnitBehaviorCatalog.WorkerFsm, UnitBehaviorCatalog.MiningProcessingGroup)
        ]);

    public static readonly UnitDefinition Zealot = new(
        "Zealot",
        [
            new(UnitBehaviorCatalog.CombatFsm, UnitBehaviorCatalog.CombatProcessingGroup)
        ]);

    public static readonly UnitDefinition Marine = new(
        "Marine",
        [
            new(UnitBehaviorCatalog.CombatFsm, UnitBehaviorCatalog.CombatProcessingGroup)
        ]);

    public static readonly UnitDefinition Zergling = new(
        "Zergling",
        [
            new(UnitBehaviorCatalog.CombatFsm, UnitBehaviorCatalog.CombatProcessingGroup)
        ]);

    public static IReadOnlyList<UnitDefinition> InitialSet =>
        [SCV, Probe, Drone, Zealot, Marine, Zergling];

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
