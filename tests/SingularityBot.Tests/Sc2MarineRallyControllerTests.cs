using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2MarineRallyControllerTests
{
    [Fact]
    public void DedicatedScoutSelectionUsesTheSameUsableWeaponEligibilityAsScoutingAdvisor()
    {
        var controller = new Sc2MarineRallyController();
        var observation = Snapshot(
            Fact(1, "Marine", 10, 10),
            new UnitObservationFacts(2, 2, "Marine", 48, UnitAlliance.Self, UnitVisibility.Visible,
                45, 45, 0, 0, 1, 12, 10, true, false, false,
                false, false, false, 100));
        var weapons = new Dictionary<uint, CombatWeaponProfile>
        {
            [2] = new CombatWeaponProfile(5, 6)
        };

        Assert.Equal(1UL, controller.GetScoutTag(observation));
        Assert.Equal(1UL, controller.GetScoutTag(observation, weapons));
    }

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
            Assert.NotEqual(MapSide.Enemy, map.Side.Assess(waypoint));
        });
        var patrolTargets = squadMoves.Select(move =>
        {
            var point = move.ActionRaw.UnitCommand.TargetWorldSpacePos;
            return new MapPoint(point.X, point.Y);
        }).ToArray();
        Assert.Equal(4, patrolTargets.Distinct().Count());

        var targets = moves.ToDictionary(
            move => move.ActionRaw.UnitCommand.UnitTags.Single(),
            move => move.ActionRaw.UnitCommand.TargetWorldSpacePos);
        var arrived = marines.Select(marine =>
        {
            var target = targets[marine.Tag];
            return Fact(marine.Tag, "Marine", target.X, target.Y);
        }).ToArray();
        var nextWaypoints = controller.TryCreateActions(
            new NormalizedObservation(324, 1, arrived, new HashSet<ulong>()), map, decisions, gameData);
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
            new MarineTacticalDecision((ulong)tag, 2, MarineTacticalAction.CloseToWeaponRange, 0, 0,
                "Contact is inside the bounded local response envelope")).ToArray();

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

    [Fact]
    public void DistributesReinforcedSquadAcrossDistinctFriendlyPatrolWaypoints()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var gameData = GameData();
        var offsets = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4,
            map.Side.EnemyStart.X - map.Side.FriendlyStart.X,
            map.Side.EnemyStart.Y - map.Side.FriendlyStart.Y);
        var initial = new List<UnitObservationFacts>
        {
            Fact(1, "Marine", map.Side.FriendlyStart.X, map.Side.FriendlyStart.Y)
        };
        initial.AddRange(offsets.Select((offset, index) => Fact((ulong)(index + 2), "Marine",
            map.Side.FriendlyStart.X + offset.X, map.Side.FriendlyStart.Y + offset.Y)));
        var initialDecisions = Enumerable.Range(1, 5).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        // Assemble the original four-Marine squad and establish its separate scout.
        var initialMoves = controller.TryCreateActions(
            Snapshot(initial.ToArray()), map, initialDecisions, gameData);
        var scoutMove = Assert.Single(initialMoves,
            move => move.ActionRaw.UnitCommand.UnitTags.Single() == 1UL);
        var scoutTarget = scoutMove.ActionRaw.UnitCommand.TargetWorldSpacePos;

        // Add two reinforcements after dispatch. Put all six squad Marines at the
        // same observation position so duplicate patrol destinations cannot hide
        // behind different short-step waypoints.
        var reinforced = new List<UnitObservationFacts>
        {
            Fact(1, "Marine", scoutTarget.X, scoutTarget.Y)
        };
        reinforced.AddRange(Enumerable.Range(2, 6).Select(tag =>
            Fact((ulong)tag, "Marine", 20, 20)));
        var decisions = Enumerable.Range(1, 7).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        var moves = controller.TryCreateActions(
            new NormalizedObservation(324, 1, reinforced, new HashSet<ulong>()),
            map, decisions, gameData);

        Assert.Equal(6, moves.Count);
        var targets = moves.Select(move =>
        {
            var point = move.ActionRaw.UnitCommand.TargetWorldSpacePos;
            Assert.Equal(MapSide.Friendly, map.Side.Assess(new MapPoint(point.X, point.Y)));
            return (point.X, point.Y);
        }).ToArray();
        Assert.Equal(6, targets.Distinct().Count());
    }

    [Fact]
    public void PatrolPlannerCoversFriendlySectorsBeyondTheImmediateSpawnNeighborhood()
    {
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));

        var waypoints = Sc2MarineRallyController.GetFriendlyPatrolWaypoints(map);

        Assert.True(waypoints.Count > 20);
        Assert.Equal(waypoints.Count, waypoints.Distinct().Count());
        Assert.All(waypoints, point =>
        {
            Assert.True(map.Bounds.Contains(point));
            Assert.Equal(MapSide.Friendly, map.Side.Assess(point));
            Assert.True(point.DistanceSquaredTo(map.Side.FriendlyStart) >= 100f);
        });
        var coveredSectors = waypoints.Select(point => map.GetSector(point).Id).Distinct().ToArray();
        Assert.True(coveredSectors.Length > 3,
            "The security screen must extend beyond the spawn sector and its immediate neighbors.");
    }

    [Fact]
    public void KeepsDistributedPatrolMovingWhenVisibleContactIsOutsideLocalDefenseEnvelope()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var offsets = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4, 80, 80);
        var marines = new List<UnitObservationFacts>
        {
            Fact(1, "Marine", 10, 10)
        };
        marines.AddRange(offsets.Select((offset, index) =>
            Fact((ulong)(index + 2), "Marine", 10 + offset.X, 10 + offset.Y)));
        var decisions = Enumerable.Range(1, 5).Select(tag =>
            new MarineTacticalDecision((ulong)tag, 99, MarineTacticalAction.Hold, 0, 0,
                "Visible contact is outside the bounded local defense envelope", true)).ToArray();

        var actions = controller.TryCreateActions(
            Snapshot(marines.ToArray()), map, decisions, GameData());

        Assert.Equal(5, actions.Count);
        Assert.Equal(5, actions.Select(action =>
            action.ActionRaw.UnitCommand.UnitTags.Single()).Distinct().Count());
    }

    [Fact]
    public void PathingAwarePatrolPlannerRejectsUnpathableStationsAndKeepsOnlyWalkableOnes()
    {
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var blockedGrid = new SpatialGridSnapshot(10, 10, 100, 100, new byte[100]);
        var blockedTerrain = new SpatialTerrain(blockedGrid, blockedGrid, blockedGrid);
        var walkableGrid = new SpatialGridSnapshot(10, 10, 100, 100,
            Enumerable.Repeat((byte)1, 100).ToArray());
        var walkableTerrain = new SpatialTerrain(walkableGrid, walkableGrid, walkableGrid);

        Assert.Empty(Sc2MarineRallyController.GetFriendlyPatrolWaypoints(map, blockedTerrain));
        var walkable = Sc2MarineRallyController.GetFriendlyPatrolWaypoints(map, walkableTerrain);
        Assert.True(walkable.Count > 20);
        Assert.All(walkable, point =>
        {
            Assert.True(walkableTerrain.TryIsPathable(point, out var pathable));
            Assert.True(pathable);
        });
    }

    [Fact]
    public void UsesTerrainValidatedRampFootAndReportsActualVisibilityWhenScoutArrives()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(90.5f, 90.5f), new MapPoint(10.5f, 10.5f));
        var terrain = CreateRampTerrain();
        var topology = SpatialTopology.Build(terrain);
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
                "Dedicated scout or no visible attackable target")).ToArray();

        var firstMoves = controller.TryCreateActions(
            Snapshot(marines.ToArray()), map, decisions, GameData(),
            terrain: terrain, topology: topology);
        var scoutMove = Assert.Single(firstMoves,
            move => move.ActionRaw.UnitCommand.UnitTags.Single() == 1UL);
        var target = scoutMove.ActionRaw.UnitCommand.TargetWorldSpacePos;
        var scoutTarget = new MapPoint(target.X, target.Y);
        Assert.True(terrain.TryGetTerrainHeight(scoutTarget, out var scoutHeight));
        Assert.Equal((byte)207, scoutHeight);
        Assert.True(topology.AreConnected(map.Side.FriendlyStart, scoutTarget));
        Assert.Equal("MovingToRampFoot", controller.ScoutStatus);

        var visibilityCells = new byte[100 * 100];
        Array.Fill(visibilityCells, (byte)2);
        var visibility = new SpatialGridSnapshot(100, 100, 100, 100, visibilityCells);
        var arrived = marines.Select(marine => marine.Tag == 1
            ? Fact(1, "Marine", scoutTarget.X, scoutTarget.Y)
            : marine).ToArray();
        controller.TryCreateActions(
            new NormalizedObservation(800, 1, arrived, new HashSet<ulong>()),
            map, decisions, GameData(), terrain: terrain, topology: topology, visibility: visibility);

        Assert.True(controller.TryTakeScoutStatusChange(out var status, out var details));
        Assert.Equal("AtRampFoot", status);
        Assert.Contains("rampEntranceVisibility=visible", details);
        Assert.Contains("enemyTownHallVisible=False", details);
    }

    [Fact]
    public void RetreatsDedicatedScoutToConnectedFriendlyGroundWhenEnemyThreatApproaches()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(90.5f, 90.5f), new MapPoint(10.5f, 10.5f));
        var terrain = CreateRampTerrain();
        var topology = SpatialTopology.Build(terrain);
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
                "Dedicated scout or no visible attackable target")).ToArray();

        var firstMoves = controller.TryCreateActions(
            Snapshot(marines.ToArray()), map, decisions, GameData(),
            terrain: terrain, topology: topology);
        var scoutMove = Assert.Single(firstMoves,
            move => move.ActionRaw.UnitCommand.UnitTags.Single() == 1UL);
        var rampTarget = scoutMove.ActionRaw.UnitCommand.TargetWorldSpacePos;

        var threatenedUnits = marines.Select(marine => marine.Tag == 1
            ? Fact(1, "Marine", rampTarget.X, rampTarget.Y)
            : marine).Append(new UnitObservationFacts(
                90, 1, "Zealot", 100, UnitAlliance.Enemy, UnitVisibility.Visible,
                45, 45, 0, 0, 1, rampTarget.X + 2, rampTarget.Y,
                true, false, false, false, false, false, 100)).ToArray();
        var retreatActions = controller.TryCreateActions(
            new NormalizedObservation(900, 1, threatenedUnits, new HashSet<ulong>()),
            map, decisions, GameData(), terrain: terrain, topology: topology);

        var retreat = Assert.Single(retreatActions,
            move => move.ActionRaw.UnitCommand.UnitTags.Single() == 1UL);
        var retreatPoint = retreat.ActionRaw.UnitCommand.TargetWorldSpacePos;
        var retreatTarget = new MapPoint(retreatPoint.X, retreatPoint.Y);
        Assert.Equal(MapSide.Friendly, map.Side.Assess(retreatTarget));
        Assert.True(retreatTarget.DistanceTo(new MapPoint(rampTarget.X + 2, rampTarget.Y)) >= 14f);
        Assert.Equal("Retreating", controller.ScoutStatus);
    }

    [Fact]
    public void AuthoredAssaultPostureAdvancesAssembledSquadTowardEnemyStart()
    {
        var controller = new Sc2MarineRallyController();
        var map = new SpatialMap(new MapBounds(0, 0, 100, 100),
            new MapPoint(10, 10), new MapPoint(90, 90));
        var offsets = MarineFormationPlanner.GetOffsets(MarineFormation.Box, 4, 80, 80);
        var marines = new List<UnitObservationFacts>
        {
            Fact(1, "Marine", 10, 10)
        };
        marines.AddRange(offsets.Select((offset, index) =>
            Fact((ulong)(index + 2), "Marine", 10 + offset.X, 10 + offset.Y)));
        var decisions = Enumerable.Range(1, 5).Select(tag =>
            new MarineTacticalDecision((ulong)tag, null, MarineTacticalAction.Hold, 0, 0,
                "No visible attackable target")).ToArray();

        var actions = controller.TryCreateActions(
            Snapshot(marines.ToArray()), map, decisions, GameData(),
            advanceToEnemyStart: true);

        Assert.Equal(5, actions.Count);
        var scoutMove = Assert.Single(actions,
            action => action.ActionRaw.UnitCommand.UnitTags.Single() == 1UL);
        Assert.NotEqual(MapSide.Enemy, map.Side.Assess(new MapPoint(
            scoutMove.ActionRaw.UnitCommand.TargetWorldSpacePos.X,
            scoutMove.ActionRaw.UnitCommand.TargetWorldSpacePos.Y)));

        var squadMoves = actions.Where(action =>
            action.ActionRaw.UnitCommand.UnitTags.Single() != 1UL).ToArray();
        Assert.Equal(4, squadMoves.Length);
        Assert.All(squadMoves, action =>
        {
            var tag = action.ActionRaw.UnitCommand.UnitTags.Single();
            var marine = marines.Single(unit => unit.Tag == tag);
            var target = action.ActionRaw.UnitCommand.TargetWorldSpacePos;
            var waypoint = new MapPoint(target.X, target.Y);
            Assert.True(waypoint.DistanceTo(new MapPoint(marine.PositionX, marine.PositionY)) <= 4.01f);
            Assert.True(waypoint.DistanceSquaredTo(map.Side.EnemyStart) <
                new MapPoint(marine.PositionX, marine.PositionY).DistanceSquaredTo(map.Side.EnemyStart));
        });
    }

    private static SpatialTerrain CreateRampTerrain()
    {
        const int size = 100;
        var pathing = new byte[size * size];
        Array.Fill(pathing, (byte)1);
        var heights = new byte[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
            heights[y * size + x] = x < 30 && y < 30 ? (byte)223 : (byte)207;
        return new SpatialTerrain(
            new SpatialGridSnapshot(size, size, size, size, pathing),
            null,
            new SpatialGridSnapshot(size, size, size, size, heights));
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
