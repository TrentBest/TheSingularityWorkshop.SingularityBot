using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OpeningMarineControllerTests
{
    [Fact]
    public void BuildsBarracksTrainsMarineAndKeepsItOnFriendlySide()
    {
        var controller = new Sc2OpeningMarineController();
        var assignments = new WorkerResourceAssignmentPlanner();
        assignments.Reserve(10);
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64),
            new MapPoint(10, 10), new MapPoint(54, 54));
        var gameData = GameData();

        Assert.Null(controller.TryCreateAction(Snapshot(150, 12, 15,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, assignments, preferredBuilderTag: 10));
        Assert.Equal(10UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(10));

        var query = controller.TakePlacementQuery(gameData);
        Assert.NotNull(query);
        Assert.NotEmpty(query.Placements);
        var placementResult = new ResponseQuery();
        placementResult.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        controller.OnPlacementQueryResult(placementResult);
        var site = new MapPoint(query.Placements[0].TargetPos.X, query.Placements[0].TargetPos.Y);

        var moveBuilder = controller.TryCreateAction(Snapshot(150, 12, 15,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, assignments);
        Assert.NotNull(moveBuilder);
        Assert.Equal(16, moveBuilder.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(10UL, moveBuilder.ActionRaw.UnitCommand.UnitTags[0]);
        controller.OnActionResult(ActionResult.Success);

        var buildBarracks = controller.TryCreateAction(Snapshot(150, 12, 15,
            Fact(10, "SCV", site.X, site.Y), Fact(11, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, assignments);
        Assert.NotNull(buildBarracks);
        Assert.Equal(321, buildBarracks.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(10UL, buildBarracks.ActionRaw.UnitCommand.UnitTags[0]);
        controller.OnActionResult(ActionResult.Success);

        Assert.Null(controller.TryCreateAction(Snapshot(0, 12, 15,
            Fact(10, "SCV", site.X, site.Y), Fact(11, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", site.X, site.Y, 0.5f)),
            map, gameData, assignments));

        var trainMarine = controller.TryCreateAction(Snapshot(50, 12, 15,
            Fact(10, "SCV", site.X, site.Y), Fact(11, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", site.X, site.Y)),
            map, gameData, assignments);
        Assert.NotNull(trainMarine);
        Assert.Equal(560, trainMarine.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(200UL, trainMarine.ActionRaw.UnitCommand.UnitTags[0]);
        controller.OnActionResult(ActionResult.Success);
        Assert.False(assignments.IsReserved(10));

        var trainingBarracks = Fact(200, "Barracks", site.X, site.Y) with
        {
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(560, null, null, null) }
        };
        Assert.Null(controller.TryCreateAction(Snapshot(0, 12, 15,
            Fact(10, "SCV", site.X, site.Y), trainingBarracks), map, gameData, assignments));

        var observedMarine = controller.TryCreateAction(Snapshot(0, 13, 15,
            Fact(10, "SCV", site.X, site.Y), Fact(200, "Barracks", site.X, site.Y),
            Fact(300, "Marine", 20, 20)), map, gameData, assignments);

        Assert.Null(observedMarine);
        Assert.True(controller.MarineObserved);
        Assert.True(controller.MarineDeployed);
    }

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 45, Name = "SCV", AbilityId = 524, MineralCost = 50, FoodRequired = 1 });
        data.Units.Add(new UnitTypeData { UnitId = 21, Name = "Barracks", AbilityId = 321, MineralCost = 150 });
        data.Units.Add(new UnitTypeData { UnitId = 48, Name = "Marine", AbilityId = 560, MineralCost = 50, FoodRequired = 1 });
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "Move" });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(uint minerals, uint used, uint cap, params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>())
        {
            Minerals = minerals,
            FoodUsed = used,
            FoodCap = cap
        };

    private static UnitObservationFacts Fact(ulong tag, string kind, float x, float y, float progress = 1f) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, progress, x, y, true,
            false, false, false, false, false, 100);
}
