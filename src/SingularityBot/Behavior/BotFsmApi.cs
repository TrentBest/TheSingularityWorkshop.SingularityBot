using System;
using System.Collections.Generic;
using TheSingularityWorkshop.FSM_API;
using IntegerFsmApi = TheSingularityWorkshop.FSM_API.IntegerBacked.FsmApi;
using IntegerFsmBuilder = TheSingularityWorkshop.FSM_API.IntegerBacked.FSMBuilderInt;
using IntegerFsmHandle = TheSingularityWorkshop.FSM_API.IntegerBacked.FSMHandleInt;

namespace SingularityBot.Behavior;

/// <summary>
/// Bot-side authoring adapter over FSM_API's native integer-backed runtime.
/// Human-readable definition, state, and processing-group names are translated
/// at the boundary; runtime definitions, handles, transitions, and updates use
/// integer identities only.
/// </summary>
public static class BotFsmApi
{
    public const string AnyStateIdentifier = "__ANY_STATE__";

    private static readonly object Sync = new();
    private static readonly Dictionary<string, int> FsmIds = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> ProcessingGroupIds = new(StringComparer.Ordinal);
    private static readonly Dictionary<int, IReadOnlyDictionary<int, string>> StateNames = new();
    private static int _nextFsmId;
    private static int _nextProcessingGroupId;

    private static int GetFsmId(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An FSM name is required.", nameof(name));

        lock (Sync)
        {
            if (FsmIds.TryGetValue(name, out var id))
                return id;

            id = _nextFsmId++;
            FsmIds.Add(name, id);
            return id;
        }
    }

    private static int GetProcessingGroupId(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A processing-group name is required.", nameof(name));

        lock (Sync)
        {
            if (ProcessingGroupIds.TryGetValue(name, out var id))
                return id;

            id = _nextProcessingGroupId++;
            ProcessingGroupIds.Add(name, id);
            return id;
        }
    }

    private static void RegisterStateNames(int fsmId, Dictionary<int, string> names)
    {
        lock (Sync)
            StateNames[fsmId] = new Dictionary<int, string>(names);
    }

    private static IReadOnlyDictionary<int, string> GetStateNames(int fsmId)
    {
        lock (Sync)
        {
            return StateNames.TryGetValue(fsmId, out var names)
                ? new Dictionary<int, string>(names)
                : new Dictionary<int, string>();
        }
    }

    public static class Create
    {
        public static int CreateProcessingGroup(string name) => GetProcessingGroupId(name);

        public static BotFsmBuilder CreateFiniteStateMachine(
            string fsmName,
            int processRate = 0,
            string processingGroup = "Update")
        {
            return new BotFsmBuilder(
                GetFsmId(fsmName),
                processRate,
                GetProcessingGroupId(processingGroup));
        }

        public static BotFsmHandle CreateInstance(
            string fsmName,
            IStateContext context,
            string processingGroup = "Update")
        {
            var fsmId = GetFsmId(fsmName);
            var expectedGroupId = GetProcessingGroupId(processingGroup);
            var definition = IntegerFsmApi.Interaction.GetDefinition(fsmId);

            if (definition == null)
                throw new InvalidOperationException($"FSM definition '{fsmName}' has not been registered.");
            if (definition.ProcessingGroupID != expectedGroupId)
                throw new InvalidOperationException(
                    $"FSM definition '{fsmName}' belongs to processing group ID {definition.ProcessingGroupID}, not '{processingGroup}'.");

            var handle = IntegerFsmApi.Create.CreateInstance(fsmId, context);
            return new BotFsmHandle(handle, GetStateNames(fsmId));
        }
    }

    public static class Interaction
    {
        public static bool Exists(string fsmName, string processingGroup)
        {
            var definition = IntegerFsmApi.Interaction.GetDefinition(GetFsmId(fsmName));
            return definition != null &&
                   definition.ProcessingGroupID == GetProcessingGroupId(processingGroup);
        }

        public static void Update(string processingGroup) =>
            IntegerFsmApi.Interaction.Update(GetProcessingGroupId(processingGroup));

        public static void UpdateAll() => IntegerFsmApi.Interaction.UpdateAll();

        public static bool DestroyInstance(BotFsmHandle handle) =>
            handle != null && IntegerFsmApi.Interaction.DestroyInstance(handle.Inner);
    }
}

/// <summary>
/// Readable FSM definition builder that compiles names to compact integer IDs
/// before registering the definition with FSM_API's integer runtime.
/// </summary>
public sealed class BotFsmBuilder
{
    private readonly int _fsmId;
    private readonly IntegerFsmBuilder _builder;
    private readonly Dictionary<string, int> _stateIds = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _stateNames = new();
    private int _nextStateId;

    internal BotFsmBuilder(int fsmId, int processRate, int processingGroupId)
    {
        _fsmId = fsmId;
        _builder = new IntegerFsmBuilder(fsmId, processRate, processingGroupId);
    }

    public BotFsmBuilder State(
        string name,
        Action<IStateContext> onEnter = null,
        Action<IStateContext> onUpdate = null,
        Action<IStateContext> onExit = null)
    {
        _builder.State(GetStateId(name), onEnter, onUpdate, onExit);
        return this;
    }

    public BotFsmBuilder Transition(
        string fromState,
        string toState,
        Func<IStateContext, bool> condition)
    {
        var toId = GetStateId(toState);
        if (string.Equals(fromState, BotFsmApi.AnyStateIdentifier, StringComparison.Ordinal))
            _builder.AnyTransition(toId, condition);
        else
            _builder.Transition(GetStateId(fromState), toId, condition);

        return this;
    }

    public BotFsmBuilder WithInitialState(string stateName)
    {
        _builder.WithInitialState(GetStateId(stateName));
        return this;
    }

    public void BuildDefinition()
    {
        var definition = _builder.BuildDefinition();
        IntegerFsmApi.Create.RegisterDefinition(definition);
        BotFsmApi.RegisterStateNames(_fsmId, _stateNames);
    }

    private int GetStateId(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A state name is required.", nameof(name));
        if (_stateIds.TryGetValue(name, out var id))
            return id;

        id = _nextStateId++;
        _stateIds.Add(name, id);
        _stateNames.Add(id, name);
        return id;
    }
}

public sealed class BotFsmHandle
{
    private readonly IReadOnlyDictionary<int, string> _stateNames;

    internal IntegerFsmHandle Inner { get; }

    internal BotFsmHandle(IntegerFsmHandle inner, IReadOnlyDictionary<int, string> stateNames)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _stateNames = stateNames ?? throw new ArgumentNullException(nameof(stateNames));
    }

    public int Id => Inner.Id;
    public int CurrentStateID => Inner.CurrentStateID;
    public bool IsValid => Inner.IsValid;
    public bool IsDestroyed => Inner.IsDestroyed;
    public IStateContext Context => Inner.Context;

    public string CurrentState =>
        _stateNames.TryGetValue(Inner.CurrentStateID, out var name)
            ? name
            : Inner.CurrentStateID.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
