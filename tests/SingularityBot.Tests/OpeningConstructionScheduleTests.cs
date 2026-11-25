using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class OpeningConstructionScheduleTests
{
    [Fact]
    public void FirstDepotBuilderPullIsTriggeredAtNineGameSeconds()
    {
        Assert.Equal(202u, OpeningConstructionSchedule.FirstDepotPullGameLoop);
        Assert.False(OpeningConstructionSchedule.ShouldPullFirstDepotBuilder(201));
        Assert.True(OpeningConstructionSchedule.ShouldPullFirstDepotBuilder(202));
        Assert.True(OpeningConstructionSchedule.ShouldPullFirstDepotBuilder(203));
    }

    [Fact]
    public void EstimatesTravelAndBuildCompletionInGameLoops()
    {
        Assert.Equal(75u, OpeningConstructionSchedule.EstimateTravelGameLoops(10, 2.9867f));
        Assert.Equal(112u, OpeningConstructionSchedule.EstimateRemainingBuildGameLoops(10f, 0.5f));
    }

    [Fact]
    public void ArrivalMustOverlapPredictedAffordability()
    {
        var affordableAt = OpeningConstructionSchedule.EstimateAffordableGameLoop(
            currentGameLoop: 100,
            currentMinerals: 80,
            requiredMinerals: 100,
            mineralIncomePerGameLoop: 0.25);
        Assert.Equal(180u, affordableAt);
        Assert.False(OpeningConstructionSchedule.ShouldDispatchForArrival(100, 70, affordableAt!.Value));
        Assert.True(OpeningConstructionSchedule.ShouldDispatchForArrival(100, 80, affordableAt.Value));
    }

    [Fact]
    public void DoesNotInventAnAffordabilityTimeWithoutMeasuredIncome()
    {
        Assert.Null(OpeningConstructionSchedule.EstimateAffordableGameLoop(
            currentGameLoop: 100,
            currentMinerals: 80,
            requiredMinerals: 100,
            mineralIncomePerGameLoop: 0));
    }

    [Fact]
    public void MineralIncomeEstimatorUsesPositiveBankDeltasAndDecaysOnSpending()
    {
        var estimator = new MineralIncomeRateEstimator();
        estimator.Observe(0, 50);
        estimator.Observe(22, 60);
        Assert.Equal(10d / 22d, estimator.MineralsPerGameLoop, 6);

        estimator.Observe(44, 55);
        Assert.Equal((10d / 22d) * 0.8d, estimator.MineralsPerGameLoop, 6);
    }
}
