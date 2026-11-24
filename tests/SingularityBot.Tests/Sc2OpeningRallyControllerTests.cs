using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OpeningRallyControllerTests
{
    [Fact]
    public void LeavesNativeMineralRallyUntouchedUntilBuilderIsDispatched()
    {
        var controller = new Sc2OpeningRallyController();
        var assignment = Assignment();
        var assignments = new Dictionary<ulong, WorkerResourceAssignment> { [10] = assignment };
        var observation = Snapshot(
            Fact(1, "CommandCenter", 10, 10, UnitAlliance.Self),
            Fact(200, "MineralField750", 6, 12, UnitAlliance.Neutral));
        var gameData = GameData();

        Assert.Null(controller.TryCreateAction(observation, Map(), gameData, assignments, builderDispatched: false));
        Assert.False(controller.IsRestoreAccepted);

        var restore = controller.TryCreateAction(observation, Map(), gameData, assignments, builderDispatched: true);
        Assert.NotNull(restore);
        Assert.Equal(3690, restore.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(1UL, restore.ActionRaw.UnitCommand.UnitTags[0]);
        Assert.Equal(200UL, restore.ActionRaw.UnitCommand.TargetUnitTag);
    }

    [Fact]
    public void RejectedMineralRallyRestorationRetriesWithoutMarkingItComplete()
    {
        var controller = new Sc2OpeningRallyController();
        var assignments = new Dictionary<ulong, WorkerResourceAssignment> { [10] = Assignment() };
        var observation = Snapshot(
            Fact(1, "CommandCenter", 10, 10, UnitAlliance.Self),
            Fact(200, "MineralField750", 6, 12, UnitAlliance.Neutral));
        var gameData = GameData();

        Assert.Null(controller.TryCreateAction(observation, Map(), gameData, assignments, builderDispatched: false));
        Assert.NotNull(controller.TryCreateAction(observation, Map(), gameData, assignments, builderDispatched: true));
        controller.OnActionResult((ActionResult)2);

        Assert.False(controller.IsRestoreAccepted);
        var retry = controller.TryCreateAction(observation, Map(), gameData, assignments, builderDispatched: true);
        Assert.NotNull(retry);
        Assert.Equal(200UL, retry.ActionRaw.UnitCommand.TargetUnitTag);
    }

    [Fact]
    public void SuccessfulMineralRallyRestorationIsNotRepeated()
    {
        var controller = new Sc2OpeningRallyController();
        var assignments = new Dictionary<ulong, WorkerResourceAssignment> { [10] = Assignment() };
        var observation = Snapshot(
            Fact(1, "CommandCenter", 10, 10, UnitAlliance.Self),
            Fact(200, "MineralField750", 6, 12, UnitAlliance.Neutral));
        var gameData = GameData();

        Assert.Null(controller.TryCreateAction(observation, Map(), gameData, assignments, builderDispatched: false));
        Assert.NotNull(controller.TryCreateAction(observation, Map(), gameData, assignments, builderDispatched: true));
        controller.OnActionResult(ActionResult.Success);

        Assert.True(controller.IsRestoreAccepted);
        Assert.Null(controller.TryCreateAction(observation, Map(), gameData, assignments, builderDispatched: true));
    }

    private static WorkerResourceAssignment Assignment() =>
        new(10, 200, 1, 1, 1, 8, 8, 6, 12, 10, 10);

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
