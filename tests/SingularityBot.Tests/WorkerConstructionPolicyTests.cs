using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class WorkerConstructionPolicyTests
{
    [Fact]
    public void TerranBuilderRemainsCommittedAndMustDepositCargo()
    {
        var policy = WorkerConstructionPolicy.For(Race.Terran);

        Assert.Equal(WorkerConstructionDisposition.RemainsUntilComplete, policy.Disposition);
        Assert.False(policy.CanBeginConstruction(isCarryingResources: true));
        Assert.True(policy.CanBeginConstruction(isCarryingResources: false));
    }

    [Fact]
    public void ProtossProbeResumesImmediatelyAndIsNotHeldAtConstructionSite()
    {
        var policy = WorkerConstructionPolicy.For(Race.Protoss);

        Assert.Equal(WorkerConstructionDisposition.ResumesImmediately, policy.Disposition);
        Assert.True(policy.CanBeginConstruction(isCarryingResources: true));
    }

    [Fact]
    public void ZergDroneIsConsumedAndMustNeverConstructWithResourcesInHand()
    {
        var policy = WorkerConstructionPolicy.For(Race.Zerg);

        Assert.Equal(WorkerConstructionDisposition.Consumed, policy.Disposition);
        Assert.False(policy.CanBeginConstruction(isCarryingResources: true));
        Assert.True(policy.CanBeginConstruction(isCarryingResources: false));
    }

    [Theory]
    [InlineData(Race.Random)]
    [InlineData(Race.NoRace)]
    public void RejectsNonConcreteRace(Race race)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkerConstructionPolicy.For(race));
    }
}
