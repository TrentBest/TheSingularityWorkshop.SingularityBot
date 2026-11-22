namespace SingularityBot;

/// <summary>
/// Immutable read-only telemetry published by the bot runtime for local
/// authoring and inspection clients. It contains observations, never commands.
/// </summary>
public sealed record BotStudioSnapshot(
    long Sequence,
    DateTimeOffset CapturedAtUtc,
    string Lifecycle,
    string MapName,
    uint GameLoop,
    string ActualRace,
    string ActiveModeId,
    string GamePhase,
    uint Minerals,
    uint Vespene,
    uint FoodUsed,
    uint FoodCap,
    int VisibleFriendlyUnits,
    int VisibleEnemyUnits,
    int VisibleWorkers,
    int BuildJobCount,
    int StrategicJobCount);
