# Terran opening and worker logistics

## Objective

The first playable target is a disciplined Terran economy that establishes four Barracks and produces a sustainable stream of Marines without gas. The immediate goal is not a theoretically optimal build order; it is a reliable, measurable loop that makes legal decisions from live observations.

## Opening responsibilities

The opening controller coordinates:
- SCV production from the Command Center as the first action in the opening;
- deterministic assignment of starting SCVs to mineral routes;
- first Supply Depot builder selection;
- Depot and Barracks construction;
- expansion to four Barracks;
- Marine production without gas;
- resource and supply budgeting so construction does not starve production.

All costs, prerequisites, build times, supply effects and abilities must come from current game data or validated ontology. Never hard-code an assumed timing as a fact.

## Worker assignment

The economy controller is the sole owner of worker-to-resource assignments and reservations. It evaluates candidate mineral routes and assigns each worker deliberately. Unit FSMs execute their assigned work; they do not race each other to select patches.

The planner uses deterministic saturation-aware allocation (two workers per mineral by default), stable assignment generations and explicit death-event invalidation. On the initial allocation it treats SC2's existing gather orders as replaceable hints and solves a minimum-total-distance matching across the primary slot at every mineral patch. It then adds second-worker slots from the patches farthest from the Command Center inward and matches the remaining workers to those slots. The initial matching now asks SC2 for pathing distance from each starting SCV to each mineral patch, plus Command-Center-to-patch pathing distance for the farthest-back ordering. If pathing results are missing, invalid or delayed beyond 22 game loops, it falls back to straight-line distance. Later workers are still assigned using the current geometric-distance allocator; pathing queries for newly produced workers remain future work. Reservations survive temporary worker or target occlusion; the action translator still requires a currently visible mineral before issuing a new gather command. Each visible worker FSM context receives its assigned mineral/town-hall tags, generation and observed route phase.

The latest Acolyte trace is the pre-change baseline: all 12 starting SCVs initially pointed at one mineral patch. By game loop 22 the old sequential allocator had spread workers across all eight patches and doubled four of them, but it did not globally match the closest SCV to each patch. For example, a worker was assigned to the patch at (140,168.5) from roughly 5 units away while another SCV was only about 1.5 units away but had been assigned elsewhere. A retrospective straight-line comparison using the initial SCV positions and the old loop-22 target map is 69.44 total map units; the global Euclidean matching lowers that assignment cost to 53.97 (about 22.3%). This is a static assignment comparison, not measured mining throughput. The live initial allocation now uses SC2 pathing distances, so its final pairing may differ from that Euclidean lower-bound calculation.

The initial mineral allocation now uses a global matching for primary mineral slots, with SC2 pathing distances where available and a straight-line fallback when pathing results are missing, invalid or delayed. Secondary workers are assigned to the farthest-back mineral slots. This replaces the earlier sequential-allocation limitation described in the historical baseline above.

The current opening has progressed beyond the original single-Depot prototype: the opening SCV chains the first Supply Depot into the first Barracks, then returns to the worker pool when the Barracks completes. Barracks two starts as soon as the first Barracks is observed under construction, overlapping the two Barracks build timers rather than waiting for Barracks one to finish. Once the first Barracks completes, its released SCV is explicitly preferred for the follow-up Supply Depot while Barracks two continues building. The Depot remains an independent supply job: it no longer serializes production-facility construction. When Barracks two completes, a separate available SCV may start Barracks three while the follow-up Depot is still building; the Depot's completion is not a prerequisite for Barracks three. This keeps the four-Barracks production milestone moving while supply infrastructure catches up. The current controller still stops at four Barracks; the intended next composition step is a configurable 4-0-0 to 8-0-0 expansion, with gas and tech structures scheduled from surplus resources rather than treated as blockers to Marine production. A second Command Center is not a prerequisite for Barracks three. Expansion placement remains a separate economic decision derived from visible neutral resource clusters, with the accepted placement retained in diagnostics for the future Orbital flight/landing controller. Construction jobs track worker identity, observed structure association and normalized structure progress. The follow-up Depot is a first repeatable-supply step, not yet a predictive multi-Depot scheduler or a proven ramp wall-in. Placement remains a candidate-search process and requires local-match validation; do not infer wall-in integrity from legal placement alone.

