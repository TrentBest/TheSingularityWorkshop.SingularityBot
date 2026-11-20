using SingularityBot.Serialization;

namespace SingularityBot.Tests;

public sealed class VersionedDocumentSerializationTests
{
    [Fact]
    public void MapDocumentSerializationIsByteStableAndRoundTrips()
    {
        var document = CreateMapDocument();
        var first = SingularityBotDocumentJson.Serialize(document);
        var second = SingularityBotDocumentJson.Serialize(document);

        Assert.Equal(first, second);
        var restored = SingularityBotDocumentJson.Deserialize<MapDocumentV1>(first);
        Assert.Equal(first, SingularityBotDocumentJson.Serialize(restored));
        Assert.Equal(
            SingularityBotDocumentJson.ComputeMapContentHash(document),
            SingularityBotDocumentJson.ComputeMapContentHash(restored));
    }

    [Fact]
    public void MapContentHashDoesNotHashItsOwnStoredDigest()
    {
        var document = CreateMapDocument();
        var first = SingularityBotDocumentJson.ComputeMapContentHash(document);
        var withStoredDigest = document with { ContentHash = first };

        Assert.Equal(first, SingularityBotDocumentJson.ComputeMapContentHash(withStoredDigest));
        Assert.True(SingularityBotDocumentJson.IsSha256(first));
    }

    [Fact]
    public void MapContentHashChangesWhenMapFactsChange()
    {
        var document = CreateMapDocument();
        var changed = document with
        {
            PlayableBounds = document.PlayableBounds with { MaxX = 241 }
        };

        Assert.NotEqual(
            SingularityBotDocumentJson.ComputeMapContentHash(document),
            SingularityBotDocumentJson.ComputeMapContentHash(changed));
    }

    [Fact]
    public void MatchSnapshotSerializationPreservesFogAndAuthoritativeDeathSeparately()
    {
        var snapshot = new MatchSnapshotV1
        {
            SchemaVersion = 1,
            MatchId = "fixture-match-01",
            MapId = "fixture-translated-map",
            MapContentHash = new string('a', 64),
            Sequence = 2,
            GameLoop = 480,
            Lifecycle = "running",
            Players = Array.Empty<MatchPlayerV1>(),
            Units =
            [
                new MatchUnitV1
                {
                    Tag = 100,
                    UnitTypeId = 48,
                    UnitKind = "Marine",
                    OwnerId = 1,
                    Alliance = "self",
                    ContactState = "visible",
                    X = 24.5,
                    Y = 32.5
                },
                new MatchUnitV1
                {
                    Tag = 200,
                    UnitTypeId = 48,
                    UnitKind = "Marine",
                    OwnerId = 2,
                    Alliance = "enemy",
                    ContactState = "remembered",
                    LastSeenGameLoop = 400,
                    X = 198.5,
                    Y = 164.5
                }
            ],
            AuthoritativelyDestroyedUnitTags = [300]
        };

        var bytes = SingularityBotDocumentJson.Serialize(snapshot);
        var restored = SingularityBotDocumentJson.Deserialize<MatchSnapshotV1>(bytes);

        Assert.Equal(bytes, SingularityBotDocumentJson.Serialize(restored));
        Assert.Equal("remembered", restored.Units[1].ContactState);
        Assert.Equal((uint?)400, restored.Units[1].LastSeenGameLoop);
        Assert.Contains((ulong)300, restored.AuthoritativelyDestroyedUnitTags);
        Assert.DoesNotContain(restored.Units, unit => unit.Tag == 300);
    }

