using System;

namespace SingularityBot.Spatial;

/// <summary>
/// Immutable byte-per-cell image mapped into world coordinates. The origin is
/// explicit so playable-area images retain their translated map coordinates.
/// </summary>
public sealed class SpatialGridSnapshot
{
    private readonly byte[] _cells;

    public SpatialGridSnapshot(
        int width,
        int height,
        float worldWidth,
        float worldHeight,
        ReadOnlySpan<byte> cells,
        float worldOriginX = 0,
        float worldOriginY = 0)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!float.IsFinite(worldWidth) || worldWidth <= 0) throw new ArgumentOutOfRangeException(nameof(worldWidth));
        if (!float.IsFinite(worldHeight) || worldHeight <= 0) throw new ArgumentOutOfRangeException(nameof(worldHeight));
        if (!float.IsFinite(worldOriginX)) throw new ArgumentOutOfRangeException(nameof(worldOriginX));
        if (!float.IsFinite(worldOriginY)) throw new ArgumentOutOfRangeException(nameof(worldOriginY));
        if (!float.IsFinite(worldOriginX + worldWidth)) throw new ArgumentOutOfRangeException(nameof(worldWidth));
        if (!float.IsFinite(worldOriginY + worldHeight)) throw new ArgumentOutOfRangeException(nameof(worldHeight));
        if (cells.Length != checked(width * height))
            throw new ArgumentException("Grid data length must equal width multiplied by height.", nameof(cells));

        Width = width;
        Height = height;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        WorldOriginX = worldOriginX;
        WorldOriginY = worldOriginY;
        _cells = cells.ToArray();
    }

    public int Width { get; }
    public int Height { get; }
    public float WorldWidth { get; }
    public float WorldHeight { get; }
    public float WorldOriginX { get; }
    public float WorldOriginY { get; }

    /// <summary>Reads a world-space cell; maximum edges are exclusive.</summary>
    public bool TryGetCellIndex(MapPoint point, out int index)
    {
        index = -1;
        var localX = point.X - WorldOriginX;
        var localY = point.Y - WorldOriginY;
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) ||
            localX < 0 || localY < 0 ||
            localX >= WorldWidth || localY >= WorldHeight)
            return false;

        var x = Math.Min(Width - 1, (int)(localX * Width / WorldWidth));
        var y = Math.Min(Height - 1, (int)(localY * Height / WorldHeight));
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
