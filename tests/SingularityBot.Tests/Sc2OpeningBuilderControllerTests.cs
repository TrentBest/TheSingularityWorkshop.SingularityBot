using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OpeningBuilderControllerTests
{
    [Fact]
    public void SelectsFirstNewScvAndReservesItBeforeMining()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();

        controller.TryCreateAction(Snapshot(50, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(1, "CommandCenter", 10, 10)),
            map, gameData, true, assignments);
        var selection = controller.TryCreateAction(Snapshot(50, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 6, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);

        Assert.NotNull(selection); // The new SCV is controlled immediately, not left to rally inheritance.
        Assert.Equal(16, selection.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, selection.ActionRaw.UnitCommand.UnitTags[0]);
        Assert.False(selection.ActionRaw.UnitCommand.QueueCommand);
        Assert.Equal(controller.Placement?.X ?? selection.ActionRaw.UnitCommand.TargetWorldSpacePos.X,
            selection.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));
        Assert.False(assignments.IsReserved(10));
        Assert.NotNull(controller.TakePlacementQuery(gameData));
    }

    [Fact]
    public void CapturesStartingWorkersBeforeProductionGateOpens()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();

        controller.ObserveWorkers(Snapshot(50, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(1, "CommandCenter", 10, 10)));
        controller.ObserveWorkers(Snapshot(50, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 6, 5),
            Fact(1, "CommandCenter", 10, 10)));

        var selection = controller.TryCreateAction(Snapshot(50, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 6, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);

        Assert.NotNull(selection);
        Assert.Equal(16, selection.ActionRaw.UnitCommand.AbilityId);
        Assert.False(selection.ActionRaw.UnitCommand.QueueCommand);
        Assert.Equal(11UL, selection.ActionRaw.UnitCommand.UnitTags[0]);
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));
    }

    [Fact]
    public void StagesFirstProducedBuilderAndWaitsUntilDepotIsAffordable()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();
        SelectFirstScv(controller, assignments, map, gameData);
        SetValidPlacement(controller);

        var move = controller.TryCreateAction(Snapshot(50, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 7, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        Assert.NotNull(move);
        Assert.Equal(16, move.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, move.ActionRaw.UnitCommand.UnitTags[0]);
        Assert.False(move.ActionRaw.UnitCommand.QueueCommand);
        var site = controller.Placement.Value;
        Assert.Equal(site.X, move.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        controller.OnActionResult(ActionResult.Success);
        Assert.True(controller.IsDispatched);

        Assert.Null(controller.TryCreateAction(Snapshot(99, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments));

        var build = controller.TryCreateAction(Snapshot(100, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        Assert.NotNull(build);
        Assert.Equal(319, build.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, build.ActionRaw.UnitCommand.UnitTags[0]);
    }

    [Fact]
    public void RetriesPlacementQueryWhenSc2RejectsEveryCandidate()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var gameData = GameData();
        SelectFirstScv(controller, assignments, Map(), gameData);

        Assert.NotNull(controller.TakePlacementQuery(gameData));
        var rejected = new ResponseQuery();
        rejected.Placements.Add(new ResponseQueryBuildingPlacement { Result = (ActionResult)2 });
        controller.OnPlacementQueryResult(rejected);
        Assert.NotNull(controller.TakePlacementQuery(gameData));
    }

    [Fact]
    public void RetriesMoveWhenAcceptedBuilderDoesNotReachPlacement()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();
        SelectFirstScv(controller, assignments, map, gameData);
        SetValidPlacement(controller);

        var firstMove = controller.TryCreateAction(Snapshot(150, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 7, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        Assert.NotNull(firstMove);
        controller.OnActionResult(ActionResult.Success);

        var retryMove = controller.TryCreateAction(SnapshotAt(123, 150, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 7, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        Assert.NotNull(retryMove);
        Assert.Equal(firstMove.ActionRaw.UnitCommand.AbilityId, retryMove.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, retryMove.ActionRaw.UnitCommand.UnitTags[0]);
    }

    [Fact]
    public void ReleasesDedicatedBuilderAfterDepotCompletes()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();
        SelectFirstScv(controller, assignments, map, gameData);
        SetValidPlacement(controller);
        var site = controller.Placement.Value;

        var move = controller.TryCreateAction(Snapshot(50, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 7, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        Assert.NotNull(move);
        controller.OnActionResult(ActionResult.Success);
        var build = controller.TryCreateAction(Snapshot(100, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        Assert.NotNull(build);
        controller.OnActionResult(ActionResult.Success);

        controller.TryCreateAction(Snapshot(0, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10),
            Fact(200, "SupplyDepot", site.X, site.Y, buildProgress: 1)),
            map, gameData, true, assignments);

        Assert.True(controller.IsCompleted);
        Assert.False(assignments.IsReserved(11));
    }

    private static void SelectFirstScv(
        Sc2OpeningBuilderController controller,
        WorkerResourceAssignmentPlanner assignments,
        SpatialMap map,
        Sc2GameDataCatalog gameData)
    {
        controller.TryCreateAction(Snapshot(50, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(1, "CommandCenter", 10, 10)),
            map, gameData, true, assignments);
        var selection = controller.TryCreateAction(Snapshot(50, 13, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 6, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        Assert.Null(selection);
        Assert.Equal(11UL, controller.BuilderTag);
    }

    private static void SetValidPlacement(Sc2OpeningBuilderController controller)
    {
        var result = new ResponseQuery();
        result.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        controller.OnPlacementQueryResult(result);
    }

    private static SpatialMap Map() =>
        new(new MapBounds(0, 0, 64, 64), new MapPoint(10, 10), new MapPoint(54, 54));

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 45, Name = "SCV", AbilityId = 524, MineralCost = 50, FoodRequired = 1 });
        data.Units.Add(new UnitTypeData { UnitId = 19, Name = "SupplyDepot", AbilityId = 319, MineralCost = 100 });
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "Move" });
         return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(uint minerals, uint used, uint cap, params UnitObservationFacts[] units) =>
        SnapshotAt(100, minerals, used, cap, units);

    private static NormalizedObservation SnapshotAt(uint gameLoop, uint minerals, uint used, uint cap, params UnitObservationFacts[] units) =>
        new(gameLoop, 1, units, new HashSet<ulong>()) { Minerals = minerals, FoodUsed = used, FoodCap = cap };

    private static UnitObservationFacts Fact(
        ulong tag, string kind, float x, float y, float buildProgress = 1) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, buildProgress, x, y, true,
            false, false, false, false, false, 100);
}