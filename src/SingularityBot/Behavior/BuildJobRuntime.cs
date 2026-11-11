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
        Name = $"BuildJob:{id}:{buildableKind}";
        WorkerTag = workerTag;
        ProducerTag = producerTag;
        CreatedAtGameLoop = createdAtGameLoop;
        LastObservedGameLoop = createdAtGameLoop;
        IsValid = true;
    }

    public long Id { get; }
    public string Name { get; set; }
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
    private readonly Dictionary<long, HashSet<ulong>> _excludedResultTags = new();
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

    public BuildJobContext CreateBarracksConstruction(uint gameLoop, ulong workerTag, IEnumerable<ulong> existingBarracksTags)
    {
        var job = Create("Barracks", gameLoop, workerTag: workerTag);
        _excludedResultTags[job.Id] = existingBarracksTags?.ToHashSet() ?? new HashSet<ulong>();
        return job;
    }

    /// <summary>Associates observed Barracks with their reserved SCV and tracks authoritative build progress.</summary>
    public void ReconcileBarracks(global::SingularityBot.Observation.NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var structures = observation.Units.Where(unit =>
            unit.Alliance == SingularityBot.Observation.UnitAlliance.Self && unit.IsLiveVisible &&
            unit.HasPosition && unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
            !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();

        foreach (var job in _jobs.Values.Where(job => job.BuildableKind == "Barracks" &&
                     !job.IsCompleted && !job.IsFailed && job.WorkerTag.HasValue).ToArray())
        {
            if (job.ResultUnitTag is ulong resultTag)
            {
                var existing = structures.FirstOrDefault(unit => unit.Tag == resultTag);
                if (existing is not null)
                    job.ObserveResult(existing.Tag, existing.BuildProgress, observation.GameLoop);
                else if (observation.DeadUnitTags.Contains(resultTag))
                    job.Fail("Construction structure was destroyed.", observation.GameLoop);
                continue;
            }

            var worker = observation.Units.FirstOrDefault(unit =>
                unit.Tag == job.WorkerTag && unit.Alliance == SingularityBot.Observation.UnitAlliance.Self &&
                unit.IsLiveVisible && unit.HasPosition && unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase));
            if (worker is null) continue;

            var excluded = _excludedResultTags.TryGetValue(job.Id, out var known) ? known : new HashSet<ulong>();
            var candidate = structures.Where(unit => !excluded.Contains(unit.Tag) && !_jobs.Values.Any(other =>
                    other.Id != job.Id && other.ResultUnitTag == unit.Tag))
                .Select(unit => new
                {
                    Unit = unit,
                    DistanceSquared = DistanceSquared(worker.PositionX, worker.PositionY, unit.PositionX, unit.PositionY)
                })
                .Where(item => item.DistanceSquared <= 36f)
                .OrderBy(item => item.DistanceSquared)
                .ThenBy(item => item.Unit.Tag)
                .FirstOrDefault();
            if (candidate is not null)
                job.ObserveResult(candidate.Unit.Tag, candidate.Unit.BuildProgress, observation.GameLoop);
        }
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    public bool Retire(long id)
    {
        if (!_jobs.Remove(id, out var job)) return false;
        job.IsValid = false;
        _excludedResultTags.Remove(id);
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
