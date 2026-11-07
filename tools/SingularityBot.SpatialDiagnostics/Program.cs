using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SingularityBot.Spatial;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/SingularityBot.SpatialDiagnostics -- <fixture.json> <output-directory>");
    return 2;
}

var fixturePath = Path.GetFullPath(args[0]);
var outputDirectory = Path.GetFullPath(args[1]);
var fixtureJson = File.ReadAllText(fixturePath);
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
options.Converters.Add(new JsonStringEnumConverter());

var fixture = JsonSerializer.Deserialize<MapFixture>(fixtureJson, options)
    ?? throw new InvalidDataException("Fixture deserialized to null.");

var first = Generate(fixture, options);
var second = Generate(fixture, options);
if (!StringComparer.Ordinal.Equals(first.Report, second.Report) ||
    !StringComparer.Ordinal.Equals(first.Svg, second.Svg))
{
    throw new InvalidOperationException("Spatial diagnostics are not deterministic for this fixture.");
}

Directory.CreateDirectory(outputDirectory);
var reportPath = Path.Combine(outputDirectory, "spatial-report.json");
var svgPath = Path.Combine(outputDirectory, "spatial-overlay.svg");
File.WriteAllText(reportPath, first.Report, new UTF8Encoding(false));
File.WriteAllText(svgPath, first.Svg, new UTF8Encoding(false));
Console.WriteLine($"Verified deterministic spatial report: {reportPath}");
Console.WriteLine($"Verified deterministic map overlay: {svgPath}");
return 0;

static (string Report, string Svg) Generate(MapFixture fixture, JsonSerializerOptions options)
{
    if (fixture.Frames.Length == 0)
        throw new InvalidDataException("A fixture must contain at least one observation frame.");

    var bounds = new MapBounds(
        fixture.Bounds.MinX, fixture.Bounds.MinY,
        fixture.Bounds.MaxX, fixture.Bounds.MaxY);
    var map = new SpatialMap(
        bounds,
        new MapPoint(fixture.FriendlyStart.X, fixture.FriendlyStart.Y),
        new MapPoint(fixture.EnemyStart.X, fixture.EnemyStart.Y));
    var intelligence = new SpatialIntelligence(map);

    foreach (var frame in fixture.Frames.OrderBy(frame => frame.GameLoop))
    {
        var visible = frame.Contacts.Select(contact => new SpatialContact(
            contact.UnitTag,
            contact.UnitType,
            new MapPoint(contact.X, contact.Y),
            Enum.Parse<SpatialAlliance>(contact.Alliance, true),
            ContactVisibility.Visible,
            frame.GameLoop));
        intelligence.Update(frame.GameLoop, visible, frame.DestroyedTags);
    }

    var contacts = Enum.GetValues<MapSide>()
        .SelectMany(intelligence.GetSide)
        .DistinctBy(item => item.Contact.UnitTag)
        .OrderBy(item => item.Contact.UnitTag)
        .ToArray();

    var report = new
    {
        fixture = fixture.Name,
        gameLoop = fixture.Frames.Max(frame => frame.GameLoop),
        bounds = new { bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY, bounds.Width, bounds.Height },
        center = map.Center,
        side = new
        {
            map.Side.FriendlyStart,
            map.Side.EnemyStart,
            map.Side.DividerCenter,
            map.Side.DividingHalfLine
        },
        quadrants = map.Quadrants.Select(quadrant => new
        {
            quadrant.Kind,
            quadrant.Bounds,
            quadrant.Center
        }),
        sectors = map.Sectors.Select(sector => new
        {
            sector.Id,
            sector.Bounds,
            sector.Center,
            neighbors = map.GetNeighbors(sector.Id).Select(neighbor => neighbor.Id)
        }),
        contacts = contacts.Select(item => new
        {
            item.Contact.UnitTag,
            item.Contact.UnitType,
            item.Contact.Alliance,
            item.Contact.Visibility,
            item.Contact.Position,
            item.Contact.LastSeenGameLoop,
            item.Sector,
            item.Quadrant,
            item.Side
        })
    };

    var reportText = JsonSerializer.Serialize(report, options);
    var svgText = BuildSvg(fixture.Name, map, contacts);
    return (reportText, svgText);
}

