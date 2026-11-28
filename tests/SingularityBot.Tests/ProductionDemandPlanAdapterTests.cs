using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ProductionDemandPlanAdapterTests
{
    private static (Sc2GameDataCatalog GameData, ProductionDemandLedger Ledger) CreateTerranFixture()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData { UnitId = 21, Name = "Barracks", AbilityId = 321, MineralCost = 150 });
        data.Units.Add(new UnitTypeData { UnitId = 48, Name = "Marine", AbilityId = 560, MineralCost = 50, FoodRequired = 1 });
        var ledger = new ProductionDemandLedger();
        Assert.True(ledger.Set(new Sc2UnitFacts(48, "Marine", 560, 50, 0, 1, 0), 10, 85));
        return (Sc2GameDataCatalog.Create(data), ledger);
    }

    [Fact]
    public void Apply_CreatesStableUnitObjectiveAndUpdatesItWithoutReplacingContext()
    {
        var (gameData, ledger) = CreateTerranFixture();
        var adapter = new ProductionDemandPlanAdapter();
        var emitted = new List<ProductionIntent>();
        using var runtime = new ProductionPlanRuntime(emitted.Add);
        var profiles = Sc2RaceProductionProfileCatalog.CreateDefault();

        var first = adapter.Apply(ledger, runtime, gameData, profiles, Race.Terran);
        Assert.Equal(1, first.CreatedObjectives);
        Assert.Empty(first.Rejections);
        var stepId = ProductionDemandPlanAdapter.GetStepId(48);
        Assert.True(runtime.TryGetStep(stepId, out var original));
        Assert.Equal("Marine", original.Definition.Target);
        Assert.Equal("Barracks", original.Definition.Producer);
        Assert.Equal(10, original.EffectiveDesiredCount);
        Assert.Equal(ProductionLane.Units, original.Definition.Lane);

        Assert.True(ledger.Set(new Sc2UnitFacts(48, "Marine", 560, 50, 0, 1, 0), 18, 90));
        var second = adapter.Apply(ledger, runtime, gameData, profiles, Race.Terran);
        Assert.Equal(0, second.CreatedObjectives);
        Assert.Equal(1, second.UpdatedObjectives);
        Assert.True(runtime.TryGetStep(stepId, out var updated));
        Assert.Same(original, updated);
        Assert.Equal(18, updated.EffectiveDesiredCount);
        Assert.Equal(90, updated.EffectivePriority);
    }

    [Fact]
    public void Apply_ZeroDemandDisablesExistingObjectiveAndClearDisablesStaleObjective()
    {
        var (gameData, ledger) = CreateTerranFixture();
        var adapter = new ProductionDemandPlanAdapter();
        using var runtime = new ProductionPlanRuntime(_ => { });
        var profiles = Sc2RaceProductionProfileCatalog.CreateDefault();
        adapter.Apply(ledger, runtime, gameData, profiles, Race.Terran);
        var stepId = ProductionDemandPlanAdapter.GetStepId(48);
        Assert.True(runtime.TryGetStep(stepId, out var step));

        Assert.True(ledger.Set(new Sc2UnitFacts(48, "Marine", 560, 50, 0, 1, 0), 0, 85));
        adapter.Apply(ledger, runtime, gameData, profiles, Race.Terran);
        Assert.Equal(0, step.EffectiveDesiredCount);
        Assert.False(step.CanIssue);

        Assert.True(ledger.Set(new Sc2UnitFacts(48, "Marine", 560, 50, 0, 1, 0), 5, 85));
        adapter.Apply(ledger, runtime, gameData, profiles, Race.Terran);
        Assert.Equal(5, step.EffectiveDesiredCount);

        ledger.Clear();
        adapter.Apply(ledger, runtime, gameData, profiles, Race.Terran);
        Assert.Equal(0, step.EffectiveDesiredCount);
    }

    [Fact]
    public void Apply_ZeroDemandStillRejectsConflictingProducerDefinition()
    {
        var (gameData, ledger) = CreateTerranFixture();
        Assert.True(ledger.Set(new Sc2UnitFacts(48, "Marine", 560, 50, 0, 1, 0), 0, 85));
        var adapter = new ProductionDemandPlanAdapter();
        using var runtime = new ProductionPlanRuntime(_ => { });
        var stepId = ProductionDemandPlanAdapter.GetStepId(48);
        var conflicting = runtime.AddStep(new ProductionStepDefinition(
            stepId, "Marine", "Factory", 10, 0, 50, Lane: ProductionLane.Units));

        var result = adapter.Apply(ledger, runtime, gameData,
            Sc2RaceProductionProfileCatalog.CreateDefault(), Race.Terran);

        Assert.Single(result.Rejections);
        Assert.Contains("conflicts with the resolved target, producer, or lane", result.Rejections[0]);
        Assert.Equal(10, conflicting.EffectiveDesiredCount);
    }

    [Fact]
    public void Apply_RejectsUnknownRaceAndDoesNotCreateBuildingObjectives()
    {
        var (gameData, ledger) = CreateTerranFixture();
        var adapter = new ProductionDemandPlanAdapter();
        using var runtime = new ProductionPlanRuntime(_ => { });
        var profiles = Sc2RaceProductionProfileCatalog.CreateDefault();

        var wrongRace = adapter.Apply(ledger, runtime, gameData, profiles, Race.Zerg);
        Assert.Empty(runtime.Steps);
        Assert.Single(wrongRace.Rejections);

        Assert.True(ledger.Set(new Sc2UnitFacts(21, "Barracks", 321, 150, 0, 0, 0), 2));
        var building = adapter.Apply(ledger, runtime, gameData, profiles, Race.Terran);
        Assert.Empty(runtime.Steps);
        Assert.Contains(building.Rejections, reason => reason.Contains("production profiles"));
    }

    [Fact]
    public void Apply_RejectsDemandWhoseNameDoesNotMatchAuthoritativeUnitId()
    {
        var (gameData, ledger) = CreateTerranFixture();
        Assert.True(ledger.Set(new Sc2UnitFacts(48, "Marauder", 560, 50, 0, 1, 0), 4));
        var adapter = new ProductionDemandPlanAdapter();
        using var runtime = new ProductionPlanRuntime(_ => { });

        var result = adapter.Apply(ledger, runtime, gameData,
            Sc2RaceProductionProfileCatalog.CreateDefault(), Race.Terran);

        Assert.Empty(runtime.Steps);
        Assert.Contains(result.Rejections, reason => reason.Contains("does not resolve"));
    }
}
