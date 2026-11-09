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
            { "id": "factory", "target": "Factory", "producer": "SCV", "desiredCount": 1, "earliestGameLoop": 0, "priority": 90 },
            { "id": "tanks", "target": "SiegeTank", "producer": "Factory", "desiredCount": 2, "earliestGameLoop": 0, "priority": 90, "dependsOn": ["factory"] }
          ]
        }
        """;

        var plan = ProductionPlanDefinition.Parse(json);

        Assert.Empty(ProductionPlanValidator.Validate(plan));
        Assert.Equal("factory", Assert.Single(plan.Steps[1].DependsOn));
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
