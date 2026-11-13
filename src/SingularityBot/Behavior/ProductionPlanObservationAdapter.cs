using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Reconciles named production objectives against the current observation,
/// shared build-job evidence, authoritative unit costs and explicit recipes.
/// It supplies facts to ProductionPlanRuntime; it does not schedule or issue
/// actions independently of the existing bot pipeline.
/// </summary>
public sealed class ProductionPlanObservationAdapter
{
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
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(buildJobs);
        ArgumentNullException.ThrowIfNull(unavailableProducerTags);
        ArgumentNullException.ThrowIfNull(recipes);
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
            var unassociatedJobCount = activeJobs.Count(job => !job.ResultUnitTag.HasValue);
            var inProgressCount = observedInProgressCount + activeJobs.Length;

            var producers = liveSelf.Where(unit =>
                unit.UnitKind.Equals(definition.Producer, StringComparison.OrdinalIgnoreCase)).ToArray();
            var producerAvailable = definition.Lane == ProductionLane.Buildings
                ? producers.Any(unit => !unit.HasCargo && !unavailableProducerTags.Contains(unit.Tag))
                : producers.Any(unit => !unit.HasOrders && !unavailableProducerTags.Contains(unit.Tag));

            var prerequisitesSatisfied = recipes.TryGetValue(target.Name, out var recipe) &&
                string.Equals(recipe.ProducerKind, definition.Producer, StringComparison.OrdinalIgnoreCase) &&
                (recipe.PrerequisiteStructures ?? Array.Empty<string>()).All(prerequisite =>
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