## First Depot builder handoff

The first Depot SCV is dispatched when its estimated arrival overlaps predicted 100-mineral affordability. Travel time uses the selected worker's current position, candidate placement, and SC2-reported SCV movement speed. Mineral income is estimated from observed bank changes; negative/flat windows decay the estimate rather than pretending spending is income. The selected worker is reserved, placement is validated by SC2, and the live bank/cost/arrival checks remain authoritative. Empty-handed mining orders can be interrupted; cargo-bearing workers finish their return before dispatch.

The old fixed 80/95-mineral threshold and 202-game-loop (nine-second) gate are no longer active decision authorities. The legacy `supplyDepotWorkerPullMinerals` field remains readable for BotDefinition v1 compatibility but is ignored by the predictive scheduler.

## Persistent route FSM

Each worker-to-mineral assignment is a persistent operation with a stable assignment generation and context. It currently tracks:
- worker, mineral node and town-hall tags;
- assignment generation;
- observed phase (`Unassigned`, `Assigned`, `ExecutingOrder`, `CarryingResources`);
- last observed game loop and phase-entry game loop.

Still to add:
- current order/target identity;
- distinct outbound, gathering, returning, unloading and interrupted phases;
- route identity and cached to/from positions or geometry;
- predicted reevaluation deadline and actual phase timestamps;
- confidence and reason for assignment suspension or invalidation.

Phases are driven by observed facts: assigned, travelling, gathering, returning cargo, unloading, interrupted, reassignment pending and complete. Predictions help schedule work; they do not cause state transitions without confirmation.

Cache route geometry and stable positions. Recompute only when the worker route, mineral/town-hall position, map/pathing identity or measured route error makes the cache stale. Use game-loop deltas for low-cost timers; never assume sub-loop precision.

## Pathing and interaction calibration

The worker-route diagnostic trace is the evidence source for the next mining experiments. It records the match/map and bot mode, game-loop observations, resource banks, worker identity and assignment generation, route phase and phase age, mineral identity and remaining contents, town-hall identity, worker position/cargo/current order, and SC2 pathing distances when those query results are available.

Use the trace to separate three quantities that must not be conflated:

1. **Pathing distance:** SC2's queried route distance between the sampled start and destination positions. It is not elapsed travel time.
2. **Observed travel duration:** game-loop delta between confirmed route-phase observations. Compare this with pathing distance and SC2-reported movement speed; do not infer an exact travel duration from Euclidean distance alone.
3. **Interaction envelope:** the effective position/range at which a gather or return interaction succeeds. Infer this only from repeated observations that correlate command acceptance, worker movement, order/target changes, cargo transitions, mineral-content changes and resource-bank changes. A command being submitted is not proof that gathering or unloading occurred.

For each map and bot-definition version, retain a passive or otherwise unchanged baseline, then change one scheduling variable per comparison. Compare worker-cycle time in travel, gathering, return, queue/wait and idle phases; minerals delivered per game loop; mineral depletion; worker reassignment and wasted travel; resource starvation; and production uptime. Use repeated paired runs where practical. Keep raw traces and report sample counts and variation, not only the best run.

Do not convert a single observed stop position into a universal mineral or town-hall radius. Terrain, pathing, unit collision, resource-node geometry and approach direction can change the result. Store observed facts separately from inferred boundaries, include the map and game-data context, and retain a confidence/sample count for any calibrated value. The first safe optimization target is to improve route-time estimates; interaction-boundary scheduling should follow only after repeatable evidence exists.

## Scheduling and measurement

Record:
- minerals delivered per game loop/minute;
- worker time in travel, queue/wait, gather, return, unload, construction and idle;
- assignments, reassignments and wasted travel;
- resource bank/starvation and supply blocks;
- Depot/Barracks start and completion;
- Barracks production uptime and Marine output;
- rejected or stale actions.

Do not optimize only time-to-first-Marine. A useful opening sustains production and keeps the economy healthy.

## Construction and production gates

Before issuing construction:
1. confirm the builder is an owned, live SCV and not committed to another action;
2. confirm the structure's ability and placement are valid;
3. confirm minerals, supply and prerequisites;
4. confirm the intended placement is legal and not stale;
5. reserve the builder and resources to prevent duplicate commands.

