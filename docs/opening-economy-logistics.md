# Terran opening and worker logistics

## Objective

The first playable target is a disciplined Terran economy that establishes four Barracks and produces a sustainable stream of Marines without gas. The immediate goal is not a theoretically optimal build order; it is a reliable, measurable loop that makes legal decisions from live observations.

## Opening responsibilities

The opening controller coordinates:
- SCV production from the Command Center;
- deterministic assignment of starting SCVs to mineral routes;
- first Supply Depot builder selection;
- Depot and Barracks construction;
- expansion to four Barracks;
- Marine production without gas;
- resource and supply budgeting so construction does not starve production.

All costs, prerequisites, build times, supply effects and abilities must come from current game data or validated ontology. Never hard-code an assumed timing as a fact.

## Worker assignment

The economy controller is the sole owner of worker-to-resource assignments and reservations. It evaluates candidate mineral routes and assigns each worker deliberately. Unit FSMs execute their assigned work; they do not race each other to select patches.

The current planner uses deterministic saturation-aware allocation (two workers per mineral by default), stable assignment generations and explicit death-event invalidation. It retains a reservation when the worker or target is temporarily absent from the observation; the action translator still requires a currently visible mineral before issuing a gather command. Each visible worker FSM context receives its assigned mineral/town-hall tags, generation and observed route phase. This is reservation/phase plumbing; it is not yet a complete route scheduler.

For the first Depot, select the closest eligible SCV using current positions and work state. Prefer a worker whose assignment can be released with the least lost mining time, rather than blindly interrupting the closest busy worker. The selected builder's assignment is explicitly suspended/released and is not silently duplicated.

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