namespace SingularityBot.Behavior;

/// <summary>
/// Match-level authoring surface. It composes existing FSM_API-backed
/// production plans with declarative strategy-adoption policies; it is not
/// an alternative state-machine implementation.
/// </summary>
public sealed class MatchBuilder
{
    private ProductionPlanDefinition _opening;
    private readonly List<StrategyTransitionDefinition> _midGame = new();
    private readonly List<StrategyTransitionDefinition> _lateGame = new();

    public MatchBuilder(string id, string name, string race = "Terran")
    {
        Id = Required(id, nameof(id));
        Name = Required(name, nameof(name));
        Race = string.IsNullOrWhiteSpace(race) ? string.Empty : race.Trim();
    }

    public string Id { get; }
    public string Name { get; }
    public string Race { get; }

    /// <summary>The deterministic opening plan, executed by ProductionPlanRuntime/FSM_API.</summary>
    public MatchBuilder WithOpening(ProductionPlanDefinition opening)
    {
        _opening = opening ?? throw new ArgumentNullException(nameof(opening));
        return this;
    }

    /// <summary>
    /// Adds a conditional midgame strategy that may be adopted when its
    /// evidence, hard guards, advantage and commitment cooldown all pass.
    /// </summary>
    public MatchBuilder WithMidGame(StrategyTransitionDefinition transition)
    {
        _midGame.Add(transition ?? throw new ArgumentNullException(nameof(transition)));
        return this;
    }

    /// <summary>Registers a conditional late-game strategy adoption policy.</summary>
    public MatchBuilder WithLateGame(StrategyTransitionDefinition transition)
    {
        _lateGame.Add(transition ?? throw new ArgumentNullException(nameof(transition)));
        return this;
    }

    public MatchDefinition Build()
    {
        if (_opening is null)
            throw new InvalidOperationException("A match requires an opening production plan.");

        var openingErrors = ProductionPlanValidator.Validate(_opening);
        if (openingErrors.Count != 0)
            throw new InvalidOperationException("Opening plan is invalid: " + string.Join(" ", openingErrors));

        var transitions = _midGame.Concat(_lateGame).ToArray();
        var duplicate = transitions.GroupBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate strategy transition id '{duplicate.Key}'.");

        var errors = transitions.SelectMany(item => item.Validate()).ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException("Match strategy is invalid: " + string.Join(" ", errors));

        return new MatchDefinition(Id, Name, Race, _opening,
            _midGame.ToArray(), _lateGame.ToArray());
    }

    private static string Required(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameter)
            : value.Trim();
}

/// <summary>Immutable match composition produced by MatchBuilder.</summary>
public sealed record MatchDefinition(
    string Id,
    string Name,
    string Race,
    ProductionPlanDefinition Opening,
    IReadOnlyList<StrategyTransitionDefinition> MidGame,
    IReadOnlyList<StrategyTransitionDefinition> LateGame);

/// <summary>
/// A candidate strategic adoption. Evidence requirements and hard guards are
/// deliberately separate from the weighted advantage threshold: safety and
/// executability cannot be traded away for a high score.
/// </summary>
public sealed record StrategyTransitionDefinition(
    string Id,
    string TargetStrategy,
    IReadOnlyList<StrategyEvidenceRequirement> Evidence,
    IReadOnlyList<string> RequiredGuards,
    double MinimumAdvantage,
    uint EvaluationHorizonGameLoops,
    uint CooldownGameLoops)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Id)) errors.Add("transition id is required.");
        if (string.IsNullOrWhiteSpace(TargetStrategy)) errors.Add($"transition '{Id}' target strategy is required.");
        if (Evidence is null || Evidence.Count == 0) errors.Add($"transition '{Id}' requires at least one evidence requirement.");
        else
        {
            foreach (var requirement in Evidence)
                errors.AddRange(requirement.Validate(Id));
        }
        if (RequiredGuards is null || RequiredGuards.Any(string.IsNullOrWhiteSpace))
            errors.Add($"transition '{Id}' contains an empty hard-guard id.");
        if (!double.IsFinite(MinimumAdvantage) || MinimumAdvantage < 0)
            errors.Add($"transition '{Id}' minimum advantage must be finite and non-negative.");
        if (EvaluationHorizonGameLoops == 0)
            errors.Add($"transition '{Id}' evaluation horizon must be positive.");
        return errors;
    }
}

/// <summary>A confidence-rated, time-bounded observation required by a transition.</summary>
public sealed record StrategyEvidenceRequirement(
    string EvidenceId,
    string Subject,
    float MinimumConfidence,
    uint MaximumAgeGameLoops,
    bool RequiresObservedFact = false)
{
    internal IReadOnlyList<string> Validate(string transitionId)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(EvidenceId)) errors.Add($"transition '{transitionId}' has an empty evidence id.");
        if (string.IsNullOrWhiteSpace(Subject)) errors.Add($"transition '{transitionId}' evidence '{EvidenceId}' has no subject.");
        if (!float.IsFinite(MinimumConfidence) || MinimumConfidence is < 0 or > 1)
            errors.Add($"transition '{transitionId}' evidence '{EvidenceId}' confidence must be between 0 and 1.");
        if (MaximumAgeGameLoops == 0)
            errors.Add($"transition '{transitionId}' evidence '{EvidenceId}' maximum age must be positive.");
        return errors;
    }
}

