using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2OpeningSprayControllerTests
{
    [Fact]
    public void QueriesWorkerAvailabilityThenSpraysAndResumesSameMineralAssignment()
    {
        var controller = new Sc2OpeningSprayController();
        var planner = new WorkerResourceAssignmentPlanner();
        var data = GameData();
        var observation = Snapshot(
            Fact(10, "SCV", 5, 5, hasOrders: true,
                orders: new[] { new UnitOrderObservationFacts(3666, 20, null, null) }),
            Fact(1, "CommandCenter", 10, 10),
            Fact(20, "MineralField750", 4, 5) with { Alliance = UnitAlliance.Neutral, OwnerId = 0 });
        var assignments = planner.Reconcile(observation);

        var query = controller.PrepareAbilityQuery(observation, assignments, data, true, planner);

        Assert.Single(query.Abilities);
        Assert.Equal(10UL, query.Abilities[0].UnitTag);
        Assert.True(planner.IsReserved(10));

        var response = new ResponseQuery();
        var available = new ResponseQueryAvailableAbilities { UnitTag = 10 };
        available.Abilities.Add(new AvailableAbility { AbilityId = 2117, RequiresPoint = true });
        response.Abilities.Add(available);
        controller.OnQueryResult(response, planner);

        var spray = controller.TryCreateAction(observation, data, planner);
        Assert.Equal(2117, spray.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(5f, spray.ActionRaw.UnitCommand.TargetWorldSpacePos.X);

        controller.OnActionResult(ActionResult.Success, planner);
        var gather = controller.TryCreateAction(observation, data, planner);
        Assert.Equal(1, gather.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(20UL, gather.ActionRaw.UnitCommand.TargetUnitTag);

        controller.OnActionResult(ActionResult.Success, planner);

        Assert.True(controller.IsComplete);
        Assert.False(planner.IsReserved(10));
        Assert.True(planner.TryGet(10, out var restored));
        Assert.Equal(20UL, restored.MineralTag);
    }

    [Fact]
    public void DoesNotFabricateSprayWhenGameDataHasNoUnambiguousAbility()
    {
        var controller = new Sc2OpeningSprayController();
        var planner = new WorkerResourceAssignmentPlanner();
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 1, LinkName = "Smart" });
        data.Abilities.Add(new AbilityData { AbilityId = 3666, LinkName = "Harvest_Gather" });
        var catalog = Sc2GameDataCatalog.Create(data);
        var observation = Snapshot(
            Fact(10, "SCV", 5, 5),
            Fact(1, "CommandCenter", 10, 10),
            Fact(20, "MineralField750", 4, 5) with { Alliance = UnitAlliance.Neutral, OwnerId = 0 });
        var assignments = planner.Reconcile(observation);

        Assert.Null(controller.PrepareAbilityQuery(observation, assignments, catalog, true, planner));
        Assert.True(controller.IsComplete);
        Assert.False(planner.IsReserved(10));
    }

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 45, Name = "SCV", AbilityId = 524, MineralCost = 50, FoodRequired = 1 });
        data.Units.Add(new UnitTypeData { UnitId = 19, Name = "SupplyDepot", AbilityId = 319, MineralCost = 100 });
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "Move" });
        data.Abilities.Add(new AbilityData { AbilityId = 2117, LinkName = "Spray" });
        data.Abilities.Add(new AbilityData { AbilityId = 3666, LinkName = "Harvest_Gather" });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Fact(
        ulong tag, string kind, float x, float y, bool hasOrders = false,
        IReadOnlyList<UnitOrderObservationFacts> orders = null) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, x, y, true,
            false, hasOrders, false, false, false, 100)
        {
            Orders = orders ?? Array.Empty<UnitOrderObservationFacts>()
        };
}
