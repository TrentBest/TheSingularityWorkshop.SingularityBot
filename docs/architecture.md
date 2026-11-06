# Architecture

## System boundary

SingularityBot is not a replacement for the StarCraft II client protocol. It is the decision and orchestration layer above a versioned game adapter.

```text
StarCraft II Client / Ladder
          |
          v
SC2 Protocol Adapter
          |
          v
Observation + Game Data
          |
          v
Game Ontology ---- GrammarAi / ProtocolAi vocabulary
          |
          v
FSM Match Runtime ---- FSM_API
          |
          +---- Strategy Compiler <---- Declarative Strategy
          |
          +---- Spatial / tactical queries <---- Physics
          |
          +---- Match memory / replay outcomes <---- DataWarehouse
          |
          v
Validated Action Set
          |
          v
SC2 Protocol Adapter
```

## Responsibilities

| Component | Responsibility | Boundary |
|---|---|---|
| SC2 adapter | Connect, negotiate, receive observations, submit actions, support local and ladder launch | Protocol-specific; no strategy policy |
| Game ontology | Units, structures, abilities, upgrades, races, costs, prerequisites and capabilities | Stable identifiers and metadata; no per-match decisions |
| Match runtime | Match/player/unit lifecycles, contexts, events and update cadence | FSM_API owns lifecycle orchestration |
| Strategy compiler | Validate versioned strategy documents and construct runtime state machines | Converts intent into executable FSM definitions |
| Strategy engine | Goals, priorities, constraints, plans, tactics and transitions | No direct protocol I/O |
| Spatial reasoning | Distance, range, terrain, pathing, threat fields and formations | Physics owns reusable math/spatial primitives |
| Memory | Match history, opponent tendencies, replay annotations and outcome data | DataWarehouse is the persistence/memory boundary |
| Editor | Strategy graph, build order, map overlays and validation feedback | GUI/Renderer/Visualize; editor consumes the same strategy contract |
| Evaluation | Deterministic fixtures, replay regression, match metrics and tuning | CiAi/HeadlessAi orchestration; never silently publishes packages |

## Runtime loop

1. Receive a protocol observation and normalize it into a versioned internal snapshot.
2. Reconcile visible units and game entities with their persistent runtime contexts.
3. Update match, player, squad and unit FSMs.
4. Evaluate strategy goals and constraints against current knowledge.
5. Produce candidate intents, arbitrate conflicts, and validate commands.
6. Translate accepted commands into SC2 protocol actions.
7. Record decisions, observations and outcomes for replay and learning.

The runtime must distinguish **unknown**, **not visible**, **destroyed**, and **not applicable**. Fog of war must never be interpreted as proof that an enemy entity no longer exists.

## Workshop integration

- **FSM_API**: lifecycle and state-machine execution. Keep game-specific types out of the reusable FSM package.
- **GrammarAi / ProtocolAi**: compact, canonical vocabulary for protocol messages, unit identifiers, abilities and command grammar. They do not replace the SC2 protocol implementation.
- **CommandAi**: optional higher-level command interpretation and goal decomposition.
- **Physics**: spatial calculations and physical reasoning; do not create a parallel math framework here.
- **DataWarehouse**: match memory and durable experience.
- **GUI**: authoring and visualization, not a required runtime dependency.
- **CiAi / HeadlessAi**: builds, deterministic tests, replay evaluation and later tournament automation.

## Architectural constraints

- The SC2 adapter is replaceable and versioned independently from strategy logic.
- No GUI dependency in the headless bot runtime.
- No strategy may bypass action validation.
- Strategy documents are versioned and validated before a match starts.
- Runtime decisions should be explainable: record the goal, evidence, constraints and selected action.
- Avoid coupling FSM_API to SC2 protocol assemblies.
