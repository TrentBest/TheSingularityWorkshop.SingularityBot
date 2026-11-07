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


## Event-driven mining operations and JIT scheduling

A worker assignment is a persistent **mining operation**, not a one-frame command. Give each operation a stable identity derived from its worker, resource node and assignment generation, and retain its context while it progresses. The resource node also has a resource-side FSM/context. The worker FSM executes the assigned operation; the economy controller remains the sole owner of assignment and reservations.

### Operation lifecycle

Represent observed and predicted phases explicitly:

- Unassigned
- Dispatching
- TravellingToResource
- WaitingForService
- Gathering
- ReturningCargo
- Unloading
- ReassignmentPending
- Suspended (for stale, hidden, blocked or otherwise insufficient observations)
- Completed / Invalidated

Transitions should be driven by observed events and validated state changes: order accepted, position enters/leaves a route segment, cargo acquired, cargo delivered, resource depleted, worker becomes unavailable, path becomes blocked, node becomes inaccessible, or the match ends. A predicted completion time is a forecast, never proof that a transition occurred.

Each operation context records worker tag, resource tag, town-hall tag, assignment generation, phase, last observed game loop, current order/target, cargo facts, route identity, predicted phase deadlines, actual phase timestamps, confidence, and reason for suspension/reassignment. Keep event history append-only for the experiment trace. Reconcile against each fresh observation; do not tick a fog-hidden worker using stale facts, and do not destroy its retained context merely because it is absent from a snapshot. Explicit death/depletion or authoritative match events retire the relevant context.

### Smallest actionable time delta

Use the finest authoritative time unit exposed by the game protocol (normally game-loop identity) as the scheduler's time axis. Do not invent sub-frame precision. For each operation, predict the earliest next event that can change a decision: worker arrival, service becoming available, gather completion, cargo return/unload, construction completion, supply threshold, or reservation expiry.

The scheduler evaluates the next decision at the minimum positive delta to a relevant predicted event, but still reconciles on every actual observation. When several events share a game loop, process them in a stable order (event class, resource tag, worker tag, operation generation). Clamp stale/past predictions to the current observation and record forecast error. Never issue a command merely because a predicted timer expired; first verify current facts and adapter legality.

### Resource-node FSM and reservations

A mineral patch/refinery context tracks stable tag, resource kind, remaining contents when visible, accessibility, observed workers/orders, estimated service availability, saturation curve, blocked/depleted state and confidence. Treat service windows as inferred, not literal exclusive locks: SC2's internal worker pathing and resource interaction may not expose exact occupancy. Preserve observed assignments and reservations across decisions, then update only when evidence materially changes.

The resource FSM reports availability windows; it does not independently dispatch workers. The economy controller solves the worker-to-node assignment globally, with deterministic tie-breaking and diminishing returns for extra workers. This avoids worker/resource FSMs racing to claim one another.

### Mining-lane geometry and cache

Cache static or slowly changing route geometry by map identity/hash, town-hall tag, resource tag, movement profile and relevant pathing-layer version. For each town-hall/resource pair, retain the validated path polyline and a lane descriptor. A useful candidate lane is the path from the resource's interaction point toward the town hall, with a cached tangent/heading at the resource-side departure and at the town-hall approach.

The "closest tangent" idea is a candidate route optimization, not an assumed SC2 pathing rule: compare the cached lane against observed worker trajectories and actual pathing queries where available. Invalidate/recompute when map/pathing identity changes, the town hall or resource is destroyed, a blocker changes connectivity, or observed travel error exceeds a configured threshold. Dynamic congestion and worker-specific deviations must not be baked into the static cache. Record cache hit/miss, route version, predicted distance/time and observed distance/time so we can tell whether caching improves decisions rather than merely reducing CPU work.

### Bunker return-path experiment

Treat worker passage through a friendly Bunker as an isolated, optional experiment—not a production assumption. First establish whether the exact SC2 build/API and map geometry permit a worker to enter, traverse and exit the Bunker safely and whether that route is legal and controllable. Measure the complete mineral cycle with and without the detour: entry/exit time, queueing, unload timing, lost mining time, pathing failures, unit safety and any effect on Bunker occupancy/defense.

A bunker route is beneficial only if repeated controlled trials show a statistically credible increase in delivered minerals per game-loop (and no unacceptable safety/defense cost) versus the direct route. Do not route all workers through it by default. Gate it behind an experiment flag and map/build capability check; abort safely if entry, exit or path state is uncertain. Compare against the best non-Bunker lane policy from the same initial conditions.

### Performance evaluation and replay record

Create one event/decision trace per match with:
- map file/hash, map provenance, game build, protocol/data version, bot commit, strategy/policy version and initial scenario;
- observation/game-loop id and normalized facts used by each decision;
- worker/resource/town-hall/operation ids, route-cache key/version and cache hit/miss;
- candidate assignments and their predicted travel, wait, gather, return, unload and total cycle times;
- selected assignment, reservation changes, reason codes and action validation/submission/result;
- predicted event time versus observed transition time, including confidence and forecast error;
- delivered mineral/gas amounts and all worker phase durations.

Compute at least:
- delivered minerals and gas per game-loop and per minute;
- cumulative income curve and time to each resource threshold;
- worker utilization by travel, wait, gather, return, unload, construction and idle;
- per-node throughput, saturation, queue time and under/over-saturation;
- cycle-time distribution (median, tail percentiles and variance), not just averages;
- assignment churn, wasted travel, blocked-path time and failed/stale commands;
- forecast error by phase and resource/map lane;
- route-cache hit rate, recomputation cost and prediction improvement;
- bunker versus direct-route throughput and safety/availability failures.

Keep raw event traces and derived summaries separate. Replay the same normalized observation/event stream with a fixed policy version and parameters; require identical decisions, reservations and reason codes. For policy comparisons, run paired trials from equivalent initial conditions and report distributions/confidence intervals. Change one factor at a time before testing interactions. Optimize delivered resources over a declared horizon subject to safety, production and expansion constraints; do not optimize a proxy such as shortest path or time-to-first-unit in isolation.

### Experiment ladder

1. Instrument the current baseline before changing behavior.
2. Reconstruct operation phases and forecast errors from real observations/replays.
3. Add deterministic operation/resource contexts and event reconciliation.
4. Compare nearest-node, saturation-aware and arrival-window/JIT assignment using identical traces.
5. Add cached mining lanes and verify route prediction against observed trajectories.
6. Test the Bunker hypothesis as a separately flagged paired experiment.
7. Only promote a policy when replay gates pass and repeated local matches show a real throughput improvement without harming macro stability.

No optimality claim is made from a single match or a geometric model alone.
