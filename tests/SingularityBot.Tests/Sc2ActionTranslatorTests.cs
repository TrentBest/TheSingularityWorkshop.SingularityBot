using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2ActionTranslatorTests
{
    [Fact]
    public void GatherIntentTargetsNearestVisibleMineralField()
    {
        var observation = Snapshot(
            Fact(10, "SCV", UnitAlliance.Self, 2, 2),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 8, 2),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 3, 2));

        var actions = Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.AssignWorkerToMinerals) },
            observation,
            3666);

        var command = Assert.Single(actions).ActionRaw.UnitCommand;
        Assert.Equal(21UL, command.TargetUnitTag);
        Assert.Equal(3666, command.AbilityId);
        Assert.Equal(new[] { 10UL }, command.UnitTags);
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
            3666,
            3667);

        var command = Assert.Single(actions).ActionRaw.UnitCommand;
        Assert.Equal(21UL, command.TargetUnitTag);
        Assert.Equal(3667, command.AbilityId);
        Assert.Equal(new[] { 10UL }, command.UnitTags);
    }

    [Fact]
    public void ReturnCargoIntentFailsClosedWithoutReturnAbilityOrTownHall()
    {
        var loadedWorker = Snapshot(Fact(10, "SCV", UnitAlliance.Self, 2, 2, hasCargo: true));
        var noAbility = Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.ReturnCarriedResources) },
            loadedWorker,
            3666);
        Assert.Empty(noAbility);

        var noTownHall = Snapshot(Fact(10, "SCV", UnitAlliance.Self, 2, 2, hasCargo: true));
        var noTarget = Sc2ActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(10, BehaviorCommand.ReturnCarriedResources) },
            noTownHall,
            3666,
            3667);
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
            3666);

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
