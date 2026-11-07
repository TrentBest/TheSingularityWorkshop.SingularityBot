using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class WorkerRouteContextTests
{
    [Fact]
    public void TracksReservationGenerationAndObservedRoutePhase()
    {
        var context = new UnitBehaviorContext(10, "SCV:10", "SCV", true, _ => { });
        var assignment = new WorkerResourceAssignment(
            10, 20, 1, 7, 100, 1, 1, 5, 1, 0, 0);

        context.Apply(Facts(hasOrders: false, hasCargo: false, loop: 100));
        context.ApplyWorkerAssignment(assignment, 100);

        Assert.Equal(20UL, context.AssignedMineralTag);
        Assert.Equal(1UL, context.AssignedTownHallTag);
        Assert.Equal(7u, context.WorkerAssignmentGeneration);
        Assert.Equal(WorkerRoutePhase.Assigned, context.WorkerRoutePhase);
        Assert.Equal(100u, context.WorkerRoutePhaseChangedAtGameLoop);

        context.Apply(Facts(hasOrders: true, hasCargo: false, loop: 112));
        context.ApplyWorkerAssignment(assignment, 112);
        Assert.Equal(WorkerRoutePhase.ExecutingOrder, context.WorkerRoutePhase);
        Assert.Equal(112u, context.WorkerRoutePhaseChangedAtGameLoop);

        context.Apply(Facts(hasOrders: true, hasCargo: true, loop: 130));
        context.ApplyWorkerAssignment(assignment, 130);
        Assert.Equal(WorkerRoutePhase.CarryingResources, context.WorkerRoutePhase);
        Assert.Equal(130u, context.WorkerRoutePhaseChangedAtGameLoop);
    }

    [Fact]
    public void NewAssignmentGenerationResetsPhaseAndMissingAssignmentClearsRoute()
    {
        var context = new UnitBehaviorContext(10, "SCV:10", "SCV", true, _ => { });
        var first = new WorkerResourceAssignment(10, 20, 1, 1, 100, 1, 1, 5, 1, 0, 0);
        var replacement = first with { MineralTag = 21, Generation = 2 };

        context.Apply(Facts(hasOrders: true, hasCargo: true, loop: 140));
        context.ApplyWorkerAssignment(first, 140);
        Assert.Equal(WorkerRoutePhase.Assigned, context.WorkerRoutePhase);

        context.ApplyWorkerAssignment(replacement, 150);
        Assert.Equal(21UL, context.AssignedMineralTag);
        Assert.Equal(2u, context.WorkerAssignmentGeneration);
        Assert.Equal(WorkerRoutePhase.Assigned, context.WorkerRoutePhase);
        Assert.Equal(150u, context.WorkerRoutePhaseChangedAtGameLoop);

        context.ApplyWorkerAssignment(null, 160);
        Assert.Null(context.AssignedMineralTag);
        Assert.Equal(0u, context.WorkerAssignmentGeneration);
        Assert.Equal(WorkerRoutePhase.Unassigned, context.WorkerRoutePhase);
        Assert.Equal(160u, context.WorkerRoutePhaseChangedAtGameLoop);
    }

    private static UnitObservationFacts Facts(bool hasOrders, bool hasCargo, uint loop) =>
        new(10, 45, "SCV", 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, 1, 1, true, hasCargo, hasOrders,
            false, false, false, loop);
}
