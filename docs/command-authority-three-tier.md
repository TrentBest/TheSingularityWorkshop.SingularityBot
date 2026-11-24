# Three-tier command authority

## The model

Every bot personality is authored as three kinds of authority. The hierarchy is semantic and race-aware; it does not require every race to use the same titles, doctrine, or operational groupings.

| Contract | Authority | Responsibility |
|---|---|---|
| IStrategy | Strategic | Defines doctrine, priorities, objectives, phase transitions, and constraints for the match. |
| ICoordinator | Operational | Owns a domain such as mining, construction, production, scouting, research, or military operations. Turns strategic objectives into assignments and policies. |
| IController | Unit | Represents one persistent unit context and its attached FSM_API capabilities. Executes local behavior and reports state. |

```text
IStrategy
  ├── ICoordinator: Mining Operations
  │      ├── IController: SCV 1
  │      ├── IController: SCV 2
  │      └── IController: SCV 3
  ├── ICoordinator: Construction Corps
  │      └── IController: SCV 4
  └── ICoordinator: Production Command
         └── IController: Barracks / Marine units
```

This is an authority hierarchy, not a requirement that every unit be permanently assigned to one coordinator. Assignment changes are explicit, observable, and reconciled through the shared reservation/assignment systems. A unit context remains the persistent identity while its operational role changes.

## Race-specific editorial profiles

| Race | Strategic presentation | Operational presentation | Design emphasis |
|---|---|---|---|
| Zerg | The Overmind | Cerebrates / broods | Preserve Drones carrying resources; treat Drone construction as a consuming commitment, with exceptions for justified emergency defense. |
| Terran | Fleet Admiral | Specialist commanders | Explicit worker handoffs, construction corps, mining operations, and production scheduling. |
| Protoss | The Enclave | Conclaves / appointed authorities | Probe construction releases the worker; production and warp-in coordination express race-specific prerequisites. |

These are authoring metaphors and presentation profiles, not assertions that every named authority is a playable SC2 entity. Creators may rename them or select a different registered doctrine. Presentation identity is independent of implementation identity.

## Shared services and race adapters

The shared runtime owns observations, persistent unit contexts, FSM_API integration, reservations, production objectives, action legality, diagnostics, and scheduling. Race modules provide the race-specific ontology and adapters for units, structures, production, construction, abilities, and resource rules.

A coordinator must not bypass shared arbitration or submit raw SC2 commands around the validated action pipeline. A strategy must not directly micromanage every unit. It emits bounded intent; coordinators translate it; unit controllers execute through their FSM capabilities.

The current runtime already has CommandLayerComposition and a trusted CommandLayerRegistry. Those remain useful for selecting compiled implementations and expressing contracts/capabilities. The three-tier authority model is the author-facing conceptual model; it should not be confused with the existing optional tactical slots (Army, TaskForce, Platoon, Squad) or force every bot to enable all of them.

## FSM_API is the unit behavior engine

UnitBehaviorContext is the persistent entity context and owns its FSM handles. It now implements IController directly. The controller exposes identity, validity, capabilities, and current FSM states without introducing a parallel state machine.

Use the integer-backed FSM_API path already adapted by BotFsmApi. Do not create string-backed duplicate state engines for the command hierarchy. Coordinators should assign/update the existing unit contexts and their named FSM capabilities.

## Current implementation status

- Implemented: IStrategy, ICoordinator, and IController contracts.
- Implemented: UnitBehaviorContext implements IController.
- Implemented: race-specific editorial vocabulary catalogue for Zerg, Terran, and Protoss.
- Implemented: Forge's first interactive visual preview for race selection, strategic title, and coordinator groupings.
- Not yet implemented: concrete runtime strategy/coordinator compositions, serialized hierarchy in BotDefinition, and command dispatch from strategic directives through coordinators into unit FSMs.
- Not yet implemented: Forge save/import/export of this hierarchy. The current preview is deliberately labelled as a prototype and does not alter the v1 JSON artifact.

## Next vertical slice

1. Define versioned, typed strategic directives and coordinator assignments.
2. Add trusted compiled strategy and coordinator registries with race/capability checks.
3. Connect the strategy to coordinator scheduling without creating a second frame loop.
4. Make Mining Operations and Construction Corps the first Terran coordinators.
5. Verify the 9-second Depot SCV pull, second SCV Barracks handoff, and immediate builder return to mining against observed game-loop evidence.
6. Add Zerg Drone-preservation and Protoss Probe-release tests against the same coordinator contracts.
7. Promote the hierarchy into the versioned BotDefinition and make Forge edit, validate, import, and export that exact contract.

No live match or three-race parity is claimed by the editor preview or unit tests.