using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OpeningRallyControllerTests
{
    [Fact]
    public void ProductionRallyIsIssuedAgainstCommandCenterAndMustSucceedBeforeUnlocking()
    {
        var controller = new Sc2OpeningRallyController();
        var observation = Snapshot(
            Fact(1, "CommandCenter", 10, 10, UnitAlliance.Self));
        var map = Map();
        var gameData = GameData();

        var action = controller.TryCreateAction(observation, map, gameData,
            new Dictionary<ulong, WorkerResourceAssignment>(), builderDispatched: false);

        Assert.NotNull(action);
        Assert.Equal(3690, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(1UL, action.ActionRaw.UnitCommand.UnitTags[0]);
        Assert.NotNull(action.ActionRaw.UnitCommand.TargetWorldSpacePos);
        Assert.False(controller.IsTemporaryRallyAccepted);

        Assert.Null(controller.TryCreateAction(observation, map, gameData,
            new Dictionary<ulong, WorkerResourceAssignment>(), builderDispatched: false));
        Assert.False(controller.IsTemporaryRallyAccepted);

        controller.OnActionResult(ActionResult.Success);
        Assert.True(controller.IsTemporaryRallyAccepted);
    }

    [Fact]
    public void FailedTemporaryRallyRetriesAndDoesNotUnlockProduction()
    {
        var controller = new Sc2OpeningRallyController();
        var observation = Snapshot(Fact(1, "CommandCenter", 10, 10, UnitAlliance.Self));
        var gameData = GameData();

        Assert.NotNull(controller.TryCreateAction(observation, Map(), gameData,
            new Dictionary<ulong, WorkerResourceAssignment>(), false));
        controller.OnActionResult((ActionResult)2);

        Assert.False(controller.IsTemporaryRallyAccepted);
        Assert.NotNull(controller.TryCreateAction(observation, Map(), gameData,
            new Dictionary<ulong, WorkerResourceAssignment>(), false));
    }

    [Fact]
    public void RestoresObservedMineralTargetOnlyAfterBuilderMoveIsAccepted()
    {
        var controller = new Sc2OpeningRallyController();
        var assignment = new WorkerResourceAssignment(
            WorkerTag: 10,
            MineralTag: 200,
            TownHallTag: 1,
            Generation: 1,
            AssignedAtGameLoop: 1,
            WorkerX: 8,
            WorkerY: 8,
            MineralX: 6,
            MineralY: 12,
            TownHallX: 10,
            TownHallY: 10);
        var assignments = new Dictionary<ulong, WorkerResourceAssignment> { [10] = assignment };
        var observation = Snapshot(
            Fact(1, "CommandCenter", 10, 10, UnitAlliance.Self),
            Fact(200, "MineralField750", 6, 12, UnitAlliance.Neutral));
        var gameData = GameData();

        Assert.NotNull(controller.TryCreateAction(observation, Map(), gameData, assignments, false));
        controller.OnActionResult(ActionResult.Success);

        Assert.Null(controller.TryCreateAction(observation, Map(), gameData, assignments, false));
        Assert.False(controller.IsRestoreAccepted);

        var restore = controller.TryCreateAction(observation, Map(), gameData, assignments, true);
        Assert.NotNull(restore);
        Assert.Equal(3690, restore.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(1UL, restore.ActionRaw.UnitCommand.UnitTags[0]);
        Assert.Equal(200UL, restore.ActionRaw.UnitCommand.TargetUnitTag);

        controller.OnActionResult(ActionResult.Success);
        Assert.True(controller.IsRestoreAccepted);
        Assert.Null(controller.TryCreateAction(observation, Map(), gameData, assignments, true));
    }


    [Fact]
    public void RejectedRallyRestorationRetriesWithoutMarkingItComplete()
    {
        var controller = new Sc2OpeningRallyController();
        var assignment = new WorkerResourceAssignment(
            10, 200, 1, 1, 1, 8, 8, 6, 12, 10, 10);
        var assignments = new Dictionary<ulong, WorkerResourceAssignment> { [10] = assignment };
        var observation = Snapshot(
            Fact(1, "CommandCenter", 10, 10, UnitAlliance.Self),
            Fact(200, "MineralField750", 6, 12, UnitAlliance.Neutral));
        var gameData = GameData();

        Assert.NotNull(controller.TryCreateAction(observation, Map(), gameData, assignments, false));
        controller.OnActionResult(ActionResult.Success);

        Assert.NotNull(controller.TryCreateAction(observation, Map(), gameData, assignments, true));
        controller.OnActionResult((ActionResult)2);

        Assert.False(controller.IsRestoreAccepted);
        var retry = controller.TryCreateAction(observation, Map(), gameData, assignments, true);
        Assert.NotNull(retry);
        Assert.Equal(200UL, retry.ActionRaw.UnitCommand.TargetUnitTag);
    }

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "Move" });
        data.Abilities.Add(new AbilityData { AbilityId = 3690, LinkName = "Rally_Workers" });
        return Sc2GameDataCatalog.Create(data);
    }

    private static SpatialMap Map() =>
        new(new MapBounds(0, 0, 64, 64), new MapPoint(10, 10), new MapPoint(54, 54));

    private static NormalizedObservation Snapshot(params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Fact(
        ulong tag, string kind, float x, float y, UnitAlliance alliance) =>
        new(tag, 1, kind, 1, alliance, UnitVisibility.Visible,
            45, 45, 0, 0, 1, x, y, true,
            false, false, false, false, false, 100);
}
