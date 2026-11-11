using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2MarineRallyControllerTests
{
    [Fact]
    public void MovesFourMarineSquadToConservativeEnemyObservationWaypointAndStopsWhenItArrives()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var gameData = GameData();
        var offsets = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4,
            map.Side.EnemyStart.X - map.Side.FriendlyStart.X,
            map.Side.EnemyStart.Y - map.Side.FriendlyStart.Y);
        var marines = offsets.Select((offset, index) => Fact((ulong)(index + 1), "Marine",
            map.Side.FriendlyStart.X + offset.X, map.Side.FriendlyStart.Y + offset.Y)).ToArray();
        var decisions = Enumerable.Range(1, 4).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        var moves = controller.TryCreateActions(Snapshot(marines), map, decisions, gameData);
        Assert.Equal(4, moves.Count);
        Assert.Equal(new ulong[] { 1, 2, 3, 4 }, moves.SelectMany(move => move.ActionRaw.UnitCommand.UnitTags));
        Assert.All(moves, move => Assert.Equal(16, move.ActionRaw.UnitCommand.AbilityId));
        var targets = moves.Select(move => move.ActionRaw.UnitCommand.TargetWorldSpacePos).ToArray();
        Assert.Equal(4, targets.Select(point => (point.X, point.Y)).Distinct().Count());
        Assert.All(targets, point => Assert.Equal(MapSide.Enemy, map.Side.Assess(new MapPoint(point.X, point.Y))));
        Assert.All(targets, point => Assert.True(new MapPoint(11, 10).DistanceSquaredTo(new MapPoint(point.X, point.Y)) > 2500f));
        Assert.All(targets, point => Assert.True(
            new MapPoint(point.X, point.Y).DistanceTo(map.Side.EnemyStart) >= 18f,
            "The observation waypoint must preserve a conservative stand-off from the enemy start."));

        var arrived = controller.TryCreateActions(
            Snapshot(Enumerable.Range(0, 4).Select(index =>
                Fact((ulong)(index + 1), "Marine", targets[index].X, targets[index].Y)).ToArray()),
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

    [Fact]
    public void StreamsLaterMarinesIntoFormationWithoutReshufflingArrivedSquad()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var gameData = GameData();
        var initialOffsets = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4,
            map.Side.EnemyStart.X - map.Side.FriendlyStart.X,
            map.Side.EnemyStart.Y - map.Side.FriendlyStart.Y);
        var initial = initialOffsets.Select((offset, index) => Fact((ulong)(index + 1), "Marine",
            map.Side.FriendlyStart.X + offset.X, map.Side.FriendlyStart.Y + offset.Y)).ToArray();
        var firstDecisions = Enumerable.Range(1, 4).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        var firstMoves = controller.TryCreateActions(Snapshot(initial), map, firstDecisions, gameData);
        var firstTargets = firstMoves.ToDictionary(
            move => move.ActionRaw.UnitCommand.UnitTags.Single(),
            move => move.ActionRaw.UnitCommand.TargetWorldSpacePos);
        var arrived = initialOffsets.Select((_, index) => Fact((ulong)(index + 1), "Marine",
            firstTargets[(ulong)(index + 1)].X, firstTargets[(ulong)(index + 1)].Y)).ToList();
        arrived.Add(Fact(5, "Marine", map.Side.FriendlyStart.X, map.Side.FriendlyStart.Y));

        var decisions = Enumerable.Range(1, 5).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();
        var reinforcementMoves = controller.TryCreateActions(
            Snapshot(arrived.ToArray()), map, decisions, gameData);

        var reinforcement = Assert.Single(reinforcementMoves);
        Assert.Equal(5UL, Assert.Single(reinforcement.ActionRaw.UnitCommand.UnitTags));
        Assert.Equal(16, reinforcement.ActionRaw.UnitCommand.AbilityId);
    }

    private static Sc2GameDataCatalog GameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 48, Name = "Marine", AbilityId = 560, MineralCost = 50, FoodRequired = 1 });
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
