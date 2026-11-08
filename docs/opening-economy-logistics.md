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

The planner uses deterministic saturation-aware allocation (two workers per mineral by default), stable assignment generations and explicit death-event invalidation. It adopts existing gather orders when their target is a visible mineral field, so SC2's initial worker distribution becomes the planner's starting assignment instead of being ignored. It retains reservations through temporary worker or target occlusion; the action translator still requires a currently visible mineral before issuing a new gather command. Each visible worker FSM context receives its assigned mineral/town-hall tags, generation and observed route phase.

The currently checked-in builder is still a single-Depot prototype with a 10-mineral threshold and releases its worker when that Depot completes. That implementation does not yet satisfy the intended opening. The target policy is to identify the exact SCV whose unload causes the mineral bank to cross 80 (not merely whichever worker happens to be carrying cargo in the next observation), reserve that persistent worker context, and keep it assigned through a raised Supply Depot followed by a Barracks. The 80 threshold is a worker-pull trigger, not the Depot affordability gate: the controller must still wait for the authoritative Depot cost before construction and the Barracks cost/prerequisite before the second build. If multiple unloads make attribution ambiguous, fail closed and record the ambiguity rather than selecting an unrelated worker. The target placement is a ramp wall: infer the ramp from map geometry/pathing, query SC2 for legal building positions, then evaluate Depot/Barracks pairs and verify that enemy ground units cannot pass in a local match. Never lower the Depot. After both structures are observed complete, release the same SCV back to its original economy route. These are target requirements, not claims about the current implementation.

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
- nearest eligible mineral assignment;
- saturation-aware distribution;
- arrival-aware service-window scheduling;
- cached route geometry versus recomputation.

Use paired local matches or equivalent replay fixtures. Retain raw traces and report full worker-cycle and production metrics. Do not add speculative route tricks or special-case map mechanics to the production policy without repeatable evidence.

## First Command Center control gate

The first production request is deliberately observable and comes before worker-route actions in the submitted action batch. The adapter reads SCV `AbilityId`, `MineralCost`, and `FoodRequired` from the authoritative game-data response and reads minerals, food used, and food cap from `PlayerCommon`. It submits an unqueued raw unit command against an idle owned Command Center. It does not invent an ability ID or assume the bank is sufficient. The API `ResponseAction.Result` is delivered back to the bot; subsequent observations provide the Command Center's actual order and any `ActionErrors`. A request is not considered proven merely because it was constructed or sent. The local runner uses realtime mode for human-readable playback; ladder sessions remain stepped. Automated tests cover eligibility, duplicate suppression, pending-request behavior, and rejection retry. A local match is still required to prove the game accepts the request and visibly begins SCV production.


## First Supply Depot builder gate

The builder controller runs only after the first Command Center SCV-training order has been observed. It latches the mineral-bank crossing, chooses a returning SCV deterministically by unit tag, reserves it, and asks SC2 to validate candidate positions using `RequestQueryBuildingPlacement` with the Supply Depot build ability and selected builder tag. A successful placement response is required before movement. The Move ability, Depot build ability, and Depot mineral cost are resolved from game data; the bot does not embed their numeric IDs or cost. The worker is moved to the accepted site and the build command is issued only when the live bank reaches the authoritative cost. The reservation is released after the Depot is observed complete. Automated tests cover the threshold, both observed return-order forms, placement-query selection, move/build sequencing, and reservation release. A live match is still required to validate the actual order/placement behavior and timing.


## First worker control proof: Spray, then mine

After the first Command Center SCV-training order is observed, the controller chooses an assigned, non-cargo SCV and asks SC2 for that unit's available abilities. It issues Spray only when a unique Spray ability is present in game data and the unit-specific query confirms it is available. It then waits for the SC2 action result before explicitly gathering the same mineral tag again. The original assignment is reserved during this sequence and restored afterward. If Spray is missing, ambiguous, or unavailable to the SCV, the controller reports that fact and restores the assignment; it does not guess an ability ID. The local match must confirm that the accepted Spray is visibly rendered.
