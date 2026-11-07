using System;
using System.Collections.Generic;
using System.Linq;

namespace SingularityBot.Spatial;

/// <summary>A protocol-independent point in StarCraft world coordinates.</summary>
public readonly record struct MapPoint(float X, float Y)
{
    public static MapPoint operator +(MapPoint a, MapPoint b) => new(a.X + b.X, a.Y + b.Y);
    public static MapPoint operator -(MapPoint a, MapPoint b) => new(a.X - b.X, a.Y - b.Y);
    public static MapPoint operator *(MapPoint point, float scale) => new(point.X * scale, point.Y * scale);
    public float LengthSquared => X * X + Y * Y;
    public float DistanceSquaredTo(MapPoint other) => (this - other).LengthSquared;
    public float DistanceTo(MapPoint other) => MathF.Sqrt(DistanceSquaredTo(other));
    public MapPoint Normalize()
    {
        var length = MathF.Sqrt(LengthSquared);
        if (length <= float.Epsilon) throw new InvalidOperationException("A zero-length vector has no direction.");
        return new MapPoint(X / length, Y / length);
    }
    public static float Dot(MapPoint a, MapPoint b) => a.X * b.X + a.Y * b.Y;
}

/// <summary>Playable world-space bounds. Max edges are exclusive for cell indexing.</summary>
public readonly record struct MapBounds(float MinX, float MinY, float MaxX, float MaxY)
{
    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;
    public MapPoint Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);

    public bool Contains(MapPoint point) =>
        point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;

    public MapPoint Clamp(MapPoint point) =>
        new(Math.Clamp(point.X, MinX, MaxX), Math.Clamp(point.Y, MinY, MaxY));

    public MapBounds
    {
        if (MaxX <= MinX) throw new ArgumentOutOfRangeException(nameof(MaxX));
        if (MaxY <= MinY) throw new ArgumentOutOfRangeException(nameof(MaxY));
    }
}

public enum MapSide
{
    Friendly,
    Enemy,
    Neutral
}

public enum MapQuadrant
{
    SouthWest,
    SouthEast,
    NorthWest,
    NorthEast
}

public readonly record struct SectorId(int Column, int Row)
{
    public override string ToString() => $"({Column},{Row})";
}

/// <summary>A line described by an origin and a normalized direction.</summary>
public readonly record struct SpatialLine(MapPoint Origin, MapPoint Direction);

/// <summary>
/// Strategic side classifier. The dividing half-line is the perpendicular
/// bisector between the two starting anchors; points on it are Neutral.
/// </summary>
public sealed class Side
{
    private readonly MapPoint _friendlyToEnemy;
    private readonly float _neutralTolerance;

    public Side(MapPoint friendlyStart, MapPoint enemyStart, float neutralTolerance = 0.25f)
    {
        _friendlyToEnemy = (enemyStart - friendlyStart).Normalize();
        if (neutralTolerance < 0) throw new ArgumentOutOfRangeException(nameof(neutralTolerance));
        _neutralTolerance = neutralTolerance;
        Center = (friendlyStart + enemyStart) * 0.5f;
        DividingHalfLine = new SpatialLine(Center, new MapPoint(-_friendlyToEnemy.Y, _friendlyToEnemy.X));
        FriendlyStart = friendlyStart;
        EnemyStart = enemyStart;
    }

    public MapPoint FriendlyStart { get; }
    public MapPoint EnemyStart { get; }
    public MapPoint Center { get; }
    public SpatialLine DividingHalfLine { get; }

    public MapSide Assess(MapPoint point)
    {
        var signedDistance = MapPoint.Dot(point - Center, _friendlyToEnemy);
        if (MathF.Abs(signedDistance) <= _neutralTolerance) return MapSide.Neutral;
        return signedDistance < 0 ? MapSide.Friendly : MapSide.Enemy;
    }
}

/// <summary>One broad quarter of the playable map, split at its center.</summary>
public sealed class Quadrant
{
    internal Quadrant(MapQuadrant kind, MapBounds bounds)
    {
        Kind = kind;
        Bounds = bounds;
        Center = bounds.Center;
    }

    public MapQuadrant Kind { get; }
    public MapBounds Bounds { get; }
    public MapPoint Center { get; }
    public bool Contains(MapPoint point) => Bounds.Contains(point);
}

/// <summary>
/// One cell in the 3x3 tactical grid. Perimeter points are ordered around the
/// cell and can seed patrol paths; neighbors include diagonals.
/// </summary>
public sealed class Sector
{
    internal Sector(SectorId id, MapBounds bounds)
    {
        Id = id;
        Bounds = bounds;
        Center = bounds.Center;
        Perimeter = Array.AsReadOnly(new[]
        {
            new MapPoint(bounds.MinX, bounds.MinY),
            new MapPoint(Center.X, bounds.MinY),
            new MapPoint(bounds.MaxX, bounds.MinY),
            new MapPoint(bounds.MaxX, Center.Y),
            new MapPoint(bounds.MaxX, bounds.MaxY),
            new MapPoint(Center.X, bounds.MaxY),
            new MapPoint(bounds.MinX, bounds.MaxY),
            new MapPoint(bounds.MinX, Center.Y)
        });
    }

