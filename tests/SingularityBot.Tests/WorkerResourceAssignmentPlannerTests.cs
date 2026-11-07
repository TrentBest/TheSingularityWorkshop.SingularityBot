using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class WorkerResourceAssignmentPlannerTests
{
    [Fact]
    public void AssignsWorkersDeterministicallyAndBalancesMineralSaturation()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var observation = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(11, "SCV", UnitAlliance.Self, 2, 0),
            Fact(12, "SCV", UnitAlliance.Self, 3, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0));

        var assignments = planner.Reconcile(observation);

        Assert.Equal(3, assignments.Count);
        Assert.Equal(20UL, assignments[10].MineralTag);
        Assert.Equal(21UL, assignments[11].MineralTag);
        Assert.Equal(20UL, assignments[12].MineralTag);
        Assert.Equal(1u, assignments[10].Generation);
        Assert.Equal(2u, assignments[11].Generation);
        Assert.Equal(3u, assignments[12].Generation);
    }

    [Fact]
    public void KeepsReservationAndGenerationWhenWorkerTemporarilyDisappears()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initial = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0));

        var first = planner.Reconcile(initial)[10];
        var next = new NormalizedObservation(
            110, 1,
            new[]
            {
                Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 110),
                Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0, loop: 110)
            },
            new HashSet<ulong>());

        var retained = planner.Reconcile(next);

        Assert.Equal(first, retained[10]);
        Assert.Equal(first.Generation, retained[10].Generation);
    }

    [Fact]
    public void RetainsReservationWhenMineralIsTemporarilyAbsentFromObservation()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var first = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0)))[10];

        var temporarilyUnobserved = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 120),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0, loop: 120)));

        Assert.Equal(first, temporarilyUnobserved[10]);
    }

    [Fact]
    public void ReassignsWhenReservedMineralDiesAndAdvancesGeneration()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initial = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0));

        var first = planner.Reconcile(initial)[10];
        var afterDeath = new NormalizedObservation(
            120, 1,
            new[]
            {
                Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 120),
                Fact(10, "SCV", UnitAlliance.Self, 1, 0, loop: 120),
                Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0, loop: 120)
            },
            new HashSet<ulong> { 20 });

        var reassigned = planner.Reconcile(afterDeath)[10];

        Assert.Equal(21UL, reassigned.MineralTag);
        Assert.NotEqual(first.Generation, reassigned.Generation);
    }

    [Fact]
    public void ReassignsWorkerWhenItsCurrentOrderTargetsItsExplicitlyDeadAssignedMineral()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var first = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0)))[10];

        var afterDeath = new NormalizedObservation(
            120, 1,
            new[]
            {
                Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 120),
                Fact(10, "SCV", UnitAlliance.Self, 1, 0, hasOrders: true, loop: 120,
                    orders: new[] { new UnitOrderObservationFacts(3666, 20, null, null) }),
                Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0, loop: 120)
            },
            new HashSet<ulong> { 20 });

        var reassigned = planner.Reconcile(afterDeath)[10];

        Assert.Equal(21UL, reassigned.MineralTag);
        Assert.NotEqual(first.Generation, reassigned.Generation);
    }

    [Fact]
    public void DoesNotOverrideAnUnrelatedActiveWorkerOrderWhenAssignedMineralDies()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0)));

        var afterDeath = new NormalizedObservation(
            120, 1,
            new[]
            {
                Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 120),
                Fact(10, "SCV", UnitAlliance.Self, 1, 0, hasOrders: true, loop: 120,
                    orders: new[] { new UnitOrderObservationFacts(9999, 500, null, null) }),
                Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0, loop: 120)
            },
            new HashSet<ulong> { 20 });

        Assert.Empty(planner.Reconcile(afterDeath));
    }

    [Fact]
    public void DoesNotAssignWithoutTownHallOrBeyondMineralCapacity()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var noTownHall = Snapshot(
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0));
        Assert.Empty(planner.Reconcile(noTownHall));

        var saturated = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(11, "SCV", UnitAlliance.Self, 2, 0),
            Fact(12, "SCV", UnitAlliance.Self, 3, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0));
        Assert.Equal(2, planner.Reconcile(saturated, mineralSaturation: 2).Count);
    }

    [Fact]
    public void ExistingAssignmentsAreNotRecomputedFromWorkerMovement()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var first = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0)))[10];

        var moved = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 130),
            Fact(10, "SCV", UnitAlliance.Self, 9, 0, loop: 130),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0, loop: 130),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0, loop: 130)))[10];

        Assert.Equal(first.MineralTag, moved.MineralTag);
        Assert.Equal(first.Generation, moved.Generation);
        Assert.Equal(first.WorkerX, moved.WorkerX);
    }

    private static NormalizedObservation Snapshot(params UnitObservationFacts[] units) =>
        new(units.Length == 0 ? 0u : units.Max(unit => unit.GameLoop), 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Fact(
        ulong tag, string kind, UnitAlliance alliance, float x, float y,
        bool hasOrders = false, uint loop = 100,
        IReadOnlyList<UnitOrderObservationFacts> orders = null) =>
        new UnitObservationFacts(tag, 1, kind, alliance == UnitAlliance.Self ? 1 : 0, alliance,
            UnitVisibility.Visible, 45, 45, 0, 0, 1, x, y, true,
            false, hasOrders, false, false, false, loop)
        {
            Orders = orders ?? Array.Empty<UnitOrderObservationFacts>()
        };
}
