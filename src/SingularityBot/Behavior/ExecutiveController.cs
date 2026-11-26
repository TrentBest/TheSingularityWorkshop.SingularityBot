namespace SingularityBot.Behavior;

/// <summary>
/// Enforces the match-level decision order: executive reaction, subordinate
/// policy, executive review, then per-unit AI. This is an orchestration guard;
/// it does not create a second game loop or bypass the SC2 action pipeline.
/// </summary>
public sealed class ExecutiveController
{
    private static readonly ExecutiveStage[] OrderedStages =
    {
        ExecutiveStage.ExecutiveReaction,
        ExecutiveStage.SubControllers,
        ExecutiveStage.ExecutiveReview,
        ExecutiveStage.UnitAi
    };

    private int _nextStage;

    public uint GameLoop { get; }
    public ExecutiveStage? CurrentStage =>
        _nextStage == 0 ? null : OrderedStages[_nextStage - 1];

    public ExecutiveController(uint gameLoop) => GameLoop = gameLoop;

    /// <summary>Advances the current observation through exactly one control layer.</summary>
    public void Advance(ExecutiveStage stage)
    {
        if (_nextStage >= OrderedStages.Length)
            throw new InvalidOperationException("The executive cycle is already complete.");

        var expected = OrderedStages[_nextStage];
        if (stage != expected)
            throw new InvalidOperationException(
                $"Invalid executive control order at game loop {GameLoop}: expected {expected}, received {stage}.");

        _nextStage++;
    }

    /// <summary>Verifies that all four layers participated in this observation.</summary>
    public void Complete()
    {
        if (_nextStage != OrderedStages.Length)
            throw new InvalidOperationException(
                $"Executive cycle at game loop {GameLoop} ended after {CurrentStage?.ToString() ?? "no stages"}; " +
                $"{OrderedStages.Length - _nextStage} control stage(s) were not executed.");
    }
}

public enum ExecutiveStage
{
    ExecutiveReaction,
    SubControllers,
    ExecutiveReview,
    UnitAi
}
