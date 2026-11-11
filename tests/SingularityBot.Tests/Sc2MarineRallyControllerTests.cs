using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2MarineRallyControllerTests
{
    [Fact]
    public void FirstMarineScoutsTheFarthestSafeAdjacentSectorWhileFourOthersDeploy()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var gameData = GameData();
        var offsets = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4,
            map.Side.EnemyStart.X - map.Side.FriendlyStart.X,
            map.Side.EnemyStart.Y - map.Side.FriendlyStart.Y);
        var marines = new List<UnitObservationFacts>
        {
            Fact(1, "Marine", map.Side.FriendlyStart.X, map.Side.FriendlyStart.Y)
        };
        marines.AddRange(offsets.Select((offset, index) => Fact((ulong)(index + 2), "Marine",
            map.Side.FriendlyStart.X + offset.X, map.Side.FriendlyStart.Y + offset.Y)));
        var decisions = Enumerable.Range(1, 5).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        var moves = controller.TryCreateActions(Snapshot(marines.ToArray()), map, decisions, gameData);
        Assert.Equal(5, moves.Count);
        var scoutMove = Assert.Single(moves,
            move => move.ActionRaw.UnitCommand.UnitTags.Single() == 1);
        var scoutPoint = scoutMove.ActionRaw.UnitCommand.TargetWorldSpacePos;
        var scoutTarget = new MapPoint(scoutPoint.X, scoutPoint.Y);
        Assert.Equal(new SectorId(1, 1), map.GetSector(scoutTarget).Id);
        Assert.NotEqual(MapSide.Enemy, map.Side.Assess(scoutTarget));
        Assert.True(map.GetNeighbors(map.GetSector(map.Side.FriendlyStart).Id)
            .Select(sector => sector.Center)
            .Where(point => map.Side.Assess(point) != MapSide.Enemy)
            .All(point => point.DistanceSquaredTo(map.Side.FriendlyStart) <=
                scoutTarget.DistanceSquaredTo(map.Side.FriendlyStart)));

        var squadMoves = moves.Where(move => move.ActionRaw.UnitCommand.UnitTags.Single() != 1).ToArray();
        Assert.Equal(4, squadMoves.Length);
        Assert.All(squadMoves, move =>
        {
            var marine = marines.Single(unit => unit.Tag == move.ActionRaw.UnitCommand.UnitTags.Single());
            var point = move.ActionRaw.UnitCommand.TargetWorldSpacePos;
            var waypoint = new MapPoint(point.X, point.Y);
            Assert.True(waypoint.DistanceTo(new MapPoint(marine.PositionX, marine.PositionY)) <= 4.01f);
            Assert.True(waypoint.DistanceTo(map.Side.EnemyStart) <
                new MapPoint(marine.PositionX, marine.PositionY).DistanceTo(map.Side.EnemyStart));
        });

        var targets = moves.ToDictionary(
            move => move.ActionRaw.UnitCommand.UnitTags.Single(),
            move => move.ActionRaw.UnitCommand.TargetWorldSpacePos);
        var arrived = marines.Select(marine =>
        {
            var target = targets[marine.Tag];
            return Fact(marine.Tag, "Marine", target.X, target.Y);
        }).ToArray();
        var nextWaypoints = controller.TryCreateActions(Snapshot(arrived), map, decisions, gameData);
        Assert.Equal(4, nextWaypoints.Count);
        Assert.DoesNotContain(nextWaypoints, move => move.ActionRaw.UnitCommand.UnitTags.Single() == 1);
        Assert.All(nextWaypoints, move =>
        {
            var marine = arrived.Single(unit => unit.Tag == move.ActionRaw.UnitCommand.UnitTags.Single());
            var point = move.ActionRaw.UnitCommand.TargetWorldSpacePos;
            Assert.True(new MapPoint(point.X, point.Y)
                .DistanceTo(new MapPoint(marine.PositionX, marine.PositionY)) <= 7.01f);
        });
    }

    [Fact]
    public void FirstMarineBeginsScoutingBeforeTheRemainingSquadIsReady()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var decisions = Enumerable.Range(1, 3).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        var moves = controller.TryCreateActions(
            Snapshot(Fact(1, "Marine", 10, 10), Fact(2, "Marine", 11, 10), Fact(3, "Marine", 12, 10)),
            map, decisions, GameData());

        var scoutMove = Assert.Single(moves);
        Assert.Equal(1UL, Assert.Single(scoutMove.ActionRaw.UnitCommand.UnitTags));
        var target = scoutMove.ActionRaw.UnitCommand.TargetWorldSpacePos;
        Assert.Equal(new SectorId(1, 1), map.GetSector(new MapPoint(target.X, target.Y)).Id);
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
        var initial = new List<UnitObservationFacts>
        {
            Fact(1, "Marine", map.Side.FriendlyStart.X, map.Side.FriendlyStart.Y)
        };
        initial.AddRange(initialOffsets.Select((offset, index) => Fact((ulong)(index + 2), "Marine",
            map.Side.FriendlyStart.X + offset.X, map.Side.FriendlyStart.Y + offset.Y)));
        var firstDecisions = Enumerable.Range(1, 5).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        var firstMoves = controller.TryCreateActions(Snapshot(initial.ToArray()), map, firstDecisions, gameData);
        Assert.Equal(5, firstMoves.Count);
        var firstTargets = firstMoves.ToDictionary(
            move => move.ActionRaw.UnitCommand.UnitTags.Single(),
            move => move.ActionRaw.UnitCommand.TargetWorldSpacePos);
        var arrived = initial.Select(marine =>
        {
            var target = firstTargets[marine.Tag];
            return Fact(marine.Tag, "Marine", target.X, target.Y);
        }).ToList();
        arrived.Add(Fact(6, "Marine", map.Side.FriendlyStart.X, map.Side.FriendlyStart.Y));

        var decisions = Enumerable.Range(1, 6).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();
        var reinforcementMoves = controller.TryCreateActions(
            Snapshot(arrived.ToArray()), map, decisions, gameData);

        Assert.Equal(5, reinforcementMoves.Count);
        var reinforcement = Assert.Single(reinforcementMoves,
            move => move.ActionRaw.UnitCommand.UnitTags.Single() == 6UL);
        Assert.Equal(16, reinforcement.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(4, reinforcementMoves.Count(move =>
            move.ActionRaw.UnitCommand.UnitTags.Single() != 6UL));
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
