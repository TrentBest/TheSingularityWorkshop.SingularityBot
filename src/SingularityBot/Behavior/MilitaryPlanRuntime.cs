namespace SingularityBot.Behavior;

/// <summary>
/// Coordinates named military operations through observed lifecycle evidence.
/// It emits semantic intents only; route execution and SC2 actions stay in the
/// existing tactical/action pipeline.
/// </summary>
public sealed class MilitaryPlanRuntime
{
    private readonly Dictionary<string, MilitaryOperationContext> _operations =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<MilitaryOperationContext> Operations =>
        _operations.Values.ToArray();

    public IReadOnlyList<MilitaryOperationContext> ReadyOperations =>
        _operations.Values.Where(operation => operation.CanIssue).ToArray();

    public IReadOnlyList<string> AddPlan(MilitaryPlanDefinition plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var errors = MilitaryPlanValidator.Validate(plan).ToList();
        if (plan.Operations.Any(operation => _operations.ContainsKey(operation.Id)))
            errors.Add("$.operations: one or more operation ids are already registered in this runtime.");
        if (errors.Count != 0) return errors;

        foreach (var definition in plan.Operations)
        {
            var context = new MilitaryOperationContext(definition);
            context.SetDependencyResolver(() => AreDependenciesComplete(definition));
            _operations.Add(definition.Id, context);
        }

        return Array.Empty<string>();
    }

    public bool TryGetOperation(string id, out MilitaryOperationContext context) =>
        _operations.TryGetValue(id, out context);

    public bool TryGetIntent(string operationId, out MilitaryOperationIntent intent)
    {
        intent = null;
        if (!_operations.TryGetValue(operationId, out var operation) || !operation.CanIssue)
            return false;

        var definition = operation.Definition;
        intent = new MilitaryOperationIntent(
            definition.Id,
            definition.Role,
            definition.RouteId,
            definition.ObjectiveLocationId,
            definition.RequiredUnitCount,
            definition.RequiredAbility,
            definition.SynchronizationGroup,
            definition.Completion);
        return true;
    }

    /// <summary>Latch an intent accepted by the host action pipeline.</summary>
    public bool MarkSubmitted(string operationId, uint gameLoop)
    {
        if (!_operations.TryGetValue(operationId, out var operation) || !operation.CanIssue)
            return false;
        operation.MarkSubmitted(gameLoop);
        return true;
    }

    /// <summary>Release a rejected or cancelled submission without claiming execution.</summary>
    public bool ResolveSubmission(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation) || !operation.RequestPending)
            return false;
        operation.ResolveSubmission();
        return true;
    }

    /// <summary>Record observed execution evidence, not merely a successful command submission.</summary>
    public bool ObserveStarted(string operationId, uint gameLoop)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return false;
        return operation.ObserveStarted(gameLoop);
    }

    /// <summary>
    /// Completes an operation only when the caller reports that its configured
    /// completion condition is satisfied by the current game observation.
    /// </summary>
    public bool ObserveCompletion(string operationId, bool completionConditionObserved, uint gameLoop)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return false;
        return operation.ObserveCompletion(completionConditionObserved, gameLoop);
    }

    private bool AreDependenciesComplete(MilitaryOperationDefinition definition)
    {
        foreach (var dependencyId in definition.DependsOn ?? Array.Empty<string>())
        {
            if (!_operations.TryGetValue(dependencyId, out var dependency) || !dependency.IsComplete)
                return false;
        }

        return true;
    }
}

/// <summary>Caller-owned evidence for one named military operation.</summary>
public sealed class MilitaryOperationContext
{
    private Func<bool> _dependenciesComplete = static () => true;

    internal MilitaryOperationContext(MilitaryOperationDefinition definition) =>
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));

    public MilitaryOperationDefinition Definition { get; }
    public bool RequestPending { get; private set; }
    public bool HasExecutionEvidence { get; private set; }
    public bool HasCompletionEvidence { get; private set; }
    public uint LastObservedGameLoop { get; private set; }
    public uint? SubmittedAtGameLoop { get; private set; }
    public uint? StartedAtGameLoop { get; private set; }
    public uint? CompletedAtGameLoop { get; private set; }
    public bool DependenciesComplete => _dependenciesComplete();
    public bool IsStarted => HasExecutionEvidence;
    public bool IsComplete => HasCompletionEvidence;
    public bool CanStart => !RequestPending && !IsStarted && !IsComplete && DependenciesComplete;
    /// <summary>Allows an active operation to issue its next waypoint or ability action until completion.</summary>
    public bool CanIssue => !RequestPending && !IsComplete && DependenciesComplete;

    internal void SetDependencyResolver(Func<bool> resolver) =>
        _dependenciesComplete = resolver ?? throw new ArgumentNullException(nameof(resolver));

    internal void MarkSubmitted(uint gameLoop)
    {
        RequestPending = true;
        SubmittedAtGameLoop = gameLoop;
    }

    internal void ResolveSubmission() => RequestPending = false;

    internal bool ObserveStarted(uint gameLoop)
    {
        if (IsComplete || gameLoop < LastObservedGameLoop)
            return false;
        HasExecutionEvidence = true;
        RequestPending = false;
        StartedAtGameLoop ??= gameLoop;
        LastObservedGameLoop = gameLoop;
        return true;
    }

    internal bool ObserveCompletion(bool completionConditionObserved, uint gameLoop)
    {
        if (!completionConditionObserved || !IsStarted || IsComplete || gameLoop < LastObservedGameLoop)
            return false;
        HasCompletionEvidence = true;
        RequestPending = false;
        CompletedAtGameLoop = gameLoop;
        LastObservedGameLoop = gameLoop;
        return true;
    }
}

public sealed record MilitaryOperationIntent(
    string OperationId,
    MilitaryForceRole Role,
    string RouteId,
    string ObjectiveLocationId,
    int RequiredUnitCount,
    string RequiredAbility,
    string SynchronizationGroup,
    MilitaryCompletionCondition Completion);
