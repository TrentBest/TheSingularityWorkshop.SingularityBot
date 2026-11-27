using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Caller-owned facts for one configured production objective. FSM_API owns the
/// registered handle; this context remains owned by the bot runtime.
/// </summary>
public sealed class ProductionStepContext : IStateContext
{
    private readonly Action<ProductionIntent> _emit;
    private Func<bool> _dependenciesSatisfied = static () => true;

    public ProductionStepContext(ProductionStepDefinition definition, Action<ProductionIntent> emit)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        EffectiveDesiredCount = definition.DesiredCount;
        EffectivePriority = definition.Priority;
        _emit = emit ?? throw new ArgumentNullException(nameof(emit));
        Name = $"Production:{definition.Id}";
        IsValid = true;
    }

    public string Name { get; set; }
    public ProductionStepDefinition Definition { get; }
    public bool IsValid { get; set; }
    public uint GameLoop { get; private set; }
    public int CurrentCount { get; private set; }
    public int EffectiveDesiredCount { get; private set; }
    public int EffectivePriority { get; private set; }
    public int InProgressCount { get; private set; }
    public bool ProducerAvailable { get; private set; }
    public bool PrerequisitesSatisfied { get; private set; }
    public bool ResourceRequirementsSatisfied { get; private set; }
    public bool RequestPending { get; private set; }

    /// <summary>
    /// Dependencies are complete only when their observed completed counts meet
    /// their configured targets. In-progress work and submitted commands do not count.
    /// </summary>
    public bool DependenciesSatisfied => _dependenciesSatisfied();
    public bool IsComplete => CurrentCount >= EffectiveDesiredCount;
    public bool CanIssue => IsValid &&
        !IsComplete &&
        CurrentCount + InProgressCount < EffectiveDesiredCount &&
        GameLoop >= Definition.EarliestGameLoop &&
        ProducerAvailable &&
        PrerequisitesSatisfied &&
        ResourceRequirementsSatisfied &&
        DependenciesSatisfied &&
        !RequestPending;

    public int Context_ID => throw new NotImplementedException();

    internal void SetDependencyResolver(Func<bool> resolver) =>
        _dependenciesSatisfied = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>Called by the observation adapter once per game observation.</summary>
    public void ApplyObservation(
        uint gameLoop,
        int currentCount,
        int inProgressCount,
        bool producerAvailable,
        bool prerequisitesSatisfied,
        bool resourceRequirementsSatisfied)
    {
        GameLoop = gameLoop;
        CurrentCount = Math.Max(0, currentCount);
        InProgressCount = Math.Max(0, inProgressCount);
        ProducerAvailable = producerAvailable;
        PrerequisitesSatisfied = prerequisitesSatisfied;
        ResourceRequirementsSatisfied = resourceRequirementsSatisfied;
    }

    /// <summary>Latch a submitted command until observed or rejected.</summary>
    public void MarkSubmitted() => RequestPending = true;

    /// <summary>Release the submission latch after an observed order or rejection.</summary>
    public void ResolveSubmission() => RequestPending = false;

    internal bool TryApplyAuthorizedTarget(int desiredCount, int priority)
    {
        if (desiredCount is < 0 or > ProductionDemandLedger.MaximumDesiredCount || priority is < 0 or > 100)
            return false;
        EffectiveDesiredCount = desiredCount;
        EffectivePriority = priority;
        return true;
    }

    internal void EmitIntent() => _emit(new ProductionIntent(Definition.Id, Definition.Target, Definition.Producer, EffectivePriority));
}

public sealed record ProductionIntent(string StepId, string Target, string Producer, int Priority);
