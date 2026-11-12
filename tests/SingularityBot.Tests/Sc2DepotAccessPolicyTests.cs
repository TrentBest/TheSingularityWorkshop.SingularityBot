using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2DepotAccessPolicyTests
{
    [Fact]
    public void ExplicitPassageRequestLowersDepotEvenWhenEnemiesAreNearby()
    {
        Assert.Equal(
            Sc2DepotAccessCommand.Lower,
            Sc2DepotAccessPolicy.Select(
                observedRaised: true,
                observedLowered: false,
                hasNearbyEnemy: true,
                passageRequested: true));
    }

    [Fact]
    public void UncontestedDepotLowersWithoutAPassageRequest()
    {
        Assert.Equal(
            Sc2DepotAccessCommand.Lower,
            Sc2DepotAccessPolicy.Select(
                observedRaised: true,
                observedLowered: false,
                hasNearbyEnemy: false,
                passageRequested: false));
    }

    [Fact]
    public void DepotRaisesWhenThreatIsNearbyAndNoPassageIsPending()
    {
        Assert.Equal(
            Sc2DepotAccessCommand.Raise,
            Sc2DepotAccessPolicy.Select(
                observedRaised: false,
                observedLowered: true,
                hasNearbyEnemy: true,
                passageRequested: false));
    }

    [Fact]
    public void PassageRequestKeepsDepotLoweredDespiteNearbyEnemies()
    {
        Assert.Equal(
            Sc2DepotAccessCommand.None,
            Sc2DepotAccessPolicy.Select(
                observedRaised: false,
                observedLowered: true,
                hasNearbyEnemy: true,
                passageRequested: true));
    }

    [Fact]
    public void UnknownOrContradictoryDepotStateFailsClosed()
    {
        Assert.Equal(
            Sc2DepotAccessCommand.None,
            Sc2DepotAccessPolicy.Select(false, false, false, false));
        Assert.Equal(
            Sc2DepotAccessCommand.None,
            Sc2DepotAccessPolicy.Select(true, true, false, true));
    }
}
