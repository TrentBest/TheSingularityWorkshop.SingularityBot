using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2RaceProductionProgressReconcilerTests
{
    [Fact]
    public void ZerglingEggCountsAsTwoInProgressUnits()
    {
        var profile = ZerglingProfile();
        var target = new Sc2UnitFacts(105, "Zergling", 1343, 25, 0, 0.5f, 0);
        var observation = Observation(
            Unit(1, "Zergling", 1f),
            Unit(2, "Zergling", 0.4f),
            Egg(3, 1343));

        var progress = Sc2RaceProductionProgressReconciler.Reconcile(profile, target, observation);

        Assert.Equal(1, progress.CompletedUnits);
        Assert.Equal(3, progress.InProgressUnits);
        Assert.Equal(1, progress.InProgressOrders);
        Assert.Equal(4, progress.AccountedUnits);
    }

    [Fact]
    public void Reconciler_IgnoresEggsWithoutMatchingOrderAndNonSelfOrDeadUnits()
    {
        var profile = ZerglingProfile();
        var target = new Sc2UnitFacts(105, "Zergling", 1343, 25, 0, 0.5f, 0);
        var observation = new NormalizedObservation(
            100,
            1,
            new[]
            {
                Egg(1, 1343),
                Egg(2, 999),
                Egg(3, 1343) with { Alliance = UnitAlliance.Enemy },
                Egg(4, 1343)
            },
            new HashSet<ulong> { 4 });

        var progress = Sc2RaceProductionProgressReconciler.Reconcile(profile, target, observation);

        Assert.Equal(0, progress.CompletedUnits);
        Assert.Equal(2, progress.InProgressUnits);
        Assert.Equal(1, progress.InProgressOrders);
    }

    [Fact]
    public void Reconciler_RejectsMismatchedTargetGameData()
    {
        var profile = ZerglingProfile();
        var target = new Sc2UnitFacts(73, "Zealot", 916, 100, 0, 2, 0);

        Assert.Throws<ArgumentException>(() =>
            Sc2RaceProductionProgressReconciler.Reconcile(
                profile, target, Observation()));
    }

    private static Sc2RaceProductionProfile ZerglingProfile() =>
        Sc2RaceProductionProfileCatalog.CreateDefault().Profiles
            .Single(profile => profile.ModeId == "zerg.zergling.v1");

    private static NormalizedObservation Observation(params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Egg(ulong tag, uint abilityId) =>
        Unit(tag, "Egg") with
        {
            HasOrders = true,
            Orders = new[] { new UnitOrderObservationFacts(abilityId, null, null, null) }
        };

    private static UnitObservationFacts Unit(ulong tag, string kind, float buildProgress = 1f) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            100, 100, 0, 0, buildProgress, 10, 10, true,
            false, false, false, false, false, 100);
}
