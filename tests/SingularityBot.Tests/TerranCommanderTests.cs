using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class TerranCommanderTests
{
    [Fact]
    public void Commander_OwnsTerranPolicySpecialists()
    {
        var commander = new TerranCommander();

        Assert.NotNull(commander.Economy);
        Assert.NotNull(commander.Intelligence);
        Assert.NotNull(commander.Scouting);
        Assert.NotSame(commander.Economy, new TerranCommander().Economy);
    }

    [Fact]
    public void Commander_ExposesExistingConfigurablePolicySettings()
    {
        var commander = new TerranCommander();

        Assert.NotNull(commander.Economy.Settings);
        Assert.NotNull(commander.Intelligence.Settings);
    }
}
