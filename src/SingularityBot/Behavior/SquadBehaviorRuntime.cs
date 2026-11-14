#nullable enable annotations

using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Owns persistent tactical squad contexts. Membership is exclusive within this
/// runtime; the host's authoritative reservation/action pipeline still decides
/// whether a member may receive an order.
/// </summary>
public sealed class SquadBehaviorRuntime
{
    private readonly Dictionary<string, SquadContext> _squads = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, string> _membership = new(StringComparer.Ordinal);

    public SquadBehaviorRuntime()
    {
        SquadBehaviorCatalog.Register();
    }

    public IReadOnlyCollection<SquadContext> Squads => _squads.Values.ToArray();

    public bool TryGetSquad(string squadId, out SquadContext squad) =>
        _squads.TryGetValue(squadId, out squad!);

    public bool TryGetMembership(ulong unitTag, out string squadId) =>
        _membership.TryGetValue(unitTag, out squadId!);

    public SquadContext Create(string squadId, string role, string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(squadId))
            throw new ArgumentException("A stable squad id is required.", nameof(squadId));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("A squad role is required.", nameof(role));
        if (string.IsNullOrWhiteSpace(objectiveId))
            throw new ArgumentException("An objective id is required.", nameof(objectiveId));
        if (_squads.ContainsKey(squadId))
            throw new InvalidOperationException($"Squad '{squadId}' already exists.");

        var squad = new SquadContext(squadId, role, objectiveId);
        squad.OwnHandle(FSM_API.Create.CreateInstance(
            SquadBehaviorCatalog.LifecycleFsm, squad, UnitBehaviorCatalog.StrategyProcessingGroup));
        _squads.Add(squadId, squad);
        return squad;
    }

    /// <summary>
    /// Adds a unit to one squad only. This records tactical membership; it does
    /// not reserve the unit or authorize an SC2 action.
    /// </summary>
    public bool TryAddMember(string squadId, ulong unitTag)
    {
        if (unitTag == 0) throw new ArgumentOutOfRangeException(nameof(unitTag));
        if (!_squads.TryGetValue(squadId, out var squad) || !squad.IsValid ||
            _membership.ContainsKey(unitTag) || squad.IsDisbanding)
            return false;

        squad.AddMember(unitTag);
        _membership.Add(unitTag, squadId);
        return true;
    }

    public bool RemoveMember(ulong unitTag)
    {
        if (!_membership.Remove(unitTag, out var squadId) ||
            !_squads.TryGetValue(squadId, out var squad))
            return false;
        squad.RemoveMember(unitTag);
        return true;
    }

    /// <summary>Reconciles only explicit dead-unit evidence; fog-of-war never removes members.</summary>
    public void ReconcileDeadUnits(IEnumerable<ulong> deadUnitTags)
    {
        ArgumentNullException.ThrowIfNull(deadUnitTags);
        foreach (var tag in deadUnitTags.Distinct().OrderBy(tag => tag))
            RemoveMember(tag);
    }

    public bool SetExecuting(string squadId, bool executing)
    {
        if (!_squads.TryGetValue(squadId, out var squad) || !squad.IsValid || squad.IsDisbanding)
            return false;
        squad.IsExecuting = executing;
        return true;
    }

    public bool SetRegrouping(string squadId, bool regrouping)
    {
        if (!_squads.TryGetValue(squadId, out var squad) || !squad.IsValid || squad.IsDisbanding)
            return false;
        squad.ShouldRegroup = regrouping;
        return true;
    }

    public bool Disband(string squadId)
    {
        if (!_squads.Remove(squadId, out var squad))
            return false;
        foreach (var tag in squad.UnitTags)
            _membership.Remove(tag);
        squad.BeginDisband();
        squad.DestroyOwnedHandle();
        return true;
    }

    public void Update() => FSM_API.Interaction.Update(UnitBehaviorCatalog.StrategyProcessingGroup);

    public void Clear()
    {
        foreach (var squad in _squads.Values)
        {
            squad.BeginDisband();
            squad.DestroyOwnedHandle();
        }
        _squads.Clear();
        _membership.Clear();
    }
}

