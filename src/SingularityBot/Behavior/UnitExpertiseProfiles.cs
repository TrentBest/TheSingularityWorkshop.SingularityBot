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
    // Race-specific worker identities share a behavior graph; the SC2 adapter
    // supplies race-specific gather/build abilities and resource semantics.
    public static readonly UnitExpertiseProfile SCV =
        new("SCV", true, UnitBehaviorCatalog.WorkerFsm, 0, 0.20, 1);

    public static readonly UnitExpertiseProfile Probe =
        new("Probe", true, UnitBehaviorCatalog.WorkerFsm, 0, 0.20, 1);

    public static readonly UnitExpertiseProfile Drone =
        new("Drone", true, UnitBehaviorCatalog.WorkerFsm, 0, 0.20, 1);

    public static readonly UnitExpertiseProfile Zealot =
        new("Zealot", false, UnitBehaviorCatalog.CombatFsm, 1, 0.25, 2);

    public static readonly UnitExpertiseProfile Marine =
        new("Marine", false, UnitBehaviorCatalog.CombatFsm, 5, 0.30, 4);

    public static readonly UnitExpertiseProfile Zergling =
        new("Zergling", false, UnitBehaviorCatalog.CombatFsm, 1, 0.20, 6);

    public static IReadOnlyList<UnitExpertiseProfile> InitialSet =>
        [SCV, Probe, Drone, Zealot, Marine, Zergling];

    private static readonly IReadOnlyDictionary<string, UnitExpertiseProfile> ByKind =
        InitialSet.ToDictionary(profile => profile.UnitKind, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string unitKind, out UnitExpertiseProfile profile)
    {
        if (string.IsNullOrWhiteSpace(unitKind))
        {
            profile = null!;
            return false;
        }

        return ByKind.TryGetValue(unitKind, out profile!);
    }
}
