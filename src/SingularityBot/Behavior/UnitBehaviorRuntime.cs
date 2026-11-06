using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Owns the per-unit contexts and FSM handles while sharing immutable FSM definitions.
/// Unit tags are the stable key; only authoritative dead-unit events retire a context.
/// </summary>
public sealed class UnitBehaviorRuntime
{
    private readonly Dictionary<ulong, UnitBehaviorContext> _contexts = new();
    private readonly Dictionary<ulong, FSMHandle> _handles = new();

    public int Count => _contexts.Count;

    public UnitBehaviorContext Attach(
        ulong unitTag,
        UnitExpertiseProfile profile,
        Action<BehaviorCommand> emit)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (_contexts.TryGetValue(unitTag, out var existing))
            return existing;

        var context = new UnitBehaviorContext(
            unitTag,
            $"{profile.UnitKind}:{unitTag}",
            profile.UnitKind,
            profile.IsWorker,
            emit);

        var handle = FSM_API.Create.CreateInstance(
            profile.BehaviorFsm,
            context,
            UnitBehaviorCatalog.ProcessingGroup);

        _contexts.Add(unitTag, context);
        _handles.Add(unitTag, handle);
        return context;
    }

    /// <summary>
    /// Applies one normalized observation. Absence, snapshots, and hidden entries
    /// never retire contexts; only explicit dead-unit tags do.
    /// </summary>
    public void Reconcile(
        NormalizedObservation observation,
        Action<ulong, BehaviorCommand> emit)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(emit);

        foreach (var context in _contexts.Values)
            context.IsCurrentlyVisible = false;

        foreach (var deadTag in observation.DeadUnitTags.OrderBy(tag => tag))
            Remove(deadTag);

        foreach (var facts in observation.Units)
        {
            if (facts.Alliance != UnitAlliance.Self ||
                !facts.IsLiveVisible ||
                observation.DeadUnitTags.Contains(facts.Tag) ||
                !UnitExpertiseProfiles.TryGet(facts.UnitKind, out var profile))
                continue;

            if (!_contexts.TryGetValue(facts.Tag, out var context))
            {
                context = Attach(facts.Tag, profile, command => emit(facts.Tag, command));
            }

            context.Apply(facts);
        }
    }

    public bool TryGetContext(ulong unitTag, out UnitBehaviorContext context) =>
        _contexts.TryGetValue(unitTag, out context);

    public bool TryGetHandle(ulong unitTag, out FSMHandle handle) =>
        _handles.TryGetValue(unitTag, out handle);

    public bool Remove(ulong unitTag)
    {
        if (!_contexts.Remove(unitTag, out var context))
            return false;

        context.IsValid = false;
        context.IsCurrentlyVisible = false;

        if (_handles.Remove(unitTag, out var handle))
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

        foreach (var handle in _handles.Values)
            FSM_API.Interaction.DestroyInstance(handle);

        _contexts.Clear();
        _handles.Clear();
    }

    public void Update() =>
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProcessingGroup);
}
