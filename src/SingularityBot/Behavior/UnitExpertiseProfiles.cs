namespace SingularityBot.Behavior;

/// <summary>
/// Initial tuning hypotheses for the first expertise set. These values are
/// provisional and must be calibrated against replay evidence.
/// </summary>
public sealed record UnitExpertiseProfile(
    string UnitKind,
    bool IsWorker,
    string BehaviorFsm,
    int PreferredEngagementRange,
    double RetreatHealthFraction,
    int PreferredGroupSize);

public static class UnitExpertiseProfiles
{
    public static readonly UnitExpertiseProfile Worker =
        new("Worker", true, UnitBehaviorCatalog.WorkerFsm, 0, 0.20, 1);

    public static readonly UnitExpertiseProfile Zealot =
        new("Zealot", false, UnitBehaviorCatalog.CombatFsm, 1, 0.25, 2);

    public static readonly UnitExpertiseProfile Marine =
        new("Marine", false, UnitBehaviorCatalog.CombatFsm, 5, 0.30, 4);

    public static readonly UnitExpertiseProfile Zergling =
        new("Zergling", false, UnitBehaviorCatalog.CombatFsm, 1, 0.20, 6);

    public static IReadOnlyList<UnitExpertiseProfile> InitialSet =>
        [Worker, Zealot, Marine, Zergling];
}
