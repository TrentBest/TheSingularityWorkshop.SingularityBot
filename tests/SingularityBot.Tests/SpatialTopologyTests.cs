using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class SpatialTopologyTests
{
    [Fact]
    public void DiagonalContactDoesNotJoinWalkableRegions()
    {
        var pathing = new SpatialGridSnapshot(2, 2, 2, 2, new byte[] { 1, 0, 0, 1 });
        var topology = SpatialTopology.Build(new SpatialTerrain(pathing, null, null));

        Assert.Equal(2, topology.Regions.Count);
        Assert.All(topology.Regions, region => Assert.Equal(1, region.CellCount));
    }

    [Fact]
    public void SolidWalkableBlockProducesOneClosedBoundary()
    {
        var pathing = new SpatialGridSnapshot(2, 2, 20, 10, new byte[] { 1, 1, 1, 1 });
        var topology = SpatialTopology.Build(new SpatialTerrain(pathing, null, null));
        var region = Assert.Single(topology.Regions);
        var contour = Assert.Single(region.Contours);

        Assert.Equal(4, region.CellCount);
        Assert.True(contour.IsClosed);
        Assert.Equal(5, contour.Points.Count);
        Assert.Equal(new MapPoint(0, 0), contour.Points[0]);
        Assert.Equal(new MapPoint(0, 0), contour.Points[^1]);
    }

    [Fact]
    public void EnclosedBlockedCellProducesAnInnerContour()
    {
        var cells = new byte[]
        {
            1, 1, 1,
            1, 0, 1,
            1, 1, 1
        };
        var pathing = new SpatialGridSnapshot(3, 3, 3, 3, cells);
        var topology = SpatialTopology.Build(new SpatialTerrain(pathing, null, null));
        var region = Assert.Single(topology.Regions);

        Assert.Equal(8, region.CellCount);
        Assert.Equal(2, region.Contours.Count);
        Assert.All(region.Contours, contour => Assert.True(contour.IsClosed));
    }

    [Fact]
    public void TerrainHeightRangeIsMeasuredAcrossRegionCells()
    {
        var pathing = new SpatialGridSnapshot(2, 1, 2, 1, new byte[] { 1, 1 });
        var heights = new SpatialGridSnapshot(2, 1, 2, 1, new byte[] { 4, 9 });
        var topology = SpatialTopology.Build(new SpatialTerrain(pathing, null, heights));
        var region = Assert.Single(topology.Regions);

        Assert.Equal((byte)4, region.MinimumTerrainHeight);
        Assert.Equal((byte)9, region.MaximumTerrainHeight);
    }
}