/// <summary>One observed fact; inferred facts must carry an explicit confidence.</summary>
public sealed record StrategyEvidence(
    string EvidenceId,
    string Subject,
    float Confidence,
    uint ObservedAtGameLoop,
    bool IsObservedFact);

/// <summary>Live inputs needed to evaluate one strategic transition candidate.</summary>
public sealed record StrategyTransitionContext(
    uint GameLoop,
    uint? LastStrategyAdoptionGameLoop,
    IReadOnlyCollection<StrategyEvidence> Evidence,
    IReadOnlySet<string> SatisfiedHardGuards,
    double EstimatedAdvantage);

public enum StrategyTransitionStatus
{
    Eligible,
    MissingEvidence,
    StaleEvidence,
    InsufficientConfidence,
    HardGuardFailed,
    CooldownActive,
    InsufficientAdvantage
}

public sealed record StrategyTransitionEvaluation(
    string TransitionId,
    StrategyTransitionStatus Status,
    IReadOnlyList<string> Reasons)
{
    public bool IsEligible => Status == StrategyTransitionStatus.Eligible;
}

/// <summary>
/// Pure policy gate for a candidate adoption. It does not mutate match state
/// or issue game commands; the accepted transition is handed to an FSM_API
/// strategy runtime by the host.
/// </summary>
public static class StrategyTransitionEvaluator
{
    public static StrategyTransitionEvaluation Evaluate(
        StrategyTransitionDefinition transition,
        StrategyTransitionContext context)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(context);
        var invalid = transition.Validate();
        if (invalid.Count != 0)
            throw new ArgumentException("Invalid transition: " + string.Join(" ", invalid), nameof(transition));

        var reasons = new List<string>();
        foreach (var required in transition.Evidence)
        {
            var evidence = context.Evidence.FirstOrDefault(item =>
                string.Equals(item.EvidenceId, required.EvidenceId, StringComparison.Ordinal) &&
                string.Equals(item.Subject, required.Subject, StringComparison.Ordinal));
            if (evidence is null)
            {
                reasons.Add($"Missing evidence '{required.EvidenceId}' for '{required.Subject}'.");
                continue;
            }

            var age = context.GameLoop >= evidence.ObservedAtGameLoop
                ? context.GameLoop - evidence.ObservedAtGameLoop
                : uint.MaxValue;
            if (age > required.MaximumAgeGameLoops)
            {
                reasons.Add($"Evidence '{required.EvidenceId}' is stale ({age} game loops old).");
                continue;
            }
            if (required.RequiresObservedFact && !evidence.IsObservedFact)
            {
                reasons.Add($"Evidence '{required.EvidenceId}' must be directly observed.");
                continue;
            }
            if (!float.IsFinite(evidence.Confidence) ||
                evidence.Confidence < required.MinimumConfidence)
                reasons.Add($"Evidence '{required.EvidenceId}' confidence is below {required.MinimumConfidence:F2}.");
        }

        if (reasons.Count != 0)
        {
            var status = reasons.Any(reason => reason.StartsWith("Missing evidence", StringComparison.Ordinal))
                ? StrategyTransitionStatus.MissingEvidence
                : reasons.Any(reason => reason.Contains("stale", StringComparison.Ordinal))
                    ? StrategyTransitionStatus.StaleEvidence
                    : StrategyTransitionStatus.InsufficientConfidence;
            return new(transition.Id, status, reasons);
        }

        var missingGuards = transition.RequiredGuards
            .Where(guard => !context.SatisfiedHardGuards.Contains(guard))
            .ToArray();
        if (missingGuards.Length != 0)
            return new(transition.Id, StrategyTransitionStatus.HardGuardFailed,
                missingGuards.Select(guard => $"Hard guard '{guard}' is not satisfied.").ToArray());

        if (context.LastStrategyAdoptionGameLoop is uint last &&
            (context.GameLoop < last ||
             context.GameLoop - last < transition.CooldownGameLoops))
            return new(transition.Id, StrategyTransitionStatus.CooldownActive,
                new[] { "Strategy adoption cooldown has not elapsed." });

        if (!double.IsFinite(context.EstimatedAdvantage) ||
            context.EstimatedAdvantage < transition.MinimumAdvantage)
            return new(transition.Id, StrategyTransitionStatus.InsufficientAdvantage,
                new[] { $"Estimated advantage is below {transition.MinimumAdvantage:F2}." });

        return new(transition.Id, StrategyTransitionStatus.Eligible, Array.Empty<string>());
    }
}
