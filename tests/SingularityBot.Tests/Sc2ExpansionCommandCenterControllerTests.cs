using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2ExpansionCommandCenterControllerTests
{
    [Fact]
    public void FindsNearestUnoccupiedFriendlyMineralExpansion()
    {
        var map = Map();
        var observation = Snapshot(0,
            Fact(1, "CommandCenter", 10, 10),
            Mineral(100, 16, 8), Mineral(101, 17, 8), Mineral(102, 18, 8), Mineral(103, 19, 8),
            Mineral(200, 34, 19), Mineral(201, 35, 19), Mineral(202, 36, 19), Mineral(203, 37, 19),
            Mineral(400, 55, 25), Mineral(401, 56, 25), Mineral(402, 57, 25), Mineral(403, 58, 25),
            Fact(300, "VespeneGeyser", 35, 25, alliance: UnitAlliance.Neutral));

        var candidates = ExpansionSitePlanner.FindCommandCenterCandidates(observation, map);

        Assert.NotEmpty(candidates);
        Assert.True(candidates.Count >= 18, "The planner must retain fallback candidates from more than one friendly mineral expansion.");
        Assert.All(candidates, point => Assert.True(map.Bounds.Contains(point)));
        Assert.True(candidates[0].DistanceTo(new MapPoint(35.5f, 19f)) < 12f);
        Assert.Equal(MapSide.Friendly, map.Side.Assess(candidates[0]));
    }

    [Fact]
    public void BuildsAndTracksExpansionCommandCenterAtPreservedSite()
    {
        var controller = new Sc2ExpansionCommandCenterController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var data = GameData();
        var minerals = new[]
        {
            Mineral(200, 34, 19), Mineral(201, 35, 19),
            Mineral(202, 36, 19), Mineral(203, 37, 19)
        };

        Assert.Null(controller.TryCreateAction(Snapshot(400,
            new[] { Fact(1, "CommandCenter", 10, 10), Fact(20, "SCV", 10, 10) }.Concat(minerals).ToArray()),
            map, data, assignments, new HashSet<ulong> { 20 }));
        Assert.Equal(20UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(20));

        var query = controller.TakePlacementQuery(data);
        Assert.NotNull(query);
        Assert.NotEmpty(query.Placements);
        var response = new ResponseQuery();
        response.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        controller.OnPlacementQueryResult(response);
        var site = controller.ExpansionLocation;
        Assert.NotNull(site);

        var move = controller.TryCreateAction(Snapshot(400,
            new[] { Fact(1, "CommandCenter", 10, 10), Fact(20, "SCV", 10, 10) }.Concat(minerals).ToArray()),
            map, data, assignments, new HashSet<ulong> { 20 });
        Assert.NotNull(move);
        Assert.Equal(16, move.ActionRaw.UnitCommand.AbilityId);
        controller.OnActionResult(ActionResult.Success, 100);

        var build = controller.TryCreateAction(Snapshot(400,
            new[] { Fact(1, "CommandCenter", 10, 10), Fact(20, "SCV", site.Value.X, site.Value.Y) }.Concat(minerals).ToArray()),
            map, data, assignments, new HashSet<ulong> { 20 });
        Assert.NotNull(build);
        Assert.Equal(318, build.ActionRaw.UnitCommand.AbilityId);
        controller.OnActionResult(ActionResult.Success, 101);

        Assert.Null(controller.TryCreateAction(Snapshot(0,
            new[] { Fact(1, "CommandCenter", 10, 10), Fact(20, "SCV", site.Value.X, site.Value.Y),
                Fact(400, "CommandCenter", site.Value.X, site.Value.Y, 0.5f) }.Concat(minerals).ToArray()),
            map, data, assignments, new HashSet<ulong> { 20 }));
        Assert.True(controller.IsStarted);
        Assert.False(controller.IsComplete);

        Assert.Null(controller.TryCreateAction(Snapshot(0,
            new[] { Fact(1, "CommandCenter", 10, 10), Fact(20, "SCV", site.Value.X, site.Value.Y),
                Fact(400, "CommandCenter", site.Value.X, site.Value.Y, 1f) }.Concat(minerals).ToArray()),
            map, data, assignments, new HashSet<ulong> { 20 }));
        Assert.True(controller.IsComplete);
        Assert.False(assignments.IsReserved(20));
        Assert.Equal(site, controller.ExpansionLocation);
    }

    private static SpatialMap Map() => new(
        new MapBounds(0, 0, 100, 100),
        new MapPoint(10, 10),
        new MapPoint(90, 90));

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 18, Name = "CommandCenter", AbilityId = 318, MineralCost = 400 });
        data.Units.Add(new UnitTypeData { UnitId = 45, Name = "SCV", AbilityId = 524, MineralCost = 50, FoodRequired = 1 });
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "Move", LinkIndex = 0 });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(uint minerals, params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>()) { Minerals = minerals };

    private static UnitObservationFacts Mineral(ulong tag, float x, float y) =>
        Fact(tag, "MineralField750", x, y, alliance: UnitAlliance.Neutral);

    private static UnitObservationFacts Fact(
        ulong tag, string kind, float x = 10, float y = 10, float progress = 1,
        UnitAlliance alliance = UnitAlliance.Self) =>
        new(tag, 1, kind, 1, alliance, UnitVisibility.Visible,
            1500, 1500, 0, 0, progress, x, y, true, false, false,
            false, false, false, 100);
}
