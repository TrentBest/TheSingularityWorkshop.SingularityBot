using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ConstructionSiteSafetyTests
{
    [Fact]
    public void DestroyedBarracksFootprintIsRetainedAndFailsClosedWithoutVisibility()
    {
        var safety = new ConstructionSiteSafety();
        var intelligence = Intelligence();
        safety.Update(Snapshot(100, Fact(200, "Barracks", 20, 20)), VisibleGrid(), intelligence);
        intelligence.Update(110, Array.Empty<SpatialContact>(), new[] { 200UL });
        safety.Update(Snapshot(110, Array.Empty<UnitObservationFacts>(), 200), null, intelligence);

        var site = Assert.Single(safety.Sites);
        Assert.Equal(200UL, site.DestroyedUnitTag);
        Assert.Equal(new MapPoint(20, 20), site.Position);
        Assert.Equal(110u, site.DestroyedAtGameLoop);
        Assert.Equal(ConstructionSiteStatus.Unknown, site.Status);
        Assert.True(safety.IsBlocked(new MapPoint(20, 20), 110));
    }

    [Fact]
    public void RecentEnemyContactRemainsContestedEvenWhenSiteVisibilityIsUnavailable()
    {
        var (safety, intelligence) = CreateDestroyedSite();
        intelligence.Update(120, new[]
        {
            new SpatialContact(900, 2, new MapPoint(25, 20),
                SpatialAlliance.Enemy, ContactVisibility.Visible, 120)
        });

        safety.Update(Snapshot(120), null, intelligence);

        Assert.Equal(ConstructionSiteStatus.Contested, Assert.Single(safety.Sites).Status);
        Assert.True(safety.IsBlocked(new MapPoint(20, 20), 120));
    }

    [Fact]
    public void RememberedEnemyContestsSiteAndStaleEvidenceRequiresVisibleClearInterval()
    {
        var (safety, intelligence) = CreateDestroyedSite();
        var enemy = new SpatialContact(
            900, 2, new MapPoint(25, 20), SpatialAlliance.Enemy,
            ContactVisibility.Visible, 120);

        intelligence.Update(120, new[] { enemy });
        safety.Update(Snapshot(120), VisibleGrid(), intelligence);
        Assert.Equal(ConstructionSiteStatus.Contested, Assert.Single(safety.Sites).Status);

        intelligence.Update(150, Array.Empty<SpatialContact>());
        safety.Update(Snapshot(150), VisibleGrid(), intelligence);
        Assert.Equal(ConstructionSiteStatus.Contested, Assert.Single(safety.Sites).Status);

        // The contact is now older than the freshness window. Expiry does not
        // establish safety: visible observation must persist for the clear interval.
        intelligence.Update(345, Array.Empty<SpatialContact>());
        safety.Update(Snapshot(345), VisibleGrid(), intelligence);
        var waiting = Assert.Single(safety.Sites);
        Assert.Equal(ConstructionSiteStatus.Unknown, waiting.Status);
        Assert.Equal(345u, waiting.ClearSinceGameLoop);
        Assert.True(safety.IsBlocked(new MapPoint(20, 20), 345));

        safety.Update(Snapshot(456), VisibleGrid(), intelligence);
        Assert.Equal(ConstructionSiteStatus.Unknown, Assert.Single(safety.Sites).Status);
        safety.Update(Snapshot(457), VisibleGrid(), intelligence);
        Assert.Equal(ConstructionSiteStatus.Clear, Assert.Single(safety.Sites).Status);
        Assert.False(safety.IsBlocked(new MapPoint(20, 20), 457));
    }

    [Fact]
    public void NewEnemyContactResetsContinuousClearInterval()
    {
        var (safety, intelligence) = CreateDestroyedSite();
        safety.Update(Snapshot(345), VisibleGrid(), intelligence);
        safety.Update(Snapshot(457), VisibleGrid(), intelligence);
        Assert.Equal(ConstructionSiteStatus.Clear, Assert.Single(safety.Sites).Status);

        intelligence.Update(458, new[]
        {
            new SpatialContact(901, 2, new MapPoint(24, 20),
                SpatialAlliance.Enemy, ContactVisibility.Visible, 458)
        });
        safety.Update(Snapshot(458), VisibleGrid(), intelligence);
        var contested = Assert.Single(safety.Sites);
        Assert.Equal(ConstructionSiteStatus.Contested, contested.Status);
        Assert.Null(contested.ClearSinceGameLoop);

        intelligence.Update(683, Array.Empty<SpatialContact>());
        safety.Update(Snapshot(683), VisibleGrid(), intelligence);
        var waiting = Assert.Single(safety.Sites);
        Assert.Equal(ConstructionSiteStatus.Unknown, waiting.Status);
        Assert.Equal(683u, waiting.ClearSinceGameLoop);
    }

    [Fact]
    public void VisionLossResetsClearIntervalAndResetClearsAllSiteHistory()
    {
        var (safety, intelligence) = CreateDestroyedSite();
        safety.Update(Snapshot(345), VisibleGrid(), intelligence);
        safety.Update(Snapshot(400), null, intelligence);
        Assert.Equal(ConstructionSiteStatus.Unknown, Assert.Single(safety.Sites).Status);
        Assert.Null(Assert.Single(safety.Sites).ClearSinceGameLoop);

        safety.Reset();
        Assert.Empty(safety.Sites);
        Assert.False(safety.IsBlocked(new MapPoint(20, 20), 500));
    }

    [Fact]
    public void PlacementQuerySkipsKnownDestroyedFootprintAndUsesAlternativeCandidate()
    {
        var (safety, intelligence) = CreateDestroyedSiteAtFirstBarracksCandidate();
        var controller = new Sc2OpeningMarineController();
        var assignments = new WorkerResourceAssignmentPlanner();
        var jobs = new BuildJobRuntime();
        var map = new SpatialMap(new MapBounds(0, 0, 64, 64),
            new MapPoint(10, 10), new MapPoint(54, 54));
        var gameData = GameData();

        Assert.Null(controller.TryCreateAction(Snapshot(100, Fact(10, "SCV", 10, 10),
            Fact(1, "CommandCenter", 10, 10)), map, gameData, assignments, jobs,
            siteSafety: safety));

        var query = controller.TakePlacementQuery(gameData, safety, 100);
        Assert.NotNull(query);
        Assert.True(query.Placements.Count < controller.CandidateCount);
        Assert.DoesNotContain(query.Placements, placement =>
            Math.Abs(placement.TargetPos.X - 13.18198f) < 0.05f &&
            Math.Abs(placement.TargetPos.Y - 13.18198f) < 0.05f);
    }

    private static (ConstructionSiteSafety Safety, SpatialIntelligence Intelligence) CreateDestroyedSite()
    {
        var safety = new ConstructionSiteSafety();
        var intelligence = Intelligence();
        safety.Update(Snapshot(100, Fact(200, "Barracks", 20, 20)), VisibleGrid(), intelligence);
        intelligence.Update(110, Array.Empty<SpatialContact>(), new[] { 200UL });
        safety.Update(Snapshot(110, Array.Empty<UnitObservationFacts>(), 200), null, intelligence);
        return (safety, intelligence);
    }

    private static (ConstructionSiteSafety Safety, SpatialIntelligence Intelligence) CreateDestroyedSiteAtFirstBarracksCandidate()
    {
        var safety = new ConstructionSiteSafety();
        var intelligence = Intelligence();
        var candidate = new MapPoint(10 + (float)(4.5 / Math.Sqrt(2)),
            10 + (float)(4.5 / Math.Sqrt(2)));
        safety.Update(Snapshot(100, Fact(200, "Barracks", candidate.X, candidate.Y)), VisibleGrid(), intelligence);
        intelligence.Update(110, Array.Empty<SpatialContact>(), new[] { 200UL });
        safety.Update(Snapshot(110, Array.Empty<UnitObservationFacts>(), 200), null, intelligence);
        return (safety, intelligence);
    }

    private static SpatialIntelligence Intelligence() =>
        new(new SpatialMap(new MapBounds(0, 0, 64, 64),
            new MapPoint(5, 5), new MapPoint(59, 59)));

    private static SpatialGridSnapshot VisibleGrid() =>
        new(64, 64, 64, 64, Enumerable.Repeat((byte)2, 64 * 64).ToArray());

    private static NormalizedObservation Snapshot(uint gameLoop, params UnitObservationFacts[] units) =>
        Snapshot(gameLoop, units, null);

    private static NormalizedObservation Snapshot(uint gameLoop, UnitObservationFacts[] units, ulong? deadTag) =>
        new(gameLoop, 1, units, deadTag.HasValue
            ? new HashSet<ulong> { deadTag.Value }
            : new HashSet<ulong>())
        {
            Minerals = 150,
            FoodUsed = 12,
            FoodCap = 15
        };

    private static UnitObservationFacts Fact(ulong tag, string kind, float x, float y) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1f, x, y, true,
            false, false, false, false, false, 100);

    private static Sc2GameDataCatalog GameData()
    {
        var data = new SC2APIProtocol.ResponseData();
        data.Units.Add(new SC2APIProtocol.UnitTypeData { UnitId = 21, Name = "Barracks", AbilityId = 321, MineralCost = 150 });
        data.Abilities.Add(new SC2APIProtocol.AbilityData { AbilityId = 16, LinkName = "Move" });
        data.Abilities.Add(new SC2APIProtocol.AbilityData { AbilityId = 321, LinkName = "Build_Barracks" });
        data.Units.Add(new SC2APIProtocol.UnitTypeData { UnitId = 45, Name = "SCV", AbilityId = 524, MineralCost = 50, FoodRequired = 1 });
        data.Units.Add(new SC2APIProtocol.UnitTypeData { UnitId = 48, Name = "Marine", AbilityId = 560, MineralCost = 50, FoodRequired = 1 });
        return Sc2GameDataCatalog.Create(data);
    }
}
