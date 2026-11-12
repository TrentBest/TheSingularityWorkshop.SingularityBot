using System;
using SingularityBot.Behavior;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2RampScoutPlannerTests
{
    [Fact]
    public void SelectsReachableLowGroundFootOfEnemyHighGround()
    {
        const int size = 100;
        var pathingCells = new byte[size * size];
        Array.Fill(pathingCells, (byte)1);
        var heightCells = new byte[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
            heightCells[y * size + x] = x < 30 && y < 30 ? (byte)223 : (byte)207;

        var terrain = new SpatialTerrain(
            new SpatialGridSnapshot(size, size, size, size, pathingCells),
            null,
            new SpatialGridSnapshot(size, size, size, size, heightCells));
        var topology = SpatialTopology.Build(terrain);
        var map = new SpatialMap(new MapBounds(0, 0, size, size),
            new MapPoint(90.5f, 90.5f), new MapPoint(10.5f, 10.5f));

        Assert.True(Sc2RampScoutPlanner.TryFindObservationPoint(
            map, terrain, topology, map.Side.FriendlyStart, out var plan));

        Assert.InRange(plan.DistanceFromEnemyStart, 24f, 52f);
        Assert.InRange(plan.CrossTrackDistance, 0f, 14f);
        Assert.Equal((byte)207, plan.TerrainHeight);
        Assert.True(plan.ObservationPoint.X >= 30f);
        Assert.True(plan.ObservationPoint.Y >= 30f);
        Assert.True(topology.AreConnected(map.Side.FriendlyStart, plan.ObservationPoint));
    }

    [Fact]
    public void RefusesToInventRampWhenTerrainHeightIsUnavailable()
    {
        var pathing = new SpatialGridSnapshot(4, 4, 40, 40, CreateWalkableCells());
        var terrain = new SpatialTerrain(pathing, null, null);
        var topology = SpatialTopology.Build(terrain);
        var map = new SpatialMap(new MapBounds(0, 0, 40, 40),
            new MapPoint(35, 35), new MapPoint(5, 5));

        Assert.False(Sc2RampScoutPlanner.TryFindObservationPoint(
            map, terrain, topology, new MapPoint(35, 35), out _));
    }

    [Fact]
    public void RefusesRampFootOutsideFriendlyPathingRegion()
    {
        const int size = 100;
        var pathingCells = new byte[size * size];
        Array.Fill(pathingCells, (byte)1);
        for (var y = 0; y < size; y++)
            pathingCells[y * size + 50] = 0;
        var heightCells = new byte[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
            heightCells[y * size + x] = x < 30 && y < 30 ? (byte)223 : (byte)207;

        var terrain = new SpatialTerrain(
            new SpatialGridSnapshot(size, size, size, size, pathingCells),
            null,
            new SpatialGridSnapshot(size, size, size, size, heightCells));
        var topology = SpatialTopology.Build(terrain);
        var map = new SpatialMap(new MapBounds(0, 0, size, size),
            new MapPoint(90.5f, 90.5f), new MapPoint(10.5f, 10.5f));

        Assert.False(Sc2RampScoutPlanner.TryFindObservationPoint(
            map, terrain, topology, map.Side.FriendlyStart, out _));
    }

    private static byte[] CreateWalkableCells()
    {
        var cells = new byte[16];
        Array.Fill(cells, (byte)1);
        return cells;
    }
}
