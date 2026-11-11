#nullable enable annotations

using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Reconciles persistent entity contexts and advances their FSM processing groups.
/// Entity contexts own their FSM handles; fog-of-war is not a destruction event.
/// Unit composition is supplied by declarative UnitDefinitions resources.
/// </summary>
public sealed class UnitBehaviorRuntime
{
    private readonly Dictionary<ulong, UnitBehaviorContext> _contexts = new();
    private Action<ulong, BehaviorCommand>? _activeEmit;

    public int Count => _contexts.Count;
    public IReadOnlyCollection<UnitBehaviorContext> Contexts => _contexts.Values.ToArray();
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
        AttachDefinitionCapabilities(context, profile.UnitKind);
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
        AttachDefinitionCapabilities(context, facts.UnitKind);
        return context;
    }

    private static void AttachDefinitionCapabilities(UnitBehaviorContext context, string unitKind)
    {
        if (!UnitDefinitions.TryGet(unitKind, out var definition))
            return;

        foreach (var capability in definition.Capabilities)
            AttachHandle(context, capability.FsmName, capability.ProcessingGroup);
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
                if (facts.HealthMax <= 0)
                    continue;
                context = AttachEntity(facts);
            }
            else if (context.UnitTypeId != 0 && facts.UnitTypeId != 0 &&
                     context.UnitTypeId != facts.UnitTypeId)
            {
                Remove(facts.Tag);
                if (facts.HealthMax <= 0)
                    continue;
                context = AttachEntity(facts);
            }

            context.Apply(facts);
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

    public void UpdateGroup(string processingGroup)
    {
        if (string.IsNullOrWhiteSpace(processingGroup))
            throw new ArgumentException("A processing group is required.", nameof(processingGroup));
        FSM_API.Interaction.Update(processingGroup);
    }

    public void EndFrame() => _activeEmit = null;

    /// <summary>Advances every declared processing group in the same order as the frame owner.</summary>
    public void Update()
    {
        foreach (var processingGroup in ProcessGroupsThisFrame)
            UpdateGroup(processingGroup);
    }
    public void UpdateRepair() => UpdateGroup(UnitBehaviorCatalog.RepairProcessingGroup);
}
