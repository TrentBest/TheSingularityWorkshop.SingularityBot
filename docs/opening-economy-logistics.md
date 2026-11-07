# Opening economy and resource logistics

## First playable milestone

The first bot milestone is not combat or ladder performance. It is a complete, measurable opening that starts with workers and reaches the first race-specific tier-one combat unit:

| Race | Supply structure | First production structure | First unit |
|---|---|---|---|
| Terran | Supply Depot | Barracks | Marine |
| Protoss | Pylon | Gateway | Zealot |
| Zerg | Overlord (trained) | Spawning Pool | Zergling |

The opening controller must respect actual game data, prerequisites, costs, supply, build times, worker availability and legal action requirements. The table is a milestone outline, not a hard-coded build order or timing claim.

## Initial Terran strategy: four-Barracks pure Marine

The first concrete strategy is a **four-Barracks, pure-Marine Terran opening**. It is the initial strategy to implement and measure, not a claim of ladder-optimality.

### Strategy constraints

- No gas collection or Refinery construction in this strategy.
- No worker scout in the baseline opening. Scouting is a later, separately measured strategy option and must not silently take an SCV away from this build.
- Maintain SCV production whenever the Command Center can legally and affordably train one, subject to supply and the mineral reservations below.
- Mineral income is the only planned resource income; preserve enough workers on minerals to sustain construction and Marine production.
- Do not issue a build or train command unless the structure/unit prerequisites, affordability, supply, placement and command validation all pass.

### Event-driven opening

1. Queue the next SCV as soon as the opening begins and continue SCV production as resources and supply permit.
2. Dispatch an SCV to build the first Supply Depot at the strategy's selected legal placement. Continue producing SCVs while the Depot is under construction whenever affordable and not supply-blocked.
3. Forecast the Depot completion and reserve **150 minerals** so Barracks #1 can begin immediately when the Depot completes. The reserve is a spending constraint, not a reason to stop SCV production if the mineral bank can support both.
4. Start Barracks #1 at the first observation where the Depot is complete, 150 minerals are available, a builder is available and the placement/action validator accepts the command.
5. Continue SCV production and mineral saturation. When another 150 minerals are available without violating the active SCV/supply commitments, dispatch a different available SCV to start Barracks #2.
6. Repeat the same affordability-and-builder gate for Barracks #3 and #4. Do not pull a worker from an active construction job; choose the lowest-opportunity-cost eligible mineral worker.
7. Keep supply ahead of demand. Add the next Supply Depot before the current supply headroom blocks SCV or Marine production; its cost and builder time must be included in the forecast.
8. Once each Barracks completes, queue Marines whenever minerals and supply permit. The production scheduler should use all completed Barracks and avoid leaving one idle while another is repeatedly queued.
9. Continue mineral saturation and SCV production while funding Marine production and required supply. Gas remains intentionally unused.

The exact SCV count at each transition, Depot placement, and build-loop timing are **measured outcomes**, not hard-coded assumptions. The controller should evaluate the event conditions from each normalized observation and game data. In particular, “150 minerals at Depot completion” is an explicit opening objective; the planner should forecast whether the objective is reachable and prioritize spending accordingly.

### Opening state and reservations

Represent the opening as explicit states: `EstablishingWorkers`, `DepotUnderConstruction`, `AwaitingFirstBarracksWindow`, `BarracksOneUnderConstruction`, `ExpandingBarracks`, and `FourBarracksProduction`. State transitions are driven by observed structure completion, resource bank, supply headroom, worker availability and validated action results—not by assumed elapsed time.

Maintain separate mineral reservations for:
- next SCV production;
- first/next Barracks construction;
- the next required Supply Depot;
- Marine production.

Reservations are priorities and forecasts, not fictitious deductions from the observed resource bank. The scheduler must resolve conflicts deterministically and record which commitment delayed another. If a supply block or unexpected income shortfall makes the planned first-Barracks window impossible, preserve the actual game state and re-plan; never emit an unaffordable action.

### Success metrics

Record game-loop timestamps for each SCV queue/completion, Depot start/completion, each Barracks start/completion, each Marine queue/completion, supply-block intervals, worker assignments and mineral bank. Report time to first Marine, time to four completed Barracks, Marine production uptime, SCV count, mineral income, idle/constructing worker-minutes and unused gas (expected to remain zero). Compare repeated runs on the same map and game version before changing the policy.

## Worker scouting decision

Worker scouting is a configurable information-versus-income decision, not an
unconditional opening rule. The default baseline for economy experiments is
**no worker scout** so mining and first-unit timing have a clean control. The
adaptive policy may assign one only when all of these are true:

- the opponent start location is not yet known;
- the map or current threat assessment makes that information useful;
- no scout is already assigned; and
- assigning it leaves the configured minimum number of workers available for
  mining.

An explicit `AlwaysWhenAffordable` mode is available for experiments, but it
still respects the minimum mining-worker floor. `Never` is the control. The
adaptive mode does not scout merely because an enemy is not currently visible:
if the start location is already known, or there is no useful uncertainty to
resolve, the worker stays productive.

The first scout should be selected by the economy controller, not by an
individual worker FSM. The controller must record the worker's prior task,
dispatch game loop, route/waypoints, expected information objective, mining
opportunity cost and return/reassignment decision. The scout FSM reports
observations; the strategy layer decides when its information value no longer
justifies the lost mining time. If a threat is detected, the safety supervisor
may recall it. Scouting must never override a hard worker-safety or opening
construction constraint.

This policy is currently a deterministic decision component with unit tests;
it is **not yet connected to map spawn-location knowledge, a scout route, or a
SC2 move/attack action**. It does not make the bot scout in a live match. Those
are separate implementation gates. Evaluate scouting against the no-scout
baseline using identical maps/seeds and report both information gained (time
to identify opponent start/race/early tech or threat) and economy cost (worker
idle/travel time, lost mineral income, first-unit timing and any opening
failure). Keep the mode and mining-worker floor versioned in the GUI strategy
definition.

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
