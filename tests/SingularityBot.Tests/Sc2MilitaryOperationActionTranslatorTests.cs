using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2MilitaryOperationActionTranslatorTests
{
    [Fact]
    public void Translate_FollowsValidatedRouteOneWaypointAtATime()
    {
        var plan = Plan(
            new("safe-route", "Safe route",
                [new("start", 2, 2), new("objective", 8, 8)]));
        var intent = Intent("operation", "safe-route", "objective-location");
        var translator = new Sc2MilitaryOperationActionTranslator();
        var terrain = OpenTerrain();
        var topology = SpatialTopology.Build(terrain);
        var gameData = MoveCatalog();

        var first = translator.Translate(intent, plan, new ulong[] { 10 },
            Observation(Unit(10, 0.5f, 0.5f)), gameData, terrain, topology);

        var firstAction = Assert.Single(first.Actions);
        Assert.False(first.IsBlocked);
        Assert.Equal(16, firstAction.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(2, firstAction.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.Equal(2, firstAction.ActionRaw.UnitCommand.TargetWorldSpacePos.Y);

        var second = translator.Translate(intent, plan, new ulong[] { 10 },
            Observation(Unit(10, 2, 2)), gameData, terrain, topology);

        var secondAction = Assert.Single(second.Actions);
        Assert.Equal(8, secondAction.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.Equal(8, secondAction.ActionRaw.UnitCommand.TargetWorldSpacePos.Y);
    }


    [Fact]
    public void Translate_UsesGroundPathingForGroundForcesButNotAirRoutes()
    {
        var cells = Enumerable.Repeat((byte)1, 100).ToArray();
        cells[5 * 10 + 5] = 0;
        var grid = new SpatialGridSnapshot(10, 10, 10, 10, cells);
        var terrain = new SpatialTerrain(grid, null, null);
        var topology = SpatialTopology.Build(terrain);
        var route = new MilitaryRouteDefinition(
            "air-route", "Air route",
            [new("start", 1, 1), new("finish", 9, 9)],
            MilitaryRouteConstraints.PreferPathableTerrain);
        var plan = Plan(route);
        var translator = new Sc2MilitaryOperationActionTranslator();

        var ground = translator.Translate(
            Intent("operation", "air-route", "objective-location"),
            plan, new ulong[] { 10 },
            Observation(Unit(10, 1, 1, isFlying: false)),
            MoveCatalog(), terrain, topology);

        Assert.True(ground.IsBlocked);
        Assert.Empty(ground.Actions);

        var air = translator.Translate(
            Intent("operation", "air-route", "objective-location"),
            plan, new ulong[] { 10 },
            Observation(Unit(10, 1, 1, isFlying: true)),
            MoveCatalog(), terrain, topology);

        Assert.False(air.IsBlocked);
        Assert.Single(air.Actions);
    }

    [Fact]
    public void Translate_RejectsAvoidContactRouteWhenVisibleEnemyIsTooClose()
    {
        var route = new MilitaryRouteDefinition(
            "evasive", "Evasive",
            [new("start", 1, 1), new("finish", 9, 9)],
            MilitaryRouteConstraints.AvoidContact | MilitaryRouteConstraints.PreferPathableTerrain);
        var plan = Plan(route);
        var enemy = Unit(99, 5, 5, UnitAlliance.Enemy, unitKind: "UnknownEnemy");
        var result = new Sc2MilitaryOperationActionTranslator().Translate(
            Intent("operation", "evasive", "objective-location"),
            plan,
            new ulong[] { 10 },
            Observation(Unit(10, 1, 1), enemy),
            MoveCatalog(),
            OpenTerrain(),
            SpatialTopology.Build(OpenTerrain()));

        Assert.True(result.IsBlocked);
        Assert.Empty(result.Actions);
        Assert.Contains("visible enemy contact", result.Reason);
    }

    [Fact]
    public void Translate_ResolvesRequiredPointAbilityFromGameDataWithoutGuessingItsId()
    {
        var plan = Plan(
            new("jump", "Jump route", [new("origin", 1, 1), new("landing", 8, 8)]),
            requiredAbility: "TacticalJump");
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData
        {
            AbilityId = 999,
            LinkName = "TacticalJump",
            ButtonName = "TacticalJump",
            FriendlyName = "Tactical Jump",
            Available = true,
            Target = AbilityData.Types.Target.Point,
            CastRange = 20
        });

        var result = new Sc2MilitaryOperationActionTranslator().Translate(
            Intent("operation", "jump", "objective-location", "TacticalJump"),
            plan,
            new ulong[] { 10 },
            Observation(Unit(10, 1, 1)),
            Sc2GameDataCatalog.Create(data),
            OpenTerrain(),
            SpatialTopology.Build(OpenTerrain()));

        var action = Assert.Single(result.Actions);
        Assert.False(result.IsBlocked);
        Assert.Equal(999, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(8, action.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.Equal(8, action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y);
    }

    private static MilitaryPlanDefinition Plan(
        MilitaryRouteDefinition route,
        string requiredAbility = null) =>
        new()
        {
            Id = "test-plan",
            Name = "Test plan",
            Locations = [new("objective-location", "Objective", 8, 8)],
            Routes = [route],
            Operations =
            [
                new("operation", "Operation", MilitaryForceRole.Harass,
                    route.Id, "objective-location", RequiredAbility: requiredAbility)
            ]
        };

    private static MilitaryOperationIntent Intent(
        string id, string routeId, string locationId, string ability = null) =>
        new(id, MilitaryForceRole.Harass, routeId, locationId, 1, ability, null,
            MilitaryCompletionCondition.AtObjective);

    private static Sc2GameDataCatalog MoveCatalog()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData
        {
            AbilityId = 16,
            LinkName = "move",
            LinkIndex = 0,
            ButtonName = "Move",
            Available = true,
            Target = AbilityData.Types.Target.Point
        });
        return Sc2GameDataCatalog.Create(data);
    }

    private static SpatialTerrain OpenTerrain()
    {
        var grid = new SpatialGridSnapshot(10, 10, 10, 10, Enumerable.Repeat((byte)1, 100).ToArray());
        return new SpatialTerrain(grid, null, null);
    }

    private static NormalizedObservation Observation(params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Unit(
        ulong tag,
        float x,
        float y,
        UnitAlliance alliance = UnitAlliance.Self,
        string unitKind = "Battlecruiser",
        bool isFlying = true) =>
        new(tag, alliance == UnitAlliance.Self ? 1u : 2u, unitKind, 1, alliance,
            UnitVisibility.Visible, 550, 550, 0, 0, 1, x, y, true, false, false,
            isFlying, false, false, 100);
}
