using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2MarineRallyControllerTests
{
    [Fact]
    public void MovesFourMarineSquadAcrossTheMapToEnemySideReconAndStopsWhenItArrives()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var gameData = GameData();
        var marines = Enumerable.Range(1, 4).Select(tag => Fact((ulong)tag, "Marine", 10 + tag, 10)).ToArray();
        var decisions = Enumerable.Range(1, 4).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        var moves = controller.TryCreateActions(Snapshot(marines), map, decisions, gameData);
        Assert.Equal(4, moves.Count);
        Assert.Equal(new ulong[] { 1, 2, 3, 4 }, moves.SelectMany(move => move.ActionRaw.UnitCommand.UnitTags));
        Assert.All(moves, move => Assert.Equal(16, move.ActionRaw.UnitCommand.AbilityId));
        var rally = moves[0].ActionRaw.UnitCommand.TargetWorldSpacePos;
        Assert.Equal(MapSide.Enemy, map.Side.Assess(new MapPoint(rally.X, rally.Y)));
        Assert.True(new MapPoint(11, 10).DistanceSquaredTo(new MapPoint(rally.X, rally.Y)) > 2500f);

        var arrived = controller.TryCreateActions(
            Snapshot(Enumerable.Range(1, 4).Select(tag => Fact((ulong)tag, "Marine", rally.X, rally.Y)).ToArray()),
            map, decisions, gameData);
        Assert.Empty(arrived);
    }

    [Fact]
    public void WaitsForFourMarinesBeforeDispatchingTheCrossMapSquad()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var decisions = Enumerable.Range(1, 3).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        Assert.Empty(controller.TryCreateActions(
            Snapshot(Fact(1, "Marine", 10, 10), Fact(2, "Marine", 11, 10), Fact(3, "Marine", 12, 10)),
            map, decisions, GameData()));
    }

    [Fact]
    public void DoesNotRallyMarineWhenCombatPlannerHasAssignedATarget()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var decisions = Enumerable.Range(1, 4).Select(tag =>
            new MarineTacticalDecision((ulong)tag, 2, MarineTacticalAction.Hold, 0, 0,
                "Contact outside local defense envelope")).ToArray();

        Assert.Empty(controller.TryCreateActions(
            Snapshot(Fact(1, "Marine", 10, 10), Fact(2, "Marine", 11, 10),
                Fact(3, "Marine", 12, 10), Fact(4, "Marine", 13, 10)), map, decisions, GameData()));
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
