#nullable enable annotations

using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OpeningBuilderControllerTests
{
    [Fact]
    public void DoesNotReserveOrPullAnScvBeforeDepotIsAffordable()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();

        var action = controller.TryCreateAction(Snapshot(50, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 6, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));

        Assert.Null(action);
        Assert.Null(controller.BuilderTag);
        Assert.False(assignments.IsReserved(10));
        Assert.False(assignments.IsReserved(11));
        Assert.Null(controller.TakePlacementQuery(gameData));
    }

    [Fact]
    public void FallsBackToAnUnreservedScvWhenConstructionCapabilitiesAreNotYetAssigned()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var gameData = GameData();

        var move = controller.TryCreateAction(Snapshot(150, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 6, 5),
            Fact(1, "CommandCenter", 10, 10)), Map(), gameData,
            assignments, new HashSet<ulong>());

        Assert.NotNull(move);
        Assert.Equal(16, move.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));
        Assert.NotNull(controller.TakePlacementQuery(gameData));
    }

    [Fact]
    public void ComparesCargoReturningCrewMemberAgainstEmptyCrewMember()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();

        var action = controller.TryCreateAction(Snapshot(100, 12, 15,
            Fact(10, "SCV", 5, 5, hasCargo: true,
                orders: new[] { new UnitOrderObservationFacts(295, 1, null, null) }),
            Fact(11, "SCV", 9, 9),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));

        // A worker already carrying its completed mineral load is dispatched
        // directly to the site; it must not return to the Command Center first.
        Assert.NotNull(action);
        Assert.Equal(16, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(10UL, action.ActionRaw.UnitCommand.UnitTags[0]);
        Assert.Equal(10UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(10));
        Assert.False(assignments.IsReserved(11));
    }

    [Fact]
    public void WaitsForSelectedMinerToFinishItsCurrentTripEvenWhenDepotIsAffordable()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();
        var miningOrder = new[] { new UnitOrderObservationFacts(295, 20, null, null) };
        var mineral = Fact(20, "MineralField", 12, 12) with { Alliance = UnitAlliance.Neutral };

        var move = controller.TryCreateAction(Snapshot(100, 12, 15,
            Fact(10, "SCV", 5, 5),
            Fact(11, "SCV", 6, 5, orders: miningOrder),
            Fact(1, "CommandCenter", 10, 10), mineral), map, gameData,
            assignments, Crew(10, 11));

        // Reaching the mineral threshold is not permission to interrupt an active
        // harvest. The builder remains reserved, but its gather order is allowed to finish.
        Assert.Null(move);
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));
    }

    [Fact]
    public void DoesNotInterruptAnActiveMineralTripEvenWhenStructureAndNextScvAreFunded()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();
        var mineral = Fact(20, "MineralField", 12, 12) with { Alliance = UnitAlliance.Neutral };
        var miningOrder = new[] { new UnitOrderObservationFacts(295, 20, null, null) };

        var move = controller.TryCreateAction(Snapshot(150, 12, 15,
            Fact(10, "SCV", 5, 5),
            Fact(11, "SCV", 6, 5, orders: miningOrder),
            Fact(1, "CommandCenter", 10, 10), mineral), map, gameData,
            assignments, Crew(10, 11));

        Assert.Null(move);
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));
    }

    [Fact]
    public void StagesBuilderAndWaitsUntilDepotIsAffordable()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();
        SelectBuilder(controller, assignments, map, gameData);
        SetValidPlacement(controller);

        var site = controller.Placement.Value;
        var move = controller.TryCreateAction(SnapshotAt(123, 50, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 7, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));
        Assert.NotNull(move);
        Assert.Equal(16, move.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, move.ActionRaw.UnitCommand.UnitTags[0]);
        Assert.False(move.ActionRaw.UnitCommand.QueueCommand);
        Assert.Equal(site.X, move.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        controller.OnActionResult(ActionResult.Success);
        Assert.True(controller.IsDispatched);

        Assert.Null(controller.TryCreateAction(Snapshot(99, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11)));

        var build = controller.TryCreateAction(Snapshot(100, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));
        Assert.NotNull(build);
        Assert.Equal(319, build.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, build.ActionRaw.UnitCommand.UnitTags[0]);
    }

    [Fact]
    public void ReplacesStagingMoveWhenPlacementQuerySelectsAnotherSite()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();
        SelectBuilder(controller, assignments, map, gameData);

        var placement = new ResponseQuery();
        placement.Placements.Add(new ResponseQueryBuildingPlacement { Result = (ActionResult)2 });
        placement.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        controller.OnPlacementQueryResult(placement);

        var site = controller.Placement.Value;
        var retarget = controller.TryCreateAction(SnapshotAt(101, 50, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 13.18f, 13.18f),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));

        Assert.NotNull(retarget);
        Assert.Equal(16, retarget.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, retarget.ActionRaw.UnitCommand.UnitTags[0]);
        Assert.False(retarget.ActionRaw.UnitCommand.QueueCommand);
        Assert.Equal(site.X, retarget.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.Equal(site.Y, retarget.ActionRaw.UnitCommand.TargetWorldSpacePos.Y);
    }

    [Fact]
    public void RetriesPlacementQueryWhenSc2RejectsEveryCandidate()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var gameData = GameData();
        SelectBuilder(controller, assignments, Map(), gameData);

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
        SelectBuilder(controller, assignments, map, gameData);
        SetValidPlacement(controller);

        var firstMove = controller.TryCreateAction(SnapshotAt(123, 150, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 7, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));
        Assert.NotNull(firstMove);
        controller.OnActionResult(ActionResult.Success);

        var retryMove = controller.TryCreateAction(SnapshotAt(146, 150, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 7, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));
        Assert.NotNull(retryMove);
        Assert.Equal(firstMove.ActionRaw.UnitCommand.AbilityId, retryMove.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, retryMove.ActionRaw.UnitCommand.UnitTags[0]);
    }

    [Fact]
    public void RetainsConstructionCrewBuilderAfterDepotCompletesForBarracksHandoff()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var gameData = GameData();
        SelectBuilder(controller, assignments, map, gameData);
        SetValidPlacement(controller);
        var site = controller.Placement.Value;

        var move = controller.TryCreateAction(SnapshotAt(123, 50, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 7, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));
        Assert.NotNull(move);
        controller.OnActionResult(ActionResult.Success);
        var build = controller.TryCreateAction(Snapshot(100, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));
        Assert.NotNull(build);
        controller.OnActionResult(ActionResult.Success);

        controller.TryCreateAction(Snapshot(0, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10),
            Fact(200, "SupplyDepot", site.X, site.Y, buildProgress: 1)),
            map, gameData, assignments, Crew(10, 11));

        Assert.True(controller.IsCompleted);
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));
    }

    [Fact]
    public void ReusesPreferredBarracksBuilderForExpansionDepotAndReleasesItWhenComplete()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var data = GameData();

        var move = controller.TryCreateAction(Snapshot(100, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 6, 5),
            Fact(1, "CommandCenter", 10, 10)), map, data,
            assignments, Crew(10), preferredBuilderTag: 11,
            releaseBuilderOnCompletion: true);
        Assert.NotNull(move);
        Assert.Equal(11UL, controller.BuilderTag);
        Assert.True(assignments.IsReserved(11));

        var query = controller.TakePlacementQuery(data);
        Assert.NotNull(query);
        var placement = new ResponseQuery();
        placement.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        controller.OnPlacementQueryResult(placement);
        var site = controller.Placement!.Value;

        var build = controller.TryCreateAction(Snapshot(100, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10)), map, data,
            assignments, Crew(10), preferredBuilderTag: 11,
            releaseBuilderOnCompletion: true);
        Assert.NotNull(build);
        Assert.Equal(319, build.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, build.ActionRaw.UnitCommand.UnitTags[0]);

        var complete = controller.TryCreateAction(Snapshot(0, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10),
            Fact(300, "SupplyDepot", site.X, site.Y, buildProgress: 1)),
            map, data, assignments, Crew(10), preferredBuilderTag: 11,
            releaseBuilderOnCompletion: true);
        Assert.Null(complete);
        Assert.True(controller.IsCompleted);
        Assert.False(assignments.IsReserved(11));
    }

    [Fact]
    public void DoesNotMistakeAnExistingNearbyDepotForTheFollowUpDepot()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var map = Map();
        var data = GameData();
        var existingDepot = Fact(200, "SupplyDepot", 13.18f, 13.18f, buildProgress: 1);

        var select = controller.TryCreateAction(Snapshot(150, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 6, 5),
            Fact(1, "CommandCenter", 10, 10), existingDepot), map, data,
            assignments, Crew(10, 11), preferredBuilderTag: 11,
            releaseBuilderOnCompletion: true);
        Assert.NotNull(select);
        Assert.NotNull(controller.TakePlacementQuery(data));

        var placement = new ResponseQuery();
        placement.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        controller.OnPlacementQueryResult(placement);
        var site = controller.Placement!.Value;

        var build = controller.TryCreateAction(Snapshot(150, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", site.X, site.Y),
            Fact(1, "CommandCenter", 10, 10), existingDepot), map, data,
            assignments, Crew(10, 11), preferredBuilderTag: 11,
            releaseBuilderOnCompletion: true);

        Assert.NotNull(build);
        Assert.Equal(319, build.ActionRaw.UnitCommand.AbilityId);
        Assert.False(controller.IsCompleted);
    }

    [Fact]
    public void PlacesOpeningDepotBeyondObservedMineralLineAwayFromTownHall()
    {
        var controller = new Sc2OpeningBuilderController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var gameData = GameData();
        var minerals = new[]
        {
            Fact(20, "MineralField", 19, 19) with { Alliance = UnitAlliance.Neutral },
            Fact(21, "MineralField", 21, 19) with { Alliance = UnitAlliance.Neutral },
            Fact(22, "MineralField", 19, 21) with { Alliance = UnitAlliance.Neutral },
            Fact(23, "MineralField", 21, 21) with { Alliance = UnitAlliance.Neutral }
        };

        var action = controller.TryCreateAction(Snapshot(100, 12, 15,
            Fact(10, "SCV", 12, 12, hasCargo: true),
            Fact(1, "CommandCenter", 10, 10), minerals[0], minerals[1], minerals[2], minerals[3]),
            Map(), gameData, assignments, Crew(10));

        Assert.NotNull(action);
        var query = controller.TakePlacementQuery(gameData);
        Assert.NotNull(query);
        var target = query.Placements[0].TargetPos;
        var centerX = 20f;
        var centerY = 20f;
        var directionX = MathF.Sqrt(0.5f);
        var directionY = MathF.Sqrt(0.5f);
        var towardMinerals = (target.X - centerX) * directionX + (target.Y - centerY) * directionY;

        Assert.True(towardMinerals > 0,
            "The Depot candidate should be beyond the mineral line, on the side away from the town hall.");
        Assert.InRange(MathF.Sqrt((target.X - centerX) * (target.X - centerX) +
            (target.Y - centerY) * (target.Y - centerY)), 3.5f, 6f);
    }

    private static void SelectBuilder(
        Sc2OpeningBuilderController controller,
        WorkerResourceAssignmentPlanner assignments,
        SpatialMap map,
        Sc2GameDataCatalog gameData)
    {
        var selection = controller.TryCreateAction(Snapshot(100, 12, 15,
            Fact(10, "SCV", 5, 5), Fact(11, "SCV", 6, 5),
            Fact(1, "CommandCenter", 10, 10)), map, gameData,
            assignments, Crew(10, 11));
        Assert.NotNull(selection);
        Assert.Equal(16, selection.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(11UL, selection.ActionRaw.UnitCommand.UnitTags[0]);
        controller.OnActionResult(ActionResult.Success);
    }

    private static HashSet<ulong> Crew(params ulong[] tags) => new(tags);

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
        ulong tag, string kind, float x, float y, float buildProgress = 1,
        bool hasCargo = false, IReadOnlyList<UnitOrderObservationFacts>? orders = null) =>
        new UnitObservationFacts(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, buildProgress, x, y, true,
            hasCargo, orders is { Count: > 0 }, false, false, false, 100)
        {
            Orders = orders ?? Array.Empty<UnitOrderObservationFacts>()
        };
}
