using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2WorkerProgressWatchdogTests
{
    [Fact]
    public void ReissuesMoveWhenScvHasNotChangedPositionForOneSecond()
    {
        var watchdog = new Sc2WorkerProgressWatchdog();
        var first = Snapshot(0, Worker(1, 10, 10, 20, 20));
        Assert.Empty(watchdog.TryCreateActions(first, 16));

        var stalled = Snapshot(22, Worker(1, 10, 10, 20, 20));
        var action = Assert.Single(watchdog.TryCreateActions(stalled, 16));
        var command = action.ActionRaw.UnitCommand;
        Assert.Equal(16, command.AbilityId);
        Assert.Equal(new[] { 1UL }, command.UnitTags);
        Assert.Equal(20f, command.TargetWorldSpacePos.X);
        Assert.Equal(20f, command.TargetWorldSpacePos.Y);
    }

    [Fact]
    public void DoesNotReissueWhenScvIsMakingProgress()
    {
        var watchdog = new Sc2WorkerProgressWatchdog();
        watchdog.TryCreateActions(Snapshot(0, Worker(1, 10, 10, 20, 20)), 16);
        Assert.Empty(watchdog.TryCreateActions(
            Snapshot(22, Worker(1, 11, 10, 20, 20)), 16));
    }

    [Fact]
    public void DoesNotInterruptMiningOrConstructionOrders()
    {
        var watchdog = new Sc2WorkerProgressWatchdog();
        var mining = Worker(1, 10, 10, 20, 20) with
        {
            Orders = new[] { new UnitOrderObservationFacts(295, 99, null, null) },
            HasOrders = true
        };
        Assert.Empty(watchdog.TryCreateActions(Snapshot(100, mining), 16));
    }

    [Fact]
    public void DoesNotDuplicateAnotherControllersCommand()
    {
        var watchdog = new Sc2WorkerProgressWatchdog();
        watchdog.TryCreateActions(Snapshot(0, Worker(1, 10, 10, 20, 20)), 16);
        Assert.Empty(watchdog.TryCreateActions(
            Snapshot(22, Worker(1, 10, 10, 20, 20)), 16,
            new HashSet<ulong> { 1 }));
    }

    private static NormalizedObservation Snapshot(uint loop, UnitObservationFacts worker) =>
        new(loop, 1, new[] { worker }, new HashSet<ulong>());

    private static UnitObservationFacts Worker(
        ulong tag, float x, float y, float targetX, float targetY) =>
        new(tag, 45, "SCV", 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, x, y, true,
            false, false, false, false, false, 100)
        {
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(16, null, targetX, targetY) }
        };
}
