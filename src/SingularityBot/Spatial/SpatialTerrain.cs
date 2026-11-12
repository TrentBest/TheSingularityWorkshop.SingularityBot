using System;

namespace SingularityBot.Spatial;

/// <summary>
/// Immutable byte-per-cell SC2 map image mapped into world coordinates.
/// SC2 map images use a bottom-left origin, matching world-space coordinates.
/// </summary>
public sealed class SpatialGridSnapshot
{
    private readonly byte[] _cells;

    public SpatialGridSnapshot(int width, int height, float worldWidth, float worldHeight, ReadOnlySpan<byte> cells)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!float.IsFinite(worldWidth) || worldWidth <= 0) throw new ArgumentOutOfRangeException(nameof(worldWidth));
        if (!float.IsFinite(worldHeight) || worldHeight <= 0) throw new ArgumentOutOfRangeException(nameof(worldHeight));
        if (cells.Length != checked(width * height))
            throw new ArgumentException("Grid data length must equal width multiplied by height.", nameof(cells));

        Width = width;
        Height = height;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        _cells = cells.ToArray();
    }

    public int Width { get; }
    public int Height { get; }
    public float WorldWidth { get; }
    public float WorldHeight { get; }

    /// <summary>Reads the cell containing a world-space point; max map edges are exclusive.</summary>
    public bool TryGetCellIndex(MapPoint point, out int index)
    {
        index = -1;
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) ||
            point.X < 0 || point.Y < 0 ||
            point.X >= WorldWidth || point.Y >= WorldHeight)
            return false;

        var x = Math.Min(Width - 1, (int)(point.X * Width / WorldWidth));
        var y = Math.Min(Height - 1, (int)(point.Y * Height / WorldHeight));
        index = y * Width + x;
        return true;
    }

    public bool TryGetValue(MapPoint point, out byte value)
    {
        value = 0;
        if (!TryGetCellIndex(point, out var index))
            return false;

        value = _cells[index];
        return true;
    }
}

/// <summary>
/// Cached, protocol-independent terrain facts decoded from SC2 StartRaw.
/// Missing image layers remain unavailable rather than being treated as blocked.
/// </summary>
public sealed class SpatialTerrain
{
    public SpatialTerrain(
        SpatialGridSnapshot pathing,
        SpatialGridSnapshot placement,
        SpatialGridSnapshot terrainHeight)
    {
        Pathing = pathing;
        Placement = placement;
        TerrainHeight = terrainHeight;
    }

    public SpatialGridSnapshot Pathing { get; }
    public SpatialGridSnapshot Placement { get; }
    public SpatialGridSnapshot TerrainHeight { get; }

    public bool TryIsPathable(MapPoint point, out bool pathable) =>
        TryReadBoolean(Pathing, point, out pathable);

    public bool TryIsBuildable(MapPoint point, out bool buildable) =>
        TryReadBoolean(Placement, point, out buildable);

    public bool TryGetTerrainHeight(MapPoint point, out byte height)
    {
        height = 0;
        return TerrainHeight != null && TerrainHeight.TryGetValue(point, out height);
    }

    private static bool TryReadBoolean(SpatialGridSnapshot grid, MapPoint point, out bool value)
    {
        value = false;
        if (grid == null || !grid.TryGetValue(point, out var cell))
            return false;
        value = cell != 0;
        return true;
    }
}
