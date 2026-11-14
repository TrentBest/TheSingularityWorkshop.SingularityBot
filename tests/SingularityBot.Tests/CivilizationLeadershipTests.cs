using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class CivilizationLeadershipTests
{
    [Theory]
    [InlineData(Civilization.Terran, "Admiral", "Commander", "Commanders")]
    [InlineData(Civilization.Zerg, "Overmind", "Cerebrate", "Cerebrates")]
    [InlineData(Civilization.Protoss, "Conclave", "Judicator", "Judicators")]
    public void Catalog_ProvidesDistinctCampaignInspiredLeadership(
        Civilization civilization,
        string supremeAuthority,
        string subordinateAuthority,
        string subordinateAuthorityPlural)
    {
        var leadership = CivilizationLeadershipCatalog.Get(civilization);

        Assert.Equal(civilization, leadership.Civilization);
        Assert.Equal(supremeAuthority, leadership.SupremeAuthority);
        Assert.Equal(subordinateAuthority, leadership.SubordinateAuthority);
        Assert.Equal(subordinateAuthorityPlural, leadership.SubordinateAuthorityPlural);
    }

    [Fact]
    public void SharedStrategicDomains_AreRaceIndependent()
    {
        Assert.Contains(StrategicDomain.Economy, Enum.GetValues<StrategicDomain>());
        Assert.Contains(StrategicDomain.Military, Enum.GetValues<StrategicDomain>());
        Assert.Contains(StrategicDomain.Intelligence, Enum.GetValues<StrategicDomain>());
        Assert.Contains(StrategicDomain.Technology, Enum.GetValues<StrategicDomain>());
        Assert.Contains(StrategicDomain.Expansion, Enum.GetValues<StrategicDomain>());
        Assert.Contains(StrategicDomain.Defense, Enum.GetValues<StrategicDomain>());
        Assert.Contains(StrategicDomain.Logistics, Enum.GetValues<StrategicDomain>());
    }

    [Fact]
    public void Catalog_RejectsUnknownCivilization()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CivilizationLeadershipCatalog.Get((Civilization)int.MaxValue));
    }
}
