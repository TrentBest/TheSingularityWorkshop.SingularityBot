using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ProductionIntentBufferTests
{
    [Fact]
    public void Enqueue_IsIdempotentAndRejectsConflictingIntentForSameObjective()
    {
        var buffer = new ProductionIntentBuffer();
        var intent = new ProductionIntent("marine-core", "Marine", "Barracks", 80);

        Assert.True(buffer.Enqueue(intent));
        Assert.True(buffer.Enqueue(intent));
        Assert.False(buffer.Enqueue(intent with { Priority = 90 }));
        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void TryPeek_OrdersByPriorityThenStableObjectiveId()
    {
        var buffer = new ProductionIntentBuffer();
        buffer.Enqueue(new ProductionIntent("z-step", "Marine", "Barracks", 80));
        buffer.Enqueue(new ProductionIntent("b-step", "Marine", "Barracks", 90));
        buffer.Enqueue(new ProductionIntent("a-step", "Marine", "Barracks", 90));

        Assert.True(buffer.TryPeek(out var first));
        Assert.Equal("a-step", first.StepId);
        Assert.True(buffer.MarkSubmitted(first.StepId));

        Assert.True(buffer.TryPeek(out var second));
        Assert.Equal("b-step", second.StepId);
    }

    [Fact]
    public void ControllerDecline_LeavesIntentReadyForRetry()
    {
        var buffer = new ProductionIntentBuffer();
        buffer.Enqueue(new ProductionIntent("marine-core", "Marine", "Barracks", 80));

        // A controller returning no action does not call MarkSubmitted.
        Assert.True(buffer.TryPeek(out var first));
        Assert.Equal("marine-core", first.StepId);
        Assert.Equal(1, buffer.ReadyCount);

        Assert.True(buffer.TryPeek(out var retry));
        Assert.Equal(first, retry);
    }

    [Fact]
    public void SubmittedIntent_IsNotOfferedAgainUntilResolved()
    {
        var buffer = new ProductionIntentBuffer();
        buffer.Enqueue(new ProductionIntent("marine-core", "Marine", "Barracks", 80));

        Assert.True(buffer.MarkSubmitted("marine-core"));
        Assert.False(buffer.MarkSubmitted("marine-core"));
        Assert.False(buffer.TryPeek(out _));
        Assert.Equal(1, buffer.Count);

        Assert.True(buffer.Resolve("marine-core"));
        Assert.False(buffer.Contains("marine-core"));
    }
}
