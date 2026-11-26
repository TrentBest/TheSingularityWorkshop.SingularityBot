using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotMatchConfigurationTests
{
    [Fact]
    public void DefaultMatchConfigurationPinsTheOnlyFullyExecutableRace()
    {
        var configuration = BotMatchConfiguration.Default;

        Assert.Equal(Race.Terran, configuration.RequestedRace);
        Assert.Equal("AcolyteLE.SC2Map", configuration.MapName);
        Assert.Equal(Race.Random, configuration.OpponentRace);
    }

    [Fact]
    public void DefaultMatchConfigurationMatchesTheCompiledTerranStrategyDefinition()
    {
        var configuration = BotMatchConfiguration.Default;
        var strategy = new AdaptiveMarineStrategy();

        Assert.Equal(Race.Terran, configuration.RequestedRace);
        Assert.Equal("Terran", strategy.Definition.Race);
    }

    [Fact]
    public void ExplicitTerranMarineModeRequestsTerranForDeterministicBaselineRuns()
    {
        var configuration = BotMatchConfiguration.ForMode("terran.marine.v1");

        Assert.Equal(Race.Terran, configuration.RequestedRace);
        Assert.Equal("AcolyteLE.SC2Map", configuration.MapName);
        Assert.Equal(Race.Random, configuration.OpponentRace);
    }

    [Fact]
    public void ExperimentalZerglingModeRequestsZergWithoutClaimingGeneralZergSupport()
    {
        var configuration = BotMatchConfiguration.ForMode("zerg.zergling.v1");

        Assert.Equal(Race.Zerg, configuration.RequestedRace);
    }

    [Fact]
    public void ExperimentalProtossZealotModeRequestsProtoss()
    {
        var configuration = BotMatchConfiguration.ForMode("protoss.zealot.v1");
        Assert.Equal(Race.Protoss, configuration.RequestedRace);
    }

    [Theory]
    [InlineData("zerg.queen.v1")]
    [InlineData("terran.marauder.v1")]
    [InlineData("")]
    public void UnsupportedModesAreRejectedBeforeStartingAMatch(string modeId)
    {
        var exception = Assert.Throws<ArgumentException>(() => BotMatchConfiguration.ForMode(modeId));

        Assert.Contains("Currently selectable", exception.Message);
    }
}
