namespace SingularityBot.Behavior;

/// <summary>
/// Top-level match lifecycle phase owned by the bot host. This is distinct
/// from <see cref="GamePhase"/>, which represents strategic early/mid/late game.
/// </summary>
public enum MatchLifecyclePhase
{
    NotStarted,
    EarlyGame
}
