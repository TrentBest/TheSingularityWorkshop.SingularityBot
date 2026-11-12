using System;
using System.Collections.Generic;
using System.Linq;

namespace SingularityBot.Spatial;

/// <summary>A directed grid-edge contour. The walkable region is on the left of each edge.</summary>
public sealed class SpatialBoundaryContour
{
    internal SpatialBoundaryContour(IReadOnlyList<MapPoint> points, bool isClosed)
    {
        Points = points;
        IsClosed = isClosed;
    }

    public IReadOnlyList<MapPoint> Points { get; }
    public bool IsClosed { get; }
}

/// <summary>A four-connected component of walkable pathing cells.</summary>
public sealed class SpatialPathingRegion
{
    internal SpatialPathingRegion(
        int id,
        int cellCount,
        byte? minimumHeight,
        byte? maximumHeight,
        IReadOnlyList<SpatialBoundaryContour> contours)
    {
        Id = id;
        CellCount = cellCount;
        MinimumTerrainHeight = minimumHeight;
        MaximumTerrainHeight = maximumHeight;
        Contours = contours;
    }

    public int Id { get; }
    public int CellCount { get; }
    public byte? MinimumTerrainHeight { get; }
    public byte? MaximumTerrainHeight { get; }
    public IReadOnlyList<SpatialBoundaryContour> Contours { get; }
}

/// <summary>
/// Extracts deterministic four-connected walkable regions and their actual cell-edge boundaries.
/// Diagonal contact alone never creates a traversable connection.
/// </summary>
public sealed class SpatialTopology
{
    private readonly SpatialPathingRegion[] _regions;
    private readonly int[] _regionLabels;
    private readonly SpatialGridSnapshot _pathingGrid;

    private SpatialTopology(
        SpatialPathingRegion[] regions,
        int[] regionLabels,
        SpatialGridSnapshot pathingGrid)
    {
        _regions = regions;
        _regionLabels = regionLabels;
        _pathingGrid = pathingGrid;
    }

    public IReadOnlyList<SpatialPathingRegion> Regions => _regions;

    /// <summary>Returns the four-connected pathing region containing a world point.</summary>
    public bool TryGetRegionId(MapPoint point, out int regionId)
    {
        regionId = -1;
        if (_pathingGrid == null || !_pathingGrid.TryGetCellIndex(point, out var index))
            return false;

        regionId = _regionLabels[index];
        return regionId >= 0;
    }

    /// <summary>True only when both points belong to the same four-connected walkable region.</summary>
    public bool AreConnected(MapPoint first, MapPoint second) =>
        TryGetRegionId(first, out var firstRegion) &&
        TryGetRegionId(second, out var secondRegion) &&
        firstRegion == secondRegion;

