using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Owns per-unit FSM instances while sharing immutable FSM definitions.
/// Unit tags are the stable key; contexts are invalidated when units disappear.
/// </summary>
public sealed class UnitBehaviorRuntime
{
    private readonly Dictionary<ulong, UnitBehaviorContext> _contexts = new();

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

        FSM_API.Create.CreateInstance(
            profile.BehaviorFsm,
            context,
            UnitBehaviorCatalog.ProcessingGroup);

        _contexts.Add(unitTag, context);
        return context;
    }

    public bool TryGetContext(ulong unitTag, out UnitBehaviorContext context) =>
        _contexts.TryGetValue(unitTag, out context);

    public bool Remove(ulong unitTag)
    {
        if (!_contexts.Remove(unitTag, out var context))
            return false;

        // FSM_API stops processing and retires the instance when its context
        // becomes invalid. The context object itself remains owned by this runtime.
        context.IsValid = false;
        return true;
    }

    public void RemoveAll()
    {
        foreach (var context in _contexts.Values)
            context.IsValid = false;

        _contexts.Clear();
    }

    public void Update() =>
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProcessingGroup);
}
