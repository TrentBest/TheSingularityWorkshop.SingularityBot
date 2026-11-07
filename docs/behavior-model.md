# Behavior and FSM model

## Core rule

The bot is an FSM-driven runtime. Strategy and execution are expressed as reusable FSM definitions bound to persistent, observation-derived contexts. FSM_API provides the heartbeat; FSM_Foundry executes the composed workload. SC2-specific types remain in the adapter.

Behavior emits domain intents. A separate action pipeline arbitrates and validates them against the current observation before translation to SC2 actions.

## Hierarchy

1. **Match** — lifecycle, opponent/player identity, game result and global phase.
2. **Commander / strategy** — current objective, priorities and hard constraints.
3. **Economy** — worker allocation, resource budget, supply, construction and production schedule.
4. **Resource route** — one persistent worker-to-resource operation, including travel, service, cargo return and unload.
5. **Unit** — local behavior for the unit's current profile.
6. **Action** — validate and translate the requested command.

Parents own goals and global constraints. Children execute assigned work and report observed facts upward. A worker FSM does not independently pick a mineral patch if the economy controller has already assigned one.

## Persistent worker routes

A mining route is not a one-frame gather command. Its context retains the worker tag, mineral tag, town-hall tag, assignment generation, route identity, phase, last observed game loop, current order/target, cargo facts and cached route geometry.

The route FSM advances from evidence in observations: dispatch, travel, gathering, cargo return, unloading, interruption or reassignment. It may use inexpensive game-loop deadlines to decide when to reevaluate. A timer expiring is not proof that travel or gathering completed; current facts must confirm the transition.

Cache the to-and-fro route geometry rather than repeatedly recomputing stable positions and distances. Recompute only when relevant positions, map/pathing identity, resource/town-hall identity or observed route error invalidate the cache. Keep estimates distinct from measured outcomes.

The economy controller owns assignment and reservation decisions globally. The route FSM owns execution of its one assignment.

## First behavior target: Terran economy

The initial proof is a no-gas, four-Barracks, pure-Marine opening. It is intentionally narrower than a generalized multi-race strategy framework.

- Start the Command Center's SCV production as appropriate.
- Assign starting SCVs to mineral routes deterministically.
- Select the closest eligible SCV for the first Supply Depot, accounting for its current route/work state.
- Complete the first Depot and Barracks with validated placement, costs, prerequisites and builder selection.
- Establish four Barracks and queue Marines as resources and supply permit.
- Keep enough workers mining to sustain production; measure rather than assume the value of each reassignment.

The build order is versioned data. Do not bake a particular timing or an assumed optimal schedule into the generic FSM runtime.

## Profile changes and entity identity

A unit tag may survive a morph while its role changes. Reconciliation must compare the observed unit kind and behavior profile. When a profile changes, retire the old handle/context safely and attach the correct profile, or perform an explicit compatible reconfiguration. Never let an SCV worker behavior continue controlling a Marine merely because the tag is unchanged.

## Action pipeline

The FSM emits a domain intent. The action pipeline then:
- verifies the source unit is owned, alive and currently actionable;
- verifies current visibility/knowledge and the intended target;
- resolves the ability from current game data rather than guessed constants;
- checks ability availability, prerequisites, affordability, range and placement where relevant;
- arbitrates multiple intents for one unit and conflicting economy reservations;
- submits only validated protocol actions and records the result.

Unsupported intents remain diagnostics, not silent no-ops or fabricated actions.

## GUI contract

GUI is the authoring and inspection surface for the runtime's actual versioned contracts. It should expose:
- hierarchical FSM graphs and reusable behavior patterns;
- typed facts, intents, capabilities and parameters;
- worker/resource assignments and route timelines;
- cached route geometry and measured travel/cycle phases;
- active state, eligible/rejected transitions and reasons;
- candidate intents, arbitration, validation and action outcomes;
- deterministic replay against recorded observations;
- versioned import/export used by CI and the runtime.

The editor must not implement a second FSM engine or become a runtime dependency.

## Current implementation status

The active codebase has an FSM foundation, normalized observation model, persistent unit reconciliation, profile-aware morph handling, intent buffering, and initial SCV/Probe/Drone gather and return action translation. This is a partial vertical slice, not a completed economy.

Persistent worker-to-mineral assignment, route scheduling/cache, builder selection, construction, four-Barracks production, live-match proof and replay-based performance evidence are still required. Documentation and tests must distinguish implemented behavior from intended behavior.
