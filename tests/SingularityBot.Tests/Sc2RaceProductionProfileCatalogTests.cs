using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2RaceProductionProfileCatalogTests
{
    [Fact]
    public void ZerglingProfile_ResolvesTargetProducerAndPrerequisiteFromAuthoritativeData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 105, Name = "Zergling", AbilityId = TrainAbilityId(), MineralCost = 25, FoodRequired = 0.5f });
        data.Units.Add(new UnitTypeData { UnitId = 151, Name = "Larva" });
        data.Units.Add(new UnitTypeData { UnitId = 89, Name = "SpawningPool", AbilityId = 1162, MineralCost = 200 });
        var catalog = Sc2GameDataCatalog.Create(data);
        var profiles = Sc2RaceProductionProfileCatalog.CreateDefault();

        Assert.True(profiles.TryResolveGameData(
            "zerg.zergling.v1", Race.Zerg, catalog,
            out var profile, out var target, out var producer, out var error));
        Assert.Equal("Zergling", target.Name);
        Assert.Equal("Larva", producer.Name);
        Assert.Equal("SpawningPool", Assert.Single(profile.RequiredCompletedUnits));
        Assert.Empty(error);
    }

    [Fact]
    public void ZealotProfile_RequiresPowerEvidenceAndRejectsWrongRace()
    {
        var profiles = Sc2RaceProductionProfileCatalog.CreateDefault();
        Assert.True(profiles.TryResolve("protoss.zealot.v1", out var profile));
        Assert.True(profile.RequiresPoweredProducer);

        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 73, Name = "Zealot", AbilityId = 916, MineralCost = 100, FoodRequired = 2 });
        data.Units.Add(new UnitTypeData { UnitId =  gatewayUnitId(), Name = "Gateway" });
        data.Units.Add(new UnitTypeData { UnitId = 60, Name = "Pylon", AbilityId = 881, MineralCost = 100, FoodProvided = 8 });
        Assert.False(profiles.TryResolveGameData(
            "protoss.zealot.v1", Race.Zerg, Sc2GameDataCatalog.Create(data),
            out _, out _, out _, out var error));
        Assert.Contains("requires race Protoss", error);
    }

    [Fact]
    public void ZerglingProfile_FailsClosedWhenPrerequisiteIsAbsent()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 105, Name = "Zergling", AbilityId = 1343, MineralCost = 25 });
        data.Units.Add(new UnitTypeData { UnitId = 151, Name = "Larva" });

        Assert.False(Sc2RaceProductionProfileCatalog.CreateDefault().TryResolveGameData(
            "zerg.zergling.v1", Race.Zerg, Sc2GameDataCatalog.Create(data),
            out _, out _, out _, out var error));
        Assert.Contains("SpawningPool", error);
    }

    private static uint TrainAbilityId() => 1343;
    private static uint gatewayUnitId() => 62;
}
