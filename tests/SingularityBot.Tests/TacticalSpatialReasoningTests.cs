using SingularityBot.Spatial.Reasoning;
using Xunit;

namespace SingularityBot.Tests;

public sealed class TacticalSpatialReasoningTests
{
    [Fact]
    public void SplashSpacingReportsWorstCaseUnitsHit()
    {
        var clustered = new[] { new SpatialPoint(0, 0), new SpatialPoint(1, 0), new SpatialPoint(8, 0) };
        var score = FormationSpacing.Evaluate(clustered, 2);

        Assert.Equal(3, score.UnitCount);
        Assert.Equal(2, score.WorstCaseUnitsHit);
        Assert.Equal(1, score.AdditionalUnitsHit);
        Assert.False(score.IsSingleHitOnly);
    }

    [Fact]
    public void FormationCanBeSpacedSoOneSplashStrikeTouchesOnlyItsTarget()
    {
        var spread = new[] { new SpatialPoint(0, 0), new SpatialPoint(3, 0), new SpatialPoint(6, 0) };
        var score = FormationSpacing.Evaluate(spread, 2);

        Assert.True(score.IsSingleHitOnly);
        Assert.Equal(1, score.WorstCaseUnitsHit);
        Assert.Equal(3, score.MinimumSeparation);
    }

    [Fact]
    public void SeparatedPositionMovesAwayFromNearestFormationMember()
    {
        var result = FormationSpacing.FindMostSeparatedPosition(
            new SpatialPoint(0, 0),
            new[] { new SpatialPoint(1, 0), new SpatialPoint(-4, 0), new SpatialPoint(0, 2) },
            new[] { new SpatialPoint(0, 0), new SpatialPoint(0, 1) });

        Assert.Equal(new SpatialPoint(-4, 0), result);
    }

    [Fact]
    public void PlatformScoreRewardsDefensibleFeaturesAndMakesDirectnessAWeightedPrior()
    {
        var strong = new PlatformProfile(1, 1, 0.7f, 0.2f, 1);
        var weak = new PlatformProfile(4, 0.2f, 0.3f, 0.9f, 0.2f);

        var strongScore = PlatformReasoning.Assess(strong, 4, PlatformScoreWeights.DefensiveTerran);
        var weakScore = PlatformReasoning.Assess(weak, 4, PlatformScoreWeights.DefensiveTerran);

        Assert.True(strongScore.Score > weakScore.Score);
        Assert.Equal(strong, strongScore.Profile);
    }

    [Fact]
    public void PlatformFeaturesMustBeNormalized()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlatformReasoning.Assess(new PlatformProfile(1, 1.2f, 0.5f, 0.5f, 0.5f), 4,
                PlatformScoreWeights.DefensiveTerran));
    }
}