    public SectorId Id { get; }
    public MapBounds Bounds { get; }
    public MapPoint Center { get; }
    public IReadOnlyList<MapPoint> Perimeter { get; }
    public bool Contains(MapPoint point) => Bounds.Contains(point);
}

/// <summary>
/// Cached, immutable spatial vocabulary for one map. Build once from the
/// playable rectangle and the two known start anchors, then query per frame.
/// </summary>
public sealed class SpatialMap
{
    private readonly Quadrant[] _quadrants;
    private readonly Sector[,] _sectors;

    public SpatialMap(MapBounds playableBounds, MapPoint friendlyStart, MapPoint enemyStart)
    {
        Bounds = playableBounds;
        Center = playableBounds.Center;
        Side = new Side(friendlyStart, enemyStart);

        var midX = Center.X;
        var midY = Center.Y;
        _quadrants =
        [
            new Quadrant(MapQuadrant.SouthWest, new MapBounds(Bounds.MinX, Bounds.MinY, midX, midY)),
            new Quadrant(MapQuadrant.SouthEast, new MapBounds(midX, Bounds.MinY, Bounds.MaxX, midY)),
            new Quadrant(MapQuadrant.NorthWest, new MapBounds(Bounds.MinX, midY, midX, Bounds.MaxY)),
            new Quadrant(MapQuadrant.NorthEast, new MapBounds(midX, midY, Bounds.MaxX, Bounds.MaxY))
        ];

        _sectors = new Sector[3, 3];
        var sectorWidth = Bounds.Width / 3f;
        var sectorHeight = Bounds.Height / 3f;
        for (var row = 0; row < 3; row++)
        for (var column = 0; column < 3; column++)
        {
            var minX = Bounds.MinX + column * sectorWidth;
            var minY = Bounds.MinY + row * sectorHeight;
            _sectors[column, row] = new Sector(
                new SectorId(column, row),
                new MapBounds(minX, minY,
                    column == 2 ? Bounds.MaxX : minX + sectorWidth,
                    row == 2 ? Bounds.MaxY : minY + sectorHeight));
        }
    }

    public MapBounds Bounds { get; }
    public MapPoint Center { get; }
    public Side Side { get; }
    public IReadOnlyList<Quadrant> Quadrants => _quadrants;
    public IReadOnlyList<Sector> Sectors =>
        Array.AsReadOnly(Enumerable.Range(0, 3).SelectMany(row =>
            Enumerable.Range(0, 3).Select(column => _sectors[column, row])).ToArray());

    public Quadrant GetQuadrant(MapPoint point)
    {
        EnsureInside(point);
        var east = point.X >= Center.X;
        var north = point.Y >= Center.Y;
        var kind = north
            ? (east ? MapQuadrant.NorthEast : MapQuadrant.NorthWest)
            : (east ? MapQuadrant.SouthEast : MapQuadrant.SouthWest);
        return _quadrants[(int)kind];
    }

    public Sector GetSector(MapPoint point)
    {
        EnsureInside(point);
        var column = Math.Min(2, (int)((point.X - Bounds.MinX) / (Bounds.Width / 3f)));
        var row = Math.Min(2, (int)((point.Y - Bounds.MinY) / (Bounds.Height / 3f)));
        return _sectors[column, row];
    }

    public IReadOnlyList<Sector> GetNeighbors(SectorId id, bool includeDiagonals = true)
    {
        Validate(id);
        var neighbors = new List<Sector>(includeDiagonals ? 8 : 4);
        for (var row = id.Row - 1; row <= id.Row + 1; row++)
        for (var column = id.Column - 1; column <= id.Column + 1; column++)
        {
            if (column == id.Column && row == id.Row) continue;
            if (!includeDiagonals && column != id.Column && row != id.Row) continue;
            if (column is >= 0 and < 3 && row is >= 0 and < 3)
                neighbors.Add(_sectors[column, row]);
        }
        return neighbors.AsReadOnly();
    }

    private void EnsureInside(MapPoint point)
    {
        if (!Bounds.Contains(point))
            throw new ArgumentOutOfRangeException(nameof(point), "Point is outside the playable map bounds.");
    }

    private static void Validate(SectorId id)
    {
        if (id.Column is < 0 or > 2 || id.Row is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(id), "Sector coordinates must be between 0 and 2.");
    }
}
