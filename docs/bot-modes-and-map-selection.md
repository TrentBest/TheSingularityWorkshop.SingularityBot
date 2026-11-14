# Bot modes and map selection

A map/experience selects a bot mode as its **What**. The map's portable `BotDefinition` stores the stable `modeId`; the runtime resolves that ID against the authoritative in-game `RaceActual`. Race is not inferred from the mode, and a mismatched or missing selection fails closed. The same explicit selection path is used even when a race temporarily has only one available mode.

| Race | Mode | Primary unit | Mode ID |
|---|---|---|---|
| Terran | Marine Mode | Marine | `terran.marine.v1` |
| Terran | Marauder Mode | Marauder | `terran.marauder.v1` |
| Protoss | Zealot Mode | Zealot | `protoss.zealot.v1` |
| Protoss | Stalker Mode | Stalker | `protoss.stalker.v1` |
| Zerg | Zergling Mode | Zergling | `zerg.zergling.v1` |
| Zerg | Queen Mode | Queen | `zerg.queen.v1` |

The primary unit names the central force and strategic identity; it is not an exclusive unit whitelist. Supporting units, workers, production structures, upgrades and race-specific mechanics remain available to the strategy when their prerequisites and safety checks permit.

The first implementation milestone is deliberately narrow: Zergling Mode must establish Zerg production and get Zerglings onto the map; Zealot Mode must establish Protoss production and get Zealots onto the map. Queen and Stalker modes exercise the same selection and strategy contracts and can follow as their production/control paths are ready. Marine Mode remains the existing Terran baseline; Marauder Mode is a separate Terran strategy, not a renamed Marine composition.

Only Marine Mode is currently connected to an executable gameplay path. Marauder, Zealot, Stalker, Zergling and Queen are registered selections but remain fail-closed until their production and tactics adapters are implemented and tested. Selecting one does not issue Terran actions. Mode catalogue registration is not gameplay support. Until a mode has a race-specific production adapter and tested action path, its descriptor is authoring/selection metadata only and the race module must remain fail-closed. Unit IDs, costs, producer requirements and ability IDs must be resolved from the match's authoritative SC2 game data; never guess IDs or silently substitute another mode.

The runtime composition remains local: the selected experience's trusted definition is composed at initialization from registered components. A web repository is not a runtime MicroBundle source.


## First-unit production profiles

The runtime now has a separate race/mode production-profile catalogue. The first profiles are:

- Zergling Mode: Zergling from Larva, gated by a completed Spawning Pool.
- Zealot Mode: Zealot from Gateway, gated by a Pylon and explicit confirmation that the Gateway is powered.
- Stalker Mode: Stalker from Gateway, gated by Pylon, Cybernetics Core, and powered Gateway.
- Queen Mode: Queen from Hatchery-family production; adapter work remains pending.

At `OnStart`, each selected profile resolves its target unit, producer, costs, food requirement, and required prerequisite unit types against the match's authoritative `ResponseData`. Missing or ambiguous data fails closed and is written to diagnostics. No numeric unit or ability IDs are embedded in these profiles.

The first action-translation slice now exists as `Sc2RaceProductionActionTranslator`. It builds raw unit commands from the target ability resolved in match game data, requires visibly completed prerequisites, filters out busy/dead/reserved producers, and computes mineral, gas, and food reservations before emitting orders. Zergling profiles declare two output units per Larva order, so cost/supply reservation and expected output account for the pair. Protoss translation requires a caller-supplied positive per-Gateway power-evidence function; without it, no command is emitted.

The translator is intentionally stateless and is not yet connected to the live race module. Submission latches, action-result reconciliation, and observed Egg/in-progress pair reconciliation remain caller/runtime responsibilities. Therefore Zergling and Zealot modules remain fail-closed; passing unit tests for command translation is not integrated match validation or verified gameplay.


### In-progress production reconciliation

```
Sc2RaceProductionProgressReconciler
``` now reconciles visible completed target units, target units that are visibly under construction, and visible Zerg Eggs whose current order ability matches the selected target's authoritative training ability. A matching Zergling Egg counts as two in-progress Zerglings, not one Egg. Enemy, hidden, dead, and unrelated Egg observations are excluded. This is an observation-side accounting primitive only: it does not submit orders, own producer latches, or prove that an action was accepted. The race modules remain fail-closed until those responsibilities are integrated and exercised in an actual match.

### Race structure-build action translation

`Sc2RaceBuildingActionTranslator` is a fail-closed raw-command boundary for a selected race worker constructing a structure. It requires the structure's authoritative `UnitTypeData.AbilityId`, a matching `AbilityData` entry explicitly marked as a building ability, a live visible self worker of the resolved worker type, an unreserved/idle/non-carrying worker, sufficient current resources, and a finite placement supplied by the caller. It emits exactly one non-queued position-targeted command and reports the worker and resource reservation. It does not select workers, search/query placement, own submission latches, reconcile action results, or connect itself to the live race module. The Zerg Drone-to-Spawning-Pool opening is therefore not yet playable; this translator is a tested building-action primitive, not an end-to-end construction path.

### Zergling opening runtime

The `zerg.zergling.v1` mode now has a narrowly scoped runtime connected to the SC2 frame, action-result, and placement-query callbacks. It resolves Drone, SpawningPool, Zergling, Larva, and the Spawning Pool build ability from match `ResponseData`. It generates candidate sites around the observed Hatchery, asks SC2 to validate them with `RequestQueryBuildingPlacement`, and only emits a build command at an approved site using the same queried Drone. It then waits for a visibly completed self Spawning Pool before issuing a Larva production order. Zergling production is serialized to one Larva command at a time; the pending producer is held until matching Egg evidence is observed or an explicit rejection/death resolves it. The progress reconciler counts one matching Egg order as two Zerglings.

This is the first live-connected Zerg opening path, but it is not yet verified in an actual StarCraft II match. It does not yet provide a complete economy/worker-management strategy, scouting, defense, supply management, or combat control. Match launch and gameplay evidence remain required before calling the mode playable or handing it over as a proven build.