Before training a Marine:
1. confirm a completed, owned Barracks;
2. confirm the train ability is available;
3. confirm resources and supply;
4. confirm queue capacity and no conflicting order;
5. submit and record the action result.

Four Barracks is the first production milestone, not the ceiling. The long-term configurable target is eight Barracks with three Tech Labs and five Reactors, subject to economy, technology, threat and measured match performance. The controller must not assume all eight should start simultaneously if doing so starves SCV production, supply, expansion or the Marine engine. Reactor queues, Tech Lab unit eligibility, add-on attachment, gas demand, supply and production-time reservations must be modeled explicitly. Marines remain the dominant army component; Marauders, Reapers and later Ghosts are bounded support allocations. Medivacs are produced through Starports and support healing, drops, extraction and repositioning. The schedule is declarative, measured and versioned.

## Replay contract

Every decision records game-loop identity, normalized facts, relevant assignments, candidate choices, selected action, validation result and outcome. Replaying the same observation sequence with the same strategy/policy version must reproduce assignments, intents and reason codes.

## Experiments

Once the baseline works, compare one scheduling change at a time:
- global minimum-distance matching for primary mineral slots;
- extra workers assigned to the farthest-back mineral slots;
- pathing-aware distance instead of straight-line distance;
- saturation-aware distribution;
- arrival-aware service-window scheduling;
- cached route geometry versus recomputation.

Use paired local matches or equivalent replay fixtures. Retain raw traces and report full worker-cycle and production metrics. Do not add speculative route tricks or special-case map mechanics to the production policy without repeatable evidence.

## Orbital fleet and MULE-backed mobile logistics

The long-term economy may grow beyond one or two town halls into a fleet of Command Centers / Orbital Commands. This is a strategic capability target, not part of the first playable opening. The economy commander must manage the fleet as a set of distinct assets with explicit roles and shared resource / safety constraints.

### Orbital roles

- **Home anchors:** remain landed at protected, saturated bases; train SCVs, provide vision / detection / energy utility and preserve a reliable production and recovery core.
- **Expansion anchors:** land at validated expansion sites, establish a new mineral line, train or receive SCVs, and support the expansion until its economy is stable.
- **Mobile MULE tenders:** lift only when the home / expansion anchor minimum is satisfied; travel to a safe, useful landing / operating area near an available mineral line; land and verify the grounded state; call down a MULE only when energy and the target's actual range / legality permit; verify the ability result and observed MULE; then lift and continue or return to a safe anchor.
- **Scouts / reserve:** perform bounded map reconnaissance or remain available for emergency relocation. Do not expose irreplaceable economic capacity to unsupported anti-air routes.

### Fleet controller requirements

- Maintain an inventory by stable unit tag, observed morph/type, landed/flying state, health, energy, orders, current role, home assignment, passengers / cargo if any, and last observed game loop.
- Set a configurable minimum number of grounded town halls at active mining bases and a minimum grounded SCV-production reserve before authorizing another lift.
- Select destinations from map bounds, pathability/buildability, landing clearance, resource clusters, friendly territory, enemy threat / anti-air exposure, route length and current vision. Query SC2 for final landing / ability legality.
- Treat landing as a multi-step operation: choose site, validate, issue move / land, observe actual landing, verify location and orders, then enable MULE / SCV / transfer tasks. A submitted command is not proof of arrival or landing.
- MULE deployment must target a visible, valid mineral field within the actual ability range and legal cast context. A wide open patch of terrain is not itself a MULE target. If no legal mineral target exists from the current position, reposition safely or wait; never spam an invalid call-down.
- Before takeoff, confirm the current base has sufficient grounded town halls / SCVs, no critical local production or evacuation task is being interrupted, and the mobile unit has a safe route and fallback destination.
- Track energy accumulation and the opportunity cost of spending it now versus saving it for a more valuable mineral line or emergency.
- Use hysteresis and role reservations to avoid fleets repeatedly lifting / landing in response to small score changes. Bound concurrent airborne Orbitals and preserve safe spacing / path corridors.
- If the SCV count collapses, switch to an emergency recovery policy: identify safe landing sites near a protected friendly base, land available flying Command Centers / Orbitals, confirm they are grounded, then resume SCV production where legal and affordable. Prioritize rebuilding a minimum worker core and protecting the remaining production chain before returning to MULE-tender operations.
- A fleet with dozens of Orbitals is an eventual high-economy experiment, not a prerequisite for winning. Expansion cost, Orbital upgrade cost, supply / worker needs, flight exposure, map space and diminishing MULE value must be measured. The scheduler should stop adding town halls when marginal return or survival risk becomes unfavorable.

