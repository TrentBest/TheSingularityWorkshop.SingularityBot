namespace SingularityBot.Behavior;

/// <summary>
/// Resolves the opponent side of the current SC2 match without guessing.
/// This contract intentionally supports 1v1 only; team and FFA identity
/// require a richer matchup key before they can be activated.
/// </summary>
public static class BotDoctrineMatchIdentity
{
    public static SC2APIProtocol.Race ResolveSingleOpponentRace(
        IEnumerable<SC2APIProtocol.Race> opponentRaces)
    {
        ArgumentNullException.ThrowIfNull(opponentRaces);
        var races = opponentRaces.ToArray();
        if (races.Length != 1)
            throw new InvalidOperationException(
                $"Doctrine activation requires exactly one opponent; observed {races.Length}.");

        if (races[0] is not (SC2APIProtocol.Race.Terran or
            SC2APIProtocol.Race.Zerg or SC2APIProtocol.Race.Protoss))
            throw new InvalidOperationException(
                $"Doctrine activation requires a concrete opponent race; observed {races[0]}.");

        return races[0];
    }
}

/// <summary>Immutable event delivered to a registered BotMethod.</summary>
public sealed record BotDoctrineEvent(
    string EventId,
    string CorrelationId,
    uint GameLoop,
    IReadOnlyDictionary<string, string> Data);

public delegate ValueTask BotDoctrineMethodHandler(BotDoctrineEvent botEvent, CancellationToken cancellationToken);

public sealed record BotDoctrineMethodRegistration(
    string Id,
    BotDoctrineMethodScope Scope,
    BotDoctrineMethodHandler Handler);

public sealed record BotDoctrineControllerRegistration(
    string Id,
    BotDoctrineNodeKind Kind,
    BotDoctrineMethodScope Scope,
    IReadOnlySet<string> Capabilities);

/// <summary>
/// Trusted compiled registrations available to a bot process. Definitions can
/// refer to these IDs but cannot add or replace executable registrations.
/// </summary>
public sealed class BotDoctrineRuntimeCatalog
{
    private readonly Dictionary<string, BotDoctrineMethodRegistration> _methods = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BotDoctrineControllerRegistration> _controllers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _services = new(StringComparer.Ordinal);

    public void RegisterMethod(BotDoctrineMethodRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrWhiteSpace(registration.Id) || registration.Handler is null)
            throw new ArgumentException("A method registration requires an ID and handler.", nameof(registration));
        if (!_methods.TryAdd(registration.Id, registration))
            throw new InvalidOperationException($"BotMethod '{registration.Id}' is already registered.");
    }

    public void RegisterController(BotDoctrineControllerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrWhiteSpace(registration.Id))
            throw new ArgumentException("A controller registration requires an ID.", nameof(registration));
        if (!_controllers.TryAdd(registration.Id, registration))
            throw new InvalidOperationException($"Controller '{registration.Id}' is already registered.");
    }

    public void RegisterService(string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId) || !_services.Add(serviceId))
            throw new InvalidOperationException($"Service '{serviceId}' is empty or already registered.");
    }

    public bool TryGetMethod(string id, out BotDoctrineMethodRegistration registration) => _methods.TryGetValue(id, out registration);
    public bool TryGetController(string id, out BotDoctrineControllerRegistration registration) => _controllers.TryGetValue(id, out registration);
    public bool HasService(string id) => _services.Contains(id);
}

/// <summary>Resolved, immutable runtime selection for one exact matchup.</summary>
public sealed record BotDoctrineRuntimePlan(
    BotDoctrineDefinition Definition,
    BotRaceInitializationPlan Initialization,
    BotDoctrineMatchup Matchup,
    IReadOnlyList<BotDoctrineMethodBinding> MethodBindings,
    IReadOnlyList<BotDoctrineMethodRegistration> Methods,
    IReadOnlyList<BotDoctrineControllerRegistration> Controllers);

