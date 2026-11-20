using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;

namespace SingularityBot.Serialization;

/// <summary>Protocol-independent immutable static map contract consumed by Forge.</summary>
public sealed record MapDocumentV1
{
    public required int SchemaVersion { get; init; } = 1;
    public required string MapId { get; init; }
    public required string MapName { get; init; }
    public required string Source { get; init; }
    public required string ContentHash { get; init; }
    public required CoordinateSystemV1 CoordinateSystem { get; init; }
    public required MapRectangleV1 FullBounds { get; init; }
    public required MapRectangleV1 PlayableBounds { get; init; }
    public required IReadOnlyList<MapStartLocationV1> StartLocations { get; init; }
    public required IReadOnlyList<MapLayerV1> Layers { get; init; }
}

/// <summary>Declares world-space axes, units and the edge convention used by all map layers.</summary>
public sealed record CoordinateSystemV1
{
    public required string Kind { get; init; } = "cartesian-2d";
    public required string Units { get; init; } = "sc2-world-units";
    public required string XAxis { get; init; } = "east";
    public required string YAxis { get; init; } = "north";
    public required string CellEdgeConvention { get; init; } = "minimum-inclusive-maximum-exclusive";
}

public sealed record MapRectangleV1
{
    public required double MinX { get; init; }
    public required double MinY { get; init; }
    public required double MaxX { get; init; }
    public required double MaxY { get; init; }
}

public sealed record MapStartLocationV1
{
    public required string Id { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
}

public sealed record MapLayerV1
{
    public required string Id { get; init; }
    public required string Semantic { get; init; }
    public required string Availability { get; init; }
    public required string Source { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public double? WorldOriginX { get; init; }
    public double? WorldOriginY { get; init; }
    public double? WorldWidth { get; init; }
    public double? WorldHeight { get; init; }
    public string? Encoding { get; init; }
    public string? Units { get; init; }
    public string? Compression { get; init; }
    public string? PayloadReference { get; init; }
    public string? PayloadSha256 { get; init; }
}

/// <summary>One complete normalized observation frame, linked to an exact immutable map digest.</summary>
public sealed record MatchSnapshotV1
{
    public required int SchemaVersion { get; init; } = 1;
    public required string MatchId { get; init; }
    public required string MapId { get; init; }
    public required string MapContentHash { get; init; }
    public required uint ObserverPlayerId { get; init; }
    public required ulong Sequence { get; init; }
    public required uint GameLoop { get; init; }
    public required string Lifecycle { get; init; }
    public required IReadOnlyList<MatchPlayerV1> Players { get; init; }
    public required IReadOnlyList<MatchUnitV1> Units { get; init; }
    public required IReadOnlyList<ulong> AuthoritativelyDestroyedUnitTags { get; init; }
}

public sealed record MatchPlayerV1
{
    public required uint PlayerId { get; init; }
    public required string Team { get; init; }
    public required string Race { get; init; }
    public uint? Minerals { get; init; }
    public uint? Vespene { get; init; }
    public uint? FoodUsed { get; init; }
    public uint? FoodCap { get; init; }
}

public sealed record MatchUnitV1
{
    public required ulong Tag { get; init; }
    public required uint UnitTypeId { get; init; }
    public required string UnitKind { get; init; }
    public required int OwnerId { get; init; }
    public required string Alliance { get; init; }
    public required string ContactState { get; init; }
    public uint? LastSeenGameLoop { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Health { get; init; }
    public double? HealthMax { get; init; }
    public double? Shield { get; init; }
    public double? ShieldMax { get; init; }
    public double? Energy { get; init; }
    public double? BuildProgress { get; init; }
    public IReadOnlyList<MatchOrderV1> Orders { get; init; } = Array.Empty<MatchOrderV1>();
}

public sealed record MatchOrderV1
{
    public required uint AbilityId { get; init; }
    public ulong? TargetUnitTag { get; init; }
    public double? TargetX { get; init; }
    public double? TargetY { get; init; }
}

/// <summary>
/// Deterministic v1 JSON codec. Callers must provide arrays in canonical order:
/// starts by ordinal Id, layers by ordinal Id, players by PlayerId, units and
/// destroyed tags by numeric tag. Hashes are lowercase SHA-256 hex over UTF-8 bytes.
/// </summary>
public static class SingularityBotDocumentJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false
    };

    public static byte[] Serialize<T>(T document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateDocumentVersion(document);
        ValidateCanonicalOrdering(document);
        return JsonSerializer.SerializeToUtf8Bytes(document, Options);
    }

    public static T Deserialize<T>(ReadOnlySpan<byte> json)
    {
        var document = JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new JsonException($"The JSON document did not contain a {typeof(T).Name} value.");
        try
        {
            ValidateDocumentVersion(document);
            ValidateCanonicalOrdering(document);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("The document violates the supported v1 contract.", exception);
        }
        return document;
    }

