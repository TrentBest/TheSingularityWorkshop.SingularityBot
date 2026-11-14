using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ScoutingAdvisorTests
{
    [Fact]
    public void AssignsDistinctObjectivesToCombatUnitsButNeverScvs()
    {
        var map = new SpatialMap(new MapBounds(0, 0, 90, 90),
            new MapPoint(10, 10), new MapPoint(80, 80));
        var advisor = new ScoutingAdvisor();
        var observation = Snapshot(
            Fact(1, "SCV", 10, 10, 1),
            Fact(2, "Marine", 12, 10, 1),
            Fact(3, "Marine", 13, 10, 1),
            Fact(4, "Marine", 14, 10, 1));

        var assignments = advisor.Plan(observation, map,
            new Dictionary<uint, CombatWeaponProfile>
            {
                [1] = new CombatWeaponProfile(5, 6)
            });

        Assert.Equal(3, assignments.Count);
        Assert.DoesNotContain(1UL, assignments.Keys);
        Assert.Equal(3, assignments.Values.Distinct().Count());
    }

    [Fact]
    public void KeepsAnAssignmentUntilTheUnitReachesItsObjective()
    {
        var map = new SpatialMap(new MapBounds(0, 0, 90, 90),
            new MapPoint(10, 10), new MapPoint(80, 80));
        var advisor = new ScoutingAdvisor();
        var weapons = new Dictionary<uint, CombatWeaponProfile>
        {
            [1] = new CombatWeaponProfile(5, 6)
        };
        var first = advisor.Plan(Snapshot(Fact(2, "Marine", 12, 10, 1)), map, weapons)[2];
        var repeated = advisor.Plan(Snapshot(Fact(2, "Marine", 12, 10, 1)), map, weapons)[2];
        Assert.Equal(first, repeated);

        var next = advisor.Plan(Snapshot(Fact(2, "Marine", first.X, first.Y, 1)), map, weapons)[2];
        Assert.NotEqual(first, next);
    }

    [Fact]
    public void PublishesAssignmentsAsOwnedScoutJobsAndWithdrawsStaleAssignments()
    {
        var map = new SpatialMap(new MapBounds(0, 0, 90, 90),
            new MapPoint(10, 10), new MapPoint(80, 80));
        var advisor = new ScoutingAdvisor();
        var board = new AdvisorJobBoard();
        var weapons = new Dictionary<uint, CombatWeaponProfile>
        {
            [1] = new CombatWeaponProfile(5, 6)
        };

        var published = advisor.PublishJobs(
            Snapshot(Fact(2, "Marine", 12, 10, 1)), map, weapons, board);

        var posting = Assert.Single(published);
        Assert.Equal(StrategicJobKind.Scout, posting.Kind);
        Assert.Equal(StrategicJobSource.Advisor, posting.Source);
        Assert.Equal(nameof(AdvisorDomain.Intelligence), posting.Owner);
        Assert.Equal("2", posting.Constraints!["unitTag"]);
        Assert.Single(new StrategicPlanner().GetPlan(board, 100));

        var withdrawn = advisor.PublishJobs(Snapshot(), map, weapons, board);
        Assert.Empty(withdrawn);
        Assert.Empty(new StrategicPlanner().GetPlan(board, 101));
    }

    private static NormalizedObservation Snapshot(params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Fact(ulong tag, string kind, float x, float y, uint type) =>
        new(tag, type, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, x, y, true, false, false,
            false, false, false, 100);
}
