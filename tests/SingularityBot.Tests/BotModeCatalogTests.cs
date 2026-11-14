using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotModeCatalogTests
{
    private readonly BotModeCatalog _catalog = BotModeCatalog.CreateDefault();

    [Theory]
    [InlineData(Race.Terran, "terran.marine.v1", "Marine")]
    [InlineData(Race.Terran, "terran.marauder.v1", "Marauder")]
    [InlineData(Race.Protoss, "protoss.zealot.v1", "Zealot")]
    [InlineData(Race.Protoss, "protoss.stalker.v1", "Stalker")]
    [InlineData(Race.Zerg, "zerg.zergling.v1", "Zergling")]
    [InlineData(Race.Zerg, "zerg.queen.v1", "Queen")]
    public void DefaultCatalogue_RegistersTwoModesPerRace(Race race, string id, string primaryUnit)
    {
        Assert.Equal(2, _catalog.GetModes(race).Count);
        Assert.True(_catalog.TryResolve(id, out var mode));
        Assert.Equal(race, mode.Race);
        Assert.Equal(primaryUnit, mode.PrimaryUnit);
        Assert.Equal(id == "terran.marine.v1", mode.CanIssueGameplayActions);
    }

    [Theory]
    [InlineData("zerg.zergling.v1", Race.Zerg)]
    [InlineData("zerg.queen.v1", Race.Zerg)]
    [InlineData("protoss.zealot.v1", Race.Protoss)]
    [InlineData("protoss.stalker.v1", Race.Protoss)]
    public void ExplicitMapSelection_ResolvesOnlyForMatchingActualRace(string id, Race race)
    {
        Assert.True(_catalog.TryResolveForMatch(new BotModeSelection(id), race, out var mode, out var error));
        Assert.Equal(id, mode.Id);
        Assert.Empty(error);
    }

    [Fact]
    public void SingleModeSelectionIsStillExplicitAndStable()
    {
        var singleMode = new BotModeCatalog(new[]
        {
            new BotModeDescriptor("zerg.zergling.v1", "Zergling Mode", Race.Zerg,
                "Zergling", "zerg.zergling.v1", false, "test")
        });

        Assert.False(singleMode.TryResolveForMatch(null, Race.Zerg, out _, out var missing));
        Assert.Contains("explicitly select", missing);
        Assert.True(singleMode.TryResolveForMatch(
            new BotModeSelection("zerg.zergling.v1"), Race.Zerg, out _, out _));
    }

    [Fact]
    public void MapSelection_FailsClosedForWrongRaceOrUnknownMode()
    {
        Assert.False(_catalog.TryResolveForMatch(
            new BotModeSelection("zerg.zergling.v1"), Race.Protoss, out _, out var wrongRace));
        Assert.Contains("requires race Zerg", wrongRace);

        Assert.False(_catalog.TryResolveForMatch(
            new BotModeSelection("zerg.hydralisk.v1"), Race.Zerg, out _, out var unknown));
        Assert.Contains("Unknown bot mode", unknown);
    }
}
