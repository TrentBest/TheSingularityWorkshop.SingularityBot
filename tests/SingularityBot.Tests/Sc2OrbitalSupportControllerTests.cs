using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OrbitalSupportControllerTests
{
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

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 132, Name = "OrbitalCommand", MineralCost = 150 });
        data.Abilities.Add(new AbilityData { AbilityId = 1516, LinkName = "UpgradeToOrbitalCommand" });
        data.Abilities.Add(new AbilityData { AbilityId = 168, LinkName = "CalldownMULE" });
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
