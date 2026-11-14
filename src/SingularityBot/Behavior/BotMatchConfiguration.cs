using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>
/// Match-level choices supplied to the runner. Race preference is deliberately
/// separate from a compiled strategy's civilization: Random is a request to SC2,
/// not a claim that every civilization strategy is already implemented.
/// </summary>
public sealed record BotMatchConfiguration(
    Race RequestedRace,
    string MapName,
    Race OpponentRace,
    Difficulty OpponentDifficulty)
{
    /// <summary>The default match asks SC2 to select the bot's race.</summary>
    public static BotMatchConfiguration Default { get; } = new(
        RequestedRace: Race.Random,
        MapName: "AcolyteLE.SC2Map",
        OpponentRace: Race.Random,
        OpponentDifficulty: Difficulty.VeryEasy);
}
