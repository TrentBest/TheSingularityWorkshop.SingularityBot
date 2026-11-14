using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotMatchConfigurationTests
{
    [Fact]
    public void DefaultMatchConfigurationRequestsRandomRaceWithoutHardCodingItInRunner()
    {
        var configuration = BotMatchConfiguration.Default;

        Assert.Equal(Race.Random, configuration.RequestedRace);
        Assert.Equal("AcolyteLE.SC2Map", configuration.MapName);
        Assert.Equal(Race.Random, configuration.OpponentRace);
    }

    [Fact]
    public void RacePreferenceIsIndependentFromTheCompiledTerranStrategyDefinition()
    {
        var configuration = BotMatchConfiguration.Default;
        var strategy = new AdaptiveMarineStrategy();

        Assert.Equal(Race.Random, configuration.RequestedRace);
        Assert.Equal("Terran", strategy.Definition.Race);
    }
}
