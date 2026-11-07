using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OpeningProductionControllerTests
{
    [Fact]
    public void FirstEligibleFrameRequestsScvFromCommandCenter()
    {
        var controller = new Sc2OpeningProductionController();
        var action = controller.TryCreateScvTrainingAction(
            Snapshot(50, 12, 15, Fact(100, "CommandCenter")),
            GameData());

        var command = Assert.IsType<ActionRawUnitCommand>(action.ActionRaw.UnitCommand);
        Assert.Equal(524, command.AbilityId);
        Assert.Equal(new[] { 100UL }, command.UnitTags);
        Assert.Equal(ActionRawUnitCommand.TargetOneofCase.None, command.TargetCase);
    }

    [Fact]
    public void DoesNotTrainWithoutMineralsOrFreeSupply()
    {
        var controller = new Sc2OpeningProductionController();
        Assert.Null(controller.TryCreateScvTrainingAction(
            Snapshot(49, 12, 15, Fact(100, "CommandCenter")), GameData()));
        Assert.Null(controller.TryCreateScvTrainingAction(
            Snapshot(50, 15, 15, Fact(100, "CommandCenter")), GameData()));
    }

    [Fact]
    public void DoesNotDuplicateAnObservedScvTrainingOrder()
    {
        var controller = new Sc2OpeningProductionController();
        var commandCenter = Fact(100, "CommandCenter") with
        {
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(524, null, null, null) }
        };

        Assert.Null(controller.TryCreateScvTrainingAction(
            Snapshot(100, 12, 15, commandCenter), GameData()));
    }

    [Fact]
    public void HoldsRequestUntilNextObservationAndRetriesWhenOrderNeverAppears()
    {
        var controller = new Sc2OpeningProductionController();
        Assert.NotNull(controller.TryCreateScvTrainingAction(
            Snapshot(50, 12, 15, Fact(100, "CommandCenter"), gameLoop: 10), GameData()));
        Assert.Null(controller.TryCreateScvTrainingAction(
            Snapshot(50, 12, 15, Fact(100, "CommandCenter"), gameLoop: 10), GameData()));
        Assert.NotNull(controller.TryCreateScvTrainingAction(
            Snapshot(50, 12, 15, Fact(100, "CommandCenter"), gameLoop: 11), GameData()));
    }

    [Fact]
    public void RejectedActionReleasesPendingRequestImmediately()
    {
        var controller = new Sc2OpeningProductionController();
        Assert.NotNull(controller.TryCreateScvTrainingAction(
            Snapshot(50, 12, 15, Fact(100, "CommandCenter")), GameData()));

        controller.OnActionResult(ActionResult.NotEnoughMinerals);

        Assert.NotNull(controller.TryCreateScvTrainingAction(
            Snapshot(50, 12, 15, Fact(100, "CommandCenter"), gameLoop: 101), GameData()));
    }

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData
        {
            UnitId = 45,
            Name = "SCV",
            AbilityId = 524,
            MineralCost = 50,
            FoodRequired = 1
        });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(
        uint minerals,
        uint foodUsed,
        uint foodCap,
        params UnitObservationFacts[] units) =>
        Snapshot(minerals, foodUsed, foodCap, units, 100);

    private static NormalizedObservation Snapshot(
        uint minerals,
        uint foodUsed,
        uint foodCap,
        UnitObservationFacts[] units,
        uint gameLoop) =>
        new(gameLoop, 1, units, new HashSet<ulong>())
        {
            Minerals = minerals,
            FoodUsed = foodUsed,
            FoodCap = foodCap
        };

    private static UnitObservationFacts Fact(ulong tag, string kind) =>
        new(tag, 18, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            1500, 1500, 0, 0, 1, 20, 20, true,
            false, false, false, false, false, 100);
}