### Required fleet telemetry and tests

Record grounded versus airborne Orbitals by role, travel time, landing rejection / failure, health lost in flight, energy gained/spent, legal MULE casts, MULE mining duration / delivered value, mineral-line uptime, SCV production uptime, worker losses / recovery time, and the fraction of time each Orbital is productively assigned.

Fixtures must cover: no safe landing site; blocked or invalid landing; threat along route; target mineral outside MULE range; insufficient energy; duplicate pending cast; takeoff denied by minimum-anchor policy; too many Orbitals airborne; emergency SCV collapse; safe emergency landing and SCV restart; and fleet hysteresis / role stability.


## First Command Center control gate

The first production request is deliberately observable and comes before worker-route actions in the submitted action batch. The adapter reads SCV `AbilityId`, `MineralCost`, and `FoodRequired` from the authoritative game-data response and reads minerals, food used, and food cap from `PlayerCommon`. It submits an unqueued raw unit command against an idle owned Command Center. It does not invent an ability ID or assume the bank is sufficient. The API `ResponseAction.Result` is delivered back to the bot; subsequent observations provide the Command Center's actual order and any `ActionErrors`. A request is not considered proven merely because it was constructed or sent. The local runner uses realtime mode for human-readable playback; ladder sessions remain stepped. Automated tests cover eligibility, duplicate suppression, pending-request behavior, and rejection retry. A local match is still required to prove the game accepts the request and visibly begins SCV production.


## First Supply Depot and construction-crew handoff

The initial Depot builder is selected by predicted arrival affordability, not a fixed pull threshold. The scheduler uses SC2-reported SCV movement speed, candidate-site distance, observed mineral-bank deltas and the authoritative 100-mineral Depot cost. Empty-handed mining orders can be interrupted when the forecast says the worker will arrive as the Depot becomes affordable; cargo-bearing workers finish their return before dispatch. SC2 placement acceptance, live affordability and observed arrival remain mandatory before construction.

While the Depot builds, a second SCV is reserved and its Barracks site is validated. Its departure is timed against the later of predicted Depot completion and 150-mineral affordability, so it should arrive at the site near the first legal Barracks start rather than waiting there idle. If build-time or income forecasts are unavailable, the controller holds the worker rather than sending it early. The Depot builder is released if a different SCV takes the Barracks; the Barracks builder is released after completion.

SCV production is also scheduled around the Barracks completion. An SCV is started only when its SC2-reported build duration plus a handoff margin fits inside the Barracks' estimated remaining build time. The Orbital's 150 minerals are reserved from the first Barracks commitment. Once the SCV queue must drain, no new SCV is started and the Command Center is reserved; on the first observation where the Depot and Barracks are complete, the Orbital morph is eligible immediately if the queue is clear and the live bank covers it.

The current travel estimate uses straight-line distance divided by SC2 movement speed. Pathing-aware ETA calibration, observed match timing and paired Depot/Barracks placement optimization remain follow-up work. This is a predictive first opening, not yet a proof of a mathematically optimal build order or a general multi-builder construction queue.

## First worker control proof: Spray, then mine

After the first Command Center SCV-training order is observed, the controller chooses an assigned, non-cargo SCV and asks SC2 for that unit's available abilities. It issues Spray only when a unique Spray ability is present in game data and the unit-specific query confirms it is available. It then waits for the SC2 action result before explicitly gathering the same mineral tag again. The original assignment is reserved during this sequence and restored afterward. If Spray is missing, ambiguous, or unavailable to the SCV, the controller reports that fact and restores the assignment; it does not guess an ability ID. The local match must confirm that the accepted Spray is visibly rendered.


## Diagnostic extraction

Local Runner matches capture diagnostics by default; `--no-diagnostics` is the explicit opt-out. Ladder runs require `--diagnostics` to opt in. `--passive-baseline` remains an explicit no-action local run.

