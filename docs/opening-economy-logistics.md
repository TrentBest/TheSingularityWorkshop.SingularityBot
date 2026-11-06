# Opening economy and resource logistics

## First playable milestone

The first bot milestone is not combat or ladder performance. It is a complete, measurable opening that starts with workers and reaches the first race-specific tier-one combat unit:

| Race | Supply structure | First production structure | First unit |
|---|---|---|---|
| Terran | Supply Depot | Barracks | Marine |
| Protoss | Pylon | Gateway | Zealot |
| Zerg | Overlord (trained) | Spawning Pool | Zergling |

The opening controller must respect actual game data, prerequisites, costs, supply, build times, worker availability and legal action requirements. The table is a milestone outline, not a hard-coded build order or timing claim.

## Logistics objective

Optimize the whole mining operation, not just an individual worker's gather command. A worker cycle includes dispatch, travel, waiting for a usable resource interaction, gathering, returning cargo, unloading and reassignment. Travel and waiting are separate costs.

The key idea is **productive overlap**: when a worker cannot use its preferred mineral patch immediately, dispatch it toward another viable patch if the predicted arrival aligns with that patch becoming available. The worker's travel time can then overlap another worker's gather/return cycle instead of becoming idle queue time.

Do not assume that a mineral patch is a literal exclusive lock or that every SC2 map/client version exposes a reliable patch-occupancy signal. Treat availability as an estimated service window derived from observed workers, orders, positions, cargo, pathing and resource state. Validate the estimate against actual local-match outcomes.

## Economy controller responsibilities

1. **Observe:** normalize workers, mineral fields, geysers, town halls, resources, supply, orders, construction, production queues and game-loop time.
2. **Estimate:** infer each worker's task phase and each resource node's expected service availability; track uncertainty when visibility or protocol data is incomplete.
3. **Schedule:** choose a worker-to-node assignment that minimizes expected idle + travel + queue time while preserving coverage and avoiding excessive reassignment.
4. **Reserve:** coordinate worker claims so multiple economy decisions do not issue conflicting assignments in one observation.
5. **Build:** schedule supply and production structures only when prerequisites, resources, worker availability, placement and action legality are satisfied.
6. **Train:** queue the first tier-one unit when the production structure is complete, supply and resources permit it, and the command validator confirms the action.
7. **Measure:** record predicted and observed cycle times so scheduling models can be calibrated rather than trusted blindly.

## Initial dispatch policy

For each idle or reassignable worker, evaluate candidate mineral nodes using a predicted completion time:

`predictedCycle = travelToNode + expectedQueue + gather + travelToTownHall + unload`

Choose the assignment with the lowest expected cycle cost, with penalties for uncertainty, congestion, excessive reassignment and leaving a node under-served. The scheduler should consider the *arrival time* at a node, not merely its current occupancy: a farther node can be better when the nearer node is predicted to remain busy until the worker would otherwise have finished travelling there.

This is a scheduling heuristic, not a claim that the exact formula is already known. Calibrate each component from observation/replay data. Use deterministic tie-breaking so identical snapshots and strategy versions produce identical assignments.

## Build-order and worker opportunity cost

Construction consumes both resources and worker-minutes. The economy planner must include:
- structure cost and prerequisite chain;
- earliest legal start based on resource and supply forecasts;
- travel to a valid placement;
- builder travel and construction duration;
- mining income forgone while the builder is occupied;
- whether construction pauses, resumes or cancels under the selected race/game mechanics;
- supply gained or consumed and the risk of a supply block;
- production-structure completion and first-unit queue time.

For Zerg, the Drone is consumed by construction; for Terran, an SCV remains occupied while constructing; for Protoss, a Probe can resume work after starting construction. These different worker opportunity costs belong in race-specific capability/ontology data, not scattered conditionals in the generic scheduler.

Supply itself is race-specific: Terran Supply Depot and Protoss Pylon are structures; Zerg supply comes from an Overlord unit. The planner must not model all three as a generic worker-built structure.

## FSM composition

The economy is a parent FSM with cooperating subdomains:

- **Opening phase:** establish economy, satisfy supply, unlock production, produce first unit.
- **Worker assignment:** dispatch, gather, return, reassign, construct.
- **Resource-node scheduling:** estimate service windows, congestion and expected cycle cost.
- **Construction scheduler:** select builder, placement candidate and legal start window.
- **Production scheduler:** reserve resources/supply and queue the target unit.
- **Safety/constraint supervisor:** reject actions that violate game state, affordability, prerequisites or command validity.

Unit FSMs report facts and accept work intents; the economy controller owns global assignments and reservations. A unit FSM must not independently choose a node in a way that conflicts with the economy scheduler.

## Experiment protocol

Use deterministic local matches and replayable snapshots. Compare policies from identical initial conditions and map seeds where supported:

1. Baseline: simple nearest-node assignment with the existing game's normal gather behavior.
2. Saturation-aware: balance workers across nodes using estimated current assignments.
3. Arrival-window scheduling: dispatch toward nodes predicted to become available near worker arrival.
4. Full opening: add supply, production construction and first-unit production while preserving the same scheduler.

Change one policy dimension at a time. Run repeated trials where randomness or pathing can affect outcomes. Keep the strategy version, map, game/client version, initial state, observation/event trace and policy parameters with each result.

## Required metrics

- time to first Marine / Zealot / Zergling;
- mineral and gas income over time, plus cumulative resources;
- worker idle, travel, queue/wait, gather, return and construction time;
- per-node worker saturation and estimated versus observed service availability;
- reassignment count and wasted travel from reassignment;
- resources banked but not spent, and time spent resource-starved;
- supply-block start, duration and lost production time;
- structure start/completion times and worker opportunity cost;
- first-unit queue/start/completion time;
- invalid/rejected actions and unmet prerequisites.

Do not optimize only time-to-first-unit. A faster first unit that leaves the economy stalled, supply-blocked or unable to continue production is not a better opening. Report the full metric vector and compare Pareto trade-offs.

## Replay and forecast contract

Every decision record should include observation/game-loop identity, worker and node identifiers, estimated service window, candidate scores, selected assignment, policy version and eventual observed result. Replay must reproduce decisions from the same normalized observations and deterministic policy configuration. Forecasts are estimates; preserve confidence/uncertainty and compare forecast to actual outcome.

The initial implementation does not require reverse simulation or full game-state restoration. A recorded observation/event stream is enough to replay and evaluate the scheduler deterministically. Branching a live match remains a separate capability requiring authoritative state restoration, including RNG and relevant game/AI state.

## Implementation gates

1. Normalize observation facts and stable identifiers for workers, mineral nodes, town halls, resources, supply and orders.
2. Implement deterministic worker-to-mineral assignment with reservations and baseline metrics.
3. Add saturation-aware assignment and replay comparison.
4. Add arrival-window scheduling; prove it improves idle/queue/travel trade-offs in local matches.
5. Implement race-specific supply and construction planning.
6. Complete one race's first-unit opening end-to-end; then generalize through ontology/capabilities to the other two.
7. Expose scheduler parameters, active assignments, predicted availability and measured outcomes in the GUI.

No competitive-optimality claim is made until repeated local experiments support it.
