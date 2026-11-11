namespace SingularityBot.Behavior;

public enum StrategicJobKind
{
    Scout,
    ProduceUnit,
    BuildStructure,
    ResearchUpgrade,
    DefendLocation,
    MaintainEconomy,
    Repair
}

public enum StrategicJobSource
{
    Advisor,
    CompositionGoal
}

/// <summary>
/// A replaceable statement of work owned by its publisher. The planner consumes
/// current postings; it does not own their strategic meaning or lifecycle.
/// </summary>
public sealed record StrategicJobPosting(
    string Id,
    StrategicJobSource Source,
    string Owner,
    StrategicJobKind Kind,
    string Target,
    int Quantity,
    int Priority,
    uint CreatedAtGameLoop,
    uint UpdatedAtGameLoop,
    uint? ExpiresAtGameLoop = null,
    IReadOnlyDictionary<string, string>? Constraints = null)
{
    public bool IsActiveAt(uint gameLoop) =>
        Quantity > 0 && (ExpiresAtGameLoop is null || gameLoop <= ExpiresAtGameLoop.Value);
}

/// <summary>
/// Advisor-owned job market. Publishers may only replace or withdraw their own
/// postings; the planner receives a read-only snapshot of active work.
/// </summary>
public sealed class AdvisorJobBoard
{
    private readonly Dictionary<string, StrategicJobPosting> _postings = new(StringComparer.Ordinal);

