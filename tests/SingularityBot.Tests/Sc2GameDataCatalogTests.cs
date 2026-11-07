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
        data.Abilities.Add(new AbilityData { AbilityId = 3666, LinkName = "Harvest_Gather" });
        data.Abilities.Add(new AbilityData { AbilityId = 3667, LinkName = "Harvest_Return" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal("SCV", catalog.UnitNames[45]);
        Assert.Equal("MineralField750", catalog.UnitNames[341]);
        Assert.Equal("CarryingMinerals", catalog.BuffNames[271]);
        Assert.Equal((uint)3666, catalog.HarvestGatherAbilityId);
        Assert.Equal((uint)3667, catalog.HarvestReturnAbilityId);
    }

    [Fact]
    public void Create_LeavesMissingAbilityMetadataUnavailable()
    {
        var catalog = Sc2GameDataCatalog.Create(new ResponseData());

        Assert.Empty(catalog.UnitNames);
        Assert.Empty(catalog.BuffNames);
        Assert.Null(catalog.HarvestGatherAbilityId);
        Assert.Null(catalog.HarvestReturnAbilityId);
    }
}
