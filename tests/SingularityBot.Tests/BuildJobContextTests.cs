using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BuildJobContextTests
{
    [Fact]
    public void ConstructionJobTransitionsFromAssignedToExecutingOnObservedResult()
    {
        var runtime = new BuildJobRuntime();
        var job = runtime.Create("Barracks", 100, workerTag: 11);

        Assert.True(job.IsValid);
        Assert.True(job.IsAssigned);
        Assert.Equal("Queued", job.CurrentState);
        Assert.Equal(11UL, job.WorkerTag);

        runtime.Update();
        Assert.Equal("Assigned", job.CurrentState);

        job.ObserveResult(9001, 0.42f, 140);
        Assert.Equal(9001UL, job.ResultUnitTag);
        Assert.Equal(42f, job.Progress);
        Assert.False(job.IsExecuting);

        runtime.Update();
        Assert.Equal("Executing", job.CurrentState);
        Assert.True(job.IsExecuting);
        Assert.False(job.IsCompleted);
        Assert.Equal(140u, job.LastObservedGameLoop);

        runtime.Clear();
    }

    [Fact]
    public void ObservedCompletionTransitionsTheBuildJobFsmToCompleted()
    {
        var runtime = new BuildJobRuntime();
        var job = runtime.Create("Barracks", 100, workerTag: 12);

        job.ObserveResult(9002, 1f, 200);
        runtime.Update();

        Assert.Equal("Completed", job.CurrentState);
        Assert.Equal(100f, job.Progress);
        Assert.True(job.IsCompleted);
        Assert.False(job.IsExecuting);
        Assert.False(job.IsFailed);

        runtime.Clear();
    }

    [Fact]
    public void ProductionJobTransitionsToFailedFromObservedFailureEvidence()
    {
        var runtime = new BuildJobRuntime();
        var job = runtime.Create("Marine", 300, producerTag: 700);

        runtime.Update();
        Assert.Equal("Assigned", job.CurrentState);

        job.MarkStarted(304);
        runtime.Update();
        Assert.Equal("Executing", job.CurrentState);

        job.Fail("Producer destroyed", 310);
        Assert.False(job.IsFailed);
        runtime.Update();

        Assert.Equal("Failed", job.CurrentState);
        Assert.True(job.IsFailed);
        Assert.False(job.IsExecuting);
        Assert.Equal("Producer destroyed", job.FailureReason);
        Assert.Equal(310u, job.LastObservedGameLoop);

        runtime.Clear();
    }

    [Fact]
    public void MarineProductionCompletesOnlyWhenResultUnitIsObserved()
    {
        var runtime = new BuildJobRuntime();
        var job = runtime.Create("Marine", 500, producerTag: 701);

        job.MarkStarted(510);
        runtime.Update();
        Assert.Equal("Executing", job.CurrentState);
        Assert.False(job.IsCompleted);

        job.ObserveResult(9101, 1f, 900);
        runtime.Update();

        Assert.Equal(9101UL, job.ResultUnitTag);
        Assert.Equal(100f, job.Progress);
        Assert.True(job.IsCompleted);
        Assert.False(job.IsExecuting);

        runtime.Clear();
    }

    [Fact]
    public void ResultIdentityCannotChangeAfterAssociation()
    {
        var runtime = new BuildJobRuntime();
        var job = runtime.Create("Barracks", 400, workerTag: 13);

        job.ObserveResult(9004, 0.25f, 410);
        runtime.Update();
        job.ObserveResult(9005, 0.9f, 420);

        Assert.Equal(9004UL, job.ResultUnitTag);
        Assert.Equal(25f, job.Progress);

        runtime.Clear();
    }
}