Terran diagnostic sessions now include `worker-route-<timestamp>.csv` and `worker-route-latest.csv`. Each observation row records the worker assignment generation, observed route phase and phase age, worker position/cargo/order, assigned mineral and remaining mineral contents, assigned town hall, player resource bank, and any available SC2 pathing distances for worker-to-mineral and town-hall-to-mineral pairs. The trace is observational; it does not issue commands or change reservations. It is the first dataset for measuring worker utilization, mineral-patch uptime, return cycles, and travel overlap.

## Predictive construction scheduling

The first opening now implements a bounded desired-arrival scheduler for the initial Depot and first Barracks. It forecasts net mineral income from observed bank deltas, uses SC2 build-time and movement-speed metadata, and schedules the two worker departures against resource affordability and prerequisite completion. SCV production is gated by the remaining Barracks build time so the starting Command Center can be clear for the Orbital morph at Barracks completion.

The next generalization is a reusable queue of typed construction jobs with explicit desired start windows, mineral/gas/supply costs, prerequisites, placement constraints, expected duration, worker disposition and observed completion. Keep workers reserved only across committed construction handoffs; otherwise return them to the mining allocator. Forecasts schedule intents, while observed resources, prerequisites, placement responses and action results remain execution gates. The first Depot worker is now selected at the predicted resource/travel convergence point; there is no fixed nine-second pull delay.



## Scalable Marine-centered production doctrine

The first playable target remains four Barracks producing Marines. Once that engine is stable, the strategy may expand toward eight Barracks with a reference allocation of three Tech Labs and five Reactors. This is not a rigid opening sequence: the production planner must choose when to expand, when to attach add-ons, and when to invest in gas / Starports based on observed income, supply, technology, map and enemy composition.

The composition policy must keep Marines visibly dominant. Marauders provide specialized durability and anti-armor support; Reapers provide reconnaissance and disruption; Ghosts are a later technology investment for specialized tactical effects. Medivacs add healing and tactical mobility. Their drops must be complete transport workflows with confirmed load, route, unload, healing / extraction and return observations.

The scheduler must never allow optional support production to starve Marine production, worker economy, supply or essential infrastructure. When the economy cannot sustain the requested composition, it should expose the unmet target and continue the highest-priority affordable Marine-centered plan.

# Terran opening and worker logistics

## Objective

The first playable target is a disciplined Terran economy that establishes four Barracks and produces a sustainable stream of Marines without gas. The immediate goal is not a theoretically optimal build order; it is a reliable, measurable loop that makes legal decisions from live observations.

## Opening responsibilities

The opening controller coordinates:
- SCV production from the Command Center as the first action in the opening;
- deterministic assignment of starting SCVs to mineral routes;
- first Supply Depot builder selection;
- Depot and Barracks construction;
- expansion to four Barracks;
- Marine production without gas;
- resource and supply budgeting so construction does not starve production.

All costs, prerequisites, build times, supply effects and abilities must come from current game data or validated ontology. Never hard-code an assumed timing as a fact.

## Worker assignment

The economy controller is the sole owner of worker-to-resource assignments and reservations. It evaluates candidate mineral routes and assigns each worker deliberately. Unit FSMs execute their assigned work; they do not race each other to select patches.

The planner uses deterministic saturation-aware allocation (two workers per mineral by default), stable assignment generations and explicit death-event invalidation. On the initial allocation it treats SC2's existing gather orders as replaceable hints and solves a minimum-total-distance matching across the primary slot at every mineral patch. It then adds second-worker slots from the patches farthest from the Command Center inward and matches the remaining workers to those slots. The initial matching now asks SC2 for pathing distance from each starting SCV to each mineral patch, plus Command-Center-to-patch pathing distance for the farthest-back ordering. If pathing results are missing, invalid or delayed beyond 22 game loops, it falls back to straight-line distance. Later workers are still assigned using the current geometric-distance allocator; pathing queries for newly produced workers remain future work. Reservations survive temporary worker or target occlusion; the action translator still requires a currently visible mineral before issuing a new gather command. Each visible worker FSM context receives its assigned mineral/town-hall tags, generation and observed route phase.

