using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ProductionPlanObservationAdapterTests
{
    [Fact]
    public void ObservationRulesProvideEligibilityWithoutProductionRecipeDurations()
    {
        var emitted = new List<ProductionIntent>();
        using var runtime = new ProductionPlanRuntime(emitted.Add);
        var step = runtime.AddStep(new ProductionStepDefinition(
            "marine-core", "Marine", "Barracks", 6, 0, 80));
        var observation = CreateObservation(
            minerals: 100,
            foodCap: 10,
            foodUsed: 1,
            barracksProgress: 1f,
            marineCount: 1);
        var gameData = CreateGameData();
        var rules = new Dictionary<string, ProductionObservationRule>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Marine"] = new(
                "Marine", "Barracks", new[] { "Barracks" })
        };

        new ProductionPlanObservationAdapter().Apply(
            runtime,
            observation,
            gameData,
            new BuildJobRuntime(),
            new HashSet<ulong>(),
            rules);

        Assert.Equal(1, step.CurrentCount);
        Assert.True(step.ProducerAvailable);
        Assert.True(step.PrerequisitesSatisfied);
        Assert.True(step.ResourceRequirementsSatisfied);
        Assert.True(step.CanIssue);
    }

    [Fact]
    public void ObservationRulesFailClosedWhenPrerequisiteIsNotComplete()
    {
        using var runtime = new ProductionPlanRuntime(_ => { });
        var step = runtime.AddStep(new ProductionStepDefinition(
            "marine-core", "Marine", "Barracks", 6, 0, 80));
        var observation = CreateObservation(
            minerals: 100,
            foodCap: 10,
            foodUsed: 1,
            barracksProgress: 0.5f,
            marineCount: 0);
        var rules = new Dictionary<string, ProductionObservationRule>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Marine"] = new(
                "Marine", "Barracks", new[] { "Barracks" })
        };

        new ProductionPlanObservationAdapter().Apply(
            runtime,
            observation,
            CreateGameData(),
            new BuildJobRuntime(),
            new HashSet<ulong>(),
            rules);

        Assert.True(step.ProducerAvailable);
        Assert.False(step.PrerequisitesSatisfied);
        Assert.False(step.CanIssue);
    }

    private static NormalizedObservation CreateObservation(
        uint minerals,
        uint foodCap,
        uint foodUsed,
        float barracksProgress,
        int marineCount)
    {
        var units = new List<UnitObservationFacts>
        {
            new(
                100, 21, "Barracks", 1, UnitAlliance.Self, UnitVisibility.Visible,
                100, 100, 0, 0, barracksProgress, 10, 10, true, false, false,
                false, false, false, 10)
        };
        for (var index = 0; index < marineCount; index++)
        {
            units.Add(new UnitObservationFacts(
                (ulong)(200 + index), 48, "Marine", 1, UnitAlliance.Self,
                UnitVisibility.Visible, 45, 45, 0, 0, 1, 11 + index, 10,
                true, false, false, false, false, false, 10));
        }

        return new NormalizedObservation(10, 1, units, new HashSet<ulong>())
        {
            Minerals = minerals,
            FoodCap = foodCap,
            FoodUsed = foodUsed
        };
    }

    private static Sc2GameDataCatalog CreateGameData()
    {
        var data = new SC2APIProtocol.ResponseData();
        data.Units.Add(new SC2APIProtocol.UnitTypeData
        {
            UnitId = 21, Name = "Barracks", AbilityId = 321, MineralCost = 150
        });
        data.Units.Add(new SC2APIProtocol.UnitTypeData
        {
            UnitId = 48, Name = "Marine", AbilityId = 560, MineralCost = 50, FoodRequired = 1
        });
        return Sc2GameDataCatalog.Create(data);
    }
}
