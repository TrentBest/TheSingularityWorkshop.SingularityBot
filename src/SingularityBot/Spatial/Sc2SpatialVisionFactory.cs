using SC2APIProtocol;

namespace SingularityBot.Spatial;

public static class Sc2SpatialVisionFactory
{
    public static bool TryCreate(
        ResponseObservation response,
        SpatialMap map,
        out SpatialGridSnapshot visibility)
    {
        visibility = null;
        if (response?.Observation?.RawData?.MapState?.Visibility is not { } image ||
            image.BitsPerPixel != 8 || image.Size is null || map is null)
            return false;

        var width = image.Size.X;
        var height = image.Size.Y;
        if (width <= 0 || height <= 0)
            return false;

        var cells = image.Data.ToByteArray();
        if (cells.Length != width * height)
            return false;

        visibility = new SpatialGridSnapshot(
            width, height, map.Bounds.Width, map.Bounds.Height, cells);
        return true;
    }
}
