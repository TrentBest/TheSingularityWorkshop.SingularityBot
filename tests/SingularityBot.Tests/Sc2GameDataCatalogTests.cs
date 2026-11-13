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
        data.Abilities.Add(new AbilityData { AbilityId = 1, ButtonName = "Smart", Target = AbilityData.Types.Target.PointOrUnit });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal("SCV", catalog.UnitNames[45]);
        Assert.Equal("MineralField750", catalog.UnitNames[341]);
        Assert.Equal("CarryingMinerals", catalog.BuffNames[271]);
        Assert.Equal((uint)1, catalog.SmartAbilityId);
        Assert.True(catalog.TryGetAbility(1, out var smart));
        Assert.Equal("Smart", smart.ButtonName);
        Assert.Equal(AbilityData.Types.Target.PointOrUnit, smart.Target);
    }

    [Fact]
    public void Create_ResolvesSmartCommandFromAuthoritativeGameData()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 1, ButtonName = "Smart" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)1, catalog.SmartAbilityId);
    }

    [Fact]
    public void Create_ResolvesGenericMoveInsteadOfAmbiguousMoveVariants()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "move", LinkIndex = 0, ButtonName = "Move", RemapsToAbilityId = 3794 });
        data.Abilities.Add(new AbilityData { AbilityId = 17, LinkName = "move", LinkIndex = 1, ButtonName = "MovePatrol", RemapsToAbilityId = 3795 });
        data.Abilities.Add(new AbilityData { AbilityId = 18, LinkName = "move", LinkIndex = 2, ButtonName = "MoveHoldPosition", RemapsToAbilityId = 3793 });
        data.Abilities.Add(new AbilityData { AbilityId = 19, LinkName = "move", LinkIndex = 3, ButtonName = "AcquireMove", RemapsToAbilityId = 3674 });
        data.Abilities.Add(new AbilityData { AbilityId = 20, LinkName = "move", LinkIndex = 4, ButtonName = "Turn" });
        data.Abilities.Add(new AbilityData { AbilityId = 3794, LinkName = "GeneralMove", ButtonName = "Move" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)16, catalog.MoveAbilityId);
    }

    [Fact]
    public void Create_ResolvesCanonicalMoveWhenGenericAliasIsUnavailable()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 3794, LinkName = "GeneralMove", ButtonName = "Move" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)3794, catalog.MoveAbilityId);
    }

    [Fact]
    public void Create_ResolvesGenericStopInsteadOfAmbiguousStopVariants()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 4, LinkName = "stop", ButtonName = "Stop", RemapsToAbilityId = 3665 });
        data.Abilities.Add(new AbilityData { AbilityId = 5, LinkName = "stop", ButtonName = "HoldFireSpecial", RemapsToAbilityId = 3665 });
        data.Abilities.Add(new AbilityData { AbilityId = 9, LinkName = "stop", Target = AbilityData.Types.Target.Unit, RemapsToAbilityId = 3665 });
        data.Abilities.Add(new AbilityData { AbilityId = 3665, LinkName = "Stop", ButtonName = "Stop" });
        data.Abilities.Add(new AbilityData { AbilityId = 3783, LinkName = "BattlecruiserStop", ButtonName = "Stop", RemapsToAbilityId = 3665 });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)3665, catalog.StopAbilityId);
    }


    [Fact]
    public void Create_ResolvesCommandCenterWorkerRallyFromAuthoritativeGameData()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 3690, LinkName = "Rally_Workers" });
        data.Abilities.Add(new AbilityData { AbilityId = 3673, LinkName = "Rally_Units" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)3690, catalog.RallyWorkersAbilityId);
    }

    [Fact]
    public void Create_RetainsAbilityTargetAndPlacementMetadataFromOnStart()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData
        {
            AbilityId = 319,
            LinkName = "TerranBuild",
            LinkIndex = 1,
            ButtonName = "SupplyDepot",
            FriendlyName = "Build SupplyDepot",
            Available = true,
            Target = AbilityData.Types.Target.Point,
            IsBuilding = true,
            FootprintRadius = 1
        });
        data.Abilities.Add(new AbilityData
        {
            AbilityId = 316,
            LinkName = "Repair",
            ButtonName = "Repair",
            Available = true,
            Target = AbilityData.Types.Target.Unit,
            RemapsToAbilityId = 3685
        });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal(2, catalog.AbilitiesById.Count);
        var build = Assert.Single(catalog.FindAbilities("SupplyDepot"));
        Assert.Equal((uint)319, build.AbilityId);
        Assert.Equal(AbilityData.Types.Target.Point, build.Target);
        Assert.True(build.IsBuilding);
        Assert.Equal(1f, build.FootprintRadius);
        Assert.Equal((uint)3685, catalog.AbilitiesById[316].RemapsToAbilityId);
        Assert.Equal(AbilityData.Types.Target.Unit, catalog.AbilitiesById[316].Target);
    }

    [Fact]
    public void Create_ResolvesBarracksAndMarineProductionFactsFromOnStartData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 21, Name = "Barracks", AbilityId = 321, MineralCost = 150 });
        data.Units.Add(new UnitTypeData { UnitId = 48, Name = "Marine", AbilityId = 560, MineralCost = 50, FoodRequired = 1 });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)321, catalog.BarracksBuildAbilityId);
        Assert.Equal((uint)150, catalog.BarracksMineralCost);
        Assert.Equal((uint)560, catalog.MarineTrainAbilityId);
        Assert.Equal((uint)50, catalog.MarineMineralCost);
        Assert.Equal(1f, catalog.MarineFoodRequired);
        Assert.True(catalog.TryGetUniqueUnit("Barracks", out var barracks));
        Assert.Equal((uint)21, barracks.UnitId);
        Assert.Equal((uint)321, barracks.AbilityId);
        Assert.True(catalog.TryGetUniqueUnit("Marine", out var marine));
        Assert.Equal(1f, marine.FoodRequired);
        Assert.Equal(2, catalog.UnitsById.Count);
    }

    [Fact]
    public void Create_ResolvesOrbitalUpgradeAndMuleAbilitiesWithoutGuessingIds()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 18, Name = "CommandCenter", MineralCost = 400 });
        data.Units.Add(new UnitTypeData { UnitId = 132, Name = "OrbitalCommand", MineralCost = 550 });
        data.Abilities.Add(new AbilityData { AbilityId = 1516, LinkName = "UpgradeToOrbitalCommand" });
        data.Abilities.Add(new AbilityData { AbilityId =  9021, LinkName = "CalldownMULE" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)1516, catalog.OrbitalUpgradeAbilityId);
        Assert.Equal((uint)150, catalog.OrbitalUpgradeMineralCost);
        Assert.Equal((uint)9021, catalog.MuleCallDownAbilityId);
    }

    [Fact]
    public void Create_ResolvesRefineryBuildAbilityAndCostFromAuthoritativeUnitData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData
        {
            UnitId = 20,
            Name = "Refinery",
            AbilityId = 320,
            MineralCost = 75
        });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)320, catalog.RefineryBuildAbilityId);
        Assert.Equal((uint)75, catalog.RefineryMineralCost);
    }

    [Fact]
    public void Create_ResolvesCommandCenterFlightAbilitiesOnlyWhenUnique()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 3678, LinkName = "LiftOff" });
        data.Abilities.Add(new AbilityData { AbilityId = 3679, LinkName = "Land" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)3678, catalog.LiftOffAbilityId);
        Assert.Equal((uint)3679, catalog.LandAbilityId);
    }

    [Fact]
    public void Create_DisablesAmbiguousCommandCenterFlightAbilities()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 3678, LinkName = "LiftOff" });
        data.Abilities.Add(new AbilityData { AbilityId = 3679, LinkName = "Land" });
        data.Abilities.Add(new AbilityData { AbilityId = 3680, LinkName = "Land" });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal((uint)3678, catalog.LiftOffAbilityId);
        Assert.Null(catalog.LandAbilityId);
    }

    [Fact]
    public void Create_ExposesGenericGasCostAndSupplyProviderFacts()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData
        {
            UnitId = 19,
            Name = "SupplyDepot",
            AbilityId = 319,
            MineralCost = 100,
            VespeneCost = 0,
            FoodProvided = 8
        });
        data.Units.Add(new UnitTypeData
        {
            UnitId = 51,
            Name = "Marauder",
            AbilityId = 563,
            MineralCost = 100,
            VespeneCost = 25,
            FoodRequired = 2
        });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.True(catalog.TryGetUniqueUnit("SupplyDepot", out var depot));
        Assert.Equal(8f, depot.FoodProvided);
        Assert.True(catalog.TryGetUniqueUnit("Marauder", out var marauder));
        Assert.Equal((uint)25, marauder.VespeneCost);
        Assert.Equal(2f, marauder.FoodRequired);
    }

    [Fact]
    public void FindUnits_RejectsAmbiguousNamesInsteadOfChoosingAnArbitraryUnitType()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 21, Name = "SiegeTank", AbilityId = 1 });
        data.Units.Add(new UnitTypeData { UnitId = 32, Name = "SiegeTank", AbilityId = 2 });

        var catalog = Sc2GameDataCatalog.Create(data);

        Assert.Equal(2, catalog.FindUnits("SiegeTank").Count);
        Assert.False(catalog.TryGetUniqueUnit("SiegeTank", out _));
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
