using Google.Protobuf;
using SC2APIProtocol;

namespace SingularityBot.Spatial;

/// <summary>Decodes the authoritative SC2 StartRaw image layers once at match start.</summary>
public static class Sc2SpatialTerrainFactory
{
    public static bool TryCreate(ResponseGameInfo gameInfo, out SpatialTerrain terrain)
    {
        terrain = null;
        var startRaw = gameInfo?.StartRaw;
        var mapSize = startRaw?.MapSize;
        if (startRaw == null || mapSize == null || mapSize.X <= 0 || mapSize.Y <= 0)
            return false;

        var worldWidth = (float)mapSize.X;
        var worldHeight = (float)mapSize.Y;
        var pathing = TryDecode(startRaw.PathingGrid, worldWidth, worldHeight);
        var placement = TryDecode(startRaw.PlacementGrid, worldWidth, worldHeight);
        var height = TryDecode(startRaw.TerrainHeight, worldWidth, worldHeight);

        if (pathing == null && placement == null && height == null)
            return false;

        terrain = new SpatialTerrain(pathing, placement, height);
        return true;
    }

    private static SpatialGridSnapshot TryDecode(ImageData image, float worldWidth, float worldHeight)
    {
        if (image?.Size == null || image.BitsPerPixel != 8 ||
            image.Size.X <= 0 || image.Size.Y <= 0)
            return null;

        var expectedLength = (long)image.Size.X * image.Size.Y;
        if (expectedLength > int.MaxValue || image.Data.Length != expectedLength)
            return null;

        return new SpatialGridSnapshot(
            image.Size.X,
            image.Size.Y,
            worldWidth,
            worldHeight,
            image.Data.Span);
    }
}
