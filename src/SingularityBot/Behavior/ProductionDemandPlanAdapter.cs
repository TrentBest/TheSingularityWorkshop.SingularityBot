using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>
/// Projects strategic unit demands onto stable, race-validated production objectives.
/// This adapter never emits actions; the host still owns observation and submission.
/// Building demands are deliberately excluded until the construction lane has a
/// complete placement, worker-reservation, and build-job reconciliation path.
/// </summary>
public sealed class ProductionDemandPlanAdapter
{
    private readonly HashSet<string> _registeredStepIds = new(StringComparer.Ordinal);

    public ProductionDemandPlanSyncResult Apply(
        ProductionDemandLedger demands,
        ProductionPlanRuntime runtime,
        Sc2GameDataCatalog gameData,
        Sc2RaceProductionProfileCatalog profiles,
        Race actualRace)
    {
        ArgumentNullException.ThrowIfNull(demands);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(profiles);

        var created = 0;
        var updated = 0;
        var rejected = new List<string>();
        var activeStepIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var demand in demands.Snapshot().Values.OrderBy(item => item.UnitTypeId))
        {
            if (!gameData.UnitsById.TryGetValue(demand.UnitTypeId, out var target) ||
                !string.Equals(target.Name, demand.UnitTypeName, StringComparison.OrdinalIgnoreCase))
            {
                rejected.Add($"Unit type {demand.UnitTypeId} ({demand.UnitTypeName}) does not resolve to matching authoritative match data.");
                continue;
            }

            if (demand.DesiredCount == 0)
            {
                var existingId = GetStepId(demand.UnitTypeId);
                if (runtime.TryGetStep(existingId, out var zeroStep))
                {
                    if (!IsMatchingObjective(zeroStep, target.Name, zeroStep.Definition.Producer))
                        rejected.Add($"Existing objective '{existingId}' conflicts with the authoritative unit identity.");
                    else if (!runtime.TrySetDesiredTarget(existingId, 0, demand.Priority))
                        rejected.Add($"Existing objective '{existingId}' rejected its zero target.");
                }
                activeStepIds.Add(existingId);
                continue;
            }

            var matches = profiles.Profiles
                .Where(profile => profile.Race == actualRace &&
                    string.Equals(profile.TargetUnit, target.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length != 1)
            {
                rejected.Add($"Demand '{target.Name}' has {matches.Length} trusted production profiles for race {actualRace}; expected exactly one.");
                continue;
            }

            var selected = matches[0];
            if (!profiles.TryResolveGameData(selected.ModeId, actualRace, gameData,
                    out var resolvedProfile, out var resolvedTarget, out var producer, out var error))
            {
                rejected.Add($"Demand '{target.Name}' is not actionable: {error}");
                continue;
            }

            if (resolvedTarget.UnitId != demand.UnitTypeId ||
                resolvedProfile.UnitsPerOrder <= 0)
            {
                rejected.Add($"Demand '{target.Name}' does not match the uniquely resolved production profile.");
                continue;
            }

            var stepId = GetStepId(demand.UnitTypeId);
            activeStepIds.Add(stepId);
            if (runtime.TryGetStep(stepId, out var existingStep))
            {
                if (!IsMatchingObjective(existingStep, resolvedTarget.Name, producer.Name))
                    rejected.Add($"Existing objective '{stepId}' conflicts with the resolved target, producer, or lane.");
                else if (!runtime.TrySetDesiredTarget(stepId, demand.DesiredCount, demand.Priority))
                    rejected.Add($"Existing objective '{stepId}' rejected the updated target.");
                else
                    updated++;
                continue;
            }

            runtime.AddStep(new ProductionStepDefinition(
                stepId,
                resolvedTarget.Name,
                producer.Name,
                demand.DesiredCount,
                0,
                demand.Priority,
                Lane: ProductionLane.Units));
            _registeredStepIds.Add(stepId);
            created++;
        }

        // A demand removed by Clear() must not leave an old objective active.
        foreach (var staleStepId in _registeredStepIds.Where(id => !activeStepIds.Contains(id)).ToArray())
        {
            if (runtime.TryGetStep(staleStepId, out _) &&
                !runtime.TrySetDesiredTarget(staleStepId, 0, 0))
                rejected.Add($"Stale objective '{staleStepId}' could not be disabled.");
        }

        return new ProductionDemandPlanSyncResult(created, updated, rejected);
    }

    private static bool IsMatchingObjective(
        ProductionStepContext step,
        string target,
        string producer) =>
        step.Definition.Lane == ProductionLane.Units &&
        string.Equals(step.Definition.Target, target, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(step.Definition.Producer, producer, StringComparison.OrdinalIgnoreCase);

    public static string GetStepId(uint unitTypeId) => $"demand.units.{unitTypeId}";
}

/// <summary>Summary of one ledger-to-plan synchronization pass.</summary>
public sealed record ProductionDemandPlanSyncResult(
    int CreatedObjectives,
    int UpdatedObjectives,
    IReadOnlyList<string> Rejections);