The latest Acolyte trace is the pre-change baseline: all 12 starting SCVs initially pointed at one mineral patch. By game loop 22 the old sequential allocator had spread workers across all eight patches and doubled four of them, but it did not globally match the closest SCV to each patch. For example, a worker was assigned to the patch at (140,168.5) from roughly 5 units away while another SCV was only about 1.5 units away but had been assigned elsewhere. A retrospective straight-line comparison using the initial SCV positions and the old loop-22 target map is 69.44 total map units; the global Euclidean matching lowers that assignment cost to 53.97 (about 22.3%). This is a static assignment comparison, not measured mining throughput. The live initial allocation now uses SC2 pathing distances, so its final pairing may differ from that Euclidean lower-bound calculation.

The initial allocation is now a global minimum-cost assignment over worker-to-mineral slots. It requests SC2 pathing distances for every starting worker/mineral pair and town-hall/mineral pair; when those results are unavailable, invalid or delayed beyond 22 game loops, the planner falls back to straight-line distance rather than stalling the economy. Every mineral receives a primary slot first, then supplemental slots are added from the farthest patch inward up to the configured saturation. The assignment solver minimizes total route cost for the available workers and slots. This is an assignment-cost optimization, not a claim of maximum mining throughput: actual throughput also depends on interaction geometry, resource depletion, worker collision, gathering cadence and return timing.

The first Depot pull is now an arrival-time scheduling decision, not a fixed 80/95-mineral threshold or a fixed nine-second timer. The controller samples the observed mineral bank over game-loop windows, estimates a conservative net mineral-income rate, reads the SCV movement speed and Depot cost from the match's `ResponseData`, estimates the selected worker's travel time to the candidate site, and reserves the worker when the predicted bank at arrival reaches the full Depot cost (plus other reservations). The actual Build command still requires live minerals, accepted SC2 placement, and observed worker arrival. Empty-handed mining orders are deliberately interruptible once the schedule says to depart; cargo-bearing workers are not sent away with a load. The income forecast is advisory and must be calibrated against match traces; it is not yet a claim of mathematically optimal timing.

## Persistent route FSM

Each worker-to-mineral assignment is a persistent operation with a stable assignment generation and context. It currently tracks:
- worker, mineral node and town-hall tags;
- assignment generation;
- observed phase (`Unassigned`, `Assigned`, `ExecutingOrder`, `CarryingResources`);
- last observed game loop and phase-entry game loop.

Still to add:
- current order/target identity;
- distinct outbound, gathering, returning, unloading and interrupted phases;
- route identity and cached to/from positions or geometry;
- predicted reevaluation deadline and actual phase timestamps;
- confidence and reason for assignment suspension or invalidation.

Phases are driven by observed facts: assigned, travelling, gathering, returning cargo, unloading, interrupted, reassignment pending and complete. Predictions help schedule work; they do not cause state transitions without confirmation.

Cache route geometry and stable positions. Recompute only when the worker route, mineral/town-hall position, map/pathing identity or measured route error makes the cache stale. Use game-loop deltas for low-cost timers; never assume sub-loop precision.

## Scheduling and measurement

Record:
- minerals delivered per game loop/minute;
- worker time in travel, queue/wait, gather, return, unload, construction and idle;
- assignments, reassignments and wasted travel;
- resource bank/starvation and supply blocks;
- Depot/Barracks start and completion;
- Barracks production uptime and Marine output;
- rejected or stale actions.

Do not optimize only time-to-first-Marine. A useful opening sustains production and keeps the economy healthy.

## Construction and production gates

Before issuing construction:
1. confirm the builder is an owned, live SCV and not committed to another action;
2. confirm the structure's ability and placement are valid;
3. confirm minerals, supply and prerequisites;
4. confirm the intended placement is legal and not stale;
5. reserve the builder and resources to prevent duplicate commands.

Before training a Marine:
1. confirm a completed, owned Barracks;
2. confirm the train ability is available;
3. confirm resources and supply;
4. confirm queue capacity and no conflicting order;
5. submit and record the action result.

Four Barracks is the first production milestone, not the ceiling. The long-term configurable target is eight Barracks with three Tech Labs and five Reactors, subject to economy, technology, threat and measured match performance. The controller must not assume all eight should start simultaneously if doing so starves SCV production, supply, expansion or the Marine engine. Reactor queues, Tech Lab unit eligibility, add-on attachment, gas demand, supply and production-time reservations must be modeled explicitly. Marines remain the dominant army component; Marauders, Reapers and later Ghosts are bounded support allocations. Medivacs are produced through Starports and support healing, drops, extraction and repositioning. The schedule is declarative, measured and versioned.

