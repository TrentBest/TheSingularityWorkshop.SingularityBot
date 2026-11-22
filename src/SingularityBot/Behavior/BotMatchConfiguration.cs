using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>
/// Match-level choices supplied to the runner. Race preference is deliberately
/// aligned with the compiled definition until every declared race has an
/// executable adapter. Race.Random must not dispatch into a race-specific strategy.
/// </summary>
public sealed record BotMatchConfiguration(
    Race RequestedRace,
    string MapName,
    Race OpponentRace,
    Difficulty OpponentDifficulty)
{
    /// <summary>The default match is pinned to the only fully executable race adapter.</summary>
    public static BotMatchConfiguration Default { get; } = new(
        RequestedRace: Race.Terran,
        MapName: "AcolyteLE.SC2Map",
        OpponentRace: Race.Random,
        OpponentDifficulty: Difficulty.VeryEasy);

    /// <summary>
    /// Creates a deterministic match request for an explicitly selected,
    /// currently selectable mode. The selected race does not imply that the
    /// race has a complete economy or sustainable composition adapter.
    /// </summary>
    public static BotMatchConfiguration ForMode(string modeId) => modeId switch
    {
        "terran.marine.v1" => Default with { RequestedRace = Race.Terran },
        "zerg.zergling.v1" => Default with { RequestedRace = Race.Zerg },
        "protoss.zealot.v1" => Default with { RequestedRace = Race.Protoss },
        _ => throw new ArgumentException($"Unsupported runner mode '{modeId}'. Currently selectable: terran.marine.v1, zerg.zergling.v1.", nameof(modeId))
    };
}
