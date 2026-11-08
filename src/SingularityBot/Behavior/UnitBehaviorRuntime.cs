using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Owns one persistent observation context and any number of complementary FSM
/// handles for each unit. Capabilities share the same context but run in distinct
/// processing groups so event-driven capabilities need not be polled every frame.
/// </summary>
public sealed class UnitBehaviorRuntime
{
    private readonly Dictionary<ulong, UnitBehaviorContext> _contexts = new();
    private readonly Dictionary<ulong, Dictionary<string, FSMHandle>> _handles = new();
    private Action<ulong, BehaviorCommand>? _activeEmit;

    public int Count => _contexts.Count;

    public UnitBehaviorContext Attach(
        ulong unitTag,
        UnitExpertiseProfile profile,
        Action<BehaviorCommand> emit)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(emit);
        if (_contexts.TryGetValue(unitTag, out var existing))
            return existing;

        var context = new UnitBehaviorContext(
            unitTag, $"{profile.UnitKind}:{unitTag}", profile.UnitKind, profile.IsWorker,
            command => Emit(unitTag, emit, command));

        _contexts.Add(unitTag, context);
        _handles.Add(unitTag, new Dictionary<string, FSMHandle>(StringComparer.Ordinal));
        AttachHandle(unitTag, profile.BehaviorFsm,
            profile.IsWorker ? UnitBehaviorCatalog.MiningProcessingGroup : UnitBehaviorCatalog.CombatProcessingGroup);
        return context;
    }

    /// <summary>Adds a complementary capability FSM to an existing unit context.</summary>
    public bool AttachCapability(ulong unitTag, string fsmName, string processingGroup)
    {
        if (!_contexts.TryGetValue(unitTag, out var context) || !context.IsValid)
            return false;
        if (string.IsNullOrWhiteSpace(fsmName) || string.IsNullOrWhiteSpace(processingGroup))
            throw new ArgumentException("FSM name and processing group are required.");
        if (_handles[unitTag].ContainsKey(fsmName))
            return false;

        AttachHandle(unitTag, fsmName, processingGroup);
        return true;
    }

    private void AttachHandle(ulong unitTag, string fsmName, string processingGroup)
    {
        var handle = FSM_API.Create.CreateInstance(fsmName, _contexts[unitTag], processingGroup);
        _handles[unitTag].Add(fsmName, handle);
    }

    public void Reconcile(NormalizedObservation observation, Action<ulong, BehaviorCommand> emit)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(emit);
        _activeEmit = emit;

        foreach (var context in _contexts.Values)
        {
            context.IsCurrentlyVisible = false;
            context.IsValid = false;
        }

        foreach (var deadTag in observation.DeadUnitTags.OrderBy(tag => tag))
            Remove(deadTag);

        foreach (var facts in observation.Units)
        {
            if (facts.Alliance != UnitAlliance.Self || !facts.IsLiveVisible ||
                observation.DeadUnitTags.Contains(facts.Tag) ||
                !UnitExpertiseProfiles.TryGet(facts.UnitKind, out var profile))
                continue;

            if (_contexts.TryGetValue(facts.Tag, out var context) &&
                (!context.UnitKind.Equals(profile.UnitKind, StringComparison.OrdinalIgnoreCase) ||
                 context.IsWorker != profile.IsWorker))
                Remove(facts.Tag);

            if (!_contexts.TryGetValue(facts.Tag, out var current))
                current = Attach(facts.Tag, profile, static _ => { });
            current.Apply(facts);
        }
    }

    public void ApplyWorkerAssignments(
        IReadOnlyDictionary<ulong, WorkerResourceAssignment> assignments, uint gameLoop)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        foreach (var context in _contexts.Values)
        {
            if (!context.IsWorker || !context.IsCurrentlyVisible) continue;
            assignments.TryGetValue(context.UnitTag, out var assignment);
            context.ApplyWorkerAssignment(assignment, gameLoop);
        }
    }

    private void Emit(ulong unitTag, Action<BehaviorCommand> fallback, BehaviorCommand command)
    {
        if (_activeEmit is { } active) active(unitTag, command);
        else fallback(command);
    }

    public bool TryGetContext(ulong unitTag, out UnitBehaviorContext context) =>
        _contexts.TryGetValue(unitTag, out context);

    // Retained for existing callers; returns the unit's first attached capability.
    public bool TryGetHandle(ulong unitTag, out FSMHandle handle)
    {
        handle = default;
        if (!_handles.TryGetValue(unitTag, out var handles) || handles.Count == 0) return false;
        handle = handles.Values.First();
        return true;
    }

    public IReadOnlyCollection<string> GetCapabilities(ulong unitTag) =>
        _handles.TryGetValue(unitTag, out var handles)
            ? handles.Keys.ToArray()
            : Array.Empty<string>();

    public bool Remove(ulong unitTag)
    {
        if (!_contexts.Remove(unitTag, out var context)) return false;
        context.IsValid = false;
        context.IsCurrentlyVisible = false;
        if (_handles.Remove(unitTag, out var handles))
            foreach (var handle in handles.Values)
                FSM_API.Interaction.DestroyInstance(handle);
        return true;
    }

    public void RemoveAll()
    {
        foreach (var context in _contexts.Values)
        {
            context.IsValid = false;
            context.IsCurrentlyVisible = false;
        }
        foreach (var handles in _handles.Values)
            foreach (var handle in handles.Values)
                FSM_API.Interaction.DestroyInstance(handle);
        _contexts.Clear();
        _handles.Clear();
        _activeEmit = null;
    }

    /// <summary>Steps continuously active groups; repair is deliberately excluded.</summary>
    public void Update()
    {
        try
        {
            FSM_API.Interaction.Update(UnitBehaviorCatalog.MiningProcessingGroup);
            FSM_API.Interaction.Update(UnitBehaviorCatalog.ConstructionProcessingGroup);
            FSM_API.Interaction.Update(UnitBehaviorCatalog.CombatProcessingGroup);
        }
        finally { _activeEmit = null; }
    }

    /// <summary>Called only when repair work or its eligibility has changed.</summary>
    public void UpdateRepair(Action<ulong, BehaviorCommand> emit)
    {
        ArgumentNullException.ThrowIfNull(emit);
        _activeEmit = emit;
        try { FSM_API.Interaction.Update(UnitBehaviorCatalog.RepairProcessingGroup); }
        finally { _activeEmit = null; }
    }
}
