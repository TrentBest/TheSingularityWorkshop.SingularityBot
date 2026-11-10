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
        Assert.Equal(9, contour.Points.Count);
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
        Assert.All(region.Contours, contour =>
        {
            Assert.True(contour.IsClosed);
            Assert.Equal(contour.Points[0], contour.Points[^1]);
            AssertNoDegenerateGridEdges(contour);
        });

        // With the SC2 bottom-left origin, the walkable exterior is counter-clockwise
        // and the boundary around the blocked hole is clockwise.
        var signedAreas = region.Contours.Select(SignedArea).OrderBy(area => area).ToArray();
        Assert.Equal(-1, signedAreas[0], 5);
        Assert.Equal(9, signedAreas[1], 5);
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
    [Fact]
    public void DiagonalPinchWithinOneConnectedRegionKeepsContoursClosedAndAreaConserved()
    {
        // The perimeter connects two interior cells that touch only at one vertex.
        // This creates a diagonal pinch without making the pathing region disconnected.
        var cells = new byte[]
        {
            1, 1, 1, 1,
            1, 1, 0, 1,
            1, 0, 1, 1,
            1, 1, 1, 1
        };
        var pathing = new SpatialGridSnapshot(4, 4, 4, 4, cells);
        var topology = SpatialTopology.Build(new SpatialTerrain(pathing, null, null));
        var region = Assert.Single(topology.Regions);

        Assert.Equal(14, region.CellCount);
        Assert.NotEmpty(region.Contours);
        Assert.All(region.Contours, contour =>
        {
            Assert.True(contour.IsClosed);
            Assert.Equal(contour.Points[0], contour.Points[^1]);
            AssertNoDegenerateGridEdges(contour);
        });

        Assert.Equal(14, region.Contours.Sum(SignedArea), 5);
    }

    private static double SignedArea(SpatialBoundaryContour contour)
    {
        double twiceArea = 0;
        for (var i = 0; i < contour.Points.Count - 1; i++)
        {
            var a = contour.Points[i];
            var b = contour.Points[i + 1];
            twiceArea += (double)a.X * b.Y - (double)b.X * a.Y;
        }

        return twiceArea / 2;
    }

    private static void AssertNoDegenerateGridEdges(SpatialBoundaryContour contour)
    {
        for (var i = 0; i < contour.Points.Count - 1; i++)
        {
            var a = contour.Points[i];
            var b = contour.Points[i + 1];
            Assert.NotEqual(a, b);
            Assert.True(a.X == b.X || a.Y == b.Y);
        }
    }
}
