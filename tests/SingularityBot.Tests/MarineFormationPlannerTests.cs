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
}