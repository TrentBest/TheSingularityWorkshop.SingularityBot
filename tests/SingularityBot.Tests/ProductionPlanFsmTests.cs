using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ProductionPlanFsmTests
{
    [Fact]
    public void ProductionStep_EmitsOnlyWhenAllCallerOwnedGatesAreSatisfied()
    {
        var emitted = new List<ProductionIntent>();
        using var runtime = new ProductionPlanRuntime(emitted.Add);
        var step = runtime.AddStep(new ProductionStepDefinition(
            "marine-rush", "Marine", "Barracks", 6, 0, 90));

        step.ApplyObservation(100, 0, 0, true, true, false);
        runtime.Update();
        runtime.Update();
        Assert.Empty(emitted);

        step.ApplyObservation(101, 0, 0, true, true, true);
        runtime.Update();
        runtime.Update();

        var intent = Assert.Single(emitted);
        Assert.Equal("marine-rush", intent.StepId);
        Assert.Equal("Marine", intent.Target);
        Assert.Equal("Barracks", intent.Producer);
        Assert.Equal(90, intent.Priority);
    }

    [Fact]
    public void ProductionStep_DoesNotEmitAgainWhileSubmissionIsPending()
    {
        var emitted = new List<ProductionIntent>();
        using var runtime = new ProductionPlanRuntime(emitted.Add);
        var step = runtime.AddStep(new ProductionStepDefinition(
            "worker", "SCV", "CommandCenter", 13, 0, 100));

        step.ApplyObservation(10, 12, 0, true, true, true);
        runtime.Update();
        runtime.Update();
        Assert.Single(emitted);

        step.MarkSubmitted();
        step.ApplyObservation(11, 12, 0, true, true, true);
        runtime.Update();
        runtime.Update();
        Assert.Single(emitted);
    }


    [Fact]
    public void ProductionPlanRuntime_BlocksDependentStepsUntilPrerequisiteIsObservedComplete()
    {
        var emitted = new List<ProductionIntent>();
        using var runtime = new ProductionPlanRuntime(emitted.Add);
        var plan = new ProductionPlanDefinition
        {
            Id = "opening",
            Name = "Opening",
            Race = "Terran",
            Steps =
            [
                new("first-barracks", "Barracks", "SCV", 1, 0, 90, Lane: ProductionLane.Buildings),
                new("orbital-command", "OrbitalCommand", "CommandCenter", 1, 0, 90, new[] { "first-barracks" }, ProductionLane.Units)
            ]
        };

        Assert.Empty(runtime.AddPlan(plan));
        Assert.True(runtime.TryGetStep("first-barracks", out var barracks));
        Assert.True(runtime.TryGetStep("orbital-command", out var orbital));

        barracks.ApplyObservation(100, 0, 0, true, true, true);
        orbital.ApplyObservation(100, 0, 0, true, true, true);

        Assert.True(barracks.CanIssue);
        Assert.False(orbital.DependenciesSatisfied);
        Assert.False(orbital.CanIssue);

        // A completed structure observed in a later game loop releases the barrier.
        barracks.ApplyObservation(120, 1, 0, true, true, true);

        Assert.True(orbital.DependenciesSatisfied);
        Assert.True(orbital.CanIssue);
    }

    [Fact]
    public void ProductionPlanBuilder_SeparatesLanesAndCarriesCompletionBarrierForward()
    {
        var orbitalCommand = new ProductionStepDefinition(
            "unused-id", "OrbitalCommand", "CommandCenter", 1, 0, 80);

        var plan = new ProductionPlanBuilder("terran-opening", "Terran Opening")
            .Buildings.Add("FirstBarracks", "Barracks", "SCV")
            .WhenComplete("OrbitalCommand", "FirstBarracks", orbitalCommand)
            .Units.AddUntil("MarineWave", "Marine", "Barracks", 40)
            .Build();

        Assert.Equal(ProductionLane.Buildings, plan.Steps[0].Lane);
        Assert.Equal("OrbitalCommand", plan.Steps[1].Id);
        Assert.Contains("FirstBarracks", plan.Steps[1].DependsOn);
        Assert.Contains("FirstBarracks", plan.Steps[2].DependsOn);
        Assert.Equal(40, plan.Steps[2].DesiredCount);
        Assert.Empty(ProductionPlanValidator.Validate(plan));
    }

    [Fact]
    public void ProductionPlanRuntime_DoesNotReleaseBarrierForSubmittedOrInProgressPrerequisite()
    {
        var emitted = new List<ProductionIntent>();
        using var runtime = new ProductionPlanRuntime(emitted.Add);
        var plan = new ProductionPlanDefinition
        {
            Id = "barrier",
            Name = "Barrier",
            Steps =
            [
                new("depot", "SupplyDepot", "SCV", 1, 0, 90, Lane: ProductionLane.Buildings),
                new("barracks", "Barracks", "SCV", 1, 0, 90, new[] { "depot" }, ProductionLane.Buildings)
            ]
        };
        Assert.Empty(runtime.AddPlan(plan));
        runtime.TryGetStep("depot", out var depot);
        runtime.TryGetStep("barracks", out var barracks);

        depot.ApplyObservation(10, 0, 0, true, true, true);
        barracks.ApplyObservation(10, 0, 0, true, true, true);

        Assert.True(depot.CanIssue);
        Assert.False(barracks.CanIssue);
        depot.MarkSubmitted();
        depot.ApplyObservation(11, 0, 1, true, true, true);

        Assert.False(depot.IsComplete);
        Assert.False(barracks.DependenciesSatisfied);
        Assert.False(barracks.CanIssue);
    }

    [Fact]
    public void ProductionPlanValidator_RejectsDuplicateStepIdsAndInvalidCounts()
    {
        var plan = new ProductionPlanDefinition
        {
            Id = "cheese",
            Name = "Cheese Attack",
            Race = "Terran",
            Steps =
            [
                new("opening", "Marine", "Barracks", 6, 0, 90),
                new("opening", "Marauder", "Barracks", 0, 0, 101)
            ]
        };

        var errors = ProductionPlanValidator.Validate(plan);

        Assert.Contains(errors, error => error.Contains("duplicate step id"));
        Assert.Contains(errors, error => error.Contains("desiredCount"));
        Assert.Contains(errors, error => error.Contains("priority"));
    }

    [Fact]
    public void ProductionPlanParser_ReadsCamelCaseAndDependencyEdges()
    {
        const string json = """
        {
          "schemaVersion": "0.1",
          "id": "slow-push",
          "name": "Slow Push",
          "race": "Terran",
          "steps": [
            { "id": "factory", "target": "Factory", "producer": "SCV", "desiredCount": 1, "earliestGameLoop": 0, "priority": 90, "lane": "buildings" },
            { "id": "tanks", "target": "SiegeTank", "producer": "Factory", "desiredCount": 2, "earliestGameLoop": 0, "priority": 90, "dependsOn": ["factory"] }
          ]
        }
        """;

        var plan = ProductionPlanDefinition.Parse(json);

        Assert.Empty(ProductionPlanValidator.Validate(plan));
        Assert.Equal("factory", Assert.Single(plan.Steps[1].DependsOn));
        Assert.Equal(ProductionLane.Buildings, plan.Steps[0].Lane);
        Assert.Equal(ProductionLane.Units, plan.Steps[1].Lane);
    }

    [Fact]
    public void ProductionPlanValidator_RejectsDependencyCycles()
    {
        var plan = new ProductionPlanDefinition
        {
            Id = "cycle",
            Name = "Cycle",
            Race = "Terran",
            Steps =
            [
                new("a", "Factory", "SCV", 1, 0, 50, new[] { "b" }),
                new("b", "Starport", "SCV", 1, 0, 50, new[] { "a" })
            ]
        };

        Assert.Contains(ProductionPlanValidator.Validate(plan), error => error.Contains("cycle"));
    }
}