## Replay contract

Every decision records game-loop identity, normalized facts, relevant assignments, candidate choices, selected action, validation result and outcome. Replaying the same observation sequence with the same strategy/policy version must reproduce assignments, intents and reason codes.

## Experiments

Once the baseline works, compare one scheduling change at a time:
- global minimum-distance matching for primary mineral slots;
- extra workers assigned to the farthest-back mineral slots;
- pathing-aware distance instead of straight-line distance;
- saturation-aware distribution;
- arrival-aware service-window scheduling;
- cached route geometry versus recomputation.

Use paired local matches or equivalent replay fixtures. Retain raw traces and report full worker-cycle and production metrics. Do not add speculative route tricks or special-case map mechanics to the production policy without repeatable evidence.

## First Command Center control gate

The first production request is deliberately observable and comes before worker-route actions in the submitted action batch. The adapter reads SCV `AbilityId`, `MineralCost`, and `FoodRequired` from the authoritative game-data response and reads minerals, food used, and food cap from `PlayerCommon`. It submits an unqueued raw unit command against an idle owned Command Center. It does not invent an ability ID or assume the bank is sufficient. The API `ResponseAction.Result` is delivered back to the bot; subsequent observations provide the Command Center's actual order and any `ActionErrors`. A request is not considered proven merely because it was constructed or sent. The local runner uses realtime mode for human-readable playback; ladder sessions remain stepped. Automated tests cover eligibility, duplicate suppression, pending-request behavior, and rejection retry. A local match is still required to prove the game accepts the request and visibly begins SCV production.


## First Supply Depot and construction-crew handoff

The initial Depot builder is selected by predicted arrival affordability, not a fixed pull threshold. The scheduler uses SC2-reported SCV movement speed, candidate-site distance, observed mineral-bank deltas and the authoritative 100-mineral Depot cost. Empty-handed mining orders can be interrupted when the forecast says the worker will arrive as the Depot becomes affordable; cargo-bearing workers finish their return before dispatch. SC2 placement acceptance, live affordability and observed arrival remain mandatory before construction.

While the Depot builds, a second SCV is reserved and its Barracks site is validated. Its departure is timed against the later of predicted Depot completion and 150-mineral affordability, so it should arrive at the site near the first legal Barracks start rather than waiting there idle. If build-time or income forecasts are unavailable, the controller holds the worker rather than sending it early. The Depot builder is released if a different SCV takes the Barracks; the Barracks builder is released after completion.

SCV production is also scheduled around the Barracks completion. An SCV is started only when its SC2-reported build duration plus a handoff margin fits inside the Barracks' estimated remaining build time. The Orbital's 150 minerals are reserved from the first Barracks commitment. Once the SCV queue must drain, no new SCV is started and the Command Center is reserved; on the first observation where the Depot and Barracks are complete, the Orbital morph is eligible immediately if the queue is clear and the live bank covers it.

The current travel estimate uses straight-line distance divided by SC2 movement speed. Pathing-aware ETA calibration, observed match timing and paired Depot/Barracks placement optimization remain follow-up work. This is a predictive first opening, not yet a proof of a mathematically optimal build order or a general multi-builder construction queue.

## First worker control proof: Spray, then mine

After the first Command Center SCV-training order is observed, the controller chooses an assigned, non-cargo SCV and asks SC2 for that unit's available abilities. It issues Spray only when a unique Spray ability is present in game data and the unit-specific query confirms it is available. It then waits for the SC2 action result before explicitly gathering the same mineral tag again. The original assignment is reserved during this sequence and restored afterward. If Spray is missing, ambiguous, or unavailable to the SCV, the controller reports that fact and restores the assignment; it does not guess an ability ID. The local match must confirm that the accepted Spray is visibly rendered.


## Diagnostic extraction

Normal local matches no longer write per-frame CSV traces, terrain snapshots or protocol snapshots. Use `--diagnostics` to opt in to those captures for a focused investigation; `--passive-baseline` remains an explicit no-action run that records a baseline. The latest complete diagnostic bundle is retained as the reference run, and superseded run files are pruned after analysis.

