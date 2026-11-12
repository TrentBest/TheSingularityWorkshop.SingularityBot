#nullable enable

using System.Diagnostics.CodeAnalysis;
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
    private FSMHandle? _handle;
    public bool HasExecutionEvidence { get; private set; }
    public bool HasCompletionEvidence { get; private set; }
    public bool HasFailureEvidence { get; private set; }

    public float Progress { get; private set; }
    public bool IsAssigned => WorkerTag.HasValue || ProducerTag.HasValue;
    public string CurrentState => _handle?.CurrentState ?? "Queued";
    public bool IsExecuting => CurrentState == "Executing";
    public bool IsCompleted => CurrentState == "Completed";
    public bool IsFailed => CurrentState == "Failed";
    public string FailureReason { get; private set; } = string.Empty;

    internal void AttachHandle(FSMHandle handle) =>
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));

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
        HasExecutionEvidence = true;
        if (Progress >= 100f) Complete(gameLoop);
    }

    public void MarkStarted(uint gameLoop)
    {
        if (IsCompleted || IsFailed) return;
        HasExecutionEvidence = true;
        LastObservedGameLoop = gameLoop;
    }

    public void Complete(uint gameLoop)
    {
        if (IsFailed) return;
        Progress = 100f;
        HasCompletionEvidence = true;
        LastObservedGameLoop = gameLoop;
    }

    public void Fail(string reason, uint gameLoop)
    {
        if (IsCompleted || IsFailed) return;
        HasFailureEvidence = true;
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
    private readonly Dictionary<long, HashSet<ulong>> _excludedMarineTags = new();
    // Result identities remain claimed for the entire match, even after their
    // completed job FSM is retired. Otherwise a later nearby job could adopt
    // the same already-produced unit/structure from an old snapshot.
    private readonly HashSet<ulong> _claimedResultTags = new();
    private long _nextId;
    public IReadOnlyCollection<BuildJobContext> Jobs => _jobs.Values.ToArray();
    public int ActiveCount => _jobs.Values.Count(job => !job.IsCompleted && !job.IsFailed);

    public static void RegisterDefinition()
    {
        if (FSM_API.Interaction.Exists(FsmName, UnitBehaviorCatalog.ProductionProcessingGroup))
            return;

        FSM_API.Create.CreateFiniteStateMachine(
                FsmName,
                processRate: -1,
                processingGroup: UnitBehaviorCatalog.ProductionProcessingGroup)
            .State("Queued", null, null, null)
            .State("Assigned", null, null, null)
            .State("Executing", null, null, null)
            .State("Completed", null, null, null)
            .State("Failed", null, null, null)
            .Transition("Queued", "Failed", HasFailureEvidence)
            .Transition("Queued", "Completed", HasCompletionEvidence)
            .Transition("Queued", "Executing", HasExecutionEvidence)
            .Transition("Queued", "Assigned", HasAssignmentEvidence)
            .Transition("Assigned", "Failed", HasFailureEvidence)
            .Transition("Assigned", "Completed", HasCompletionEvidence)
            .Transition("Assigned", "Executing", HasExecutionEvidence)
            .Transition("Executing", "Failed", HasFailureEvidence)
            .Transition("Executing", "Completed", HasCompletionEvidence)
            .WithInitialState("Queued")
            .BuildDefinition();
    }

    // Named conditions make the lifecycle contract independently inspectable.
    private static bool HasAssignmentEvidence(IStateContext context) =>
        context is BuildJobContext job && job.IsAssigned;

    private static bool HasExecutionEvidence(IStateContext context) =>
        context is BuildJobContext job && job.HasExecutionEvidence;

    private static bool HasCompletionEvidence(IStateContext context) =>
        context is BuildJobContext job && job.HasCompletionEvidence;

    private static bool HasFailureEvidence(IStateContext context) =>
        context is BuildJobContext job && job.HasFailureEvidence;

    public BuildJobContext Create(string buildableKind, uint gameLoop, ulong? workerTag = null, ulong? producerTag = null)
    {
        RegisterDefinition();
        var job = new BuildJobContext(++_nextId, buildableKind, workerTag, producerTag, gameLoop);
        _jobs.Add(job.Id, job);
        var handle = FSM_API.Create.CreateInstance(FsmName, job, UnitBehaviorCatalog.ProductionProcessingGroup);
        job.AttachHandle(handle);
        _handles.Add(job.Id, handle);
        return job;
    }

    //[NotNullWhen(true)]
    public bool TryGet(long id, out BuildJobContext? job) => _jobs.TryGetValue(id, out job);

    public BuildJobContext CreateBarracksConstruction(uint gameLoop, ulong workerTag, IEnumerable<ulong> existingBarracksTags)
    {
        var job = Create("Barracks", gameLoop, workerTag: workerTag);
        _excludedResultTags[job.Id] = existingBarracksTags?.ToHashSet() ?? new HashSet<ulong>();
        return job;
    }

    public BuildJobContext CreateSupplyDepotConstruction(uint gameLoop, ulong workerTag, IEnumerable<ulong> existingDepotTags)
    {
        var job = Create("SupplyDepot", gameLoop, workerTag: workerTag);
        _excludedResultTags[job.Id] = existingDepotTags?.ToHashSet() ?? new HashSet<ulong>();
        return job;
    }

    /// <summary>Associates observed Supply Depots with their reserved SCV and tracks authoritative progress.</summary>
    public void ReconcileSupplyDepotConstruction(global::SingularityBot.Observation.NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var structures = observation.Units.Where(unit =>
            unit.Alliance == global::SingularityBot.Observation.UnitAlliance.Self && unit.IsLiveVisible &&
            unit.HasPosition && (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
                unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)) &&
            !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();

        foreach (var job in _jobs.Values.Where(job => job.BuildableKind == "SupplyDepot" &&
                     !job.IsCompleted && !job.IsFailed && job.WorkerTag.HasValue).ToArray())
        {
            if (job.ResultUnitTag is ulong resultTag)
            {
                var existing = structures.FirstOrDefault(unit => unit.Tag == resultTag);
                if (existing is not null)
                    job.ObserveResult(existing.Tag, existing.BuildProgress, observation.GameLoop);
                else if (observation.DeadUnitTags.Contains(resultTag))
                    job.Fail("Supply Depot was destroyed during construction.", observation.GameLoop);
                continue;
            }

            var worker = observation.Units.FirstOrDefault(unit =>
                unit.Tag == job.WorkerTag && unit.Alliance == global::SingularityBot.Observation.UnitAlliance.Self &&
                unit.IsLiveVisible && unit.HasPosition && unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase));
            if (worker is null)
            {
                if (observation.DeadUnitTags.Contains(job.WorkerTag.Value))
                    job.Fail("Assigned SCV was destroyed before its Supply Depot could be associated.", observation.GameLoop);
                continue;
            }

            var excluded = _excludedResultTags.TryGetValue(job.Id, out var known) ? known : new HashSet<ulong>();
            var candidate = structures.Where(unit => !excluded.Contains(unit.Tag) && !_claimedResultTags.Contains(unit.Tag) && !_jobs.Values.Any(other =>
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
            {
                _claimedResultTags.Add(candidate.Unit.Tag);
                job.ObserveResult(candidate.Unit.Tag, candidate.Unit.BuildProgress, observation.GameLoop);
            }
        }
    }

    public BuildJobContext CreateMarineProduction(uint gameLoop, ulong producerTag, IEnumerable<ulong> existingMarineTags)
    {
        var job = Create("Marine", gameLoop, producerTag: producerTag);
        _excludedMarineTags[job.Id] = existingMarineTags?.ToHashSet() ?? new HashSet<ulong>();
        return job;
    }

    /// <summary>Tracks Marine training orders and associates each completed job with its newly observed Marine.</summary>
    public void ReconcileMarineProduction(global::SingularityBot.Observation.NormalizedObservation observation, uint marineAbilityId)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (marineAbilityId == 0) return;
        var marines = observation.Units.Where(unit =>
            unit.Alliance == global::SingularityBot.Observation.UnitAlliance.Self &&
            unit.IsLiveVisible && unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase) &&
            !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();

        foreach (var job in _jobs.Values.Where(job => job.BuildableKind == "Marine" &&
                     !job.IsCompleted && !job.IsFailed && job.ProducerTag.HasValue).ToArray())
        {
            var producer = observation.Units.FirstOrDefault(unit => unit.Tag == job.ProducerTag &&
                unit.Alliance == global::SingularityBot.Observation.UnitAlliance.Self &&
                unit.IsLiveVisible && !observation.DeadUnitTags.Contains(unit.Tag));
            if (producer is null)
            {
                if (observation.DeadUnitTags.Contains(job.ProducerTag.Value))
                    job.Fail("Marine production facility was destroyed.", observation.GameLoop);
                continue;
            }

            if (producer.Orders.Any(order => order.AbilityId == marineAbilityId))
                job.MarkStarted(observation.GameLoop);

            // Observation reconciliation consumes execution evidence directly.
            // The FSM may not have taken its next scheduled step yet, so waiting
            // for CurrentState == Executing here would delay result association
            // and can miss a result observed in the same frame as the order.
            if (!job.HasExecutionEvidence || !producer.HasPosition) continue;
            var excluded = _excludedMarineTags.TryGetValue(job.Id, out var known)
                ? known : new HashSet<ulong>();
            var candidate = marines.Where(unit => !excluded.Contains(unit.Tag) && !_claimedResultTags.Contains(unit.Tag) &&
                    !_jobs.Values.Any(other => other.Id != job.Id && other.ResultUnitTag == unit.Tag))
                .Select(unit => new
                {
                    Unit = unit,
                    DistanceSquared = DistanceSquared(producer.PositionX, producer.PositionY, unit.PositionX, unit.PositionY)
                })
                .Where(item => item.DistanceSquared <= 64f)
                .OrderBy(item => item.DistanceSquared)
                .ThenBy(item => item.Unit.Tag)
                .FirstOrDefault();
            if (candidate is not null)
            {
                _claimedResultTags.Add(candidate.Unit.Tag);
                job.ObserveResult(candidate.Unit.Tag, 1f, observation.GameLoop);
            }
        }
    }

    /// <summary>Associates observed Barracks with their reserved SCV and tracks authoritative build progress.</summary>
    public void ReconcileBarracks(global::SingularityBot.Observation.NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var structures = observation.Units.Where(unit =>
            unit.Alliance == global::SingularityBot.Observation.UnitAlliance.Self && unit.IsLiveVisible &&
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
                unit.Tag == job.WorkerTag && unit.Alliance == global::SingularityBot.Observation.UnitAlliance.Self &&
                unit.IsLiveVisible && unit.HasPosition && unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase));
            if (worker is null)
            {
                if (observation.DeadUnitTags.Contains(job.WorkerTag.Value))
                    job.Fail("Assigned SCV was destroyed before its Barracks could be associated.", observation.GameLoop);
                continue;
            }

            var excluded = _excludedResultTags.TryGetValue(job.Id, out var known) ? known : new HashSet<ulong>();
            var candidate = structures.Where(unit => !excluded.Contains(unit.Tag) && !_claimedResultTags.Contains(unit.Tag) && !_jobs.Values.Any(other =>
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
            {
                _claimedResultTags.Add(candidate.Unit.Tag);
                job.ObserveResult(candidate.Unit.Tag, candidate.Unit.BuildProgress, observation.GameLoop);
            }
        }
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    /// <summary>Advances all build-job lifecycle FSMs in the production processing group.</summary>
    public void Update() =>
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProductionProcessingGroup);

    public bool Retire(long id)
    {
        if (!_jobs.Remove(id, out var job)) return false;
        job.IsValid = false;
        _excludedResultTags.Remove(id);
        _excludedMarineTags.Remove(id);
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
        _claimedResultTags.Clear();
    }
}
