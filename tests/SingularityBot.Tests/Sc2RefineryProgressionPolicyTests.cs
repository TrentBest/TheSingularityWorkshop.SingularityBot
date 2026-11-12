using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2RefineryProgressionPolicyTests
{
    [Fact]
    public void StartsFirstRefineryAfterBarracksWithoutWaitingForOrbital()
    {
        Assert.True(Sc2RefineryProgressionPolicy.ShouldConstruct(
            firstBarracksComplete: true,
            orbitalMorphingObserved: false,
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
            true, false, 0, false, true));
        Assert.False(Sc2RefineryProgressionPolicy.ShouldConstruct(
            true, true, 2, true, false));
    }
}