## Predictive construction scheduling

The first opening now implements a bounded desired-arrival scheduler for the initial Depot and first Barracks. It forecasts net mineral income from observed bank deltas, uses SC2 build-time and movement-speed metadata, and schedules the two worker departures against resource affordability and prerequisite completion. SCV production is gated by the remaining Barracks build time so the starting Command Center can be clear for the Orbital morph at Barracks completion.

The next generalization is a reusable queue of typed construction jobs with explicit desired start windows, mineral/gas/supply costs, prerequisites, placement constraints, expected duration, worker disposition and observed completion. Keep workers reserved only across committed construction handoffs; otherwise return them to the mining allocator. Forecasts schedule intents, while observed resources, prerequisites, placement responses and action results remain execution gates.



## Scalable Marine-centered production doctrine

The first playable target remains four Barracks producing Marines. Once that engine is stable, the strategy may expand toward eight Barracks with a reference allocation of three Tech Labs and five Reactors. This is not a rigid opening sequence: the production planner must choose when to expand, when to attach add-ons, and when to invest in gas / Starports based on observed income, supply, technology, map and enemy composition.

The composition policy must keep Marines visibly dominant. Marauders provide specialized durability and anti-armor support; Reapers provide reconnaissance and disruption; Ghosts are a later technology investment for specialized tactical effects. Medivacs add healing and tactical mobility. Their drops must be complete transport workflows with confirmed load, route, unload, healing / extraction and return observations.

The scheduler must never allow optional support production to starve Marine production, worker economy, supply or essential infrastructure. When the economy cannot sustain the requested composition, it should expose the unmet target and continue the highest-priority affordable Marine-centered plan.



## Expansion and Orbital scaling

The first expansion Command Center is now a prerequisite gate before Barracks three. The controller clusters visible neutral mineral fields (and nearby geysers), rejects the occupied main and enemy-side clusters, derives candidate town-hall positions behind the mineral line, and asks SC2 to validate the actual placement. It does not use guessed map coordinates. The selected position is retained after construction so a later Orbital mobility controller can return a base-assigned town hall to that exact resource location.

The current milestone stops at a grounded expansion Command Center. Automatic lift/flight/landing, roaming Orbital behavior, multi-base assignment, Refinery construction, and Engineering Bay construction are follow-on work. The existing Orbital controller continues to upgrade eligible grounded Command Centers and call MULEs on minerals within authoritative cast range. Gas and Engineering Bay timing will be added as separate resource-constrained jobs rather than spending the same minerals twice or delaying Marine production without diagnostic evidence.


## Just-in-time opening schedule

The opening is coordinated around desired arrival windows:

1. **Supply Depot:** estimate the SCV's travel loops and the observed mineral-income rate; depart when the forecast says 100 minerals will be available at the accepted site. Construction begins only when the SCV is there and the live bank covers the full cost.
2. **First Barracks:** while the Depot is building, reserve a second SCV and validate the Barracks placement. Estimate the Depot's completion loop from its authoritative build time and observed progress. Dispatch the second SCV so its arrival overlaps the later of Depot completion and predicted 150-mineral affordability (after other reservations). If either forecast is unavailable, hold rather than dispatch early. The Build command still waits for the Depot completion and live minerals.
3. **Orbital Command:** reserve the 150-mineral morph as soon as the first Barracks build is committed. Continue SCV production only when the SCV's authoritative build time plus a handoff margin fits inside the Barracks' estimated remaining build time. Once that window closes, stop starting SCVs and reserve the Command Center queue. On the first observation where the Depot and Barracks are complete, the Orbital controller may issue the morph immediately if the Command Center has no SCV order and the live bank covers the morph.

Build times and movement speeds come from SC2 `ResponseData`; progress and mineral bank come from live observations. Forecasts only decide when to begin travel or whether to begin another SCV. They never override live costs, prerequisites, accepted placement, worker arrival, or observed action results. Initial worker-to-mineral matching already requests SC2 pathing distances; the new worker-route trace preserves those measurements where available. Later assignments still use geometric fallback until their own pathing measurements are requested. The next experiments should compare straight-line distance, SC2 pathing distance, and measured movement duration, then calibrate the mineral and town-hall interaction boundaries from observed gather/unload outcomes.
