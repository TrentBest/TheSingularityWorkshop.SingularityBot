using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2RaceProductionActionTranslatorTests
{
    [Fact]
    public void ZerglingTranslation_AccountsForPairCostAndEmitsOneCommandPerLarva()
    {
        var profile = Sc2RaceProductionProfileCatalog.CreateDefault().Profiles
            .Single(item => item.ModeId == "zerg.zergling.v1");
        var observation = Observation(
            minerals: 100, vespene: 0, foodUsed: 10, foodCap: 20,
            Unit(1, "Larva"),
            Unit(2, "Larva"),
            Unit(3, "SpawningPool", buildProgress: 1f));

        var result = Sc2RaceProductionActionTranslator.Translate(
            profile,
            new Sc2UnitFacts(105, "Zergling", 1343, 25, 0, 0.5f, 0),
            new Sc2UnitFacts(151, "Larva", 0, 0, 0, 0, 0),
            observation,
            desiredRemainingUnits: 4,
            new HashSet<ulong>());

        Assert.False(result.IsBlocked);
        Assert.Equal(2, result.Actions.Count);
        Assert.Equal(new ulong[] { 1, 2 }, result.ProducerTags);
        Assert.Equal(100u, result.ReservedMinerals);
        Assert.Equal(0u, result.ReservedVespene);
        Assert.Equal(2f, result.ReservedFood);
        Assert.Equal(4, result.ExpectedOutputUnits);
        Assert.All(result.Actions, action =>
        {
            Assert.Equal(1343, action.ActionRaw.UnitCommand.AbilityId);
            Assert.False(action.ActionRaw.UnitCommand.QueueCommand);
        });
    }

    [Fact]
    public void ZerglingTranslation_DoesNotIssueWhenSpawningPoolIsIncomplete()
    {
        var profile = Sc2RaceProductionProfileCatalog.CreateDefault().Profiles
            .Single(item => item.ModeId == "zerg.zergling.v1");
        var observation = Observation(
            100, 0, 10, 20,
            Unit(1, "Larva"),
            Unit(3, "SpawningPool", buildProgress: 0.99f));

        var result = Sc2RaceProductionActionTranslator.Translate(
            profile,
            new Sc2UnitFacts(105, "Zergling", 1343, 25, 0, 0.5f, 0),
            new Sc2UnitFacts(151, "Larva", 0, 0, 0, 0, 0),
            observation,
            2,
            new HashSet<ulong>());

        Assert.True(result.IsBlocked);
        Assert.Contains("completed", result.BlockReason);
    }

    [Fact]
    public void ZealotTranslation_FailsClosedWithoutPositiveGatewayPowerEvidence()
    {
        var profile = Sc2RaceProductionProfileCatalog.CreateDefault().Profiles
            .Single(item => item.ModeId == "protoss.zealot.v1");
        var observation = Observation(
            100, 0, 10, 20,
            Unit(10, "Gateway"),
            Unit(11, "Pylon", buildProgress: 1f));

        var result = Sc2RaceProductionActionTranslator.Translate(
            profile,
            new Sc2UnitFacts(73, "Zealot", 916, 100, 0, 2, 0),
            new Sc2UnitFacts(62, "Gateway", 0, 0, 0, 0, 0),
            observation,
            1,
            new HashSet<ulong>());

        Assert.True(result.IsBlocked);
        Assert.Contains("power", result.BlockReason.ToLowerInvariant());
    }

    [Fact]
    public void Translation_ExcludesBusyDeadAndReservedProducers()
    {
        var profile = Sc2RaceProductionProfileCatalog.CreateDefault().Profiles
            .Single(item => item.ModeId == "zerg.zergling.v1");
        var observation = Observation(
            100, 0, 10, 20,
            new[]
            {
                Unit(1, "Larva"),
                Unit(2, "Larva", hasOrders: true),
                Unit(3, "Larva"),
                Unit(4, "SpawningPool", buildProgress: 1f)
            },
            new HashSet<ulong> { 3 });

        var result = Sc2RaceProductionActionTranslator.Translate(
            profile,
            new Sc2UnitFacts(105, "Zergling", 1343, 25, 0, 0.5f, 0),
            new Sc2UnitFacts(151, "Larva", 0, 0, 0, 0, 0),
            observation,
            4,
            new HashSet<ulong> { 1 });

        Assert.True(result.IsBlocked);
        Assert.Empty(result.ProducerTags);
    }

    private static NormalizedObservation Observation(
        uint minerals,
        uint vespene,
        uint foodUsed,
        uint foodCap,
        params UnitObservationFacts[] units) =>
        Observation(minerals, vespene, foodUsed, foodCap, units, new HashSet<ulong>());

    private static NormalizedObservation Observation(
        uint minerals,
        uint vespene,
        uint foodUsed,
        uint foodCap,
        UnitObservationFacts[] units,
        IReadOnlySet<ulong> dead) =>
        new(100, 1, units, dead)
        {
            Minerals = minerals,
            Vespene = vespene,
            FoodUsed = foodUsed,
            FoodCap = foodCap
        };

    private static UnitObservationFacts Unit(
        ulong tag,
        string kind,
        float buildProgress = 1f,
        bool hasOrders = false) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            100, 100, 0, 0, buildProgress, 10, 10, true,
            false, hasOrders, false, false, false, 100);
}
