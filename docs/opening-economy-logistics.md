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

The planner uses deterministic saturation-aware allocation (two workers per mineral by default), stable assignment generations and explicit death-event invalidation. On the initial allocation it treats SC2's existing gather orders as replaceable hints and solves a minimum-total-distance matching across the primary slot at every mineral patch. It then adds second-worker slots from the patches farthest from the Command Center inward and matches the remaining workers to those slots. Later workers are assigned without disturbing stable routes. Reservations survive temporary worker or target occlusion; the action translator still requires a currently visible mineral before issuing a new gather command. Each visible worker FSM context receives its assigned mineral/town-hall tags, generation and observed route phase.

The latest Acolyte trace is the pre-change baseline: all 12 starting SCVs initially pointed at one mineral patch. By game loop 22 the old sequential allocator had spread workers across all eight patches and doubled four of them, but it did not globally match the closest SCV to each patch. For example, a worker was assigned to the patch at (140,168.5) from roughly 5 units away while another SCV was only about 1.5 units away but had been assigned elsewhere. The new initial matching addresses that class of mismatch while preserving the four-extra-workers-on-the-farthest-patches policy. Its cost is still straight-line distance; the next refinement is pathing-aware route distance so the mineral-line approach and its two side routes are represented rather than assuming the hypotenuse is the path.

The latest Acolyte opening trace shows all 12 starting SCVs initially pointed at one mineral patch. The planner spreads them across all eight patches and then fills four second-worker slots on the patches farthest from the Command Center. However, it assigns workers sequentially and does not globally match each patch's first worker to the closest available SCV: at game loop 22, for example, a worker was assigned to the patch at (140,168.5) from roughly 5 units away while another SCV was only about 1.5 units away but had been assigned elsewhere. The next mining optimization is a global nearest-worker/patch matching for the first slot at each patch, followed by the four extra workers on the farthest-back patches. The eventual cost should use pathing-aware route distance, not just straight-line distance, so the mineral-line approach and its two side routes are represented rather than assuming the hypotenuse is the path.

The currently checked-in builder is still a single-Depot prototype with an 80-mineral threshold and releases its worker when that Depot completes. That implementation does not yet satisfy the intended opening. The target policy is to identify the exact SCV whose unload causes the mineral bank to cross 80 (not merely whichever worker happens to be carrying cargo in the next observation), reserve that persistent worker context, and keep it assigned through a raised Supply Depot followed by a Barracks. The 80 threshold is a worker-pull trigger, not the Depot affordability gate: the controller must still wait for the authoritative Depot cost before construction and the Barracks cost/prerequisite before the second build. If multiple unloads make attribution ambiguous, fail closed and record the ambiguity rather than selecting an unrelated worker. The target placement is a ramp wall: infer the ramp from map geometry/pathing, query SC2 for legal building positions, then evaluate Depot/Barracks pairs and verify that enemy ground units cannot pass in a local match. Never lower the Depot. After both structures are observed complete, release the same SCV back to its original economy route. These are target requirements, not claims about the current implementation.

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

Four Barracks is the target production footprint. The controller must not assume all four should start simultaneously if doing so starves SCV production, supply or the first structure. The schedule is measured and versioned.

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

Every SCV unit definition already carries the `ConstructionCapability` FSM. The opening controller now selects its builder from the live SCVs that actually have that capability, excluding workers already reserved by another task. It compares an empty worker's distance to the staging point against a cargo-bearing worker's estimated return-to-Command-Center distance, unload allowance and onward staging distance. The selected SCV is reserved and sent toward the candidate site before the Depot is affordable; if it is carrying resources, it finishes that return/unload before receiving the staging Move.

The Depot action remains gated by the authoritative game-data mineral cost and by the builder actually reaching the accepted placement. When the Depot is observed complete, the same SCV remains reserved and is passed as the preferred builder to the Barracks controller. The Barracks controller releases that worker only after the Barracks is observed complete, allowing the mining scheduler to take it back when no construction request remains. This is the first one-worker construction chain, not yet a general multi-builder queue.

The cargo comparison is currently a straight-line distance proxy with a fixed unload allowance, not a calibrated time-to-arrival model. The current candidate arc is a generic forward-side search around the town hall: it is not a ramp detector, does not evaluate Depot/Barracks pairs, and does not prove a wall-in. A local match must still verify the actual construction timing and placement.

## First worker control proof: Spray, then mine

After the first Command Center SCV-training order is observed, the controller chooses an assigned, non-cargo SCV and asks SC2 for that unit's available abilities. It issues Spray only when a unique Spray ability is present in game data and the unit-specific query confirms it is available. It then waits for the SC2 action result before explicitly gathering the same mineral tag again. The original assignment is reserved during this sequence and restored afterward. If Spray is missing, ambiguous, or unavailable to the SCV, the controller reports that fact and restores the assignment; it does not guess an ability ID. The local match must confirm that the accepted Spray is visibly rendered.


## Predictive construction scheduling

A build request should eventually carry a desired start game loop/time window, its mineral/gas/supply costs, prerequisites, placement constraints and expected duration. The construction scheduler should forecast resource income from observed worker-cycle rates, then dispatch a crew member early enough to arrive near the requested start—not merely wait at the threshold and begin travelling then. The current opening is the simpler first step: pre-stage the selected crew member immediately, then issue the Depot command as soon as the observed bank reaches its authoritative cost.

After the Depot-to-Barracks chain is proven, generalize this into a queue. If another request is ready, keep the builder reserved and dispatch it to the next site; otherwise release the worker to the mining scheduler. Mining then reconciles the returning worker with current patch saturation and can expose the next construction opportunity (for example, a Refinery when the economy can support it). Prediction may schedule a request, but observed resources, prerequisites, placement responses and action results remain the execution gates.

