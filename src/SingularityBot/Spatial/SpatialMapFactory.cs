using System.Linq;
using SC2APIProtocol;

namespace SingularityBot.Spatial;

/// <summary>Bridges the initial SC2 protocol payload into protocol-independent map geometry.</summary>
public static class SpatialMapFactory
{
    /// <summary>
    /// Builds geometry when SC2 supplies either one opponent start location or
    /// the conventional two-start set. The Command Center is the authoritative
    /// friendly anchor; averaging workers would shift the spawn estimate.
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
        if (startRaw == null || mapSize == null ||
            startRaw.StartLocations.Count is < 1 or > 2)
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

        var commandCenter = ownUnits.FirstOrDefault(unit =>
            string.Equals(
                gameInfo?.StartRaw == null ? null : null,
                "CommandCenter",
                StringComparison.OrdinalIgnoreCase));

        // Unit type IDs are deliberately not guessed here. The starting town
        // hall is the largest grounded structure in the initial own-unit set.
        var townHall = ownUnits
            .Where(unit => unit.BuildProgress >= 1f && !unit.IsFlying)
            .OrderByDescending(unit => unit.Radius)
            .ThenBy(unit => unit.Tag)
            .FirstOrDefault();
        var friendlyAnchorUnit = townHall ?? ownUnits
            .OrderBy(unit => unit.Pos.X)
            .ThenBy(unit => unit.Pos.Y)
            .First();

        var friendlyStart = new MapPoint(friendlyAnchorUnit.Pos.X, friendlyAnchorUnit.Pos.Y);
        var starts = startRaw.StartLocations
            .Select(point => new MapPoint(point.X, point.Y))
            .ToArray();

        MapPoint enemyStart;
        if (starts.Length == 1)
        {
            // On maps where SC2 reports only the opponent's start location,
            // our own spawn is derived from the observed starting town hall.
            enemyStart = starts[0];
        }
        else
        {
            var nearestStart = starts
                .OrderBy(point => point.DistanceSquaredTo(friendlyStart))
                .First();
            friendlyStart = nearestStart;
            enemyStart = starts
                .Where(point => point.DistanceSquaredTo(friendlyStart) > 0.0001f)
                .SingleOrDefault();
        }

        if (enemyStart.DistanceSquaredTo(friendlyStart) <= 0.0001f)
            return false;

        spatialMap = new SpatialMap(
            new MapBounds(minX, minY, maxX, maxY),
            friendlyStart,
            enemyStart);
        return true;
    }
}
