# Economy Advisor

## Responsibility

The Economy Advisor is the policy boundary between observed economic facts and production/construction controllers. It does not issue SC2 commands, fabricate resources, or declare a build legal. Controllers still validate current mineral bank, prerequisites, supply, placement and action results before submitting an action.

The bot exposes the policy through:

- `ConfigureEconomyAdvisorJson(string)` to validate and apply development-GUI settings;
- `EconomySettings` to read the active settings back into the GUI;
- `EconomyAdvisor.GetReservedMinerals(...)` to derive the current infrastructure reservation;
- `WorkerResourceAssignmentPlanner.Reconcile(..., mineralSaturation)` to apply the configured worker saturation.

Configuration is versioned and applied atomically: invalid JSON or invalid values leave the previous settings active.

## Version 1 settings

```json
{
  "version": 1,
  "mineralSaturation": 2,
  "prioritizeOpeningInfrastructure": true,
  "holdWorkerProductionForFirstDepot": true,
  "allowLongDistanceMining": false
}
```

- `mineralSaturation`: target workers per mineral field, constrained to 1–4.
- `prioritizeOpeningInfrastructure`: when enabled, reserve the next observed opening milestone's mineral cost before optional production.
- `holdWorkerProductionForFirstDepot`: when enabled, prevent additional SCV training from consuming the first Depot's mineral budget after initial SCV production has begun.
- `allowLongDistanceMining`: defaults to `false`; when disabled, mineral fields more than 12 map units from every completed Command Center, Orbital Command, or Planetary Fortress are excluded from worker assignments. This keeps workers on established base mineral lines instead of sending them to unoccupied expansions.

The current infrastructure priority is: expansion Command Center, follow-up Depot plus an SCV reserve, first Depot, first Barracks, then Marine production. These are mineral reservations, not promises that the corresponding job is ready.

## Persistent mineral-node contexts

Mineral fields are entities in the observation registry, not anonymous coordinates. Their SC2 unit tags are stable job references. The runtime retains a mineral context even when the protocol does not report a positive unit-health maximum for that resource observation.

A worker assignment continues to store the mineral tag and assignment generation, and the worker context now also holds a direct reference to the matching persistent mineral context. The reference is cleared when the assignment is cleared or when its target context is no longer valid. This makes the mineral node an explicit participant in the mining job rather than a copied position.

The planner remains responsible for stable reservations and saturation. Worker FSMs execute assignments; the Economy Advisor controls policy settings; neither role takes ownership of the other's work.

## Current implementation boundary

The first version exposes policy configuration and resource-context binding. It does not yet provide a full GUI screen, gas/refinery scheduling, income forecasting, multi-base saturation policy, or a generalized Task Master. Those remain follow-on work and must be driven by observed economy facts and measured match results.