    public static SpatialTopology Build(SpatialTerrain terrain, byte pathableThreshold = 1)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));
        var grid = terrain.Pathing ?? throw new ArgumentException("A pathing grid is required to extract topology.", nameof(terrain));
        var width = grid.Width;
        var height = grid.Height;
        var walkable = new bool[checked(width * height)];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var center = CellCenter(grid, x, y);
            walkable[y * width + x] =
                grid.TryGetValue(center, out var value) && value >= pathableThreshold;
        }

        var labels = new int[walkable.Length];
        Array.Fill(labels, -1);
        var regions = new List<SpatialPathingRegion>();
        var queue = new Queue<int>();

        for (var start = 0; start < walkable.Length; start++)
        {
            if (!walkable[start] || labels[start] >= 0) continue;
            var id = regions.Count;
            var cells = new List<int>();
            byte? minHeight = null;
            byte? maxHeight = null;
            labels[start] = id;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                cells.Add(index);
                var x = index % width;
                var y = index / width;
                if (terrain.TryGetTerrainHeight(CellCenter(grid, x, y), out var sample))
                {
                    minHeight = minHeight.HasValue ? Math.Min(minHeight.Value, sample) : sample;
                    maxHeight = maxHeight.HasValue ? Math.Max(maxHeight.Value, sample) : sample;
                }

                Visit(x - 1, y);
                Visit(x + 1, y);
                Visit(x, y - 1);
                Visit(x, y + 1);

                void Visit(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) return;
                    var next = ny * width + nx;
                    if (!walkable[next] || labels[next] >= 0) return;
                    labels[next] = id;
                    queue.Enqueue(next);
                }
            }

            regions.Add(new SpatialPathingRegion(
                id, cells.Count, minHeight, maxHeight,
                TraceContours(cells, labels, id, width, height, grid)));
        }

        return new SpatialTopology(regions.ToArray(), labels, grid);
    }

    private static IReadOnlyList<SpatialBoundaryContour> TraceContours(
        List<int> cells, int[] labels, int regionId, int width, int height, SpatialGridSnapshot grid)
    {
        var edges = new List<GridEdge>();
        foreach (var index in cells)
        {
            var x = index % width;
            var y = index / width;
            if (!IsRegion(x, y - 1)) edges.Add(new GridEdge(x, y, x + 1, y));
            if (!IsRegion(x + 1, y)) edges.Add(new GridEdge(x + 1, y, x + 1, y + 1));
            if (!IsRegion(x, y + 1)) edges.Add(new GridEdge(x + 1, y + 1, x, y + 1));
            if (!IsRegion(x - 1, y)) edges.Add(new GridEdge(x, y + 1, x, y));

            bool IsRegion(int nx, int ny) =>
                nx >= 0 && ny >= 0 && nx < width && ny < height &&
                labels[ny * width + nx] == regionId;
        }

        var outgoing = new Dictionary<GridVertex, List<int>>();
        for (var i = 0; i < edges.Count; i++)
        {
            if (!outgoing.TryGetValue(edges[i].Start, out var list))
                outgoing.Add(edges[i].Start, list = new List<int>());
            list.Add(i);
        }

        var used = new bool[edges.Count];
        var contours = new List<SpatialBoundaryContour>();
        for (var seed = 0; seed < edges.Count; seed++)
        {
            if (used[seed]) continue;
            var points = new List<MapPoint>();
            var current = seed;
            var start = edges[seed].Start;
            var closed = false;
            points.Add(GridPoint(grid, start));

            for (var guard = 0; guard <= edges.Count; guard++)
            {
                if (used[current]) break;
                used[current] = true;
                var edge = edges[current];
                points.Add(GridPoint(grid, edge.End));
                if (edge.End.Equals(start))
                {
                    closed = true;
                    break;
                }

                if (!outgoing.TryGetValue(edge.End, out var candidates)) break;
                var next = ChooseNext(edges, used, candidates, edge.Direction);
                if (next < 0) break;
                current = next;
            }

            contours.Add(new SpatialBoundaryContour(points.AsReadOnly(), closed));
        }

        return contours.AsReadOnly();
    }

    private static int ChooseNext(List<GridEdge> edges, bool[] used, List<int> candidates, int direction)
    {
        // Prefer a left turn, then straight, right, and finally a reversal.
        var best = -1;
        var bestRank = int.MaxValue;
        foreach (var candidate in candidates)
        {
            if (used[candidate]) continue;
            var turn = (edges[candidate].Direction - direction + 4) % 4;
            var rank = turn switch { 1 => 0, 0 => 1, 3 => 2, _ => 3 };
            if (rank < bestRank) { best = candidate; bestRank = rank; }
        }
        return best;
    }

    private static MapPoint CellCenter(SpatialGridSnapshot grid, int x, int y) =>
        new((x + 0.5f) * grid.WorldWidth / grid.Width,
            (y + 0.5f) * grid.WorldHeight / grid.Height);

    private static MapPoint GridPoint(SpatialGridSnapshot grid, GridVertex point) =>
        new(point.X * grid.WorldWidth / grid.Width,
            point.Y * grid.WorldHeight / grid.Height);

    private readonly record struct GridVertex(int X, int Y);
    private readonly record struct GridEdge(int X1, int Y1, int X2, int Y2)
    {
        public GridVertex Start => new(X1, Y1);
        public GridVertex End => new(X2, Y2);
        public int Direction => X2 > X1 ? 0 : Y2 > Y1 ? 1 : X2 < X1 ? 2 : 3;
    }
}
