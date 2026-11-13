using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2RefineryProgressionPolicyTests
{
    [Theory]
    [InlineData(150, 75, 150, false)]
    [InlineData(224, 75, 150, false)]
    [InlineData(225, 75, 150, true)]
    [InlineData(75, 75, 0, true)]
    public void FirstRefineryCanStartWithoutSpendingReservedOrbitalMinerals(
        uint minerals, uint refineryCost, uint orbitalReserve, bool expected) =>
        Assert.Equal(expected, Sc2RefineryProgressionPolicy.CanFundRefinery(
            minerals, refineryCost, orbitalReserve));

    [Fact]
    public void HoldsFirstRefineryUntilOrbitalMorphOrderIsObserved()
    {
        Assert.False(Sc2RefineryProgressionPolicy.ShouldConstruct(
            firstBarracksComplete: true,
            orbitalMorphingObserved: false,
            completedRefineryCount: 0,
            thirdSupplyDepotComplete: false,
            refineryUnderConstruction: false));

        Assert.True(Sc2RefineryProgressionPolicy.ShouldConstruct(
            firstBarracksComplete: true,
            orbitalMorphingObserved: true,
            completedRefineryCount: 0,
            thirdSupplyDepotComplete: false,
            refineryUnderConstruction: false));
    }

    [Fact]
    public void KeepsSecondRefineryBehindOrbitalAndThirdDepot()
    {
        Assert.False(Sc2RefineryProgressionPolicy.ShouldConstruct(
            true, false, 1, false, false));
        Assert.False(Sc2RefineryProgressionPolicy.ShouldConstruct(
            true, true, 1, false, false));
        Assert.True(Sc2RefineryProgressionPolicy.ShouldConstruct(
            true, true, 1, true, false));
    }

    [Fact]
    public void DoesNotStartRefineryBeforeBarracksOrDuplicateActiveWork()
    {
        Assert.False(Sc2RefineryProgressionPolicy.ShouldConstruct(
            false, false, 0, false, false));
        Assert.False(Sc2RefineryProgressionPolicy.ShouldConstruct(
            true, true, 0, false, true));
        Assert.False(Sc2RefineryProgressionPolicy.ShouldConstruct(
            true, true, 2, true, false));
    }
}