/// <summary>
/// Fail-closed resolver. Structural validity is necessary but every executable
/// method, controller, and required service must also exist in the host catalog.
/// </summary>
public static class BotDoctrineRuntimeResolver
{
    public static BotDoctrineRuntimePlan Resolve(
        BotDoctrineDefinition definition,
        SC2APIProtocol.Race ownRace,
        SC2APIProtocol.Race opponentRace,
        BotDoctrineRuntimeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = definition.Validate().ToList();
        if (errors.Count != 0)
            throw new InvalidOperationException("Doctrine validation failed: " + string.Join(" ", errors));

        var matchup = definition.Matchups.SingleOrDefault(entry =>
            entry.OwnRace == ownRace && entry.OpponentRace == opponentRace);
        if (matchup is null)
            throw new InvalidOperationException($"No exact doctrine matchup is configured for {ownRace}-vs-{opponentRace}; fallback is prohibited.");

        var initialization = definition.InitializationPlans.Single(plan => plan.Race == ownRace);
        foreach (var service in definition.Services.Where(service => service.Required &&
                     (service.AppliesToRaces is null || service.AppliesToRaces.Count == 0 || service.AppliesToRaces.Contains(ownRace))))
            if (!catalog.HasService(service.ServiceId))
                errors.Add($"$.services: required service '{service.ServiceId}' is not registered.");

        var activeNodes = Walk(matchup.PrimaryCommanders).ToDictionary(entry => entry.Node.NodeId, entry => entry, StringComparer.Ordinal);
        var activeBindings = definition.Methods.Where(binding =>
            binding.Scope == BotDoctrineMethodScope.Bot || activeNodes.ContainsKey(binding.NodeId)).ToArray();
        var methods = new List<BotDoctrineMethodRegistration>();
        foreach (var binding in activeBindings)
        {
            if (!catalog.TryGetMethod(binding.MethodId, out var registration))
                errors.Add($"$.methods: BotMethod '{binding.MethodId}' is not registered.");
            else if (registration.Scope != binding.Scope)
                errors.Add($"$.methods: BotMethod '{binding.MethodId}' scope mismatch; expected {binding.Scope}, registered {registration.Scope}.");
            else if (binding.Scope != BotDoctrineMethodScope.Bot &&
                     binding.Scope == BotDoctrineMethodScope.Commander &&
                     activeNodes[binding.NodeId].Node.Kind != BotDoctrineNodeKind.Commander)
                errors.Add($"$.methods: binding '{binding.MethodId}' scope does not match commander node '{binding.NodeId}'.");
            else if (binding.Scope == BotDoctrineMethodScope.Unit &&
                     activeNodes[binding.NodeId].Node.Kind != BotDoctrineNodeKind.UnitFsm)
                errors.Add($"$.methods: unit-scoped binding '{binding.MethodId}' must target a unit-FSM leaf.");
            else if (!methods.Any(method => string.Equals(method.Id, registration.Id, StringComparison.Ordinal)))
                methods.Add(registration);
        }

        var controllers = new List<BotDoctrineControllerRegistration>();
        foreach (var (node, parent) in activeNodes.Values)
        {
            if (!catalog.TryGetController(node.ImplementationId, out var registration))
                errors.Add($"$.matchups: controller '{node.ImplementationId}' is not registered.");
            else
            {
                if (registration.Kind != node.Kind || registration.Scope != node.Scope)
                    errors.Add($"$.matchups: controller '{node.ImplementationId}' kind/scope mismatch.");
                foreach (var capability in node.ProvidesCapabilities)
                    if (!registration.Capabilities.Contains(capability))
                        errors.Add($"$.matchups: controller '{node.ImplementationId}' does not provide declared capability '{capability}'.");
                foreach (var capability in node.RequiresCapabilities)
                    if (!(parent?.ProvidesCapabilities.Contains(capability, StringComparer.Ordinal) ?? false) &&
                        !catalog.HasService(capability))
                        errors.Add($"$.matchups: controller '{node.ImplementationId}' requires unavailable capability '{capability}'.");
                controllers.Add(registration);
            }
        }

        if (errors.Count != 0)
            throw new InvalidOperationException("Doctrine cannot be activated: " + string.Join(" ", errors));

        return new BotDoctrineRuntimePlan(definition, initialization, matchup,
            activeBindings, methods.AsReadOnly(), controllers.AsReadOnly());
    }

    private static IEnumerable<(BotDoctrineNode Node, BotDoctrineNode Parent)> Walk(
        IEnumerable<BotDoctrineNode> roots, BotDoctrineNode parent = null)
    {
        foreach (var node in roots)
        {
            yield return (node, parent);
            foreach (var child in Walk(node.Children ?? Array.Empty<BotDoctrineNode>(), node))
                yield return child;
        }
    }
}

/// <summary>
/// Small deterministic event dispatcher for the configured doctrine bindings.
/// It invokes only methods resolved from the trusted host catalog.
/// </summary>
public sealed class BotDoctrineEventDispatcher
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<BotDoctrineMethodRegistration>> _handlers;
    private readonly int _maximumHandlersPerEvent;

    public BotDoctrineEventDispatcher(BotDoctrineRuntimePlan plan, int maximumHandlersPerEvent = 32)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (maximumHandlersPerEvent is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(maximumHandlersPerEvent));
        _maximumHandlersPerEvent = maximumHandlersPerEvent;
        var registrations = plan.MethodBindings
            .GroupBy(binding => binding.EventId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BotDoctrineMethodRegistration>)group
                    .Select(binding => plan.Methods.Single(method => method.Id == binding.MethodId))
                    .ToArray(),
                StringComparer.Ordinal);
        if (registrations.Values.Any(handlers => handlers.Count > _maximumHandlersPerEvent))
            throw new InvalidOperationException($"An event exceeds the configured handler limit of {_maximumHandlersPerEvent}.");
        _handlers = registrations;
    }

    public async ValueTask<int> DispatchAsync(BotDoctrineEvent botEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(botEvent);
        if (string.IsNullOrWhiteSpace(botEvent.EventId))
            throw new ArgumentException("An event ID is required.", nameof(botEvent));
        if (!_handlers.TryGetValue(botEvent.EventId, out var handlers))
            return 0;
        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler.Handler(botEvent, cancellationToken).ConfigureAwait(false);
        }
        return handlers.Count;
    }
}
