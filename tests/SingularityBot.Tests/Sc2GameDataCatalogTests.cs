using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2GameDataCatalogTests
{
    [Fact]
    public void Create_UsesAuthoritativeGameDataForUnitBuffAndWorkerAbilityIds()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 45, Name = "SCV" });
        data.Units.Add(new UnitTypeData { UnitId = 341, Name = "MineralField750" });
        data.Buffs.Add(new BuffData { BuffId = 271, Name = "CarryingMinerals" });
        data.Abilities.Add(new AbilityData { AbilityId = 1, LinkName = "Smart" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal("SCV", catalog.UnitNames[45]);
        Assert.Equal("MineralField750", catalog.UnitNames[341]);
        Assert.Equal("CarryingMinerals", catalog.BuffNames[271]);
        Assert.Equal((uint)1, catalog.SmartAbilityId);
    }

    [Fact]
    public void Create_ResolvesSmartCommandFromAuthoritativeGameData()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 1, LinkName = "Smart" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)1, catalog.SmartAbilityId);
    }

    [Fact]
    public void Create_LeavesMissingAbilityMetadataUnavailable()
    {
        var catalog = Sc2GameDataCatalog.Create(new ResponseData());

        Assert.Empty(catalog.UnitNames);
        Assert.Empty(catalog.BuffNames);
        Assert.Null(catalog.SmartAbilityId);
    }
}
