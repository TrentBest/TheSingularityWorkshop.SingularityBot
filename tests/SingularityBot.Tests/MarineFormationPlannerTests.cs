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


    [Fact]
    public void RoleAwareFormationKeepsVanguardAheadOfLineAndArtilleryAtRear()
    {
        var roles = new[]
        {
            CombatFormationRole.Line,
            CombatFormationRole.Artillery,
            CombatFormationRole.Vanguard,
            CombatFormationRole.Line
        };

        var offsets = MarineFormationPlanner.GetRoleAwareOffsets(roles, 1, 0);

        Assert.Equal(4, offsets.Distinct().Count());
        Assert.True(offsets[2].X > offsets[0].X);
        Assert.True(offsets[0].X > offsets[1].X);
        Assert.Equal(-1f, offsets[0].Y);
        Assert.Equal(-4f, offsets[1].X);
    }

    [Fact]
    public void RoleAwareFormationRotatesWithMarchDirection()
    {
        var roles = new[] { CombatFormationRole.Vanguard, CombatFormationRole.Artillery };
        var east = MarineFormationPlanner.GetRoleAwareOffsets(roles, 1, 0);
        var north = MarineFormationPlanner.GetRoleAwareOffsets(roles, 0, 1);

        Assert.Equal(east[0].X, north[0].Y);
        Assert.Equal(-east[0].Y, north[0].X);
        Assert.True(east[0].X > east[1].X);
    }

    [Fact]
    public void RoleAwareSpacingCanCompressAndImmediatelyOpenTheFormation()
    {
        var roles = Enumerable.Repeat(CombatFormationRole.Line, 4).ToArray();
        var compact = MarineFormationPlanner.GetRoleAwareOffsets(roles, 1, 0, slotSpacing: 1f);
        var spread = MarineFormationPlanner.GetRoleAwareOffsets(roles, 1, 0, slotSpacing: 4f);

        Assert.Equal(1f, MathF.Abs(compact[1].Y - compact[2].Y));
        Assert.Equal(4f, MathF.Abs(spread[1].Y - spread[2].Y));
        Assert.Equal(12f, spread.Max(point => point.Y) - spread.Min(point => point.Y));
    }

    [Fact]
    public void LineThreatEscapeStepsPerpendicularIntoThePreferredGap()
    {
        var moved = MarineFormationPlanner.TryGetLineThreatEscapeTarget(
            (5f, 0.2f), (0f, 0f), (10f, 0f), 2f, (5f, 5f), out var target);

        Assert.True(moved);
        Assert.Equal(5f, target.X);
        Assert.Equal(2f, target.Y);
    }

    [Fact]
    public void LineThreatEscapeDoesNothingWhenMarineIsAlreadyClear()
    {
        var moved = MarineFormationPlanner.TryGetLineThreatEscapeTarget(
            (5f, 3f), (0f, 0f), (10f, 0f), 2f, (5f, 5f), out _);

        Assert.False(moved);
    }

    [Fact]
    public void LineThreatEscapeRotatesWithTheAttackLine()
    {
        var moved = MarineFormationPlanner.TryGetLineThreatEscapeTarget(
            (0.2f, 5f), (0f, 0f), (0f, 10f), 2f, (5f, 5f), out var target);

        Assert.True(moved);
        Assert.Equal(2f, target.X);
        Assert.Equal(5f, target.Y);
    }

    [Fact]
    public void RoleAwareFormationRejectsInvalidSpacingAndDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MarineFormationPlanner.GetRoleAwareOffsets(new[] { CombatFormationRole.Line }, 1, 0, 0));
        Assert.Throws<ArgumentException>(() =>
            MarineFormationPlanner.GetRoleAwareOffsets(new[] { CombatFormationRole.Line }, 0, 0));
    }

}