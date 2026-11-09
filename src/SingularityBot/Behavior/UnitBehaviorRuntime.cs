using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Reconciles persistent entity contexts and advances their FSM processing groups.
/// Entity contexts own their FSM handles; fog-of-war is not a destruction event.
/// </summary>
public sealed class UnitBehaviorRuntime
{
    private readonly Dictionary<ulong, UnitBehaviorContext> _contexts = new();
    private Action<ulong, BehaviorCommand>? _activeEmit;

    public int Count => _contexts.Count;
    public IReadOnlyList<string> ProcessGroupsThisFrame => UnitBehaviorCatalog.ProcessGroupsThisFrame;

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
        AttachHandle(context, UnitBehaviorCatalog.EntityExistenceFsm, UnitBehaviorCatalog.DataSyncProcessingGroup);
        AttachProfileCapability(context, profile);
        return context;
    }

    private UnitBehaviorContext AttachEntity(UnitObservationFacts facts)
    {
        var kind = string.IsNullOrWhiteSpace(facts.UnitKind)
            ? $"UnknownUnitType:{facts.UnitTypeId}"
            : facts.UnitKind;
        var hasProfile = UnitExpertiseProfiles.TryGet(facts.UnitKind, out var profile);
        var context = new UnitBehaviorContext(
            facts.Tag, $"{kind}:{facts.Tag}", kind, hasProfile && profile.IsWorker,
            command => Emit(facts.Tag, static _ => { }, command));
        _contexts.Add(facts.Tag, context);
        AttachHandle(context, UnitBehaviorCatalog.EntityExistenceFsm, UnitBehaviorCatalog.DataSyncProcessingGroup);
        if (hasProfile)
            AttachProfileCapability(context, profile);
        return context;
    }

    private static void AttachProfileCapability(UnitBehaviorContext context, UnitExpertiseProfile profile)
    {
        if (profile.IsWorker)
            AttachHandle(context, profile.BehaviorFsm, UnitBehaviorCatalog.MiningProcessingGroup);
        else
            AttachHandle(context, profile.BehaviorFsm, UnitBehaviorCatalog.CombatProcessingGroup);
    }

    private static void AttachHandle(UnitBehaviorContext context, string fsmName, string processingGroup)
    {
        if (context.HasCapability(fsmName)) return;
        var handle = FSM_API.Create.CreateInstance(fsmName, context, processingGroup);
        context.OwnHandle(fsmName, handle);
    }

    public bool AttachCapability(ulong unitTag, string fsmName, string processingGroup)
    {
        if (!_contexts.TryGetValue(unitTag, out var context) || !context.IsValid)
            return false;
        if (string.IsNullOrWhiteSpace(fsmName) || string.IsNullOrWhiteSpace(processingGroup))
            throw new ArgumentException("FSM name and processing group are required.");
        if (context.HasCapability(fsmName))
            return false;
        AttachHandle(context, fsmName, processingGroup);
        return true;
    }

    public void Reconcile(NormalizedObservation observation, Action<ulong, BehaviorCommand> emit)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(emit);
        _activeEmit = emit;

        // An entity omitted from this frame is merely unobserved. Keep its
        // context valid and retain its last-known facts until explicit death.
        foreach (var context in _contexts.Values)
            context.MarkNotObserved(observation.GameLoop);

        foreach (var deadTag in observation.DeadUnitTags.OrderBy(tag => tag))
            Remove(deadTag);

        foreach (var facts in observation.Units.OrderBy(unit => unit.Tag))
        {
            if (facts.Tag == 0 || observation.DeadUnitTags.Contains(facts.Tag))
                continue;

            var exists = _contexts.TryGetValue(facts.Tag, out var context);
            if (!exists)
            {
                // Positive HealthMax is the initial protocol-backed signal that
                // this entity has hit points. Once created, its context survives
                // later records that omit health data.
                if (facts.HealthMax <= 0)
                    continue;
                context = AttachEntity(facts);
            }
            else if (context.UnitTypeId != 0 && facts.UnitTypeId != 0 &&
                     context.UnitTypeId != facts.UnitTypeId)
            {
                // Morphs retain the game tag but may change their capabilities.
                // Rebuild the context/handles for the new authoritative unit type.
                Remove(facts.Tag);
                if (facts.HealthMax <= 0)
                    continue;
                context = AttachEntity(facts);
            }

            context.Apply(facts);
            if (UnitExpertiseProfiles.TryGet(facts.UnitKind, out var profile) &&
                !context.HasCapability(profile.BehaviorFsm))
                AttachProfileCapability(context, profile);
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

    public bool TryGetHandle(ulong unitTag, out FSMHandle handle)
    {
        handle = default;
        return _contexts.TryGetValue(unitTag, out var context) && context.TryGetFirstHandle(out handle);
    }

    public IReadOnlyCollection<string> GetCapabilities(ulong unitTag) =>
        _contexts.TryGetValue(unitTag, out var context)
            ? context.Capabilities
            : Array.Empty<string>();

    public bool Remove(ulong unitTag)
    {
        if (!_contexts.Remove(unitTag, out var context)) return false;
        context.Invalidate();
        context.DestroyOwnedHandles();
        return true;
    }

    public void RemoveAll()
    {
        foreach (var context in _contexts.Values)
        {
            context.Invalidate();
            context.DestroyOwnedHandles();
        }
        _contexts.Clear();
        _activeEmit = null;
    }

    /// <summary>Advances one explicitly selected frame group.</summary>
    public void UpdateGroup(string processingGroup)
    {
        if (string.IsNullOrWhiteSpace(processingGroup))
            throw new ArgumentException("A processing group is required.", nameof(processingGroup));
        FSM_API.Interaction.Update(processingGroup);
    }

    /// <summary>Ends the current observation's intent-emission scope.</summary>
    public void EndFrame() => _activeEmit = null;

    /// <summary>Test/helper scheduler using the same declared group order as OnFrame.</summary>
    public void Update()
    {
        try
        {
            foreach (var group in ProcessGroupsThisFrame)
                UpdateGroup(group);
        }
        finally { EndFrame(); }
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
