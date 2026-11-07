# Behavior and FSM model

## Runtime rule

SingularityBot is a player runtime, not an editor project. The live decision path is:

`StarCraft II -> SC2 adapter -> normalized facts -> game ontology/capabilities -> FSM_API contexts executed by FSM_Foundry -> intent arbitration/validation -> SC2 actions`.

FSM_API owns reusable lifecycle and state-machine behavior. FSM_Foundry executes the composed workload. SC2 protocol DTOs stay in the adapter. GUI, Renderer and Visualize inspect or author the same versioned contracts; they do not implement a second runtime and are not required to run the headless bot.

## Ownership hierarchy

1. **Match / commander** — match lifecycle, global objective and hard constraints.
2. **Economy controller** — worker assignments, reservations, resource budget, supply, construction and production priorities.
3. **Resource-line scheduler** — coordinates mineral-node service windows and route assignments across workers.
4. **Worker route FSM** — executes one persistent worker-to-node assignment and reports observed facts.
5. **Unit FSM** — executes the current unit profile.
6. **Action pipeline** — arbitrates intents, validates current evidence and translates accepted commands.

Parents own goals and global constraints. Children execute assigned work and report outcomes. A worker FSM must not independently choose a mineral patch when the economy controller has assigned one.

## Persistent worker-route model

A route is a long-lived operation, not a repeated one-frame gather command. Its context should retain worker, mineral and town-hall tags; assignment generation; route identity; current phase; current order/target; cargo facts; last observed game loop; cached outbound/return geometry; and phase-entry/deadline data.

Use game-loop deltas for inexpensive reevaluation. A deadline is only a prompt to inspect again: observations, cargo, orders and positions confirm transitions. Cache stable route geometry and recompute only when a relevant position, route identity, map/pathing basis or measured route error invalidates it. Keep predicted travel/service times distinct from measured results.

The economy controller owns global assignments and reservations. The resource-line scheduler coordinates saturation and service windows. Each worker route FSM owns execution of its one assignment. Replanning a returning worker must not casually discard its current return leg.

For the first Supply Depot, select the closest eligible SCV from the non-mining/available pool for the intended placement. Do not pull a productive miner merely because it is geometrically closest when another eligible worker is available. The opening command sequence is: queue Command Center SCV production; on the next scheduling pass assign existing SCVs to mineral routes; then select the Depot builder.

## First playable proof

The first target is deliberately narrow: Terran, no gas, four Barracks, pure Marines. It is not a generalized multi-race optimizer.

- Maintain SCV production and deterministic mineral assignments.
- Build the first Supply Depot and Barracks with validated placement, costs and prerequisites.
- Expand to four Barracks.
- Sustain Marine production without gas while preserving mining throughput.
- Measure the complete economy, not only time-to-first-Marine.

Do not bake an assumed optimal build timing into the generic FSM runtime. Strategy/build-order data is versioned input; observed game state and validated game data remain authoritative.

## Entity lifecycle and profile changes

Unit tags are stable keys within a match. Fog or temporary absence suspends behavior; it does not prove destruction. Explicit dead-unit evidence or authoritative match end retires the context. If a unit morph changes its supported behavior profile, retire/replace the old profile context safely so an SCV worker FSM cannot continue controlling a Marine.

## Action safety

An FSM emits an intent, not permission to submit a protocol action. The action pipeline must verify current ownership, liveness, visibility, target identity, ability metadata and applicable resource, prerequisite, range and placement constraints. It must arbitrate duplicate or conflicting commands. Missing or stale evidence fails closed. Every accepted or rejected intent should be explainable and traceable to the observation/game loop that produced it.

## Current implementation status on `development`

Implemented and present in the integration branch:

- SC2 observations are normalized into deterministic, protocol-independent unit facts using game-data catalogs.
- Visible own units are reconciled into persistent FSM contexts. Fog/absence suspends participation while retaining context; explicit death evidence retires it.
- Supported unit-profile morphs replace the old behavior context/handle.
- FSM emissions are collected per observation rather than routed to a stale frame buffer.
- Initial gather and return-cargo intents translate to SC2 unit commands for SCV/Probe/Drone workers. Gather resolves `Harvest_Gather`; return resolves `Harvest_Return` from game data and selects a visible friendly town hall deterministically.
- Regression tests cover normalization, reconciliation, fog/death/morph behavior and initial gather/return translation. The build workflow invokes the test project.

Not yet proven or implemented as a complete economy:

- Per-unit available-ability validation and complete action-result/error handling.
- Persistent worker-to-mineral assignments, reservation/saturation, route FSM scheduling and cached geometry.
- Closest-eligible Depot builder selection and validated construction/placement.
- Four-Barracks scheduling, resource/supply budgeting and Marine production.
- Deterministic recorded-observation replay, cycle/throughput metrics and live-match proof.

A wired test step is not the same as a successful current CI run. A passing unit test is not a live-match result. Keep those evidence levels separate in status reports.

## GUI contract

GUI is a later authoring/inspection surface over the runtime's actual versioned contracts. It should expose state/transition graphs, typed facts and intents, route assignments/timelines, cached geometry, active-state explanations, rejected guards, arbitration/validation outcomes and replay inspection. It must not become a second behavior engine or delay the first playable economy.