    public StrategicJobPosting Upsert(
        AdvisorDomain owner,
        string id,
        StrategicJobKind kind,
        string target,
        int quantity,
        int priority,
        uint gameLoop,
        uint? expiresAtGameLoop = null,
        IReadOnlyDictionary<string, string>? constraints = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable posting ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("A target is required.", nameof(target));
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        var ownerName = owner.ToString();
        if (_postings.TryGetValue(id, out var existing) &&
            (existing.Source != StrategicJobSource.Advisor || !string.Equals(existing.Owner, ownerName, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Posting '{id}' is owned by {existing.Owner} and cannot be replaced by {ownerName}.");

        var posting = new StrategicJobPosting(
            id, StrategicJobSource.Advisor, ownerName, kind, target, quantity, priority,
            existing?.CreatedAtGameLoop ?? gameLoop, gameLoop, expiresAtGameLoop, constraints);
        _postings[id] = posting;
        return posting;
    }

    public bool Withdraw(AdvisorDomain owner, string id)
    {
        if (!_postings.TryGetValue(id, out var existing)) return false;
        if (existing.Source != StrategicJobSource.Advisor ||
            !string.Equals(existing.Owner, owner.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException($"Advisor {owner} does not own posting '{id}'.");
        return _postings.Remove(id);
    }

    public StrategicJobPosting UpsertComposition(
        string goalId,
        string id,
        StrategicJobKind kind,
        string target,
        int quantity,
        int priority,
        uint gameLoop,
        IReadOnlyDictionary<string, string>? constraints = null)
    {
        if (string.IsNullOrWhiteSpace(goalId)) throw new ArgumentException("A goal ID is required.", nameof(goalId));
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable posting ID is required.", nameof(id));
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (_postings.TryGetValue(id, out var existing) &&
            (existing.Source != StrategicJobSource.CompositionGoal || !string.Equals(existing.Owner, goalId, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Posting '{id}' is owned by {existing.Owner} and cannot be replaced by goal {goalId}.");

        var posting = new StrategicJobPosting(
            id, StrategicJobSource.CompositionGoal, goalId, kind, target, quantity, priority,
            existing?.CreatedAtGameLoop ?? gameLoop, gameLoop, null, constraints);
        _postings[id] = posting;
        return posting;
    }

    public int WithdrawComposition(string goalId)
    {
        var owned = _postings.Values
            .Where(posting => posting.Source == StrategicJobSource.CompositionGoal &&
                              string.Equals(posting.Owner, goalId, StringComparison.Ordinal))
            .Select(posting => posting.Id)
            .ToArray();
        foreach (var id in owned) _postings.Remove(id);
        return owned.Length;
    }

    public IReadOnlyList<StrategicJobPosting> GetActive(uint gameLoop) =>
        _postings.Values.Where(posting => posting.IsActiveAt(gameLoop))
            .OrderByDescending(posting => posting.Priority)
            .ThenBy(posting => posting.CreatedAtGameLoop)
            .ThenBy(posting => posting.Id, StringComparer.Ordinal)
            .ToArray();
}

/// <summary>A desired standing force, independent of the way jobs are generated.</summary>
public sealed record CompositionUnitTarget(
    string UnitKind,
    int TargetCount,
    double ReplacementRatePerMinute = 0);

public sealed class CompositionGoalDefinition
{
    public int Version { get; init; } = 1;
    public string GoalId { get; init; } = "primary-force";
    public string Name { get; init; } = "Primary force";
    public double InitialBuildOutMinutes { get; init; } = 10;
    public IReadOnlyList<CompositionUnitTarget> Units { get; init; } = Array.Empty<CompositionUnitTarget>();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add("$.version: only version 1 is supported.");
        if (string.IsNullOrWhiteSpace(GoalId)) errors.Add("$.goalId: is required.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("$.name: is required.");
        if (!double.IsFinite(InitialBuildOutMinutes) || InitialBuildOutMinutes <= 0)
            errors.Add("$.initialBuildOutMinutes: must be a finite value greater than zero.");
        if (Units.Count == 0) errors.Add("$.units: at least one unit target is required.");
        for (var i = 0; i < Units.Count; i++)
        {
            var unit = Units[i];
            if (string.IsNullOrWhiteSpace(unit.UnitKind)) errors.Add($"$.units[{i}].unitKind: is required.");
            if (unit.TargetCount < 0) errors.Add($"$.units[{i}].targetCount: cannot be negative.");
            if (!double.IsFinite(unit.ReplacementRatePerMinute) || unit.ReplacementRatePerMinute < 0)
                errors.Add($"$.units[{i}].replacementRatePerMinute: must be finite and non-negative.");
        }
        if (Units.GroupBy(unit => unit.UnitKind, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            errors.Add("$.units: each unit kind may appear only once.");
        return errors;
    }
}

/// <summary>
/// Data-driven production knowledge. The caller must populate this from
/// authoritative game data or explicitly reviewed strategy metadata.
/// </summary>
public sealed record ProductionRecipe(
    string UnitKind,
    string ProducerKind,
    double ProductionSeconds,
    string? RequiredAddonKind = null,
    IReadOnlyList<string>? PrerequisiteStructures = null);

public sealed record CompositionPlanResult(
    IReadOnlyList<StrategicJobPosting> Postings,
    IReadOnlyList<string> Errors);

/// <summary>
/// Compiles a force target into the same job-posting market used by advisors.
/// It calculates throughput capacity from explicit recipe metadata; it never
/// guesses unit costs, build times, prerequisites or game-data identifiers.
/// </summary>
public static class CompositionGoalCompiler
{
    public static CompositionPlanResult Compile(
        CompositionGoalDefinition goal,
        IReadOnlyDictionary<string, ProductionRecipe> recipes,
        AdvisorJobBoard board,
        uint gameLoop,
        int priority = 50)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(board);
        var errors = goal.Validate().ToList();
        foreach (var target in goal.Units.Where(unit => unit.TargetCount > 0))
        {
            if (!recipes.ContainsKey(target.UnitKind))
                errors.Add($"$.units[{target.UnitKind}]: no production recipe is available.");
        }
        if (errors.Count > 0) return new CompositionPlanResult(Array.Empty<StrategicJobPosting>(), errors);

        foreach (var target in goal.Units.Where(unit => unit.TargetCount > 0))
        {
            var recipe = recipes[target.UnitKind];
            if (!double.IsFinite(recipe.ProductionSeconds) || recipe.ProductionSeconds <= 0)
                errors.Add($"Recipe for {target.UnitKind} must have a finite production time greater than zero.");
        }
        if (errors.Count > 0) return new CompositionPlanResult(Array.Empty<StrategicJobPosting>(), errors);

        board.WithdrawComposition(goal.GoalId);
        var result = new List<StrategicJobPosting>();
        var producerDemand = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var addonDemand = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var prerequisites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in goal.Units.Where(unit => unit.TargetCount > 0))
        {
            var recipe = recipes[target.UnitKind];
            var requiredUnitsPerMinute = target.TargetCount / goal.InitialBuildOutMinutes +
                                        target.ReplacementRatePerMinute;
            var facilities = requiredUnitsPerMinute * recipe.ProductionSeconds / 60d;
            producerDemand[recipe.ProducerKind] = producerDemand.GetValueOrDefault(recipe.ProducerKind) + facilities;
            if (!string.IsNullOrWhiteSpace(recipe.RequiredAddonKind))
                addonDemand[recipe.RequiredAddonKind] = addonDemand.GetValueOrDefault(recipe.RequiredAddonKind) + facilities;
            foreach (var prerequisite in recipe.PrerequisiteStructures ?? Array.Empty<string>())
                prerequisites.Add(prerequisite);

            result.Add(board.UpsertComposition(
                goal.GoalId, $"goal:{goal.GoalId}:unit:{target.UnitKind}",
                StrategicJobKind.ProduceUnit, target.UnitKind, target.TargetCount,
                priority, gameLoop,
                new Dictionary<string, string>
                {
                    ["replacementRatePerMinute"] = target.ReplacementRatePerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["initialBuildOutMinutes"] = goal.InitialBuildOutMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
        }

        foreach (var (producer, demand) in producerDemand)
            result.Add(board.UpsertComposition(goal.GoalId, $"goal:{goal.GoalId}:producer:{producer}",
                StrategicJobKind.BuildStructure, producer, (int)Math.Ceiling(demand), priority, gameLoop));

        foreach (var (addon, demand) in addonDemand)
            result.Add(board.UpsertComposition(goal.GoalId, $"goal:{goal.GoalId}:addon:{addon}",
                StrategicJobKind.BuildStructure, addon, (int)Math.Ceiling(demand), priority, gameLoop));

        foreach (var prerequisite in prerequisites.Where(name => !producerDemand.ContainsKey(name)))
            result.Add(board.UpsertComposition(goal.GoalId, $"goal:{goal.GoalId}:prerequisite:{prerequisite}",
                StrategicJobKind.BuildStructure, prerequisite, 1, priority + 5, gameLoop));

        return new CompositionPlanResult(result, Array.Empty<string>());
    }
}

/// <summary>
/// Minimal planner view: merges all publishers' current postings and ranks them.
/// Dependency/resource/unit arbitration is a subsequent planner stage.
/// </summary>
public sealed class StrategicPlanner
{
    public IReadOnlyList<StrategicJobPosting> GetPlan(AdvisorJobBoard board, uint gameLoop)
    {
        ArgumentNullException.ThrowIfNull(board);
        return board.GetActive(gameLoop);
    }
}