    [Fact]
    public void CanonicalMapFixtureHasAValidContentDigestAndExplicitUnavailableLayer()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MapDocuments",
            "translated-two-start.map.v1.json");
        var bytes = File.ReadAllBytes(path);
        var map = SingularityBotDocumentJson.Deserialize<MapDocumentV1>(bytes);

        Assert.Equal(map.ContentHash, SingularityBotDocumentJson.ComputeMapContentHash(map));
        Assert.NotEqual(map.FullBounds.MinX, map.PlayableBounds.MinX);
        Assert.NotEqual(map.FullBounds.MaxY, map.PlayableBounds.MaxY);
        Assert.Equal(2, map.StartLocations.Count);
        Assert.Contains(map.Layers, layer => layer.Id == "resource-nodes" && layer.Availability == "unavailable");
    }

    [Fact]
    public void CanonicalMatchFramesRetainFogAndDoNotInferDestructionFromAbsence()
    {
        MatchSnapshotV1 Read(string name)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MatchSnapshots", name);
            return SingularityBotDocumentJson.Deserialize<MatchSnapshotV1>(File.ReadAllBytes(path));
        }

        var visible = Read("translated-two-start.frame-0001.snapshot.v1.json");
        var remembered = Read("translated-two-start.frame-0002.snapshot.v1.json");
        var enemy = Assert.Single(remembered.Units, unit => unit.Tag == 200);

        Assert.Equal(visible.MapContentHash, remembered.MapContentHash);
        Assert.Equal("visible", Assert.Single(visible.Units, unit => unit.Tag == 200).ContactState);
        Assert.Equal("remembered", enemy.ContactState);
        Assert.Equal((uint?)400, enemy.LastSeenGameLoop);
        Assert.Contains((ulong)300, remembered.AuthoritativelyDestroyedUnitTags);
        Assert.DoesNotContain(remembered.Units, unit => unit.Tag == 300);
    }

    [Fact]
    public void MatchSnapshotRejectsNonCanonicalUnitOrdering()
    {
        var snapshot = new MatchSnapshotV1
        {
            SchemaVersion = 1,
            MatchId = "fixture-match-01",
            MapId = "fixture-translated-map",
            MapContentHash = new string('a', 64),
            Sequence = 1,
            GameLoop = 1,
            Lifecycle = "running",
            Players = Array.Empty<MatchPlayerV1>(),
            Units =
            [
                new MatchUnitV1 { Tag = 200, UnitTypeId = 1, UnitKind = "B", OwnerId = 2, Alliance = "enemy", ContactState = "unknown" },
                new MatchUnitV1 { Tag = 100, UnitTypeId = 1, UnitKind = "A", OwnerId = 1, Alliance = "self", ContactState = "unknown" }
            ],
            AuthoritativelyDestroyedUnitTags = Array.Empty<ulong>()
        };

        Assert.Throws<ArgumentException>(() => SingularityBotDocumentJson.Serialize(snapshot));
    }

    private static MapDocumentV1 CreateMapDocument() => new()
    {
        SchemaVersion = 1,
        MapId = "fixture-translated-map",
        MapName = "Translated Two-Start Fixture",
        Source = "canonical-test-fixture",
        ContentHash = new string('a', 64),
        CoordinateSystem = new CoordinateSystemV1
        {
            Kind = "cartesian-2d",
            Units = "sc2-world-units",
            XAxis = "east",
            YAxis = "north",
            CellEdgeConvention = "minimum-inclusive-maximum-exclusive"
        },
        FullBounds = new MapRectangleV1 { MinX = 0, MinY = 0, MaxX = 256, MaxY = 224 },
        PlayableBounds = new MapRectangleV1 { MinX = 16, MinY = 8, MaxX = 240, MaxY = 200 },
        StartLocations =
        [
            new MapStartLocationV1 { Id = "start-01", X = 32.5, Y = 28.5 },
            new MapStartLocationV1 { Id = "start-02", X = 220.5, Y = 180.5 }
        ],
        Layers =
        [
            new MapLayerV1
            {
                Id = "terrain-height",
                Semantic = "terrain-elevation-byte",
                Availability = "available",
                Source = "SC2 StartRaw TerrainHeight",
                Width = 2,
                Height = 2,
                WorldOriginX = 0,
                WorldOriginY = 0,
                WorldWidth = 256,
                WorldHeight = 224,
                Encoding = "uint8-row-major",
                Units = "raw-byte-un-calibrated",
                Compression = "none",
                PayloadReference = "payloads/terrain-height.fixture.hex",
                PayloadSha256 = new string('b', 64)
            },
            new MapLayerV1
            {
                Id = "resource-nodes",
                Semantic = "resource-node-features",
                Availability = "unavailable",
                Source = "not-yet-decoded"
            }
        ]
    };
}
