using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class RaceModuleCatalogTests
{
    [Theory]
    [InlineData(Race.Terran, "sc2.race.terran.v1", RaceModuleExecutionMode.LegacyTerranRuntime, true)]
    [InlineData(Race.Protoss, "sc2.race.protoss.scaffold.v1", RaceModuleExecutionMode.LoggingOnly, false)]
    [InlineData(Race.Zerg, "sc2.race.zerg.experimental-opener.v1", RaceModuleExecutionMode.ExperimentalOpener, true)]
    public void DefaultCatalog_RegistersEveryPlayableRaceWithExplicitExecutionMode(
        Race race, string expectedId, RaceModuleExecutionMode expectedMode, bool canIssueActions)
    {
        var catalog = RaceModuleCatalog.CreateDefault();

        Assert.True(catalog.TryResolve(race, out var module));
        Assert.Equal(expectedId, module.Id);
        Assert.Equal(expectedMode, module.ExecutionMode);
        Assert.Equal(canIssueActions, module.CanIssueGameplayActions);
        Assert.NotEmpty(module.Capabilities);
        Assert.False(string.IsNullOrWhiteSpace(module.Status));
    }

    [Fact]
    public void ExecutableRaces_ContainsOnlyRacesWithLiveGameplayPaths()
    {
        var catalog = RaceModuleCatalog.CreateDefault();

        Assert.Equal(new[] { Race.Terran, Race.Zerg }, catalog.ExecutableRaces);
    }

    [Fact]
    public void DefaultCatalog_DoesNotPretendRandomRaceIsAPlayableModule()
    {
        var catalog = RaceModuleCatalog.CreateDefault();

        Assert.False(catalog.TryResolve(Race.Random, out _));
    }

    [Fact]
    public void ProtossScaffoldDeclaresRaceSpecificCapabilityNamespaces()
    {
        var catalog = RaceModuleCatalog.CreateDefault();

        Assert.True(catalog.TryResolve(Race.Protoss, out var protoss));
        Assert.Contains("units.protoss", protoss.Capabilities);
        Assert.Contains("production.protoss", protoss.Capabilities);
    }

    [Fact]
    public void ZergExperimentalOpenerDoesNotClaimFullRaceSupport()
    {
        var catalog = RaceModuleCatalog.CreateDefault();

        Assert.True(catalog.TryResolve(Race.Zerg, out var zerg));
        Assert.Contains("opening.zergling-pair", zerg.Capabilities);
        Assert.DoesNotContain("economy.zerg", zerg.Capabilities);
        Assert.Contains("sustainable Zerg economy", zerg.Status);
    }
}
