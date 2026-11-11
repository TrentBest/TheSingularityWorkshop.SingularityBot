using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BuildJobContextTests
{
    [Fact]
    public void ConstructionJobTracksWorkerResultAndNormalizedProgress()
    {
        var job = new BuildJobContext(1, "Barracks", 11, null, 100);

        Assert.True(job.IsValid);
        Assert.True(job.IsAssigned);
        Assert.Equal(11UL, job.WorkerTag);
        Assert.Null(job.ResultUnitTag);
        Assert.Equal(0f, job.Progress);

        job.ObserveResult(9001, 0.42f, 140);

        Assert.Equal(9001UL, job.ResultUnitTag);
        Assert.Equal(42f, job.Progress);
        Assert.True(job.IsExecuting);
        Assert.False(job.IsCompleted);
        Assert.Equal(140u, job.LastObservedGameLoop);
    }

    [Fact]
    public void ObservedCompletionSetsProgressToOneHundred()
    {
        var job = new BuildJobContext(2, "Barracks", 12, null, 100);

        job.ObserveResult(9002, 1f, 200);

        Assert.Equal(100f, job.Progress);
        Assert.True(job.IsCompleted);
        Assert.False(job.IsExecuting);
        Assert.False(job.IsFailed);
    }

    [Fact]
    public void ProductionJobTracksProducerAndFailureReason()
    {
        var job = new BuildJobContext(3, "Marine", null, 700, 300);

        Assert.True(job.IsAssigned);
        Assert.Equal(700UL, job.ProducerTag);
        job.MarkStarted(304);
        job.Fail("Producer destroyed", 310);

        Assert.True(job.IsFailed);
        Assert.False(job.IsExecuting);
        Assert.Equal("Producer destroyed", job.FailureReason);
        Assert.Equal(310u, job.LastObservedGameLoop);
    }

    [Fact]
    public void ResultIdentityCannotChangeAfterAssociation()
    {
        var job = new BuildJobContext(4, "Barracks", 13, null, 400);

        job.ObserveResult(9004, 0.25f, 410);
        job.ObserveResult(9005, 0.9f, 420);

        Assert.Equal(9004UL, job.ResultUnitTag);
        Assert.Equal(25f, job.Progress);
    }
}
