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
}
