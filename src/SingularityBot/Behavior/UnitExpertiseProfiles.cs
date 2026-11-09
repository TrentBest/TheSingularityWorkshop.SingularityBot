namespace SingularityBot.Behavior;

/// <summary>
/// Initial tuning hypotheses for the first expertise set. These values are
/// provisional and must be calibrated against replay evidence. Capability
/// composition lives independently in UnitDefinitions.
/// </summary>
public sealed record UnitExpertiseProfile(
    string UnitKind,
    bool IsWorker,
    int PreferredEngagementRange,
    double RetreatHealthFraction,
    int PreferredGroupSize);

public static class UnitExpertiseProfiles
{
    public static readonly UnitExpertiseProfile SCV =
        new("SCV", true, 0, 0.20, 1);

    public static readonly UnitExpertiseProfile Probe =
        new("Probe", true, 0, 0.20, 1);

    public static readonly UnitExpertiseProfile Drone =
        new("Drone", true, 0, 0.20, 1);

    public static readonly UnitExpertiseProfile Zealot =
        new("Zealot", false, 1, 0.25, 2);

    public static readonly UnitExpertiseProfile Marine =
        new("Marine", false, 5, 0.30, 4);

    public static readonly UnitExpertiseProfile Marauder =
        new("Marauder", false, 6, 0.35, 3);

    public static readonly UnitExpertiseProfile Zergling =
        new("Zergling", false, 1, 0.20, 6);

    public static IReadOnlyList<UnitExpertiseProfile> InitialSet =>
        [SCV, Probe, Drone, Zealot, Marine, Marauder, Zergling];

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