/// <summary>Persistent, FSM_API-backed state for one tactical squad.</summary>
public sealed class SquadContext : IStateContext
{
    private readonly HashSet<ulong> _unitTags = new();
    private FSMHandle _handle;
    private bool _ownsHandle;

    internal SquadContext(string squadId, string role, string objectiveId)
    {
        SquadId = squadId;
        Role = role;
        ObjectiveId = objectiveId;
        IsValid = true;
    }

    public string SquadId { get; }
    public string Role { get; }
    public string ObjectiveId { get; private set; }
    public IReadOnlyCollection<ulong> UnitTags => _unitTags.OrderBy(tag => tag).ToArray();
    public int MemberCount => _unitTags.Count;
    public bool IsExecuting { get; internal set; }
    public bool ShouldRegroup { get; internal set; }
    public bool IsDisbanding { get; private set; }
    public bool IsValid { get; set; }
    public string LifecycleState => _ownsHandle ? _handle.CurrentState : "Uninitialized";

    public bool SetObjective(string objectiveId)
    {
        if (!IsValid || IsDisbanding || string.IsNullOrWhiteSpace(objectiveId))
            return false;
        ObjectiveId = objectiveId;
        return true;
    }

    internal void AddMember(ulong unitTag) => _unitTags.Add(unitTag);
    internal void RemoveMember(ulong unitTag) => _unitTags.Remove(unitTag);

    internal void OwnHandle(FSMHandle handle)
    {
        _handle = handle;
        _ownsHandle = true;
    }

    internal void BeginDisband()
    {
        IsDisbanding = true;
        IsExecuting = false;
        ShouldRegroup = false;
        if (_ownsHandle)
            _handle.TransitionTo("Disbanding");
        IsValid = false;
    }

    internal void DestroyOwnedHandle()
    {
        if (!_ownsHandle) return;
        FSM_API.Interaction.DestroyInstance(_handle);
        _ownsHandle = false;
    }
}

/// <summary>FSM_API lifecycle definition for tactical squads.</summary>
public static class SquadBehaviorCatalog
{
    public const string LifecycleFsm = "SingularityBot.SquadLifecycle";
    private static readonly object RegistrationLock = new();
    private static bool _registered;

    public static void Register()
    {
        lock (RegistrationLock)
        {
            if (_registered) return;
            RegisterDefinition();
            _registered = true;
        }
    }

    private static void RegisterDefinition()
    {
        FSM_API.Create.CreateFiniteStateMachine(
                LifecycleFsm, processRate: -1, processingGroup: UnitBehaviorCatalog.StrategyProcessingGroup)
            .State("Forming", null, null, null)
            .State("Ready", null, null, null)
            .State("Executing", null, null, null)
            .State("Regrouping", null, null, null)
            .State("Disbanding", null, null, null)
            .Transition("Forming", "Ready", IsReady)
            .Transition("Ready", "Executing", IsExecuting)
            .Transition("Executing", "Regrouping", IsRegrouping)
            .Transition("Regrouping", "Executing", IsExecutingAfterRegroup)
            .Transition("Forming", "Disbanding", IsDisbanding)
            .Transition("Ready", "Disbanding", IsDisbanding)
            .Transition("Executing", "Disbanding", IsDisbanding)
            .Transition("Regrouping", "Disbanding", IsDisbanding)
            .WithInitialState("Forming")
            .BuildDefinition();
    }

    private static bool IsReady(IStateContext context) =>
        context is SquadContext squad && squad.IsValid && squad.MemberCount > 0 &&
        !string.IsNullOrWhiteSpace(squad.ObjectiveId);

    private static bool IsExecuting(IStateContext context) =>
        context is SquadContext squad && squad.IsValid && squad.IsExecuting && !squad.ShouldRegroup;

    private static bool IsRegrouping(IStateContext context) =>
        context is SquadContext squad && squad.IsValid && squad.ShouldRegroup;

    private static bool IsExecutingAfterRegroup(IStateContext context) =>
        context is SquadContext squad && squad.IsValid && squad.IsExecuting && !squad.ShouldRegroup;

    private static bool IsDisbanding(IStateContext context) =>
        context is SquadContext squad && squad.IsDisbanding;
}