    /// <summary>
    /// Computes the map identity digest with ContentHash cleared, avoiding a
    /// self-referential hash. Store the returned lowercase digest in ContentHash.
    /// </summary>
    public static string ComputeMapContentHash(MapDocumentV1 document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return ComputeSha256(document with { ContentHash = string.Empty });
    }

    public static string ComputeSha256<T>(T document) =>
        Convert.ToHexString(SHA256.HashData(Serialize(document))).ToLowerInvariant();

    public static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void ValidateDocumentVersion<T>(T document)
    {
        if (document is MapDocumentV1 map && map.SchemaVersion != 1)
            throw new ArgumentException($"Unsupported MapDocument schema version {map.SchemaVersion}.", nameof(document));
        if (document is MatchSnapshotV1 snapshot && snapshot.SchemaVersion != 1)
            throw new ArgumentException($"Unsupported MatchSnapshot schema version {snapshot.SchemaVersion}.", nameof(document));
    }

    private static void ValidateCanonicalOrdering<T>(T document)
    {
        if (document is MapDocumentV1 map)
        {
            if (map.StartLocations is null || map.Layers is null ||
                !map.StartLocations.Select(item => item.Id)
                    .SequenceEqual(map.StartLocations.Select(item => item.Id).OrderBy(id => id, StringComparer.Ordinal)) ||
                !map.Layers.Select(item => item.Id)
                    .SequenceEqual(map.Layers.Select(item => item.Id).OrderBy(id => id, StringComparer.Ordinal)) ||
                map.StartLocations.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != map.StartLocations.Count ||
                map.Layers.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != map.Layers.Count)
                throw new ArgumentException("Map start locations and layers must have unique IDs in ordinal ascending order.", nameof(document));
        }
        else if (document is MatchSnapshotV1 snapshot)
        {
            if (snapshot.Players is null || snapshot.Units is null || snapshot.AuthoritativelyDestroyedUnitTags is null ||
                !snapshot.Players.Select(item => item.PlayerId).SequenceEqual(snapshot.Players.Select(item => item.PlayerId).OrderBy(id => id)) ||
                !snapshot.Units.Select(item => item.Tag).SequenceEqual(snapshot.Units.Select(item => item.Tag).OrderBy(tag => tag)) ||
                !snapshot.AuthoritativelyDestroyedUnitTags.SequenceEqual(snapshot.AuthoritativelyDestroyedUnitTags.OrderBy(tag => tag)) ||
                snapshot.Players.Select(item => item.PlayerId).Distinct().Count() != snapshot.Players.Count ||
                snapshot.Units.Select(item => item.Tag).Distinct().Count() != snapshot.Units.Count ||
                snapshot.AuthoritativelyDestroyedUnitTags.Distinct().Count() != snapshot.AuthoritativelyDestroyedUnitTags.Count ||
                snapshot.Units.Any(unit => snapshot.AuthoritativelyDestroyedUnitTags.Contains(unit.Tag)))
                throw new ArgumentException("Match players, units and destruction tags must be unique, sorted, and destruction tags must not remain in the unit set.", nameof(document));
            if (!IsSha256(snapshot.MapContentHash))
                throw new ArgumentException("Match snapshots must reference a lowercase SHA-256 map content hash.", nameof(document));

            if (snapshot.Lifecycle is not ("initializing" or "running" or "ended" or "unknown"))
                throw new ArgumentException($"Unsupported match lifecycle '{snapshot.Lifecycle}'.", nameof(document));

            foreach (var unit in snapshot.Units)
            {
                if (unit.Alliance is not ("unknown" or "self" or "ally" or "neutral" or "enemy"))
                    throw new ArgumentException($"Unsupported unit alliance '{unit.Alliance}'.", nameof(document));
                var hasPosition = unit.X.HasValue && unit.Y.HasValue;
                switch (unit.ContactState)
                {
                    case "visible" when !hasPosition:
                        throw new ArgumentException("Visible contacts require both world coordinates.", nameof(document));
                    case "remembered" when !hasPosition || !unit.LastSeenGameLoop.HasValue:
                        throw new ArgumentException("Remembered contacts require coordinates and lastSeenGameLoop.", nameof(document));
                    case "unknown" when unit.X.HasValue || unit.Y.HasValue:
                        throw new ArgumentException("Unknown contacts cannot assert world coordinates.", nameof(document));
                    case "visible":
                    case "remembered":
                    case "unknown":
                        break;
                    default:
                        throw new ArgumentException($"Unsupported contact state '{unit.ContactState}'.", nameof(document));
                }
            }
        }
    }
}
