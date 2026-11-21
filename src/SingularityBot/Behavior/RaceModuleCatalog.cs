using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>Describes which race-specific gameplay path is currently executable.</summary>
public enum RaceModuleExecutionMode
{
    LegacyTerranRuntime,
    ExperimentalOpener,
    LoggingOnly
}

/// <summary>Race-specific capability boundary selected from authoritative
/// RaceActual at match initialization.</summary>
public sealed record RaceModuleDescriptor(
    Race Race,
    string Id,
    RaceModuleExecutionMode ExecutionMode,
    IReadOnlyList<string> Capabilities,
    string Status)
{
    /// <summary>True only after the race opener passes deterministic and controlled-match acceptance.</summary>
    public bool IsRaceSelectionEligible { get; init; }

    public bool CanIssueGameplayActions =>
        ExecutionMode is RaceModuleExecutionMode.LegacyTerranRuntime or RaceModuleExecutionMode.ExperimentalOpener;
}

/// <summary>
/// Trusted in-process catalogue. Only races with a live gameplay path are
/// exposed as executable candidates; diagnostic scaffolds remain unavailable.
/// </summary>
public sealed class RaceModuleCatalog
{
    private readonly IReadOnlyDictionary<Race, RaceModuleDescriptor> _modules;

    private RaceModuleCatalog(IEnumerable<RaceModuleDescriptor> modules)
    {
        var indexed = modules.ToDictionary(module => module.Race);
        _modules = new System.Collections.ObjectModel.ReadOnlyDictionary<Race, RaceModuleDescriptor>(indexed);
    }

    public IReadOnlyCollection<RaceModuleDescriptor> Modules => _modules.Values.ToArray();

    /// <summary>Race adapters that can currently issue at least one audited gameplay action.</summary>
    public IReadOnlyList<Race> ExecutableRaces => Modules
        .Where(module => module.CanIssueGameplayActions)
        .Select(module => module.Race)
        .OrderBy(race => race)
        .ToArray();

    /// <summary>Race adapters that have passed the stronger gate for Random/Custom selection.</summary>
    public IReadOnlyList<Race> RaceSelectionEligibleRaces => Modules
        .Where(module => module.IsRaceSelectionEligible && module.CanIssueGameplayActions)
        .Select(module => module.Race)
        .OrderBy(race => race)
        .ToArray();

    public bool TryResolve(Race actualRace, out RaceModuleDescriptor module) =>
        _modules.TryGetValue(actualRace, out module);

    public static RaceModuleCatalog CreateDefault() => new(new[]
    {
        new RaceModuleDescriptor(
            Race.Terran,
            "sc2.race.terran.v1",
            RaceModuleExecutionMode.LegacyTerranRuntime,
            new[] { "units.terran", "structures.terran", "production.terran", "abilities.terran" },
            "Existing Terran runtime; migration behind the race-module boundary is in progress.")
        { IsRaceSelectionEligible = true },
        new RaceModuleDescriptor(
            Race.Protoss,
            "sc2.race.protoss.scaffold.v1",
            RaceModuleExecutionMode.LoggingOnly,
            new[] { "units.protoss", "structures.protoss", "production.protoss", "abilities.protoss" },
            "Registered scaffold only. Capability catalogue and gameplay adapter are not implemented."),
        new RaceModuleDescriptor(
            Race.Zerg,
            "sc2.race.zerg.experimental-opener.v1",
            RaceModuleExecutionMode.ExperimentalOpener,
            new[] { "units.zergling", "production.zergling", "opening.zergling-pair" },
            "Experimental bounded opener: initializes and issues the first Zergling-pair production action; sustainable Zerg economy and follow-on composition are not implemented.")
    });
}
