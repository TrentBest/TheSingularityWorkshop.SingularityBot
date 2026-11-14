using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ZerglingOpeningRuntimeTests
{
    [Fact]
    public void Runtime_QueriesPoolPlacementBuildsWithDroneThenTrainsZergling()
    {
        var runtime = CreateRuntime();
        var opening = Observation(
            300, 0, 12, 14,
            Unit(1, "Hatchery"),
            Unit(2, "Drone"),
            Unit(3, "Drone"),
            Unit(4, "Larva"));

        Assert.Empty(runtime.OnFrame(opening));
        var query = runtime.TakeQueryRequest();
        Assert.NotNull(query);
        Assert.Equal(48, query.Placements.Count);
        Assert.All(query.Placements, placement =>
        {
            Assert.Equal(1344, placement.AbilityId);
            Assert.Equal(1ul, placement.PlacingUnitTag);
        });

        var queryResult = new ResponseQuery();
        queryResult.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        for (var i = 1; i < query.Placements.Count; i++)
            queryResult.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Error });
        runtime.OnQueryResult(queryResult);

        var buildActions = runtime.OnFrame(opening);
        var build = Assert.Single(buildActions);
        Assert.Equal(1344, build.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(2ul, Assert.Single(build.ActionRaw.UnitCommand.UnitTags));
        Assert.Equal("BuildSubmitted", runtime.State);

        var accepted = new ResponseAction();
        accepted.Result.Add(ActionResult.Success);
        runtime.OnActionResult(accepted);
        Assert.Equal("AwaitingPool", runtime.State);

        var poolReady = Observation(
            100, 0, 12, 14,
            Unit(1, "Hatchery"),
            Unit(5, "SpawningPool"),
            Unit(6, "Larva"),
            Unit(7, "Larva"));
        var production = runtime.OnFrame(poolReady);
        Assert.Single(production);
        Assert.Equal(1343, production[0].ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(6ul, Assert.Single(production[0].ActionRaw.UnitCommand.UnitTags));
        Assert.Equal("Producing", runtime.State);
    }

    [Fact]
    public void Runtime_DoesNotBuildUntilPlacementQueryApprovesASite()
    {
        var runtime = CreateRuntime();
        var opening = Observation(300, 0, 12, 14,
            Unit(1, "Hatchery"), Unit(2, "Drone"));
        Assert.Empty(runtime.OnFrame(opening));
        var query = runtime.TakeQueryRequest();
        Assert.NotNull(query);

        var rejected = new ResponseQuery();
        foreach (var _ in query.Placements)
            rejected.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.CantBuildLocationInvalid });
        runtime.OnQueryResult(rejected);

        Assert.Empty(runtime.OnFrame(opening));
        Assert.Equal("NeedPlacement", runtime.State);
        Assert.NotNull(runtime.TakeQueryRequest());
    }

    [Fact]
    public void Runtime_DoesNotRetryAcceptedPoolBuildBeforeStructureEvidence()
    {
        var runtime = CreateRuntime();
        var opening = Observation(300, 0, 12, 14,
            Unit(1, "Hatchery"), Unit(2, "Drone"));
        Assert.Empty(runtime.OnFrame(opening));
        var query = runtime.TakeQueryRequest();
        var approved = new ResponseQuery();
        approved.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        for (var i = 1; i < query.Placements.Count; i++)
            approved.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.CantBuildLocationInvalid });
        runtime.OnQueryResult(approved);
        Assert.Single(runtime.OnFrame(opening));

        var accepted = new ResponseAction();
        accepted.Result.Add(ActionResult.Success);
        runtime.OnActionResult(accepted);

        Assert.Empty(runtime.OnFrame(opening));
        Assert.Equal("AwaitingPool", runtime.State);
        Assert.Null(runtime.TakeQueryRequest());
    }

    private static ZerglingOpeningRuntime CreateRuntime()
    {
        var profile = Sc2RaceProductionProfileCatalog.CreateDefault().Profiles
            .Single(item => item.ModeId == "zerg.zergling.v1");
        return new ZerglingOpeningRuntime(
            new Sc2UnitFacts(104, "Drone", 0, 50, 0, 1, 0),
            new Sc2UnitFacts(200, "SpawningPool", 1344, 200, 0, 0, 0),
            Ability(1344),
            profile,
            new Sc2UnitFacts(105, "Zergling", 1343, 25, 0, 0.5f, 0),
            new Sc2UnitFacts(151, "Larva", 0, 0, 0, 0, 0));
    }

    private static Sc2AbilityFacts Ability(uint id) =>
        new(id, "Build_SpawningPool", 0, "BuildSpawningPool", "Build Spawning Pool",
            0, true, default, true, false, 1.5f, 0);

    private static NormalizedObservation Observation(
        uint minerals, uint vespene, uint foodUsed, uint foodCap,
        params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>())
        {
            Minerals = minerals, Vespene = vespene, FoodUsed = foodUsed, FoodCap = foodCap
        };

    private static UnitObservationFacts Unit(ulong tag, string kind) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            100, 100, 0, 0, 1, 20, 20, true,
            false, false, false, false, false, 100);

}
