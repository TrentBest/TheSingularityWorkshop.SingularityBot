using System;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// A low-ground observation point at the friendly-facing foot of the enemy's
/// high-ground boundary. It is a staging point, never an instruction to cross
/// the ramp or enter the enemy base.
/// </summary>
public readonly record struct RampScoutPlan(
    MapPoint ObservationPoint,
    float DistanceFromEnemyStart,
    float CrossTrackDistance,
    byte TerrainHeight);

/// <summary>
/// Derives a conservative enemy-ramp observation point from SC2's actual
/// terrain-height and pathing images plus four-connected pathing topology.
/// </summary>
public static class Sc2RampScoutPlanner
{
    private const float MinimumAlongDistance = 24f;
    private const float MaximumAlongDistance = 52f;
    private const float PreferredAlongDistance = 38f;
    private const float MaximumCrossTrackDistance = 14f;

    public static bool TryFindObservationPoint(
        SpatialMap map,
        SpatialTerrain terrain,
        SpatialTopology topology,
        MapPoint origin,
        out RampScoutPlan plan)
    {
        plan = default;
        if (map == null || terrain?.Pathing == null || terrain.TerrainHeight == null || topology == null)
            return false;
        if (!terrain.TryGetTerrainHeight(map.Side.EnemyStart, out var enemyHeight))
            return false;

        var directionX = map.Side.FriendlyStart.X - map.Side.EnemyStart.X;
        var directionY = map.Side.FriendlyStart.Y - map.Side.EnemyStart.Y;
        var startDistance = MathF.Sqrt(directionX * directionX + directionY * directionY);
        if (startDistance <= 0.01f)
            return false;

        directionX /= startDistance;
        directionY /= startDistance;
        var lowGroundMaximum = enemyHeight > 16 ? enemyHeight - 16 : 0;
        var highGroundMinimum = enemyHeight > 8 ? enemyHeight - 8 : 0;
        var grid = terrain.Pathing;
        var cellWidth = grid.WorldWidth / grid.Width;
        var cellHeight = grid.WorldHeight / grid.Height;

        var found = false;
        var bestScore = float.MaxValue;
        var bestPoint = default(MapPoint);
        var bestDistance = 0f;
        var bestCrossTrack = 0f;
        byte bestHeight = 0;

        for (var y = 0; y < grid.Height; y++)
        for (var x = 0; x < grid.Width; x++)
        {
            var candidate = new MapPoint(
                (x + 0.5f) * cellWidth,
                (y + 0.5f) * cellHeight);
            if (!map.Bounds.Contains(candidate))
                continue;
            if (!terrain.TryIsPathable(candidate, out var pathable) || !pathable)
                continue;
            if (!terrain.TryGetTerrainHeight(candidate, out var height) || height > lowGroundMaximum)
                continue;

            var offsetX = candidate.X - map.Side.EnemyStart.X;
            var offsetY = candidate.Y - map.Side.EnemyStart.Y;
            var along = offsetX * directionX + offsetY * directionY;
            if (along < MinimumAlongDistance || along > MaximumAlongDistance)
                continue;

            var crossTrack = MathF.Abs(offsetX * directionY - offsetY * directionX);
            if (crossTrack > MaximumCrossTrackDistance ||
                !topology.AreConnected(origin, candidate))
                continue;

            if (!HasEnemyHighGroundEdge(
                    terrain, candidate, map.Side.EnemyStart,
                    highGroundMinimum, cellWidth, cellHeight, out var edgeDistance))
                continue;

            var distance = MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
            var score = crossTrack * 2f +
                        MathF.Abs(along - PreferredAlongDistance) * 0.25f +
                        edgeDistance * 0.5f;
            if (found && score >= bestScore)
                continue;

            found = true;
            bestScore = score;
            bestPoint = candidate;
            bestDistance = distance;
            bestCrossTrack = crossTrack;
            bestHeight = height;
        }

        if (!found)
            return false;

        plan = new RampScoutPlan(bestPoint, bestDistance, bestCrossTrack, bestHeight);
        return true;
    }

    private static bool HasEnemyHighGroundEdge(
        SpatialTerrain terrain,
        MapPoint candidate,
        MapPoint enemyStart,
        byte minimumHeight,
        float cellWidth,
        float cellHeight,
        out float nearestDistance)
    {
        nearestDistance = float.MaxValue;
        var toEnemyX = enemyStart.X - candidate.X;
        var toEnemyY = enemyStart.Y - candidate.Y;
        var found = false;

        for (var y = -1; y <= 1; y++)
        for (var x = -1; x <= 1; x++)
        {
            if (x == 0 && y == 0)
                continue;

            var neighbor = new MapPoint(
                candidate.X + x * cellWidth,
                candidate.Y + y * cellHeight);
            if (!terrain.TryIsPathable(neighbor, out var pathable) || !pathable ||
                !terrain.TryGetTerrainHeight(neighbor, out var height) ||
                height < minimumHeight)
                continue;

            var towardEnemy = x * cellWidth * toEnemyX + y * cellHeight * toEnemyY;
            if (towardEnemy <= 0)
                continue;

            var distance = MathF.Sqrt(
                x * x * cellWidth * cellWidth +
                y * y * cellHeight * cellHeight);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            found = true;
        }

        return found;
    }
}
