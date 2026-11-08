using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OpeningBuilderControllerTests
{
    [Fact]
    public void PullsTheReturningScvWhenMineralBankCrossesEightyAndQueriesDepotPlacement()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64), new MapPoint(10, 10), new MapPoint(54, 54));
        var gameData = GameData();

        Assert.Null(controller.TryCreateAction(
            Snapshot(0, 10, 10, Fact(10, "SCV", 5, 5, true, true,
                new[] { new UnitOrderObservationFacts(3667, 1, null, null) }),
                Fact(1, "CommandCenter", 10, 10)),
            map, gameData, true, assignments));

        Assert.Null(controller.TryCreateAction(
            Snapshot(80, 10, 10, Fact(10, "SCV", 5, 5, true, true,
                new[] { new UnitOrderObservationFacts(3667, 1, null, null) }),
                Fact(1, "CommandCenter", 10, 10)),
            map, gameData, true, assignments));

        Assert.Equal(10UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(10));

        var query = controller.TakePlacementQuery(gameData);
        Assert.NotNull(query);
        Assert.NotEmpty(query.Placements);
        Assert.All(query.Placements, placement =>
        {
            Assert.Equal(319, placement.AbilityId);
            Assert.Equal(10UL, placement.PlacingUnitTag);
        });
    }

    [Fact]
    public void RecognizesCargoBearingMinerWhenSc2KeepsMineralTargetOnGatherOrder()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64), new MapPoint(10, 10), new MapPoint(54, 54));
        var gameData = GameData();

        var first = Snapshot(0, 10, 10,
            Fact(10, "SCV", 5, 5, true, true,
                new[] { new UnitOrderObservationFacts(3666, 20, null, null) }),
            Fact(1, "CommandCenter", 10, 10),
            Fact(20, "MineralField750", 4, 5) with { Alliance = UnitAlliance.Neutral, OwnerId = 0 });
        assignments.Reconcile(first);
        controller.TryCreateAction(first, map, gameData, true, assignments);

        var crossed = Snapshot(80, 10, 11,
            Fact(10, "SCV", 5, 5, true, true,
                new[] { new UnitOrderObservationFacts(3666, 20, null, null) }),
            Fact(1, "CommandCenter", 10, 10),
            Fact(20, "MineralField750", 4, 5) with { Alliance = UnitAlliance.Neutral, OwnerId = 0 });
        assignments.Reconcile(crossed);
        controller.TryCreateAction(crossed, map, gameData, true, assignments);

        Assert.Equal(10UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(10));
        Assert.NotNull(controller.TakePlacementQuery(gameData));
    }

    [Fact]
    public void MovesSelectedBuilderThenBuildsOnlyAtQueriedSiteWithSufficientMinerals()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64), new MapPoint(10, 10), new MapPoint(54, 54));
        var gameData = GameData();

        controller.TryCreateAction(Snapshot(0, 10, 10,
            Fact(10, "SCV", 5, 5, true, true, new[] { new UnitOrderObservationFacts(3667, 1, null, null) }),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        controller.TryCreateAction(Snapshot(10, 10, 11,
            Fact(10, "SCV", 5, 5, true, true, new[] { new UnitOrderObservationFacts(3667, 1, null, null) }),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);

        var query = controller.TakePlacementQuery(gameData);
        var response = new ResponseQuery();
        response.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        controller.OnPlacementQueryResult(response);
        var site = controller.Placement.Value;

        Assert.Null(controller.TryCreateAction(Snapshot(10, 10, 12,
            Fact(10, "SCV", 5, 5, true, true, new[] { new UnitOrderObservationFacts(3667, 1, null, null) }),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments));

        var move = controller.TryCreateAction(Snapshot(10, 10, 13,
            Fact(10, "SCV", 5, 5, false, true, Array.Empty<UnitOrderObservationFacts>()),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        Assert.Equal(16, move.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(site.X, move.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        controller.OnActionResult(ActionResult.Success);

        var build = controller.TryCreateAction(Snapshot(100, 10, 14,
            Fact(10, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        Assert.Equal(319, build.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(site.X, build.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.Equal(site.Y, build.ActionRaw.UnitCommand.TargetWorldSpacePos.Y);
    }

    [Fact]
    public void ReleasesBuilderReservationWhenDepotCompletes()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64), new MapPoint(10, 10), new MapPoint(54, 54));
        var gameData = GameData();

        controller.TryCreateAction(Snapshot(0, 10, 10,
            Fact(10, "SCV", 5, 5, true, true, new[] { new UnitOrderObservationFacts(3667, 1, null, null) }),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        controller.TryCreateAction(Snapshot(10, 10, 11,
            Fact(10, "SCV", 5, 5, true, true, new[] { new UnitOrderObservationFacts(3667, 1, null, null) }),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        var response = new ResponseQuery();
        response.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        controller.OnPlacementQueryResult(response);
        var site = controller.Placement.Value;

        controller.TryCreateAction(Snapshot(10, 10, 12,
            Fact(10, "SCV", 5, 5, false, true, Array.Empty<UnitOrderObservationFacts>()),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        controller.OnActionResult(ActionResult.Success);
        controller.TryCreateAction(Snapshot(100, 10, 13,
            Fact(10, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, true, assignments);
        controller.OnActionResult(ActionResult.Success);

        controller.TryCreateAction(Snapshot(0, 10, 20,
            Fact(10, "SCV", site.X, site.Y, false, true, Array.Empty<UnitOrderObservationFacts>()),
            Fact(1, "CommandCenter", 10, 10),
            Fact(200, "SupplyDepot", site.X, site.Y, buildProgress: 1)), map, gameData, true, assignments);

        Assert.True(controller.IsCompleted);
        Assert.False(assignments.IsReserved(10));
    }

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 45, Name = "SCV", AbilityId = 524, MineralCost = 50, FoodRequired = 1 });
        data.Units.Add(new UnitTypeData { UnitId = 19, Name = "SupplyDepot", AbilityId = 319, MineralCost = 100 });
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "Move" });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(uint minerals, uint used, uint cap, params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>()) { Minerals = minerals, FoodUsed = used, FoodCap = cap };

    private static UnitObservationFacts Fact(
        ulong tag, string kind, float x, float y, bool cargo = false, bool orders = false,
        IReadOnlyList<UnitOrderObservationFacts> observedOrders = null, float buildProgress = 1) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, buildProgress, x, y, true,
            cargo, orders, false, false, false, 100)
        {
            Orders = observedOrders ?? Array.Empty<UnitOrderObservationFacts>()
        };
}
