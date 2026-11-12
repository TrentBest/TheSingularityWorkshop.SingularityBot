using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2ActionTranslatorTests
{
    [Fact]
    public void GatherIntentUsesThePersistentMineralReservation()
    {
        var observation = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 2, 2),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 8, 2),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 3, 2));
        var assignments = new WorkerResourceAssignmentPlanner().Reconcile(observation);

        var actions = Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.AssignWorkerToMinerals) },
            observation,
            1,
            workerAssignments: assignments);

        var command = Assert.Single(actions).ActionRaw.UnitCommand;
        Assert.Equal(21UL, command.TargetUnitTag);
        Assert.Equal(1, command.AbilityId);
        Assert.Equal(new[] { 10UL }, command.UnitTags);
        Assert.False(command.QueueCommand);
    }

    [Fact]
    public void RecoversAnIdleAssignedWorkerWithoutInterruptingActiveWorkers()
    {
        var observation = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 2, 2),
            Fact(11, "SCV", UnitAlliance.Self, 3, 2, hasCargo: true),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 8, 2));
        var assignments = new WorkerResourceAssignmentPlanner().Reconcile(observation);

        var actions = Sc2ActionTranslator.Translate(
            Array.Empty<KeyValuePair<ulong, BehaviorCommand>>(),
            observation,
            1,
            workerAssignments: assignments);

        Assert.Equal(2, actions.Count);
        var gather = Assert.Single(actions.Select(action => action.ActionRaw.UnitCommand)
            .Where(command => command.UnitTags.Single() == 10));
        Assert.Equal(20UL, gather.TargetUnitTag);
        Assert.Equal(1, gather.AbilityId);

        var returnCargo = Assert.Single(actions.Select(action => action.ActionRaw.UnitCommand)
            .Where(command => command.UnitTags.Single() == 11));
        Assert.Equal(1UL, returnCargo.TargetUnitTag);
        Assert.Equal(1, returnCargo.AbilityId);
    }

    [Fact]
    public void ReturnCargoIntentTargetsNearestVisibleFriendlyTownHall()
    {
        var observation = Snapshot(
            Fact(10, "SCV", UnitAlliance.Self, 2, 2, hasCargo: true),
            Fact(20, "CommandCenter", UnitAlliance.Self, 8, 2),
            Fact(21, "CommandCenter", UnitAlliance.Self, 3, 2));

        var actions = Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.ReturnCarriedResources) },
            observation,
            1);

        var command = Assert.Single(actions).ActionRaw.UnitCommand;
        Assert.Equal(21UL, command.TargetUnitTag);
        Assert.Equal(1, command.AbilityId);
        Assert.Equal(new[] { 10UL }, command.UnitTags);
        Assert.False(command.QueueCommand);
    }

    [Fact]
    public void ReturnCargoIntentOverridesAnObservedMineralGatherOrder()
    {
        var loadedWorker = Fact(10, "SCV", UnitAlliance.Self, 2, 2, hasCargo: true) with
        {
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(295, 20, null, null) }
        };
        var observation = Snapshot(
            loadedWorker,
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 8, 2));

        var actions = Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.ReturnCarriedResources) },
            observation,
            1);

        var command = Assert.Single(actions).ActionRaw.UnitCommand;
        Assert.Equal(1UL, command.TargetUnitTag);
        Assert.Equal(1, command.AbilityId);
        Assert.Equal(new[] { 10UL }, command.UnitTags);
        Assert.False(command.QueueCommand);
    }

    [Fact]
    public void ReturnCargoIntentFailsClosedWithoutSmartAbilityOrTownHall()
    {
        var loadedWorker = Snapshot(Fact(10, "SCV", UnitAlliance.Self, 2, 2, hasCargo: true));
        var noAbility = Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.ReturnCarriedResources) },
            loadedWorker,
            null);
        Assert.Empty(noAbility);

        var noTownHall = Snapshot(Fact(10, "SCV", UnitAlliance.Self, 2, 2, hasCargo: true));
        var noTarget = Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.ReturnCarriedResources) },
            noTownHall,
            1);
        Assert.Empty(noTarget);
    }

    [Fact]
    public void GatherIntentRejectsMissingOrNonFriendlyWorkerAndStaleMinerals()
    {
        var observation = new NormalizedObservation(
            100, 1,
            new[]
            {
                Fact(10, "SCV", UnitAlliance.Self, 2, 2),
                Fact(20, "MineralField750", UnitAlliance.Neutral, 3, 2)
            },
            new HashSet<ulong> { 20 });

        var actions = Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.AssignWorkerToMinerals) },
            observation,
            1);

        Assert.Empty(actions);
    }

    [Fact]
    public void GatherIntentFailsClosedWhenAbilityMetadataIsMissing()
    {
        var observation = Snapshot(
            Fact(10, "SCV", UnitAlliance.Self, 2, 2),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 3, 2));

        Assert.Empty(Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.AssignWorkerToMinerals) },
            observation,
            null));
    }

    private static NormalizedObservation Snapshot(params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Fact(
        ulong tag, string kind, UnitAlliance alliance, float x, float y, bool hasCargo = false) =>
        new(tag, kind == "SCV" ? 45u : kind == "CommandCenter" ? 18u : 341u, kind, alliance == UnitAlliance.Self ? 1 : 0,
            alliance, UnitVisibility.Visible, 45, 45, 0, 0, 1, x, y, true,
            hasCargo, false, false, false, false, 100);
}
