namespace SingularityBot.Behavior;

/// <summary>
/// Predictive, observation-driven timing helpers for opening construction.
/// Resource forecasts are advisory; the live mineral bank and SC2 action
/// acceptance remain the final execution gates.
/// </summary>
public static class OpeningConstructionSchedule
{
    public const double GameLoopsPerSecond = 22.4d;

    /// <summary>
    /// First observation at or after nine game seconds (ceil(9 * 22.4)).
    /// </summary>
    public const uint FirstDepotPullGameLoop = 202;

    public static bool ShouldPullFirstDepotBuilder(uint currentGameLoop) =>
        currentGameLoop >= FirstDepotPullGameLoop;

    public static uint EstimateTravelGameLoops(double distance, float movementSpeed)
    {
        if (!double.IsFinite(distance) || distance < 0 ||
            !float.IsFinite(movementSpeed) || movementSpeed <= 0)
            return 0;

        var loops = Math.Ceiling(distance / movementSpeed * GameLoopsPerSecond);
        return (uint)Math.Clamp(loops, 0d, uint.MaxValue);
    }

    public static uint EstimateRemainingBuildGameLoops(float buildTimeSeconds, float buildProgress)
    {
        if (!float.IsFinite(buildTimeSeconds) || buildTimeSeconds <= 0 ||
            !float.IsFinite(buildProgress))
            return 0;

        var remainingSeconds = buildTimeSeconds * Math.Clamp(1f - buildProgress, 0f, 1f);
        var loops = Math.Ceiling(remainingSeconds * GameLoopsPerSecond);
        return (uint)Math.Clamp(loops, 0d, uint.MaxValue);
    }

    public static uint? EstimateAffordableGameLoop(
        uint currentGameLoop,
        uint currentMinerals,
        uint requiredMinerals,
        double mineralIncomePerGameLoop)
    {
        if (currentMinerals >= requiredMinerals)
            return currentGameLoop;
        if (!double.IsFinite(mineralIncomePerGameLoop) || mineralIncomePerGameLoop <= 0)
            return null;

        var deficit = requiredMinerals - currentMinerals;
        var loops = Math.Ceiling(deficit / mineralIncomePerGameLoop);
        if (!double.IsFinite(loops) || loops > uint.MaxValue - currentGameLoop)
            return null;
        return currentGameLoop + (uint)Math.Max(0, loops);
    }

    /// <summary>
    /// Dispatch when the worker can reach the site no earlier than the predicted
    /// resource/prerequisite start window. The actual Build command still checks
    /// live resources, placement, and worker arrival.
    /// </summary>
    public static bool ShouldDispatchForArrival(
        uint currentGameLoop,
        uint travelGameLoops,
        uint desiredArrivalGameLoop) =>
        (ulong)currentGameLoop + travelGameLoops >= desiredArrivalGameLoop;

}

/// <summary>
/// Conservative net-income estimator. Positive mineral-bank deltas update the
/// rate; spending or flat windows decay it rather than treating costs as income.
/// </summary>
public sealed class MineralIncomeRateEstimator
{
    private const uint MinimumSampleWindowGameLoops = 22;
    private uint? _sampleGameLoop;
    private uint _sampleMinerals;

    public double MineralsPerGameLoop { get; private set; }

    public void Observe(uint gameLoop, uint minerals)
    {
        if (_sampleGameLoop is not uint previousLoop)
        {
            _sampleGameLoop = gameLoop;
            _sampleMinerals = minerals;
            return;
        }

        if (gameLoop <= previousLoop || gameLoop - previousLoop < MinimumSampleWindowGameLoops)
            return;

        var elapsed = gameLoop - previousLoop;
        var gained = minerals > _sampleMinerals ? minerals - _sampleMinerals : 0u;
        var measuredRate = (double)gained / elapsed;
        if (measuredRate > 0)
            MineralsPerGameLoop = MineralsPerGameLoop <= 0
                ? measuredRate
                : MineralsPerGameLoop * 0.65d + measuredRate * 0.35d;
        else
            MineralsPerGameLoop *= 0.80d;

        _sampleGameLoop = gameLoop;
        _sampleMinerals = minerals;
    }
}
