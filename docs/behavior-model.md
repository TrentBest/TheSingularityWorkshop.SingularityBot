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

A route is a long-lived operation, not a repeated one-frame gather command. Its context retains worker, mineral and town-hall tags; assignment generation; current observed phase; last observed game loop; and phase-entry game loop. The planner owns reservation identity; the worker FSM context owns the observed execution phase. Current phases are `Unassigned`, `Assigned`, `ExecutingOrder`, and `CarryingResources`.

Phase changes are observation-driven: a new reservation generation enters `Assigned`; observed orders enter `ExecutingOrder`; observed cargo enters `CarryingResources`. The runtime does not claim a worker is gathering or returning merely because it issued a command. Hidden contexts retain their last known assignment/phase. Explicit death events invalidate reservations; temporary target occlusion does not erase the reservation, while action translation fails closed until the mineral is visible again.

Use game-loop deltas for inexpensive reevaluation. A deadline is only a prompt to inspect again: observations, cargo, orders and positions confirm transitions. Cache stable route geometry and recompute only when a relevant position, route identity, map/pathing basis or measured route error invalidates it. Keep predicted travel/service times distinct from measured results.

The economy controller owns global assignments and reservations. The resource-line scheduler coordinates saturation and service windows. Each worker route FSM owns execution of its one assignment. Replanning a returning worker must not casually discard its current return leg.

For the first Supply Depot, wait for the first Command Center SCV-training order to be observed. When the mineral bank crosses 10, pull an SCV already returning to the Command Center (including a cargo-bearing miner whose gather order still targets its assigned patch), reserve it from mineral allocation, query SC2 for a legal forward-side placement, move it to that site, and build once the game-data cost is available. The first protocol action is the SCV-training command; worker-route actions follow after its order is observed. Before the first Depot pull, a selected SCV may run the query-verified Spray-then-gather proof: the bot queries the unit's available abilities, sends Spray only if SC2 confirms it, waits for the action result, and then explicitly reissues the same mineral target.

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
- Every newly encountered entity with positive observed maximum health is reconciled into a persistent context regardless of alliance; known profiles add their current capability FSMs. Contexts survive snapshots, hidden records, and absence from a frame; only explicit dead-unit evidence retires them.
- Entity contexts now own their FSM handles. The runtime no longer maintains a parallel per-entity handle dictionary.
- Morphs that change the authoritative unit type rebuild the context and its capability handles under the same SC2 tag.
- The catalog declares the ordered `DataSync -> Strategy -> SubStrategy -> HighDPS -> LowDPS -> ActionResolution` processing groups. Current worker and combat capabilities are mapped into LowDPS and HighDPS respectively; Strategy, SubStrategy, and ActionResolution are registered pipeline stages but their dedicated FSM definitions are not implemented yet.
- The frame path now reconciles entity contexts immediately after normalization and no longer returns early from opening-rally/production gates before advancing the registered FSM groups. Those opening controllers remain imperative migration debt; this is not yet the completed FSM-only runtime.
- FSM emissions are collected per observation rather than routed to a stale frame buffer.
- Initial gather and return-cargo intents translate to SC2 unit commands for SCV/Probe/Drone workers. Gather resolves `Harvest_Gather`; return resolves `Harvest_Return` from game data and selects a visible friendly town hall deterministically.
- Worker mineral reservations are deterministic and saturation-aware (two workers per mineral by default), with stable generations and retention through temporary worker/target occlusion. Explicit dead-unit events invalidate the affected reservation.
- Reservation tags, generation and observation-derived route phase are synchronized into each visible worker FSM context; phase-entry game-loop timestamps are retained. The worker FSM only enters its mining state when a live reservation exists, and returns to assessment when a reservation is invalidated or replaced.
- Regression tests cover normalization, reconciliation, fog/death/morph behavior, assignment planning, route-context phases and initial gather/return translation. The build workflow invokes the test project.

Not yet proven or implemented as a complete economy:

- Per-unit available-ability validation and complete action-result/error handling.
- Worker order target identity/reconciliation, separate outbound/gather/return/unload phases, cached path geometry and arrival-window scheduling.
- Closest-eligible Depot builder selection and validated construction/placement.
- Four-Barracks scheduling, resource/supply budgeting and Marine production.
- Deterministic recorded-observation replay, cycle/throughput metrics and live-match proof.

A wired test step is not the same as a successful current CI run. A passing unit test is not a live-match result. Keep those evidence levels separate in status reports.

See [Terran FSM catalog and processing pipeline](terran-fsm-catalog.md) for the proposed shared lifecycle graph, strategic FSMs, unit/structure capability definitions, and staged implementation order. These catalog definitions are design targets unless explicitly listed above as implemented.

## GUI contract

GUI is a later authoring/inspection surface over the runtime's actual versioned contracts. It should expose state/transition graphs, typed facts and intents, route assignments/timelines, cached geometry, active-state explanations, rejected guards, arbitration/validation outcomes and replay inspection. It must not become a second behavior engine or delay the first playable economy.