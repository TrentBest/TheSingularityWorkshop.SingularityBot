using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Caller-owned facts for one configured production objective. FSM_API owns the
/// registered handle; this context remains owned by the bot runtime.
/// </summary>
public sealed class ProductionStepContext : IStateContext
{
    private readonly Action<ProductionIntent> _emit;

    public ProductionStepContext(ProductionStepDefinition definition, Action<ProductionIntent> emit)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _emit = emit ?? throw new ArgumentNullException(nameof(emit));
        Name = $"Production:{definition.Id}";
        IsValid = true;
    }

    public string Name { get; set; }
    public ProductionStepDefinition Definition { get; }
    public bool IsValid { get; set; }
    public uint GameLoop { get; private set; }
    public int CurrentCount { get; private set; }
    public int InProgressCount { get; private set; }
    public bool ProducerAvailable { get; private set; }
    public bool PrerequisitesSatisfied { get; private set; }
    public bool ResourceRequirementsSatisfied { get; private set; }
    public bool RequestPending { get; private set; }
    public bool IsComplete => CurrentCount >= Definition.DesiredCount;
    public bool CanIssue => IsValid &&
        !IsComplete &&
        CurrentCount + InProgressCount < Definition.DesiredCount &&
        GameLoop >= Definition.EarliestGameLoop &&
        ProducerAvailable &&
        PrerequisitesSatisfied &&
        ResourceRequirementsSatisfied &&
        !RequestPending;

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

    internal void EmitIntent() => _emit(new ProductionIntent(Definition.Id, Definition.Target, Definition.Producer, Definition.Priority));
}

public sealed record ProductionIntent(string StepId, string Target, string Producer, int Priority);
