using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>Describes whether a race module is connected to executable gameplay
/// or is currently a diagnostic-only scaffold.</summary>
public enum RaceModuleExecutionMode
{
    LegacyTerranRuntime,
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
    public bool CanIssueGameplayActions =>
        ExecutionMode == RaceModuleExecutionMode.LegacyTerranRuntime;
}

/// <summary>
/// Trusted in-process catalogue. Protoss and Zerg are deliberately registered
/// now so race selection is explicit and diagnosable, while their gameplay
/// adapters are built. No race silently falls back to Terran.
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

    public bool TryResolve(Race actualRace, out RaceModuleDescriptor module) =>
        _modules.TryGetValue(actualRace, out module);

    public static RaceModuleCatalog CreateDefault() => new(new[]
    {
        new RaceModuleDescriptor(
            Race.Terran,
            "sc2.race.terran.v1",
            RaceModuleExecutionMode.LegacyTerranRuntime,
            new[] { "units.terran", "structures.terran", "production.terran", "abilities.terran" },
            "Existing Terran runtime; migration behind the race-module boundary is in progress."),
        new RaceModuleDescriptor(
            Race.Protoss,
            "sc2.race.protoss.scaffold.v1",
            RaceModuleExecutionMode.LoggingOnly,
            new[] { "units.protoss", "structures.protoss", "production.protoss", "abilities.protoss" },
            "Registered scaffold only. Capability catalogue and gameplay adapter are not implemented."),
        new RaceModuleDescriptor(
            Race.Zerg,
            "sc2.race.zerg.scaffold.v1",
            RaceModuleExecutionMode.LoggingOnly,
            new[] { "units.zerg", "structures.zerg", "production.zerg", "abilities.zerg" },
            "Registered scaffold only. Capability catalogue and gameplay adapter are not implemented.")
    });
}
