using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>
/// A named, race-bound gameplay "What" selected by a map/experience. The mode
/// identifies the primary combat unit and strategy family; it is not a unit
/// whitelist and may include supporting units.
/// </summary>
public sealed record BotModeDescriptor(
    string Id,
    string Name,
    Race Race,
    string PrimaryUnit,
    string StrategyProfile,
    string Status);

/// <summary>Portable map selection. The host supplies this with the selected map.</summary>
public sealed record BotModeSelection(string ModeId);

/// <summary>
/// Trusted compile-time catalogue of available race/mode combinations. Selection
/// is validated against RaceActual at match start; there is never a cross-race fallback.
/// </summary>
public sealed class BotModeCatalog
{
    private readonly IReadOnlyDictionary<string, BotModeDescriptor> _modes;

    public BotModeCatalog(IEnumerable<BotModeDescriptor> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        var indexed = modes.ToDictionary(mode => mode.Id, StringComparer.Ordinal);
        if (indexed.Count == 0)
            throw new ArgumentException("At least one bot mode must be registered.", nameof(modes));
        if (indexed.Values.Any(mode => string.IsNullOrWhiteSpace(mode.Id) ||
            string.IsNullOrWhiteSpace(mode.Name) || string.IsNullOrWhiteSpace(mode.PrimaryUnit) ||
            string.IsNullOrWhiteSpace(mode.StrategyProfile)))
            throw new ArgumentException("Mode identity, name, primary unit and strategy profile are required.", nameof(modes));
        _modes = new System.Collections.ObjectModel.ReadOnlyDictionary<string, BotModeDescriptor>(indexed);
    }

    public IReadOnlyCollection<BotModeDescriptor> Modes => _modes.Values
        .OrderBy(mode => mode.Race).ThenBy(mode => mode.Name, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<BotModeDescriptor> GetModes(Race race) => Modes
        .Where(mode => mode.Race == race).ToArray();

    public bool TryResolve(string modeId, out BotModeDescriptor mode) =>
        _modes.TryGetValue(modeId ?? string.Empty, out mode);

    /// <summary>Resolve an explicit map selection against the authoritative in-game race.</summary>
    public bool TryResolveForMatch(
        BotModeSelection selection,
        Race actualRace,
        out BotModeDescriptor mode,
        out string error)
    {
        mode = null;
        if (selection is null || string.IsNullOrWhiteSpace(selection.ModeId))
        {
            error = "A map must explicitly select a bot mode.";
            return false;
        }
        if (!_modes.TryGetValue(selection.ModeId, out var selected))
        {
            error = $"Unknown bot mode '{selection.ModeId}'.";
            return false;
        }
        if (selected.Race != actualRace)
        {
            error = $"Bot mode '{selected.Id}' requires race {selected.Race}, but the match race is {actualRace}.";
            return false;
        }

        mode = selected;
        error = string.Empty;
        return true;
    }

    public static BotModeCatalog CreateDefault() => new(new[]
    {
        new BotModeDescriptor("terran.marine.v1", "Marine Mode", Race.Terran,
            "Marine", "terran.marine-economy.v1", "Existing implemented Terran strategy."),
        new BotModeDescriptor("terran.marauder.v1", "Marauder Mode", Race.Terran,
            "Marauder", "terran.marauder-pressure.v1", "Mode selection registered; dedicated production and tactics are pending."),
        new BotModeDescriptor("protoss.zealot.v1", "Zealot Mode", Race.Protoss,
            "Zealot", "protoss.zealot-pressure.v1", "Mode selection registered; production and tactics are pending."),
        new BotModeDescriptor("protoss.stalker.v1", "Stalker Mode", Race.Protoss,
            "Stalker", "protoss.stalker-control.v1", "Mode selection registered; production and tactics are pending."),
        new BotModeDescriptor("zerg.zergling.v1", "Zergling Mode", Race.Zerg,
            "Zergling", "zerg.zergling-pressure.v1", "Mode selection registered; production and tactics are pending."),
        new BotModeDescriptor("zerg.queen.v1", "Queen Mode", Race.Zerg,
            "Queen", "zerg.queen-control.v1", "Mode selection registered; production and tactics are pending.")
    });
}
