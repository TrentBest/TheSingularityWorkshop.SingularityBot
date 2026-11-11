using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2BarracksProductionControllerTests
{
    [Fact]
    public void TrainsMarineFromSecondaryBarracks()
    {
        var controller = new Sc2BarracksProductionController();
        var action = controller.TryCreateMarineTrainingAction(Snapshot(50, 10, 15,
            Fact(100, "Barracks"), Fact(200, "Barracks")), GameData());
        Assert.NotNull(action);
        Assert.Equal(560, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(new[] { 200UL }, action.ActionRaw.UnitCommand.UnitTags);
    }

    [Fact]
    public void DoesNotTrainWithoutMarineResourcesOrSupply()
    {
        var controller = new Sc2BarracksProductionController();
        Assert.Null(controller.TryCreateMarineTrainingAction(Snapshot(49, 10, 15,
            Fact(100, "Barracks"), Fact(200, "Barracks")), GameData()));
        Assert.Null(controller.TryCreateMarineTrainingAction(Snapshot(50, 15, 15,
            Fact(100, "Barracks"), Fact(200, "Barracks")), GameData()));
    }

    [Fact]
    public void DoesNotDuplicatePendingOrObservedTrainingOrder()
    {
        var controller = new Sc2BarracksProductionController();
        Assert.NotNull(controller.TryCreateMarineTrainingAction(Snapshot(50, 10, 15,
            Fact(100, "Barracks"), Fact(200, "Barracks")), GameData()));
        Assert.Null(controller.TryCreateMarineTrainingAction(Snapshot(50, 10, 15,
            Fact(100, "Barracks"), Fact(200, "Barracks")), GameData()));
        var training = Fact(200, "Barracks") with { HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(560, null, null, null) } };
        Assert.Null(controller.TryCreateMarineTrainingAction(Snapshot(50, 10, 15,
            Fact(100, "Barracks"), training), GameData()));
    }

    [Fact]
    public void HoldsSuccessfulRequestAcrossObservationLatencyThenRetries()
    {
        var controller = new Sc2BarracksProductionController();
        Assert.NotNull(controller.TryCreateMarineTrainingAction(Snapshot(50, 10, 15,
            Fact(100, "Barracks"), Fact(200, "Barracks")), GameData()));
        Assert.Null(controller.TryCreateMarineTrainingAction(SnapshotAt(101, 50, 10, 15,
            Fact(100, "Barracks"), Fact(200, "Barracks")), GameData()));
        Assert.NotNull(controller.TryCreateMarineTrainingAction(SnapshotAt(109, 50, 10, 15,
            Fact(100, "Barracks"), Fact(200, "Barracks")), GameData()));
    }

    [Fact]
    public void AdvancesToAnotherBarracksAfterTrainingOrderIsObserved()
    {
        var controller = new Sc2BarracksProductionController();
        Assert.NotNull(controller.TryCreateMarineTrainingAction(Snapshot(50, 10, 15,
            Fact(100, "Barracks"), Fact(200, "Barracks"), Fact(300, "Barracks")), GameData()));
        var training = Fact(200, "Barracks") with { HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(560, null, null, null) } };
        var next = controller.TryCreateMarineTrainingAction(SnapshotAt(101, 50, 10, 15,
            Fact(100, "Barracks"), training, Fact(300, "Barracks")), GameData());
        Assert.NotNull(next);
        Assert.Equal(new[] { 300UL }, next.ActionRaw.UnitCommand.UnitTags);
    }

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 48, Name = "Marine", AbilityId = 560, MineralCost = 50, FoodRequired = 1 });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(uint minerals, uint used, uint cap, params UnitObservationFacts[] units) =>
        SnapshotAt(100, minerals, used, cap, units);

    private static NormalizedObservation SnapshotAt(uint loop, uint minerals, uint used, uint cap, params UnitObservationFacts[] units) =>
        new(loop, 1, units, new HashSet<ulong>()) { Minerals = minerals, FoodUsed = used, FoodCap = cap };

    private static UnitObservationFacts Fact(ulong tag, string kind) =>
        new(tag, 21, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            1000, 1000, 0, 0, 1, 20, 20, true,
            false, false, false, false, false, 0);
}
