using SingularityBot.Behavior;
using SingularityBot.Observation;
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
    public void GenericStructureJobAssociatesOnlyNewObservedStructureAndTracksProgress()
    {
        var runtime = new BuildJobRuntime();
        var job = runtime.CreateStructureConstruction("Factory", 100, 11, new ulong[] { 9000 });
        var observation = new NormalizedObservation(
            120, 1,
            new[]
            {
                Unit(11, "SCV", 10, 10),
                Unit(9000, "Factory", 9, 10, 1f),
                Unit(9001, "Factory", 12, 10, 0.42f)
            },
            new HashSet<ulong>());

        runtime.ReconcileStructureConstruction(observation, "Factory");

        Assert.Equal(9001UL, job.ResultUnitTag);
        Assert.Equal(42f, job.Progress);
        Assert.True(job.HasExecutionEvidence);

        runtime.Update();
        Assert.Equal("Executing", job.CurrentState);

        runtime.ReconcileStructureConstruction(new NormalizedObservation(
            180, 1,
            new[]
            {
                Unit(11, "SCV", 10, 10),
                Unit(9001, "Factory", 12, 10, 1f)
            },
            new HashSet<ulong>()), "Factory");
        runtime.Update();

        Assert.True(job.IsCompleted);
        Assert.Equal("Completed", job.CurrentState);
        runtime.Clear();
    }

    [Fact]
    public void GenericUnitProductionReconcilesObservedResultForNonMarineUnit()
    {
        var runtime = new BuildJobRuntime();
        var job = runtime.CreateUnitProduction("Marauder", 200, 700);
        var producer = Unit(700, "Barracks", 10, 10) with
        {
            Orders = new[] { new UnitOrderObservationFacts(123, null, null, null) }
        };
        var marauder = Unit(9002, "Marauder", 12, 10, 1f);

        runtime.ReconcileUnitProduction(
            new NormalizedObservation(240, 1, new[] { producer, marauder }, new HashSet<ulong>()),
            "Marauder",
            123);
        runtime.Update();

        Assert.Equal(9002UL, job.ResultUnitTag);
        Assert.True(job.HasExecutionEvidence);
        Assert.True(job.IsCompleted);
        Assert.Equal("Completed", job.CurrentState);
        runtime.Clear();
    }

    private static UnitObservationFacts Unit(
        ulong tag,
        string kind,
        float x,
        float y,
        float buildProgress = 1f) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            100, 100, 0, 0, buildProgress, x, y,
            true, false, false, false, false, false, 100);

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
