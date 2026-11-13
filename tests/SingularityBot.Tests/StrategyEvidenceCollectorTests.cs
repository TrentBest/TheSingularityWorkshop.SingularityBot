using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class StrategyEvidenceCollectorTests
{
    [Fact]
    public void CollectsOnlyVisibleEnemyRoachWarrenAsObservedEvidence()
    {
        var collector = new StrategyEvidenceCollector();
        var evidence = collector.Apply(Observation(100,
            Unit(1, "RoachWarren", UnitAlliance.Enemy, UnitVisibility.Visible),
            Unit(2, "RoachWarren", UnitAlliance.Enemy, UnitVisibility.Snapshot),
            Unit(3, "Factory", UnitAlliance.Enemy, UnitVisibility.Visible),
            Unit(4, "RoachWarren", UnitAlliance.Self, UnitVisibility.Visible)));

        var item = Assert.Single(evidence);
        Assert.Equal("enemy.structure.roach-warren", item.EvidenceId);
        Assert.Equal("enemy", item.Subject);
        Assert.Equal(1f, item.Confidence);
        Assert.Equal(100u, item.ObservedAtGameLoop);
        Assert.True(item.IsObservedFact);
    }

    [Fact]
    public void RetainsOriginalObservationTimeWhileOpponentStructureIsFogged()
    {
        var collector = new StrategyEvidenceCollector();
        collector.Apply(Observation(100,
            Unit(1, "RoachWarren", UnitAlliance.Enemy, UnitVisibility.Visible)));

        var evidence = Assert.Single(collector.Apply(Observation(120)));
        Assert.Equal(100u, evidence.ObservedAtGameLoop);
        Assert.True(evidence.IsObservedFact);
    }

    [Fact]
    public void RemovesEvidenceWhenTheObservedUnitDies()
    {
        var collector = new StrategyEvidenceCollector();
        collector.Apply(Observation(100,
            Unit(1, "RoachWarren", UnitAlliance.Enemy, UnitVisibility.Visible)));

        Assert.Empty(collector.Apply(Observation(120, Array.Empty<UnitObservationFacts>(), new HashSet<ulong> { 1 })));
    }

    [Fact]
    public void ResetClearsRetainedEvidence()
    {
        var collector = new StrategyEvidenceCollector();
        collector.Apply(Observation(100,
            Unit(1, "RoachWarren", UnitAlliance.Enemy, UnitVisibility.Visible)));

        collector.Reset();

        Assert.Empty(collector.Apply(Observation(120)));
    }

    private static NormalizedObservation Observation(
        uint gameLoop,
        params UnitObservationFacts[] units) =>
        Observation(gameLoop, units, new HashSet<ulong>());

    private static NormalizedObservation Observation(
        uint gameLoop,
        UnitObservationFacts[] units,
        IReadOnlySet<ulong> deadTags) =>
        new(gameLoop, 1, units, deadTags);

    private static UnitObservationFacts Unit(
        ulong tag, string kind, UnitAlliance alliance, UnitVisibility visibility) =>
        new(tag, 1, kind, 2, alliance, visibility,
            100, 100, 0, 0, 1, 10, 10, true, false, false, false, false, false, 100);
}
