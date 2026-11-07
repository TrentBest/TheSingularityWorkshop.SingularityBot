using System.Linq;
using SC2APIProtocol;

namespace SingularityBot.Spatial;

/// <summary>Bridges the initial SC2 protocol payload into protocol-independent map geometry.</summary>
public static class SpatialMapFactory
{
    /// <summary>
    /// Builds geometry for a standard two-start-location match. If the initial
    /// observation cannot identify our spawn or the opponent start is ambiguous,
    /// returns false instead of guessing.
    /// </summary>
    public static bool TryCreate(
        ResponseGameInfo gameInfo,
        ResponseObservation response,
        out SpatialMap spatialMap)
    {
        spatialMap = null;

        var startRaw = gameInfo?.StartRaw;
        var playableArea = startRaw?.PlayableArea;
        var mapSize = startRaw?.MapSize;
        if (startRaw == null || mapSize == null || startRaw.StartLocations.Count != 2)
            return false;

        var minX = playableArea?.P0?.X ?? 0;
        var minY = playableArea?.P0?.Y ?? 0;
        var maxX = playableArea?.P1?.X ?? mapSize.X;
        var maxY = playableArea?.P1?.Y ?? mapSize.Y;
        if (maxX <= minX || maxY <= minY)
            return false;

        var ownUnits = response?.Observation?.RawData?.Units
            .Where(unit => unit.Alliance == Alliance.Self && unit.Pos != null)
            .ToArray();

        if (ownUnits == null || ownUnits.Length == 0)
            return false;

        var ownCenter = new MapPoint(
            ownUnits.Average(unit => unit.Pos.X),
            ownUnits.Average(unit => unit.Pos.Y));

        var starts = startRaw.StartLocations
            .Select(point => new MapPoint(point.X, point.Y))
            .ToArray();

        var friendlyStart = starts
            .OrderBy(point => point.DistanceSquaredTo(ownCenter))
            .First();
        var enemyStarts = starts
            .Where(point => point.DistanceSquaredTo(friendlyStart) > 0.0001f)
            .ToArray();

        if (enemyStarts.Length != 1)
            return false;

        spatialMap = new SpatialMap(
            new MapBounds(minX, minY, maxX, maxY),
            friendlyStart,
            enemyStarts[0]);
        return true;
    }
}
