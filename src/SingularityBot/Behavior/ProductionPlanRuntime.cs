using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Owns plan contexts and FSM_API handles while leaving both context data and
/// SC2 action submission under the host application's control.
/// </summary>
public sealed class ProductionPlanRuntime : IDisposable
{
    private readonly Dictionary<string, (ProductionStepContext Context, FSMHandle Handle)> _steps =
        new(StringComparer.Ordinal);
    private readonly Action<ProductionIntent> _emit;

    public ProductionPlanRuntime(Action<ProductionIntent> emit)
    {
        _emit = emit ?? throw new ArgumentNullException(nameof(emit));
        ProductionBehaviorCatalog.Register();
    }

    public IReadOnlyCollection<ProductionStepContext> Steps =>
        _steps.Values.Select(value => value.Context).ToArray();

    public ProductionStepContext AddStep(ProductionStepDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_steps.ContainsKey(definition.Id))
            throw new InvalidOperationException($"Production step '{definition.Id}' is already registered.");

        var context = new ProductionStepContext(definition, _emit);
        var handle = FSM_API.Create.CreateInstance(
            ProductionBehaviorCatalog.ProductionStepFsm,
            context,
            UnitBehaviorCatalog.ProductionProcessingGroup);
        _steps.Add(definition.Id, (context, handle));
        return context;
    }

    public IReadOnlyList<string> AddPlan(ProductionPlanDefinition plan)
    {
        var errors = ProductionPlanValidator.Validate(plan);
        if (errors.Count != 0)
            return errors;
        if (plan.Steps.Any(step => _steps.ContainsKey(step.Id)))
            return new[] { "$.steps: one or more step ids are already registered in this runtime." };

        foreach (var step in plan.Steps)
            AddStep(step);
        return Array.Empty<string>();
    }

    public IReadOnlyList<string> LoadPlan(string json) => AddPlan(ProductionPlanDefinition.Parse(json));

    public void Update() =>
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProductionProcessingGroup);

    public void Dispose()
    {
        foreach (var step in _steps.Values)
            FSM_API.Interaction.DestroyInstance(step.Handle);
        _steps.Clear();
    }
}