static string BuildSvg(string name, SpatialMap map, IReadOnlyList<LocatedContact> contacts)
{
    static string N(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    var width = map.Bounds.Width;
    var height = map.Bounds.Height;
    float X(MapPoint point) => point.X - map.Bounds.MinX;
    float Y(MapPoint point) => map.Bounds.MaxY - point.Y;
    var escapedName = SecurityElement.Escape(name) ?? "Spatial fixture";
    var builder = new StringBuilder();
    builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {N(width)} {N(height)}\" role=\"img\" aria-label=\"Spatial map: {escapedName}\">");
    builder.AppendLine("<title>Spatial map — " + escapedName + "</title>");
    builder.AppendLine("<defs><clipPath id=\"map-clip\"><rect x=\"0\" y=\"0\" width=\"" + N(width) + "\" height=\"" + N(height) + "\" /></clipPath></defs>");
    builder.AppendLine("<rect x=\"0\" y=\"0\" width=\"" + N(width) + "\" height=\"" + N(height) + "\" fill=\"#17212b\" stroke=\"#e5e7eb\" stroke-width=\"0.25\" />");
    builder.AppendLine("<g clip-path=\"url(#map-clip)\" fill=\"none\">");

    // Quadrant boundaries.
    builder.AppendLine($"<path d=\"M {N(X(map.Center is var c ? new MapPoint(c.X, map.Bounds.MinY) : default))} 0 V {N(height)} M 0 {N(Y(new MapPoint(map.Bounds.MinX, map.Center.Y)))} H {N(width)}\" stroke=\"#94a3b8\" stroke-width=\"0.2\" stroke-dasharray=\"1 0.6\" />");

    // 3x3 tactical grid.
    for (var i = 1; i <= 2; i++)
    {
        var x = width * i / 3f;
        var y = height * i / 3f;
        builder.AppendLine($"<path d=\"M {N(x)} 0 V {N(height)} M 0 {N(y)} H {N(width)}\" stroke=\"#64748b\" stroke-width=\"0.16\" />");
    }

    // The Side divider is a full line; clipping keeps it inside playable bounds.
    var line = map.Side.DividingHalfLine;
    var reach = MathF.Sqrt(width * width + height * height) * 2f;
    var a = line.Origin - line.Direction * reach;
    var b = line.Origin + line.Direction * reach;
    builder.AppendLine($"<line x1=\"{N(X(a))}\" y1=\"{N(Y(a))}\" x2=\"{N(X(b))}\" y2=\"{N(Y(b))}\" stroke=\"#fbbf24\" stroke-width=\"0.35\" />");
    builder.AppendLine("</g>");

    // Center and spawn anchors.
    builder.AppendLine($"<circle cx=\"{N(X(map.Center))}\" cy=\"{N(Y(map.Center))}\" r=\"0.7\" fill=\"none\" stroke=\"#f8fafc\" stroke-width=\"0.2\" />");
    builder.AppendLine($"<path d=\"M {N(X(map.Side.FriendlyStart))} {N(Y(map.Side.FriendlyStart))} l 1.1 0 l -0.55 -1 z\" fill=\"#38bdf8\" />");
    builder.AppendLine($"<path d=\"M {N(X(map.Side.EnemyStart))} {N(Y(map.Side.EnemyStart))} l 1.1 0 l -0.55 -1 z\" fill=\"#fb7185\" />");

    foreach (var sector in map.Sectors)
        builder.AppendLine($"<text x=\"{N(X(sector.Center))}\" y=\"{N(Y(sector.Center))}\" fill=\"#cbd5e1\" font-size=\"1.5\" text-anchor=\"middle\" dominant-baseline=\"middle\">{sector.Id}</text>");

    foreach (var item in contacts)
    {
        var contact = item.Contact;
        var color = contact.Alliance switch
        {
            SpatialAlliance.Friendly => "#38bdf8",
            SpatialAlliance.Enemy => "#fb7185",
            _ => "#a3a3a3"
        };
        var radius = contact.Visibility == ContactVisibility.Visible ? 0.9f : 0.65f;
        var opacity = contact.Visibility == ContactVisibility.Visible ? "1" : "0.45";
        var dash = contact.Visibility == ContactVisibility.Visible ? "" : " stroke-dasharray=\"0.35 0.25\"";
        builder.AppendLine($"<circle cx=\"{N(X(contact.Position))}\" cy=\"{N(Y(contact.Position))}\" r=\"{N(radius)}\" fill=\"{color}\" fill-opacity=\"{opacity}\" stroke=\"#f8fafc\" stroke-width=\"0.18\"{dash}><title>tag {contact.UnitTag}: {contact.Alliance}, {contact.Visibility}, last seen {contact.LastSeenGameLoop}</title></circle>");
    }

    builder.AppendLine("</svg>");
    return builder.ToString();
}

sealed class MapFixture
{
    public string Name { get; init; } = "unnamed";
    public BoundsDto Bounds { get; init; } = new();
    public PointDto FriendlyStart { get; init; } = new();
    public PointDto EnemyStart { get; init; } = new();
    public FrameDto[] Frames { get; init; } = [];
}
sealed class BoundsDto
{
    public float MinX { get; init; }
    public float MinY { get; init; }
    public float MaxX { get; init; }
    public float MaxY { get; init; }
}
sealed class PointDto
{
    public float X { get; init; }
    public float Y { get; init; }
}
sealed class FrameDto
{
    public uint GameLoop { get; init; }
    public ContactDto[] Contacts { get; init; } = [];
    public ulong[] DestroyedTags { get; init; } = [];
}
sealed class ContactDto
{
    public ulong UnitTag { get; init; }
    public uint UnitType { get; init; }
    public string Alliance { get; init; } = "Neutral";
    public float X { get; init; }
    public float Y { get; init; }
}
