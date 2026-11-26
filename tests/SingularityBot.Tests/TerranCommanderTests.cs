using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class RaceCompositionTests
{
    [Fact]
    public void Factory_CreatesDistinctRaceOwnedImplementations()
    {
        var zerg = RaceRuntimeFactory.Create("Zerg");
        var terran = RaceRuntimeFactory.Create("Terran");
        var protoss = RaceRuntimeFactory.Create("Protoss");

        Assert.IsAssignableFrom<IZerg>(zerg);
        Assert.IsAssignableFrom<ITerran>(terran);
        Assert.IsAssignableFrom<IProtoss>(protoss);
        Assert.Equal(Race.Zerg, zerg.Race);
        Assert.Equal(Race.Terran, terran.Race);
        Assert.Equal(Race.Protoss, protoss.Race);
        Assert.NotSame(zerg, terran);
        Assert.NotSame(terran, protoss);
    }

    [Fact]
    public void TerranRuntime_OwnsItsRaceSpecificPolicySpecialists()
    {
        var terran = Assert.IsType<SingularityTerran>(RaceRuntimeFactory.Create("Terran"));

        Assert.NotNull(terran.Economy);
        Assert.NotNull(terran.Intelligence);
        Assert.NotNull(terran.Scouting);
        Assert.NotSame(terran.Economy, Assert.IsType<SingularityTerran>(RaceRuntimeFactory.Create("Terran")).Economy);
        Assert.NotNull(terran.Economy.Settings);
        Assert.NotNull(terran.Intelligence.Settings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Random")]
    [InlineData("Protossish")]
    public void Factory_RejectsUnknownOrImplicitRace(string race)
    {
        Assert.Throws<ArgumentException>(() => RaceRuntimeFactory.Create(race));
    }
}
