using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class SpatialTerrainTests
{
    [Fact]
    public void GridMapsWorldCoordinatesIntoBottomLeftOriginCells()
    {
        var grid = new SpatialGridSnapshot(2, 2, 20, 20, new byte[] { 1, 2, 3, 4 });

        Assert.True(grid.TryGetValue(new MapPoint(1, 1), out var southWest));
        Assert.Equal(1, southWest);
        Assert.True(grid.TryGetValue(new MapPoint(11, 1), out var southEast));
        Assert.Equal(2, southEast);
        Assert.True(grid.TryGetValue(new MapPoint(1, 11), out var northWest));
        Assert.Equal(3, northWest);
        Assert.True(grid.TryGetValue(new MapPoint(11, 11), out var northEast));
        Assert.Equal(4, northEast);
    }

    [Fact]
    public void MaxWorldEdgesAreExclusive()
    {
        var grid = new SpatialGridSnapshot(1, 1, 10, 10, new byte[] { 255 });

        Assert.False(grid.TryGetValue(new MapPoint(10, 5), out _));
        Assert.False(grid.TryGetValue(new MapPoint(5, 10), out _));
        Assert.False(grid.TryGetValue(new MapPoint(-0.1f, 5), out _));
    }

    [Fact]
    public void TerrainKeepsUnavailableLayersDistinctFromBlockedCells()
    {
        var pathing = new SpatialGridSnapshot(1, 1, 10, 10, new byte[] { 0 });
        var terrain = new SpatialTerrain(pathing, null, null);

        Assert.True(terrain.TryIsPathable(new MapPoint(5, 5), out var pathable));
        Assert.False(pathable);
        Assert.False(terrain.TryIsBuildable(new MapPoint(5, 5), out _));
        Assert.False(terrain.TryGetTerrainHeight(new MapPoint(5, 5), out _));
    }

    [Fact]
    public void GridRejectsMismatchedDataLength()
    {
        Assert.Throws<ArgumentException>(() =>
            new SpatialGridSnapshot(2, 2, 10, 10, new byte[] { 1, 2, 3 }));
    }
}
