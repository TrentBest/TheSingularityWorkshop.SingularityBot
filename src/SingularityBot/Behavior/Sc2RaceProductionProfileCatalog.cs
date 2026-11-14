using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>
/// Race/mode-specific production intent. Names are semantic lookups into the
/// match's authoritative ResponseData; this catalogue contains no SC2 numeric IDs.
/// </summary>
public sealed record Sc2RaceProductionProfile(
    string ModeId,
    Race Race,
    string TargetUnit,
    string ProducerUnit,
    IReadOnlyList<string> RequiredCompletedUnits,
    bool RequiresPoweredProducer,
    string Status)
{
    /// <summary>Units created by one accepted producer order (Zerglings are produced in pairs).</summary>
    public int UnitsPerOrder { get; init; } = 1;
}

/// <summary>
/// Trusted production profile catalogue for the first-unit milestones. A profile
/// is actionable only when every unit/ability fact resolves uniquely from game data.
/// </summary>
public sealed class Sc2RaceProductionProfileCatalog
{
    private readonly IReadOnlyDictionary<string, Sc2RaceProductionProfile> _profiles;

    public Sc2RaceProductionProfileCatalog(IEnumerable<Sc2RaceProductionProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var indexed = profiles.ToDictionary(profile => profile.ModeId, StringComparer.Ordinal);
        if (indexed.Values.Any(profile =>
            string.IsNullOrWhiteSpace(profile.ModeId) ||
            string.IsNullOrWhiteSpace(profile.TargetUnit) ||
            string.IsNullOrWhiteSpace(profile.ProducerUnit) ||
            profile.RequiredCompletedUnits is null ||
            profile.RequiredCompletedUnits.Any(string.IsNullOrWhiteSpace)))
            throw new ArgumentException("Production profile identities and unit requirements are required.", nameof(profiles));
        _profiles = new System.Collections.ObjectModel.ReadOnlyDictionary<string, Sc2RaceProductionProfile>(indexed);
    }

    public IReadOnlyCollection<Sc2RaceProductionProfile> Profiles => _profiles.Values
        .OrderBy(profile => profile.Race).ThenBy(profile => profile.ModeId, StringComparer.Ordinal).ToArray();

    public bool TryResolve(string modeId, out Sc2RaceProductionProfile profile) =>
        _profiles.TryGetValue(modeId ?? string.Empty, out profile);

    /// <summary>
    /// Checks that all required unit types and the target's training ability are
    /// uniquely present in the authoritative match data. It does not issue actions.
    /// </summary>
    public bool TryResolveGameData(
        string modeId,
        Race actualRace,
        Sc2GameDataCatalog gameData,
        out Sc2RaceProductionProfile profile,
        out Sc2UnitFacts target,
        out Sc2UnitFacts producer,
        out string error)
    {
        profile = null;
        target = null;
        producer = null;
        ArgumentNullException.ThrowIfNull(gameData);

        if (!TryResolve(modeId, out var selected))
        {
            error = $"No production profile is registered for mode '{modeId}'.";
            return false;
        }
        if (selected.Race != actualRace)
        {
            error = $"Production profile '{modeId}' requires race {selected.Race}, not {actualRace}.";
            return false;
        }
        if (!gameData.TryGetUniqueUnit(selected.TargetUnit, out target) ||
            target.AbilityId == 0 || target.MineralCost == 0)
        {
            error = $"Target unit '{selected.TargetUnit}' or its training ability/cost is missing or ambiguous in match game data.";
            target = null;
            return false;
        }
        if (!gameData.TryGetUniqueUnit(selected.ProducerUnit, out producer))
        {
            error = $"Producer unit '{selected.ProducerUnit}' is missing or ambiguous in match game data.";
            target = null;
            return false;
        }
        foreach (var prerequisite in selected.RequiredCompletedUnits)
        {
            if (!gameData.TryGetUniqueUnit(prerequisite, out _))
            {
                error = $"Required prerequisite '{prerequisite}' is missing or ambiguous in match game data.";
                target = null;
                producer = null;
                return false;
            }
        }

        profile = selected;
        error = string.Empty;
        return true;
    }

    public static Sc2RaceProductionProfileCatalog CreateDefault() => new(new[]
    {
        new Sc2RaceProductionProfile(
            "zerg.zergling.v1", Race.Zerg, "Zergling", "Larva",
            new[] { "SpawningPool" }, false,
            "First Zerg production path; requires a completed Spawning Pool and an available Larva.")
            { UnitsPerOrder = 2 },
        new Sc2RaceProductionProfile(
            "protoss.zealot.v1", Race.Protoss, "Zealot", "Gateway",
            new[] { "Pylon" }, true,
            "First Protoss production path; Gateway power must be positively confirmed before issuing."),
        new Sc2RaceProductionProfile(
            "protoss.stalker.v1", Race.Protoss, "Stalker", "Gateway",
            new[] { "Pylon", "CyberneticsCore" }, true,
            "Stalker production requires a completed Cybernetics Core and powered Gateway."),
        new Sc2RaceProductionProfile(
            "zerg.queen.v1", Race.Zerg, "Queen", "Hatchery",
            Array.Empty<string>(), false,
            "Queen production uses a completed Hatchery/Lair/Hive production context; adapter pending.")
    });
}
