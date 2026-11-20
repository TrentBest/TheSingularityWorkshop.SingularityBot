using SingularityBot.Serialization;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

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
            ObserverPlayerId = 1,
            CapturedFields = ["position"],
            Sequence = 2,
            GameLoop = 480,
            Lifecycle = "running",
            Players = [new MatchPlayerV1 { PlayerId = 1, Team = "1", Race = "Terran" }],
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
            ObserverPlayerId = 1,
            CapturedFields = ["position"],
            Sequence = 1,
            GameLoop = 1,
            Lifecycle = "running",
            Players = [new MatchPlayerV1 { PlayerId = 1, Team = "1", Race = "Terran" }],
            Units =
            [
                new MatchUnitV1 { Tag = 200, UnitTypeId = 1, UnitKind = "B", OwnerId = 2, Alliance = "enemy", ContactState = "unknown" },
                new MatchUnitV1 { Tag = 100, UnitTypeId = 1, UnitKind = "A", OwnerId = 1, Alliance = "self", ContactState = "unknown" }
            ],
            AuthoritativelyDestroyedUnitTags = Array.Empty<ulong>()
        };

        Assert.Throws<ArgumentException>(() => SingularityBotDocumentJson.Serialize(snapshot));
    }

    [Fact]
    public void UnknownContactCannotAssertWorldCoordinates()
    {
        var snapshot = new MatchSnapshotV1
        {
            SchemaVersion = 1,
            MatchId = "fixture-match-01",
            MapId = "fixture-translated-map",
            MapContentHash = new string('a', 64),
            ObserverPlayerId = 1,
            CapturedFields = ["position"],
            Sequence = 1,
            GameLoop = 1,
            Lifecycle = "running",
            Players = [new MatchPlayerV1 { PlayerId = 1, Team = "1", Race = "Terran" }],
            Units =
            [
                new MatchUnitV1
                {
                    Tag = 100,
                    UnitTypeId = 1,
                    UnitKind = "Unknown",
                    OwnerId = 2,
                    Alliance = "enemy",
                    ContactState = "unknown",
                    X = 10,
                    Y = 10
                }
            ],
            AuthoritativelyDestroyedUnitTags = Array.Empty<ulong>()
        };

        Assert.Throws<ArgumentException>(() => SingularityBotDocumentJson.Serialize(snapshot));
    }

    [Fact]
    public void UnsupportedMapSchemaVersionsAreRejected()
    {
        var document = CreateMapDocument();
        Assert.Throws<ArgumentException>(() =>
            SingularityBotDocumentJson.Serialize(document with { SchemaVersion = 2 }));

        var json = System.Text.Encoding.UTF8.GetString(SingularityBotDocumentJson.Serialize(document));
        var futureVersion = json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal);
        Assert.Throws<System.Text.Json.JsonException>(() =>
            SingularityBotDocumentJson.Deserialize<MapDocumentV1>(System.Text.Encoding.UTF8.GetBytes(futureVersion)));
    }

    [Fact]
    public void UnknownMapDocumentPropertiesAreRejected()
    {
        var json = System.Text.Encoding.UTF8.GetString(SingularityBotDocumentJson.Serialize(CreateMapDocument()));
        var extended = json.Replace("\"mapId\":", "\"unexpected\":true,\"mapId\":", StringComparison.Ordinal);

        Assert.Throws<System.Text.Json.JsonException>(() =>
            SingularityBotDocumentJson.Deserialize<MapDocumentV1>(System.Text.Encoding.UTF8.GetBytes(extended)));
    }

    [Fact]
    public void MapDocumentFactoryPreservesLayerOriginsAndExplicitAvailability()
    {
        var spatialMap = new SpatialMap(
            new MapBounds(10, 20, 30, 40),
            new MapPoint(12, 22),
            new MapPoint(28, 38));
        var pathing = new SpatialGridSnapshot(
            2, 2, 40, 50, new byte[] { 1, 0, 1, 1 }, -5, 2);
        var heights = new SpatialGridSnapshot(
            2, 2, 40, 50, new byte[] { 2, 3, 4, 5 }, -5, 2);
        var terrain = new SpatialTerrain(pathing, null, heights);
        var storedPayloads = new Dictionary<string, byte[]>();

        var document = MapDocumentV1Factory.Create(
            "translated-fixture",
            "Translated Fixture",
            "unit-test",
            new MapRectangleV1 { MinX = -5, MinY = 2, MaxX = 35, MaxY = 52 },
            spatialMap,
            [
                new MapStartLocationV1 { Id = "start-b", X = 28, Y = 38 },
                new MapStartLocationV1 { Id = "start-a", X = 12, Y = 22 }
            ],
            terrain,
            (id, payload) =>
            {
                storedPayloads.Add(id, payload);
                return "payloads/" + id + ".bin";
            });

        Assert.Equal(document.ContentHash, SingularityBotDocumentJson.ComputeMapContentHash(document));
        Assert.Equal(new[] { "start-a", "start-b" }, document.StartLocations.Select(start => start.Id));
        Assert.Equal(new[] { "pathing", "placement", "terrain-height" }, document.Layers.Select(layer => layer.Id));
        var pathingLayer = document.Layers[0];
        Assert.Equal("available", pathingLayer.Availability);
        Assert.Equal(-5, pathingLayer.WorldOriginX);
        Assert.Equal(2, pathingLayer.WorldOriginY);
        Assert.Equal("payloads/pathing.bin", pathingLayer.PayloadReference);
        Assert.Equal("unavailable", document.Layers[1].Availability);
        Assert.Equal(new byte[] { 1, 0, 1, 1 }, storedPayloads["pathing"]);
        Assert.Equal(new byte[] { 2, 3, 4, 5 }, storedPayloads["terrain-height"]);
    }

    [Fact]
    public void MatchSnapshotFactoryUsesLiveHistoryForRememberedContactsAndDeathEvidence()
    {
        var captured = ObservationFields.Position | ObservationFields.Health |
                       ObservationFields.Shields | ObservationFields.Energy |
                       ObservationFields.BuildProgress | ObservationFields.OrderDetails |
                       ObservationFields.Economy | ObservationFields.Cargo |
                       ObservationFields.HasOrders | ObservationFields.Mobility |
                       ObservationFields.WeaponCooldown | ObservationFields.CollisionRadius |
                       ObservationFields.VespeneContents;
        UnitObservationFacts Unit(
            ulong tag,
            UnitAlliance alliance,
            UnitVisibility visibility,
            bool hasPosition,
            float x,
            float y,
            ObservationFields fields) =>
            new(tag, 48, "Marine", alliance == UnitAlliance.Self ? 1 : 2,
                alliance, visibility, 45, 45, 0, 0, 1,
                x, y, hasPosition, false, false, false, false, false, 480)
            {
                CapturedFields = fields,
                Energy = 50,
                WeaponCooldownRemaining = 2,
                CollisionRadius = 0.375f,
                VespeneContents = 100,
                Orders = Array.Empty<UnitOrderObservationFacts>()
            };

        var observation = new NormalizedObservation(
            480,
            1,
            [
                Unit(100, UnitAlliance.Self, UnitVisibility.Visible, true, 25, 35, captured),
                Unit(200, UnitAlliance.Enemy, UnitVisibility.Snapshot, false, 0, 0, ObservationFields.Health),
                Unit(300, UnitAlliance.Enemy, UnitVisibility.Hidden, false, 0, 0, ObservationFields.None),
                Unit(400, UnitAlliance.Enemy, UnitVisibility.Visible, true, 40, 40, captured)
            ],
            new HashSet<ulong> { 400 })
        {
            Minerals = 150,
            Vespene = 75,
            FoodUsed = 19,
            FoodCap = 23,
            CapturedFields = captured
        };

        var snapshot = MatchSnapshotV1Factory.Create(
            "fixture-match-01",
            "fixture-translated-map",
            new string('a', 64),
            2,
            "running",
            observation,
            [
                new MatchPlayerV1 { PlayerId = 1, Team = "1", Race = "Terran", Minerals = 1 },
                new MatchPlayerV1 { PlayerId = 2, Team = "2", Race = "Terran" }
            ],
            new Dictionary<ulong, LastSeenContactV1>
            {
                [200] = new(200, 48, "Marine", 2, UnitAlliance.Enemy, 1, 400, 198.5, 164.5),
                [250] = new(250, 48, "Marine", 2, UnitAlliance.Enemy, 1, 350, 185.5, 155.5),
                [400] = new(400, 48, "Marine", 2, UnitAlliance.Enemy, 1, 470, 40, 40)
            });

        var visible = Assert.Single(snapshot.Units, unit => unit.Tag == 100);
        var remembered = Assert.Single(snapshot.Units, unit => unit.Tag == 200);
        var unknown = Assert.Single(snapshot.Units, unit => unit.Tag == 300);
        var absentButRemembered = Assert.Single(snapshot.Units, unit => unit.Tag == 250);
        Assert.Equal("visible", visible.ContactState);
        Assert.Equal((double?)25, visible.X);
        Assert.Equal((double?)35, visible.Y);
        Assert.Equal((bool?)false, visible.HasCargo);
        Assert.Equal((bool?)false, visible.HasOrders);
        Assert.Equal((bool?)false, visible.IsFlying);
        Assert.Equal((double?)2, visible.WeaponCooldownRemaining);
        Assert.Equal((double?)0.375, visible.CollisionRadius);
        Assert.Equal((uint?)100, visible.VespeneContents);
        Assert.Equal("remembered", remembered.ContactState);
        Assert.Equal((uint?)400, remembered.LastSeenGameLoop);
        Assert.Equal((double?)198.5, remembered.X);
        Assert.Equal((double?)164.5, remembered.Y);
        Assert.Equal("unknown", unknown.ContactState);
        Assert.Equal("remembered", absentButRemembered.ContactState);
        Assert.Equal((uint?)350, absentButRemembered.LastSeenGameLoop);
        Assert.Null(unknown.X);
        Assert.Contains((ulong)400, snapshot.AuthoritativelyDestroyedUnitTags);
        Assert.DoesNotContain(snapshot.Units, unit => unit.Tag == 400);
        Assert.Equal((uint?)150, snapshot.Players[0].Minerals);
        Assert.Equal(new string('a', 64), snapshot.MapContentHash);
        Assert.Equal((uint)1, snapshot.ObserverPlayerId);
        Assert.Equal(new[] { "buildProgress", "cargo", "collisionRadius", "economy", "energy", "hasOrders", "health", "mobility", "orderDetails", "position", "shields", "vespeneContents", "weaponCooldown" }, snapshot.CapturedFields);
        _ = SingularityBotDocumentJson.Serialize(snapshot);
    }

    [Fact]
    public void MatchSnapshotFactoryRejectsVisibleUnitsWithoutCapturedPosition()
    {
        var facts = new UnitObservationFacts(
            100, 48, "Marine", 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, 0, 0, false, false, false,
            false, false, false, 10)
        {
            CapturedFields = ObservationFields.Health
        };
        var observation = new NormalizedObservation(10, 1, [facts], new HashSet<ulong>());
        var players = new[] { new MatchPlayerV1 { PlayerId = 1, Team = "1", Race = "Terran" } };

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = MatchSnapshotV1Factory.Create(
                "match", "map", new string('a', 64), 1, "running",
                observation, players, new Dictionary<ulong, LastSeenContactV1>());
        });
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
                Id = "resource-nodes",
                Semantic = "resource-node-features",
                Availability = "unavailable",
                Source = "not-yet-decoded"
            },
            new MapLayerV1
            {
                Id = "terrain-height",
                Semantic = "terrain-elevation-byte",
                Availability = "available",
                Source = "canonical-fixture-payload",
                Width = 2,
                Height = 2,
                WorldOriginX = 0,
                WorldOriginY = 0,
                WorldWidth = 256,
                WorldHeight = 224,
                Encoding = "hex-text-uint8-row-major",
                Units = "raw-byte-un-calibrated",
                Compression = "none",
                PayloadReference = "payloads/terrain-height.fixture.hex",
                PayloadSha256 = "d6bbff4501793f6776bc5ece5ee470d31d909af1ce1cdbc817c623921eef948a"
            }
        ]
    };
}
