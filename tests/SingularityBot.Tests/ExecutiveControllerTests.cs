using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ExecutiveControllerTests
{
    [Fact]
    public void ControlCycle_RequiresTheFourLayersInDeclaredOrder()
    {
        var executive = new ExecutiveController(1234);

        executive.Advance(ExecutiveStage.ExecutiveReaction);
        executive.Advance(ExecutiveStage.SubControllers);
        executive.Advance(ExecutiveStage.ExecutiveReview);
        executive.Advance(ExecutiveStage.UnitAi);

        executive.Complete();
        Assert.Equal(ExecutiveStage.UnitAi, executive.CurrentStage);
    }

    [Theory]
    [InlineData(ExecutiveStage.SubControllers)]
    [InlineData(ExecutiveStage.ExecutiveReview)]
    [InlineData(ExecutiveStage.UnitAi)]
    public void ControlCycle_RejectsSkippingAhead(ExecutiveStage attemptedStage)
    {
        var executive = new ExecutiveController(42);

        Assert.Throws<InvalidOperationException>(() => executive.Advance(attemptedStage));
    }

    [Fact]
    public void ControlCycle_RejectsOutOfOrderAndDuplicateStages()
    {
        var executive = new ExecutiveController(42);
        executive.Advance(ExecutiveStage.ExecutiveReaction);

        Assert.Throws<InvalidOperationException>(() =>
            executive.Advance(ExecutiveStage.ExecutiveReview));
        executive.Advance(ExecutiveStage.SubControllers);
        Assert.Throws<InvalidOperationException>(() =>
            executive.Advance(ExecutiveStage.SubControllers));
    }

    [Fact]
    public void ControlCycle_CannotCompleteBeforeUnitAi()
    {
        var executive = new ExecutiveController(42);
        executive.Advance(ExecutiveStage.ExecutiveReaction);
        executive.Advance(ExecutiveStage.SubControllers);
        executive.Advance(ExecutiveStage.ExecutiveReview);

        Assert.Throws<InvalidOperationException>(() => executive.Complete());
    }
}
