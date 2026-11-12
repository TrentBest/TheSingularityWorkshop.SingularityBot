using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class MarineFormationPlannerTests
{
    [Theory]
    [InlineData(MarineFormation.Box)]
    [InlineData(MarineFormation.Wedge)]
    [InlineData(MarineFormation.Line)]
    [InlineData(MarineFormation.Column)]
    [InlineData(MarineFormation.HalfCircle)]
    public void ProducesOneDistinctFiniteSlotPerMarine(MarineFormation formation)
    {
        var offsets = MarineFormationPlanner.GetOffsets(formation, 4, 1, 0);
        Assert.Equal(4, offsets.Count);
        Assert.Equal(4, offsets.Distinct().Count());
        Assert.All(offsets, point =>
        {
            Assert.True(float.IsFinite(point.X));
            Assert.True(float.IsFinite(point.Y));
        });
    }

    [Fact]
    public void BoxUsesTwoRowsAndTwoColumns()
    {
        var offsets = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4, 1, 0);
        Assert.Equal(2, offsets.Select(point => point.X).Distinct().Count());
        Assert.Equal(2, offsets.Select(point => point.Y).Distinct().Count());
    }

    [Fact]
    public void BoxKeepsExistingMarineSlotsStableWhenReinforcementsArrive()
    {
        var four = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4, 1, 0);
        var six = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 6, 1, 0);

        Assert.Equal(four, six.Take(4));
        Assert.Equal(6, six.Distinct().Count());
    }

    [Fact]
    public void RotatesFormationWithTravelDirection()
    {
        var east = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4, 1, 0);
        var north = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4, 0, 1);
        Assert.Equal(east.Select(point => (point.X, point.Y)).ToArray(),
            north.Select(point => (point.Y, -point.X)).ToArray());
    }

    [Fact]
    public void MarchingMovesLeadingEdgeForwardAndFollowersToFrontMarinePositions()
    {
        var positions = new[]
        {
            (X: 10f, Y: 20f),
            (X: 10f, Y: 18f),
            (X: 12f, Y: 18f),
            (X: 10f, Y: 16f)
        };
        // The first two Marines form the leading edge; the next row follows
        // the corresponding Marine ahead rather than targeting that unit.
        var targets = MarineFormationPlanner.GetMarchingTargets(
            positions, new[] { -1, -1, 0, 1 }, 1, 0, advanceDistance: 2f);

        Assert.Equal((12f, 20f), targets[0]);
        Assert.Equal((12f, 18f), targets[1]);
        Assert.Equal(positions[0], targets[2]);
        Assert.Equal(positions[1], targets[3]);
    }

    [Fact]
    public void MarchingRotatesLeadingEdgeAdvanceWithTravelDirection()
    {
        var targets = MarineFormationPlanner.GetMarchingTargets(
            new[] { (X: 3f, Y: 4f) }, new[] { -1 }, 0, 2, advanceDistance: 3f);

        Assert.Equal((3f, 7f), targets[0]);
    }

    [Fact]
    public void MarchingRejectsForwardOrSelfReferences()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MarineFormationPlanner.GetMarchingTargets(
                new[] { (X: 0f, Y: 0f), (X: 1f, Y: 0f) },
                new[] { -1, 1 }, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MarineFormationPlanner.GetMarchingTargets(
                new[] { (X: 0f, Y: 0f), (X: 1f, Y: 0f) },
                new[] { -1, 2 }, 1, 0));
    }

}