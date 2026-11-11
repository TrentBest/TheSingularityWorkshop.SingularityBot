using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2SecondBarracksControllerTests
{
    [Fact]
    public void BuildsSecondBarracksAndReleasesBuilderForContinuedScaling()
    {
        var controller = new Sc2SecondBarracksController();
        var assignments = new WorkerResourceAssignmentPlanner();
        assignments.Reserve(10);
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64),
            new MapPoint(10, 10), new MapPoint(54, 54));
        var data = GameData();
        var crew = new HashSet<ulong> { 10, 11 };

        Assert.Null(controller.TryCreateAction(Snapshot(150,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", 15, 15)),
            map, data, assignments, crew));
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));
        Assert.True(assignments.IsReserved(10));

        var query = controller.TakePlacementQuery(data);
        Assert.NotNull(query);
        var result = new ResponseQuery();
        result.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        controller.OnPlacementQueryResult(result);
        var site = new MapPoint(query.Placements[0].TargetPos.X, query.Placements[0].TargetPos.Y);

        var move = controller.TryCreateAction(Snapshot(150,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", 15, 15)),
            map, data, assignments, crew);
        Assert.NotNull(move);
        Assert.Equal(16, move.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, move.ActionRaw.UnitCommand.UnitTags[0]);
        controller.OnActionResult(ActionResult.Success);

        Assert.Null(controller.TryCreateAction(Snapshot(149,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", 15, 15)),
            map, data, assignments, crew));

        var build = controller.TryCreateAction(Snapshot(150,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", 15, 15)),
            map, data, assignments, crew);
        Assert.NotNull(build);
        Assert.Equal(321, build.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, build.ActionRaw.UnitCommand.UnitTags[0]);
        controller.OnActionResult(ActionResult.Success);

        Assert.Null(controller.TryCreateAction(Snapshot(0,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", 15, 15),
            Fact(201, "Barracks", site.X, site.Y, 0.5f)),
            map, data, assignments, crew));
        Assert.False(controller.IsComplete);

        Assert.Null(controller.TryCreateAction(Snapshot(0,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", 15, 15),
            Fact(201, "Barracks", site.X, site.Y)),
            map, data, assignments, crew));
        Assert.False(controller.IsComplete);
        Assert.False(assignments.IsReserved(11));
        Assert.True(assignments.IsReserved(10));
    }

    [Fact]
    public void StartsSecondDepotWhileBarracksTwoIsStillBuilding()
    {
        var controller = new Sc2SecondBarracksController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64),
            new MapPoint(10, 10), new MapPoint(54, 54));
        var data = GameData();
        var crew = new HashSet<ulong> { 10, 11 };

        Assert.Null(controller.TryCreateAction(Snapshot(150,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", 15, 15)),
            map, data, assignments, crew));
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));

        // SC2 now reports Barracks two as an in-progress structure. The
        // second builder remains assigned to it while the follow-up Depot
        // is authorized for the first-Barracks builder.
        Assert.Null(controller.TryCreateAction(Snapshot(250,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", 18, 18),
            Fact(12, "SCV", 12, 10),
            Fact(1, "CommandCenter", 10, 10),
            Fact(200, "Barracks", 15, 15),
            Fact(201, "Barracks", 20, 20, 0.35f)),
            map, data, assignments, crew));
        Assert.True(controller.RequiresSupplyDepot);
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));

        controller.NotifySupplyDepotCompleted(12);
        Assert.False(controller.RequiresSupplyDepot);

        // Barracks two is still progressing: no third Barracks is dispatched.
        Assert.Null(controller.TryCreateAction(Snapshot(0,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", 18, 18),
            Fact(12, "SCV", 12, 10),
            Fact(1, "CommandCenter", 10, 10),
            Fact(200, "Barracks", 15, 15),
            Fact(201, "Barracks", 20, 20, 0.65f)),
            map, data, assignments, crew));
        Assert.False(controller.RequiresSupplyDepot);
        Assert.Equal(11UL, controller.BuilderTag);
    }

    [Fact]
    public void PausesAfterSecondBarracksUntilSupplyDepotAndReusesFirstBarracksBuilder()
    {
        var controller = new Sc2SecondBarracksController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64),
            new MapPoint(10, 10), new MapPoint(54, 54));
        var data = GameData();

        // The second Barracks is now complete. Expansion must pause before
        // reserving another worker for Barracks three.
        Assert.Null(controller.TryCreateAction(Snapshot(300,
            Fact(11, "SCV", 10, 10), Fact(12, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10),
            Fact(200, "Barracks", 15, 15), Fact(201, "Barracks", 20, 20)),
            map, data, assignments, new HashSet<ulong> { 11, 12 }));
        Assert.True(controller.RequiresSupplyDepot);
        Assert.Null(controller.BuilderTag);

        // The SCV that built the first Barracks is no longer in the
        // supplemental construction-crew set, but is explicitly recycled
        // after the follow-up Depot completes.
        controller.NotifySupplyDepotCompleted(12);
        Assert.False(controller.RequiresSupplyDepot);
        Assert.Null(controller.TryCreateAction(Snapshot(300,
            Fact(11, "SCV", 10, 10), Fact(12, "SCV", 11, 10),
            Fact(1, "CommandCenter", 10, 10),
            Fact(200, "Barracks", 15, 15), Fact(201, "Barracks", 20, 20)),
            map, data, assignments, new HashSet<ulong> { 11 }));
        Assert.Equal(12UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(12));
    }

    [Fact]
    public void DoesNotStealCargoCarryingOrAlreadyReservedWorkers()
    {
        var controller = new Sc2SecondBarracksController();
        var assignments = new WorkerResourceAssignmentPlanner();
        assignments.Reserve(10);
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64),
            new MapPoint(10, 10), new MapPoint(54, 54));
        var crew = new HashSet<ulong> { 10, 11 };

        Assert.Null(controller.TryCreateAction(Snapshot(150,
            Fact(10, "SCV", 10, 10), Fact(11, "SCV", 11, 10) with { HasCargo = true },
            Fact(1, "CommandCenter", 10, 10), Fact(200, "Barracks", 15, 15)),
            map, GameData(), assignments, crew));
        Assert.Null(controller.BuilderTag);
    }

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 21, Name = "Barracks", AbilityId = 321, MineralCost = 150 });
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "Move" });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(uint minerals, params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>()) { Minerals = minerals };

    private static UnitObservationFacts Fact(ulong tag, string kind, float x = 10, float y = 10, float progress = 1) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            1500, 1500, 0, 0, progress, x, y, true, false, false,
            false, false, false, 100);
}
