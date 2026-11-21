using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotRaceSelectionTests
{
    private static readonly BotRaceSelectionContext Context =
        new(Race.Zerg, Difficulty.Hard, "AcolyteLE.SC2Map");

    [Fact]
    public void RandomSelection_UsesOnlyDeclaredRacesWithExecutableAdapters()
    {
        var definition = new BotRaceSelectionDefinition
        {
            Policy = BotRaceSelectionPolicy.Random,
            EligibleRaces = new[] { Race.Terran, Race.Protoss, Race.Zerg }
        };

        var result = new BotRaceSelectionResolver().Resolve(
            definition, RaceModuleCatalog.CreateDefault().ExecutableRaces, Context, _ => 0);

        Assert.Equal(Race.Terran, result.SelectedRace);
        Assert.Equal(new[] { Race.Terran }, result.EligibleExecutableRaces);
    }

    [Theory]
    [InlineData(0, Race.Terran)]
    [InlineData(1, Race.Zerg)]
    public void RandomSelection_CanSelectEveryExecutableRace(int index, Race expected)
    {
        var definition = new BotRaceSelectionDefinition
        {
            Policy = BotRaceSelectionPolicy.Random,
            EligibleRaces = new[] { Race.Terran, Race.Protoss, Race.Zerg }
        };

        var result = new BotRaceSelectionResolver().Resolve(
            definition, RaceModuleCatalog.CreateDefault().ExecutableRaces, Context, _ => index);

        Assert.Equal(expected, result.SelectedRace);
    }

    [Fact]
    public void FixedSelection_RejectsRaceWithoutExecutableAdapter()
    {
        var definition = new BotRaceSelectionDefinition
        {
            Policy = BotRaceSelectionPolicy.Fixed,
            FixedRace = Race.Protoss,
            EligibleRaces = new[] { Race.Terran, Race.Protoss }
        };

        Assert.Throws<InvalidOperationException>(() =>
            new BotRaceSelectionResolver().Resolve(definition, new[] { Race.Terran }, Context));
    }

    [Fact]
    public void CustomSelection_ReceivesBoundedOpponentContextAndExecutableAllowList()
    {
        var selector = new OpponentAwareSelector();
        var definition = new BotRaceSelectionDefinition
        {
            Policy = BotRaceSelectionPolicy.Custom,
            EligibleRaces = new[] { Race.Terran, Race.Protoss, Race.Zerg },
            SelectorId = selector.Id
        };

        var result = new BotRaceSelectionResolver(new[] { selector }).Resolve(
            definition, RaceModuleCatalog.CreateDefault().ExecutableRaces, Context);

        Assert.Equal(Race.Zerg, result.SelectedRace);
        Assert.Equal(BotRaceSelectionPolicy.Custom, result.Policy);
        Assert.Equal(selector.Id, result.SelectorId);
        Assert.Equal(Race.Zerg, selector.ObservedOpponentRace);
    }

    [Fact]
    public void CustomSelection_RejectsUnregisteredSelector()
    {
        var definition = new BotRaceSelectionDefinition
        {
            Policy = BotRaceSelectionPolicy.Custom,
            EligibleRaces = new[] { Race.Terran },
            SelectorId = "missing.selector.v1"
        };

        Assert.Throws<InvalidOperationException>(() =>
            new BotRaceSelectionResolver().Resolve(definition, new[] { Race.Terran }, Context));
    }

    [Fact]
    public void CustomSelection_RejectsRaceOutsideExecutableAllowList()
    {
        var selector = new FixedSelector(Race.Protoss);
        var definition = new BotRaceSelectionDefinition
        {
            Policy = BotRaceSelectionPolicy.Custom,
            EligibleRaces = new[] { Race.Terran, Race.Protoss },
            SelectorId = selector.Id
        };

        Assert.Throws<InvalidOperationException>(() =>
            new BotRaceSelectionResolver(new[] { selector }).Resolve(
                definition, new[] { Race.Terran }, Context));
    }

    [Fact]
    public void RandomSelection_RejectsOutOfRangeRandomIndex()
    {
        var definition = new BotRaceSelectionDefinition
        {
            Policy = BotRaceSelectionPolicy.Random,
            EligibleRaces = new[] { Race.Terran }
        };

        Assert.Throws<InvalidOperationException>(() =>
            new BotRaceSelectionResolver().Resolve(definition, new[] { Race.Terran }, Context, _ => 1));
    }

    [Fact]
    public void DefinitionValidation_RejectsDuplicateAndNonPlayableRaceValues()
    {
        var duplicate = new BotRaceSelectionDefinition
        {
            Policy = BotRaceSelectionPolicy.Random,
            EligibleRaces = new[] { Race.Terran, Race.Terran }
        };
        var invalid = new BotRaceSelectionDefinition
        {
            Policy = BotRaceSelectionPolicy.Random,
            EligibleRaces = new[] { Race.Terran, (Race)999 }
        };

        Assert.Throws<InvalidOperationException>(() =>
            new BotRaceSelectionResolver().Resolve(duplicate, new[] { Race.Terran }, Context));
        Assert.Throws<InvalidOperationException>(() =>
            new BotRaceSelectionResolver().Resolve(invalid, new[] { Race.Terran }, Context));
    }

    private sealed class OpponentAwareSelector : IBotRaceSelector
    {
        public string Id => "test.opponent-aware.v1";
        public Race? ObservedOpponentRace { get; private set; }

        public Race SelectRace(BotRaceSelectionContext context, IReadOnlyList<Race> eligibleRaces)
        {
            ObservedOpponentRace = context.OpponentRace;
            return eligibleRaces.Contains(context.OpponentRace) ? context.OpponentRace : eligibleRaces[0];
        }
    }

    private sealed class FixedSelector(Race selectedRace) : IBotRaceSelector
    {
        public string Id => "test.fixed.v1";
        public Race SelectRace(BotRaceSelectionContext context, IReadOnlyList<Race> eligibleRaces) => selectedRace;
    }
}
