using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OrbitalSupportControllerTests
{
    [Fact]
    public void ReservesFirstCommandCenterWhileAnExistingScvOrderDrains()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var busyCommandCenter = Fact(1, "CommandCenter") with
        {
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(524, null, null, null) }
        };

        var reservedTag = controller.GetCommandCenterReservedForUpgrade(
            Snapshot(350, busyCommandCenter, Fact(2, "SupplyDepot"), Fact(3, "Barracks")),
            data);

        Assert.Equal(1UL, reservedTag);
    }

    [Fact]
    public void KeepsStartingCommandCenterReservedWhenOrbitalAbilityMetadataIsMissing()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData(includeOrbitalAbility: false);
        var observation = Snapshot(500,
            Fact(1, "CommandCenter"),
            Fact(2, "SupplyDepot"),
            Fact(3, "Barracks"));

        Assert.Null(controller.TryCreateAction(observation, data));
        Assert.Equal(1UL, controller.GetCommandCenterReservedForUpgrade(observation, data));
    }

    [Fact]
    public void DoesNotReserveCommandCenterBeforeOrbitalTechIsReady()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();

        Assert.Null(controller.GetCommandCenterReservedForUpgrade(
            Snapshot(500, Fact(1, "CommandCenter"), Fact(2, "SupplyDepot")),
            data));
    }

    [Fact]
    public void ReservesCommandCenterBeforeOrbitalIsAffordableToPreventScvRefillStarvation()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();

        var reservedTag = controller.GetCommandCenterReservedForUpgrade(
            Snapshot(50, Fact(1, "CommandCenter"), Fact(2, "SupplyDepot"), Fact(3, "Barracks")),
            data);

        Assert.Equal(1UL, reservedTag);
    }

    [Fact]
    public void UpgradesIdleCommandCenterOnlyWhenUnreservedMineralsAreAvailable()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var commandCenter = Fact(1, "CommandCenter");

        Assert.Null(controller.TryCreateAction(Snapshot(149, commandCenter, Fact(2, "SupplyDepot"), Fact(3, "Barracks"), Fact(4, "Barracks")), data));
        Assert.Null(controller.TryCreateAction(Snapshot(350, commandCenter, Fact(2, "SupplyDepot"), Fact(3, "Barracks"), Fact(4, "Barracks")), data, reservedMinerals: 201));

        var action = controller.TryCreateAction(Snapshot(350, commandCenter, Fact(2, "SupplyDepot"), Fact(3, "Barracks"), Fact(4, "Barracks")), data, reservedMinerals: 150);
        Assert.NotNull(action);
        Assert.Equal(1516, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(new[] { 1UL }, action.ActionRaw.UnitCommand.UnitTags);
        Assert.Equal(ActionRawUnitCommand.TargetOneofCase.None, action.ActionRaw.UnitCommand.TargetCase);
    }

    [Fact]
    public void UpgradesCommandCenterWithPersistentWorkerRallyOrder()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var commandCenterWithRally = Fact(1, "CommandCenter") with
        {
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(3690, null, 10, 14) }
        };

        var action = controller.TryCreateAction(Snapshot(150,
            commandCenterWithRally,
            Fact(2, "SupplyDepot"),
            Fact(3, "Barracks")), data);

        Assert.NotNull(action);
        Assert.Equal(1516, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(new[] { 1UL }, action.ActionRaw.UnitCommand.UnitTags);
    }

    [Fact]
    public void WaitsForAnObservedScvTrainingOrderBeforeUpgrading()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var commandCenterTrainingScv = Fact(1, "CommandCenter") with
        {
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(524, null, null, null) }
        };

        Assert.Null(controller.TryCreateAction(Snapshot(150,
            commandCenterTrainingScv,
            Fact(2, "SupplyDepot"),
            Fact(3, "Barracks")), data));
    }

    [Fact]
    public void UpgradesWhenTheCompletedSupplyDepotIsLowered()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var observation = Snapshot(150,
            Fact(1, "CommandCenter"),
            Fact(2, "SupplyDepotLowered"),
            Fact(3, "Barracks"));

        Assert.Equal(1UL, controller.GetCommandCenterReservedForUpgrade(observation, data));
        var action = controller.TryCreateAction(observation, data);

        Assert.NotNull(action);
        Assert.Equal(1516, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(new[] { 1UL }, action.ActionRaw.UnitCommand.UnitTags);
    }

    [Fact]
    public void UpgradesAfterTheFirstCompletedBarracksWithoutWaitingForASecond()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var action = controller.TryCreateAction(Snapshot(150,
            Fact(1, "CommandCenter"),
            Fact(2, "SupplyDepot"),
            Fact(3, "Barracks")), data);

        Assert.NotNull(action);
        Assert.Equal(1516, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(new[] { 1UL }, action.ActionRaw.UnitCommand.UnitTags);
    }

    [Fact]
    public void WaitsForUpgradeOrderBeforeIssuingAnotherUpgrade()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        Assert.NotNull(controller.TryCreateAction(Snapshot(150, Fact(1, "CommandCenter"), Fact(2, "SupplyDepot"), Fact(3, "Barracks"), Fact(4, "Barracks")), data));
        Assert.Null(controller.TryCreateAction(Snapshot(500, Fact(1, "CommandCenter"), Fact(2, "SupplyDepot"), Fact(3, "Barracks"), Fact(4, "Barracks"), loop: 101), data));

        controller.OnActionResult(ActionResult.Success);
        Assert.Null(controller.TryCreateAction(Snapshot(500, Fact(1, "CommandCenter"), Fact(2, "SupplyDepot"), Fact(3, "Barracks"), Fact(4, "Barracks"), loop: 102), data));
    }

    [Fact]
    public void DoesNotResubmitMorphWhileSc2ContinuesReportingTheUpgradeOrder()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var morphingCommandCenter = Fact(1, "CommandCenter") with
        {
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(1516, null, null, null) }
        };

        Assert.Null(controller.TryCreateAction(Snapshot(500,
            morphingCommandCenter,
            Fact(2, "SupplyDepot"),
            Fact(3, "Barracks")), data));

        controller.OnActionResult(ActionResult.Success);
        Assert.Null(controller.TryCreateAction(Snapshot(500,
            morphingCommandCenter,
            Fact(2, "SupplyDepot"),
            Fact(3, "Barracks"),
            loop: 500), data));
    }

    [Fact]
    public void UpgradesTheOriginalStartingCommandCenterAfterExpansionExists()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();

        var twoCommandCenters = Snapshot(500,
            Fact(1, "CommandCenter"),
            Fact(2, "CommandCenter", 30, 30),
            Fact(3, "SupplyDepot"),
            Fact(4, "Barracks"));
        var action = controller.TryCreateAction(twoCommandCenters, data);
        Assert.NotNull(action);
        Assert.Equal(new[] { 1UL }, action.ActionRaw.UnitCommand.UnitTags);

        var reservedTag = controller.GetCommandCenterReservedForUpgrade(twoCommandCenters, data);
        Assert.Equal(1UL, reservedTag);
    }

    [Fact]
    public void DoesNotUpgradeExpansionCommandCenterWhenStartingBaseIsAlreadyOrbital()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();

        var firstBaseAlreadyOrbital = Snapshot(500,
            Fact(1, "OrbitalCommand"),
            Fact(2, "CommandCenter", 30, 30),
            Fact(3, "SupplyDepot"),
            Fact(4, "Barracks"));
        Assert.Null(controller.TryCreateAction(firstBaseAlreadyOrbital, data));
        Assert.Null(controller.GetCommandCenterReservedForUpgrade(firstBaseAlreadyOrbital, data));
    }

    [Fact]
    public void CanUpgradeStartingCommandCenterWhenAnotherTownHallIsPlanetaryFortress()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var observation = Snapshot(500,
            Fact(1, "CommandCenter"),
            Fact(2, "PlanetaryFortress", 30, 30),
            Fact(3, "SupplyDepot"),
            Fact(4, "Barracks"));

        var action = controller.TryCreateAction(observation, data);
        Assert.NotNull(action);
        Assert.Equal(new[] { 1UL }, action.ActionRaw.UnitCommand.UnitTags);
    }

    [Fact]
    public void UpgradesTheExplicitlyPermittedStartingCommandCenter()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var observation = Snapshot(150,
            Fact(1, "CommandCenter"),
            Fact(2, "CommandCenter", 30, 30),
            Fact(3, "SupplyDepot"),
            Fact(4, "Barracks"),
            loop: 100);

        var action = controller.TryCreateAction(
            observation,
            data,
            permittedCommandCenterTag: 2);

        Assert.NotNull(action);
        Assert.Equal(1516, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(new[] { 2UL }, action.ActionRaw.UnitCommand.UnitTags);
    }

    [Fact]
    public void CallsMuleOnNearestMineralWhenOrbitalHasEnoughEnergy()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var orbital = Fact(1, "OrbitalCommand") with { Energy = 75 };
        var near = Fact(10, "MineralField750", 12, 10) with { Alliance = UnitAlliance.Neutral };
        var far = Fact(11, "MineralField750", 40, 40) with { Alliance = UnitAlliance.Neutral };

        var action = controller.TryCreateAction(Snapshot(0, orbital, near, far), data);

        Assert.NotNull(action);
        var command = action.ActionRaw.UnitCommand;
        Assert.Equal(168, command.AbilityId);
        Assert.Equal(new[] { 1UL }, command.UnitTags);
        Assert.Equal(ActionRawUnitCommand.TargetOneofCase.TargetUnitTag, command.TargetCase);
        Assert.Equal(10UL, command.TargetUnitTag);
    }

    [Fact]
    public void DoesNotCallMuleOnMineralsOutsideAuthoritativeAbilityRange()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var orbital = Fact(1, "OrbitalCommand", 10, 10) with { Energy = 100 };
        var mineral = Fact(10, "MineralField750", 21, 10) with { Alliance = UnitAlliance.Neutral };

        Assert.Null(controller.TryCreateAction(Snapshot(0, orbital, mineral), data));
    }

    [Fact]
    public void DoesNotSpendMuleEnergyReservedForAnOrbitalScan()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var orbital = Fact(1, "OrbitalCommand") with { Energy = 100 };
        var mineral = Fact(10, "MineralField750", 12, 10) with { Alliance = UnitAlliance.Neutral };

        Assert.Null(controller.TryCreateAction(Snapshot(0, orbital, mineral), data,
            reservedOrbitalTags: new HashSet<ulong> { 1 }));
    }

    [Fact]
    public void PreservesConfiguredScanEnergyBeforeCallingMule()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var mineral = Fact(10, "MineralField750", 12, 10) with { Alliance = UnitAlliance.Neutral };

        Assert.Null(controller.TryCreateAction(Snapshot(0,
            Fact(1, "OrbitalCommand") with { Energy = 99 }, mineral), data,
            reservedEnergyForScan: 50));

        var action = controller.TryCreateAction(Snapshot(0,
            Fact(1, "OrbitalCommand") with { Energy = 100 }, mineral), data,
            reservedEnergyForScan: 50);

        Assert.NotNull(action);
        Assert.Equal(168, action.ActionRaw.UnitCommand.AbilityId);
    }

    [Fact]
    public void EconomyCanDisableMuleCallDownEvenWhenEnergyAndTargetAreAvailable()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var mineral = Fact(10, "MineralField750", 12, 10) with { Alliance = UnitAlliance.Neutral };

        Assert.Null(controller.TryCreateAction(Snapshot(0,
            Fact(1, "OrbitalCommand") with { Energy = 200 }, mineral), data,
            allowMules: false));
    }

    [Fact]
    public void DoesNotCallMuleWithoutFiftyEnergyOrWithoutMineralTarget()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        Assert.Null(controller.TryCreateAction(Snapshot(0,
            Fact(1, "OrbitalCommand") with { Energy = 49 },
            Fact(10, "MineralField750", 12, 10) with { Alliance = UnitAlliance.Neutral }), data));
        Assert.Null(controller.TryCreateAction(Snapshot(0,
            Fact(1, "OrbitalCommand") with { Energy = 100 }), data));
    }

    [Fact]
    public void DoesNotDuplicateObservedMuleOrder()
    {
        var controller = new Sc2OrbitalSupportController();
        var data = GameData();
        var orbital = Fact(1, "OrbitalCommand") with
        {
            Energy = 100,
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(168, 10, null, null) }
        };
        Assert.Null(controller.TryCreateAction(Snapshot(0,
            orbital,
            Fact(10, "MineralField750", 12, 10) with { Alliance = UnitAlliance.Neutral }), data));
    }

    private static Sc2GameDataCatalog GameData(bool includeOrbitalAbility = true)
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 18, Name = "CommandCenter", MineralCost = 400 });
        data.Units.Add(new UnitTypeData { UnitId = 132, Name = "OrbitalCommand", MineralCost = 550 });
        data.Units.Add(new UnitTypeData { UnitId = 45, Name = "SCV", AbilityId = 524, MineralCost = 50, FoodRequired = 1 });
        if (includeOrbitalAbility)
        {
            data.Abilities.Add(new AbilityData { AbilityId = 1516, LinkName = "UpgradeToOrbitalCommand" });
            // Some ResponseData sets also expose the older compatibility alias.
            // The canonical link name must remain authoritative.
            data.Abilities.Add(new AbilityData { AbilityId = 1517, LinkName = "UpgradeToOrbital" });
        }
        data.Abilities.Add(new AbilityData { AbilityId = 168, LinkName = "CalldownMULE", CastRange = 10 });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(uint minerals, params UnitObservationFacts[] units) =>
        Snapshot(minerals, units, 100);

    private static NormalizedObservation Snapshot(uint minerals, UnitObservationFacts unit, uint loop) =>
        Snapshot(minerals, new[] { unit }, loop);

    private static NormalizedObservation Snapshot(uint minerals, UnitObservationFacts first, UnitObservationFacts second, uint loop) =>
        Snapshot(minerals, new[] { first, second }, loop);

    private static NormalizedObservation Snapshot(uint minerals, UnitObservationFacts first, UnitObservationFacts second, UnitObservationFacts third, uint loop) =>
        Snapshot(minerals, new[] { first, second, third }, loop);

    private static NormalizedObservation Snapshot(uint minerals, UnitObservationFacts first, UnitObservationFacts second, UnitObservationFacts third, UnitObservationFacts fourth, uint loop) =>
        Snapshot(minerals, new[] { first, second, third, fourth }, loop);

    private static NormalizedObservation Snapshot(uint minerals, UnitObservationFacts[] units, uint loop) =>
        new(loop, 1, units, new HashSet<ulong>()) { Minerals = minerals };

    private static UnitObservationFacts Fact(ulong tag, string kind, float x = 10, float y = 10) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            1500, 1500, 0, 0, 1, x, y, true, false, false,
            false, false, false, 100);
}
