namespace SingularityBot.Behavior;

/// <summary>
/// Fluent authoring surface for a single production plan. Buildings and units
/// are separate authoring lanes but compile into the same validated plan/runtime.
/// </summary>
public sealed class ProductionPlanBuilder
{
    private readonly List<ProductionStepDefinition> _steps = new();
    private readonly HashSet<string> _barriers = new(StringComparer.Ordinal);

    public ProductionPlanBuilder(string id, string name, string race = "Terran")
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("A plan id is required.", nameof(id)) : id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A plan name is required.", nameof(name)) : name;
        Race = race ?? string.Empty;
        Buildings = new ProductionLineBuilder(this, ProductionLane.Buildings);
        Units = new ProductionLineBuilder(this, ProductionLane.Units);
    }

    public string Id { get; }
    public string Name { get; }
    public string Race { get; }
    public ProductionLineBuilder Buildings { get; }
    public ProductionLineBuilder Units { get; }

    /// <summary>
    /// Adds a named step gated by a completed prerequisite, then establishes
    /// that prerequisite as a barrier for subsequently authored steps.
    /// </summary>
    public ProductionPlanBuilder WhenComplete(
        string stepId,
        string prerequisiteStepId,
        ProductionStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (string.IsNullOrWhiteSpace(stepId)) throw new ArgumentException("A step id is required.", nameof(stepId));
        if (string.IsNullOrWhiteSpace(prerequisiteStepId)) throw new ArgumentException("A prerequisite id is required.", nameof(prerequisiteStepId));

        var dependencies = (step.DependsOn ?? Array.Empty<string>())
            .Append(prerequisiteStepId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Add(step with { Id = stepId, DependsOn = dependencies });
        _barriers.Add(prerequisiteStepId);
        return this;
    }

    internal void Add(ProductionStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var dependencies = (step.DependsOn ?? Array.Empty<string>())
            .Concat(_barriers)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _steps.Add(step with { DependsOn = dependencies });
    }

    public ProductionPlanDefinition Build()
    {
        var plan = new ProductionPlanDefinition
        {
            Id = Id,
            Name = Name,
            Race = Race,
            Steps = _steps.ToArray()
        };
        var errors = ProductionPlanValidator.Validate(plan);
        if (errors.Count != 0)
            throw new InvalidOperationException("Production plan is invalid: " + string.Join(" ", errors));
        return plan;
    }
}

/// <summary>Authoring lane for either structure construction or unit assembly.</summary>
public sealed class ProductionLineBuilder
{
    private readonly ProductionPlanBuilder _owner;
    private readonly ProductionLane _lane;

    internal ProductionLineBuilder(ProductionPlanBuilder owner, ProductionLane lane)
    {
        _owner = owner;
        _lane = lane;
    }

    public ProductionLineBuilder Add(
        string id,
        string target,
        string producer,
        int desiredCount = 1,
        int priority = 50,
        int earliestGameLoop = 0) =>
        Add(new ProductionStepDefinition(id, target, producer, desiredCount, earliestGameLoop, priority, Lane: _lane));

    public ProductionLineBuilder Add(ProductionStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _owner.Add(step with { Lane = _lane });
        return this;
    }


    /// <summary>Inserts a completion barrier and returns to the plan so another lane can continue authoring.</summary>
    public ProductionPlanBuilder WhenComplete(
        string stepId,
        string prerequisiteStepId,
        ProductionStepDefinition step) =>
        _owner.WhenComplete(stepId, prerequisiteStepId, step with { Lane = _lane });

    /// <summary>Adds a target-count objective; issuance stops when completed plus in-progress reaches the count.</summary>
    public ProductionLineBuilder AddUntil(
        string id,
        string target,
        string producer,
        int desiredCount,
        int priority = 50,
        int earliestGameLoop = 0) =>
        Add(id, target, producer, desiredCount, priority, earliestGameLoop);
}
