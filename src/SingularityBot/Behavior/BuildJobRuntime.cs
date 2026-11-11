using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>One independently tracked unit or structure production job.</summary>
public sealed class BuildJobContext : IStateContext
{
    public BuildJobContext(long id, string buildableKind, ulong? workerTag, ulong? producerTag, uint createdAtGameLoop)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (string.IsNullOrWhiteSpace(buildableKind)) throw new ArgumentException("A buildable kind is required.", nameof(buildableKind));
        Id = id;
        BuildableKind = buildableKind;
        WorkerTag = workerTag;
        ProducerTag = producerTag;
        CreatedAtGameLoop = createdAtGameLoop;
        LastObservedGameLoop = createdAtGameLoop;
        IsValid = true;
    }

    public long Id { get; }
    public string Name => $"BuildJob:{Id}:{BuildableKind}";
    public bool IsValid { get; set; }
    public string BuildableKind { get; }
    public ulong? WorkerTag { get; private set; }
    public ulong? ProducerTag { get; private set; }
    public ulong? ResultUnitTag { get; private set; }
    public uint CreatedAtGameLoop { get; }
    public uint LastObservedGameLoop { get; private set; }
    public float Progress { get; private set; }
    public bool IsAssigned => WorkerTag.HasValue || ProducerTag.HasValue;
    public bool IsExecuting { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }
    public string FailureReason { get; private set; }

    public void AssignWorker(ulong workerTag)
    {
        if (workerTag == 0) throw new ArgumentOutOfRangeException(nameof(workerTag));
        if (IsCompleted || IsFailed) return;
        WorkerTag = workerTag;
    }

    public void AssignProducer(ulong producerTag)
    {
        if (producerTag == 0) throw new ArgumentOutOfRangeException(nameof(producerTag));
        if (IsCompleted || IsFailed) return;
        ProducerTag = producerTag;
    }

    public void ObserveResult(ulong resultUnitTag, float progress, uint gameLoop)
    {
        if (resultUnitTag == 0) throw new ArgumentOutOfRangeException(nameof(resultUnitTag));
        if (IsCompleted || IsFailed) return;
        ResultUnitTag ??= resultUnitTag;
        if (ResultUnitTag != resultUnitTag) return;
        LastObservedGameLoop = gameLoop;
        Progress = Math.Clamp(float.IsFinite(progress) ? progress * 100f : Progress, 0f, 100f);
        IsExecuting = true;
        if (Progress >= 100f) Complete(gameLoop);
    }

    public void MarkStarted(uint gameLoop)
    {
        if (IsCompleted || IsFailed) return;
        IsExecuting = true;
        LastObservedGameLoop = gameLoop;
    }

    public void Complete(uint gameLoop)
    {
        if (IsFailed) return;
        Progress = 100f;
        IsExecuting = false;
        IsCompleted = true;
        LastObservedGameLoop = gameLoop;
    }

    public void Fail(string reason, uint gameLoop)
    {
        if (IsCompleted || IsFailed) return;
        IsFailed = true;
        IsExecuting = false;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unspecified failure" : reason;
        LastObservedGameLoop = gameLoop;
    }
}

/// <summary>Owns one FSM handle per outstanding build job and reconciles jobs against observations.</summary>
public sealed class BuildJobRuntime
{
    public const string FsmName = "SingularityBot.BuildJob";
    private readonly Dictionary<long, BuildJobContext> _jobs = new();
    private readonly Dictionary<long, FSMHandle> _handles = new();
    private long _nextId;
    private static bool _registered;

    public IReadOnlyCollection<BuildJobContext> Jobs => _jobs.Values.ToArray();
    public int ActiveCount => _jobs.Values.Count(job => !job.IsCompleted && !job.IsFailed);

    public static void RegisterDefinition()
    {
        if (_registered) return;
        FSM_API.Create.CreateFiniteStateMachine(FsmName, processRate: 1, processingGroup: UnitBehaviorCatalog.ProductionProcessingGroup)
            .State("Queued", null, null, null)
            .State("Assigned", null, null, null)
            .State("Executing", null, null, null)
            .State("Completed", null, null, null)
            .State("Failed", null, null, null)
            .Transition("Queued", "Assigned", ctx => ctx is BuildJobContext job && job.IsAssigned)
            .Transition("Queued", "Executing", ctx => ctx is BuildJobContext job && job.IsExecuting)
            .Transition("Assigned", "Executing", ctx => ctx is BuildJobContext job && job.IsExecuting)
            .Transition("Assigned", "Failed", ctx => ctx is BuildJobContext job && job.IsFailed)
            .Transition("Queued", "Failed", ctx => ctx is BuildJobContext job && job.IsFailed)
            .Transition("Executing", "Completed", ctx => ctx is BuildJobContext job && job.IsCompleted)
            .Transition("Assigned", "Completed", ctx => ctx is BuildJobContext job && job.IsCompleted)
            .Transition("Queued", "Completed", ctx => ctx is BuildJobContext job && job.IsCompleted)
            .Transition("Executing", "Failed", ctx => ctx is BuildJobContext job && job.IsFailed)
            .WithInitialState("Queued").BuildDefinition();
        _registered = true;
    }

    public BuildJobContext Create(string buildableKind, uint gameLoop, ulong? workerTag = null, ulong? producerTag = null)
    {
        if (!_registered) throw new InvalidOperationException("Build-job FSM must be registered before creating jobs.");
        var job = new BuildJobContext(++_nextId, buildableKind, workerTag, producerTag, gameLoop);
        _jobs.Add(job.Id, job);
        _handles.Add(job.Id, FSM_API.Create.CreateInstance(FsmName, job, UnitBehaviorCatalog.ProductionProcessingGroup));
        return job;
    }

    public bool TryGet(long id, out BuildJobContext job) => _jobs.TryGetValue(id, out job);

    public bool Retire(long id)
    {
        if (!_jobs.Remove(id, out var job)) return false;
        job.IsValid = false;
        if (_handles.Remove(id, out var handle)) FSM_API.Interaction.DestroyInstance(handle);
        return true;
    }

    public void RetireTerminalJobs()
    {
        foreach (var id in _jobs.Values.Where(job => job.IsCompleted || job.IsFailed).Select(job => job.Id).ToArray())
            Retire(id);
    }

    public void Clear()
    {
        foreach (var id in _jobs.Keys.ToArray()) Retire(id);
    }
}
