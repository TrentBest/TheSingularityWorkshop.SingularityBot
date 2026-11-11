using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2MarineRallyControllerTests
{
    [Fact]
    public void MovesIdleMarineAcrossTheMapToEnemySideReconAndStopsWhenItArrives()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var gameData = GameData();
        var decision = new MarineTacticalDecision(1, null, MarineTacticalAction.Hold, 0, 0,
            "No visible attackable target");

        var move = Assert.Single(controller.TryCreateActions(
            Snapshot(Fact(1, "Marine", 10, 10)), map, new[] { decision }, gameData));

        Assert.Equal(16, move.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(1UL, Assert.Single(move.ActionRaw.UnitCommand.UnitTags));
        var rally = move.ActionRaw.UnitCommand.TargetWorldSpacePos;
        Assert.Equal(MapSide.Enemy, map.Side.Assess(new MapPoint(rally.X, rally.Y)));
        Assert.True(new MapPoint(10, 10).DistanceSquaredTo(new MapPoint(rally.X, rally.Y)) > 2500f);

        var arrived = controller.TryCreateActions(
            Snapshot(Fact(1, "Marine", rally.X, rally.Y)), map, new[] { decision }, gameData);
        Assert.Empty(arrived);
    }

    [Fact]
    public void DoesNotRallyMarineWhenCombatPlannerHasAssignedATarget()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var decision = new MarineTacticalDecision(1, 2, MarineTacticalAction.Hold, 0, 0,
            "Contact outside local defense envelope");

        Assert.Empty(controller.TryCreateActions(
            Snapshot(Fact(1, "Marine", 10, 10)), map, new[] { decision }, GameData()));
    }

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "Move" });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Fact(ulong tag, string kind, float x, float y) =>
        new(tag, 1, kind, 48, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, x, y, true, false, false,
            false, false, false, 100);
}
