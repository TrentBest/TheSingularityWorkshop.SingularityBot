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
        var pathing = TryDecodeBooleanGrid(startRaw.PathingGrid, worldWidth, worldHeight);
        var placement = TryDecodeBooleanGrid(startRaw.PlacementGrid, worldWidth, worldHeight);
        var height = TryDecodeByteGrid(startRaw.TerrainHeight, worldWidth, worldHeight);

        if (pathing == null && placement == null && height == null)
            return false;

        terrain = new SpatialTerrain(pathing, placement, height);
        return true;
    }

    /// <summary>
    /// Decodes the authoritative per-frame raw visibility layer into full-map
    /// world coordinates. Unsupported or mismatched data remains unavailable.
    /// </summary>
    public static bool TryCreateVisibilityGrid(
        ResponseObservation response,
        SpatialTerrain terrain,
        out SpatialGridSnapshot visibility)
    {
        visibility = null;
        var image = response?.Observation?.RawData?.MapState?.Visibility;
        var pathing = terrain?.Pathing;
        if (image?.Size == null || image.BitsPerPixel != 8 ||
            image.Size.X <= 0 || image.Size.Y <= 0 ||
            pathing == null)
            return false;

        var cellCount = (long)image.Size.X * image.Size.Y;
        if (cellCount > int.MaxValue || image.Data.Length != cellCount)
            return false;

        visibility = new SpatialGridSnapshot(
            image.Size.X,
            image.Size.Y,
            pathing.WorldWidth,
            pathing.WorldHeight,
            image.Data.ToByteArray());
        return true;
    }

    // StartRaw pathing and placement images are protocol-defined as 1-bit bitmaps.
    // Accept 8-bit variants as well for compatibility with producers that expand the bits.
    private static SpatialGridSnapshot TryDecodeBooleanGrid(ImageData image, float worldWidth, float worldHeight)
    {
        if (image?.Size == null || image.Size.X <= 0 || image.Size.Y <= 0)
            return null;

        var cellCount = (long)image.Size.X * image.Size.Y;
        if (cellCount > int.MaxValue)
            return null;

        var data = image.Data;
        var decoded = new byte[(int)cellCount];
        if (image.BitsPerPixel == 1)
        {
            var expectedLength = (cellCount + 7) / 8;
            if (data.Length != expectedLength)
                return null;

            for (var index = 0; index < decoded.Length; index++)
            {
                var packed = data[index >> 3];
                var mask = 1 << (7 - (index & 7));
                decoded[index] = (packed & mask) != 0 ? (byte)1 : (byte)0;
            }
        }
        else if (image.BitsPerPixel == 8)
        {
            if (data.Length != cellCount)
                return null;

            for (var index = 0; index < decoded.Length; index++)
                decoded[index] = data[index] == 0 ? (byte)0 : (byte)1;
        }
        else
        {
            return null;
        }

        return new SpatialGridSnapshot(
            image.Size.X, image.Size.Y, worldWidth, worldHeight, decoded);
    }

    private static SpatialGridSnapshot TryDecodeByteGrid(ImageData image, float worldWidth, float worldHeight)
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
            image.Data.ToByteArray());
    }
}
