using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Observation-only production facts. Unlike ProductionRecipe, this contract
/// deliberately contains no production duration and is not consumed by the
/// strategic composition compiler.
/// </summary>
public sealed record ProductionObservationRule(
    string Target,
    string Producer,
    IReadOnlyList<string>? PrerequisiteStructures = null);

/// <summary>
/// Reconciles named production objectives against the current observation,
/// shared build-job evidence, authoritative unit costs and explicit observation
/// rules. It supplies facts to ProductionPlanRuntime; it does not schedule or
/// issue actions independently of the existing bot pipeline.
/// </summary>
public sealed class ProductionPlanObservationAdapter
{
    /// <summary>
    /// Compatibility overload for callers that already have complete strategic
    /// recipes. Runtime eligibility only consumes the recipe's producer and
    /// prerequisite fields; production duration remains owned by composition.
    /// </summary>
    public void Apply(
        ProductionPlanRuntime runtime,
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        BuildJobRuntime buildJobs,
        IReadOnlySet<ulong> unavailableProducerTags,
        IReadOnlyDictionary<string, ProductionRecipe> recipes,
        uint reservedMinerals = 0,
        uint reservedVespene = 0,
        float reservedFood = 0)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        var rules = recipes.Values
            .Where(recipe => !string.IsNullOrWhiteSpace(recipe.UnitKind))
            .GroupBy(recipe => recipe.UnitKind, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var recipe = group.First();
                    return new ProductionObservationRule(
                        recipe.UnitKind, recipe.ProducerKind, recipe.PrerequisiteStructures);
                },
                StringComparer.OrdinalIgnoreCase);
        Apply(runtime, observation, gameData, buildJobs, unavailableProducerTags,
            rules, reservedMinerals, reservedVespene, reservedFood);
    }

    /// <summary>
    /// Applies match-observation facts using rules that do not require invented
    /// production-time metadata.
    /// </summary>
    public void Apply(
        ProductionPlanRuntime runtime,
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        BuildJobRuntime buildJobs,
        IReadOnlySet<ulong> unavailableProducerTags,
        IReadOnlyDictionary<string, ProductionObservationRule> rules,
        uint reservedMinerals = 0,
        uint reservedVespene = 0,
        float reservedFood = 0)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(buildJobs);
        ArgumentNullException.ThrowIfNull(unavailableProducerTags);
        ArgumentNullException.ThrowIfNull(rules);
        if (!float.IsFinite(reservedFood) || reservedFood < 0)
            throw new ArgumentOutOfRangeException(nameof(reservedFood));

        var liveSelf = observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();

        foreach (var step in runtime.Steps)
        {
            var definition = step.Definition;
            if (!gameData.TryGetUniqueUnit(definition.Target, out var target))
            {
                step.ApplyObservation(
                    observation.GameLoop, 0, 0, false, false, false);
                continue;
            }

            var targetUnits = liveSelf.Where(unit =>
                unit.UnitKind.Equals(target.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            var completedCount = targetUnits.Count(unit => unit.BuildProgress >= 1f);
            var activeJobs = buildJobs.Jobs.Where(job =>
                job.BuildableKind.Equals(target.Name, StringComparison.OrdinalIgnoreCase) &&
                !job.IsCompleted && !job.IsFailed).ToArray();
            var associatedResultTags = activeJobs
                .Where(job => job.ResultUnitTag.HasValue)
                .Select(job => job.ResultUnitTag!.Value)
                .ToHashSet();
            var observedInProgressCount = targetUnits.Count(unit =>
                unit.BuildProgress < 1f && !associatedResultTags.Contains(unit.Tag));
            var inProgressCount = observedInProgressCount + activeJobs.Length;

            var producers = liveSelf.Where(unit =>
                unit.UnitKind.Equals(definition.Producer, StringComparison.OrdinalIgnoreCase)).ToArray();
            var producerAvailable = definition.Lane == ProductionLane.Buildings
                ? producers.Any(unit => !unit.HasCargo && !unavailableProducerTags.Contains(unit.Tag))
                : producers.Any(unit => !unit.HasOrders && !unavailableProducerTags.Contains(unit.Tag));

            var prerequisitesSatisfied = rules.TryGetValue(target.Name, out var rule) &&
                string.Equals(rule.Producer, definition.Producer, StringComparison.OrdinalIgnoreCase) &&
                (rule.PrerequisiteStructures ?? Array.Empty<string>()).All(prerequisite =>
                    liveSelf.Any(unit =>
                        unit.UnitKind.Equals(prerequisite, StringComparison.OrdinalIgnoreCase) &&
                        unit.BuildProgress >= 1f));

            var availableMinerals = observation.Minerals >= (ulong)target.MineralCost + reservedMinerals;
            var availableVespene = observation.Vespene >= (ulong)target.VespeneCost + reservedVespene;
            var availableFood = observation.FoodCap >= observation.FoodUsed &&
                observation.FoodCap - observation.FoodUsed + 0.0001f >= target.FoodRequired + reservedFood;

            step.ApplyObservation(
                observation.GameLoop,
                completedCount,
                inProgressCount,
                producerAvailable,
                prerequisitesSatisfied,
                availableMinerals && availableVespene && availableFood);
        }
    }
}
