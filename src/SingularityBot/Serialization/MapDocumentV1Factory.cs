using System.Security.Cryptography;
using SingularityBot.Spatial;

namespace SingularityBot.Serialization;

/// <summary>
/// Projects already-normalized spatial facts into a static MapDocument v1.
/// SC2 protocol decoding remains in the spatial adapter; this class only
/// packages verified grid bytes and protocol-independent geometry.
/// </summary>
public static class MapDocumentV1Factory
{
    public static MapDocumentV1 Create(
        string mapId,
        string mapName,
        string source,
        MapRectangleV1 fullBounds,
        SpatialMap spatialMap,
        IReadOnlyList<MapStartLocationV1> startLocations,
        SpatialTerrain? terrain,
        Func<string, byte[], string> storePayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapName);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(fullBounds);
        ArgumentNullException.ThrowIfNull(spatialMap);
        ArgumentNullException.ThrowIfNull(startLocations);
        ArgumentNullException.ThrowIfNull(storePayload);

        ValidateBounds(fullBounds, spatialMap.Bounds);
        var starts = startLocations
            .OrderBy(location => location.Id, StringComparer.Ordinal)
            .ToArray();
        if (starts.Length == 0 || starts.Any(location =>
                string.IsNullOrWhiteSpace(location.Id) ||
                location.X < spatialMap.Bounds.MinX || location.X > spatialMap.Bounds.MaxX ||
                location.Y < spatialMap.Bounds.MinY || location.Y > spatialMap.Bounds.MaxY))
            throw new ArgumentException("Start locations must have IDs and lie within playable bounds.", nameof(startLocations));

        var layers = new[]
        {
            CreateLayer("pathing", "pathing-passability", "SC2 StartRaw PathingGrid",
                terrain?.Pathing, "uint8-row-major", "boolean-0-or-1", storePayload),
            CreateLayer("placement", "placement-buildability", "SC2 StartRaw PlacementGrid",
                terrain?.Placement, "uint8-row-major", "boolean-0-or-1", storePayload),
            CreateLayer("terrain-height", "terrain-elevation-byte", "SC2 StartRaw TerrainHeight",
                terrain?.TerrainHeight, "uint8-row-major", "raw-byte-un-calibrated", storePayload)
        }.OrderBy(layer => layer.Id, StringComparer.Ordinal).ToArray();

        var document = new MapDocumentV1
        {
            SchemaVersion = 1,
            MapId = mapId,
            MapName = mapName,
            Source = source,
            ContentHash = string.Empty,
            CoordinateSystem = new CoordinateSystemV1
            {
                Kind = "cartesian-2d",
                Units = "sc2-world-units",
                XAxis = "east",
                YAxis = "north",
                CellEdgeConvention = "minimum-inclusive-maximum-exclusive"
            },
            FullBounds = fullBounds,
            PlayableBounds = new MapRectangleV1
            {
                MinX = spatialMap.Bounds.MinX,
                MinY = spatialMap.Bounds.MinY,
                MaxX = spatialMap.Bounds.MaxX,
                MaxY = spatialMap.Bounds.MaxY
            },
            StartLocations = starts,
            Layers = layers
        };

        return document with
        {
            ContentHash = SingularityBotDocumentJson.ComputeMapContentHash(document)
        };
    }

    private static MapLayerV1 CreateLayer(
        string id,
        string semantic,
        string source,
        SpatialGridSnapshot? grid,
        string encoding,
        string units,
        Func<string, byte[], string> storePayload)
    {
        if (grid is null)
        {
            return new MapLayerV1
            {
                Id = id,
                Semantic = semantic,
                Availability = "unavailable",
                Source = source + " (not supplied)"
            };
        }

        var payload = grid.CopyCells();
        var reference = storePayload(id, payload);
        if (string.IsNullOrWhiteSpace(reference) ||
            reference.StartsWith("/", StringComparison.Ordinal) ||
            reference.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"The payload store returned an invalid relative reference for layer '{id}'.");

        return new MapLayerV1
        {
            Id = id,
            Semantic = semantic,
            Availability = "available",
            Source = source,
            Width = grid.Width,
            Height = grid.Height,
            WorldOriginX = grid.WorldOriginX,
            WorldOriginY = grid.WorldOriginY,
            WorldWidth = grid.WorldWidth,
            WorldHeight = grid.WorldHeight,
            Encoding = encoding,
            Units = units,
            Compression = "none",
            PayloadReference = reference,
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()
        };
    }

    private static void ValidateBounds(MapRectangleV1 full, MapBounds playable)
    {
        if (!double.IsFinite(full.MinX) || !double.IsFinite(full.MinY) ||
            !double.IsFinite(full.MaxX) || !double.IsFinite(full.MaxY) ||
            full.MaxX <= full.MinX || full.MaxY <= full.MinY)
            throw new ArgumentException("Full map bounds must be finite and have positive width and height.", nameof(full));

        if (playable.MinX < full.MinX || playable.MinY < full.MinY ||
            playable.MaxX > full.MaxX || playable.MaxY > full.MaxY)
            throw new ArgumentException("Playable bounds must be contained by full map bounds.", nameof(playable));
    }
}
