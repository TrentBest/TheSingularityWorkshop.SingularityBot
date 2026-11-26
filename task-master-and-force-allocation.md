# Strategic Planner, scouting and military force allocation

## Purpose

The Strategic Planner is the central consumer and planner for the bot's available work and resources. It does not own strategic intent or job lifecycle. Each advisor owns its postings and may create, update, renew or withdraw them as its assessment changes. The Strategic Planner reads the combined live job market, resolves dependencies and conflicts, sequences work, and eventually reserves eligible units/resources. Task and unit FSMs execute assignments through the validated action pipeline.

This document records the intended architecture and future direction. It is not a claim that military task allocation, base-site FSMs, force composition matching or the fluent authoring API are implemented.

## Execution model: synchronous logical game tasks

A **job** or **task** here means an internal StarCraft II game objective or unit assignment—not a system process, operating-system job, thread-pool item, background task or asynchronous operation.

The ladder runner supports synchronous execution only. Advisors update logical task postings as part of the bot's normal frame processing. The Strategic Planner evaluates the current postings and produces decisions/assignments that flow through the existing synchronous FSM and action pipeline. No advisor or planner work is dispatched to threads, and no task posting implies concurrency.

## 1. Advisor-owned postings, one planning system

Each advisor is authoritative for its own job postings. A posting has a stable identity, owner, job kind, target, quantity, priority, timestamps, optional expiry and constraints. Updating a posting preserves its creation time and refreshes its revision timestamp. Withdrawing a posting removes that advisor's demand; an advisor cannot edit or withdraw another advisor's work. Expired postings stop participating in planning without being confused with completed jobs.

The planner does not manufacture advisor intent. It consumes the active postings and performs dependency analysis, priority ordering, conflict resolution, capacity planning and resource/unit allocation. This is a marketplace of current intent, not a one-shot command queue.

## 2. One planner, distinct resource domains

The Strategic Planner coordinates at least two pools:

- **Worker pool:** SCVs available for mining, gas, construction, repair, scouting or other worker jobs.
- **Military pool:** combat units available for defense, escort, patrol, scouting, attack, harassment, drops and other military operations.

Both use the same lifecycle and reservation principles, but retain domain-specific eligibility and execution rules. A unit must never be simultaneously committed to incompatible tasks. Existing commitments, travel, cargo/passenger state, health, capability, location, command latency and return/recovery requirements affect availability.

The Strategic Planner owns assignment and reservation, not strategic intent. For example, the build strategy decides when a Refinery should be built; the Strategic Planner chooses an eligible SCV and later returns it to the worker pool. Likewise, the strategy decides that an expansion needs scouting; the Strategic Planner assigns an eligible scout and tracks the job.

## 2. Jobs are first-class FSM-backed work

A job has a stable identity, type, priority, prerequisites, eligibility requirements, target, parent objective, expiry or revisit policy, assigned entities, reservation state, progress evidence, completion condition and failure/recovery behavior.

A job lifecycle may include:

`Created → Eligible → AwaitingResources → Assigned → Executing → Completed`

with explicit `Blocked`, `Expired`, `Cancelled` and `Failed` paths. The actual lifecycle should be modeled with FSMs and evidence-driven transitions. A task is not complete because an order was issued; completion is confirmed by observations or an explicitly defined result.

The Strategic Planner reevaluates assignments when relevant events occur: a unit is created or destroyed, a task is created or completed, a unit becomes available, a prerequisite changes, a reservation expires, or a strategic priority changes. It may also perform bounded periodic reconciliation for facts that cannot be evented reliably.

## 3. Persistent base locations and scouting jobs

Every known base location is a persistent place entity with its own FSM and stable identity. This extends the place-intelligence model in [World model, platform doctrine and contingency planning](world-model-and-maneuver-planning.md).

Track, at minimum:

- map-derived site identity and position;
- resource cluster and town-hall placement suitability;
- current ownership/control belief: neutral, friendly, enemy, contested or unknown;
- whether the site has been explored, scouted, occupied, established, depleted or abandoned;
- last time visited by a friendly unit;
- last time directly observed and last time confirmed clear;
- last known enemy/friendly presence, structures and defenses;
- confidence and evidence provenance, so remembered information is not mistaken for current vision;
- next revisit time/priority and any active scouting assignment.

The timestamps are distinct. A unit visiting a location does not necessarily mean it observed every relevant area; a place can remain unobserved even after a nearby pass. Use SC2 game-loop values as the authoritative match-time basis, and preserve the observation source.

The strategy/operational picture can create scouting jobs for the natural expansion, third, fourth and subsequent candidate bases. These are separate jobs keyed to stable site identities. The scheduler can prioritize them based on strategic value, uncertainty, opponent expansion likelihood, elapsed time since last observation, available scouts and the cost/risk of reaching the site. Completing a scout job updates the place FSM and schedules a future revisit when appropriate; it does not erase the location from the world model.

Scouting jobs should be composable: visit and report, confirm whether a town hall exists, inspect defenses, watch construction progress, maintain vision, or patrol an approach. A scout task should specify what evidence it needs, not merely a coordinate to visit.

## 4. Military tasks declare force requirements

A military task describes the capability and composition it requires. It should not hard-code unit tags into the strategy.

Examples:

- **Scout expansion:** one fast, expendable or otherwise suitable scout; visit a base-site entity and report observations.
- **Defend base:** a configurable defensive force with role/capability requirements, a protected base identity and a threat/expiry condition.
- **Escort:** protect a specified friendly entity or convoy along a route.
- **Patrol:** assign a force or scout to a route/area with a revisit schedule.
- **Drop:** one transport with capacity and transport capability plus eight Marines (illustrative initial composition); load, travel, unload, execute the objective, then extract or transition to a defined follow-up.
- **Reinforce:** move a requested compatible force to a named rally point or active operation.
- **Attack/harass:** require specified roles, minimum force strength, support and retreat/recovery conditions.

Requirements should be expressed as filterable constraints and role slots: unit kind/type, capabilities, minimum/maximum count, health/readiness thresholds, mobility, transport capacity, required upgrades, location/range, and whether a unit can be detached from its current assignment. Hard requirements must pass; preferences can rank eligible candidates. Never satisfy a task by silently weakening a hard requirement.

A task reserves its selected units atomically as one assignment group. If the full requested composition is unavailable, the strategy may permit partial staging, waiting, resizing or cancellation—but that policy must be explicit. The Strategic Planner must not dispatch an incomplete drop group simply because some Marines are available.

## 5. Fluent, filterable military task API

The authoring surface should make military intent readable and composable, while compiling to typed job definitions rather than issuing commands directly. Illustrative direction (not a current API):

```csharp
MilitaryTasks.Drop()
    .At(targetBase)
    .Requires(Units.Transport().Count(1))
    .And(Units.OfType(Terran.Marine).Count(8))
    .WithObjective(DropObjectives.DisruptWorkers)
    .When(Conditions.RouteIsViable)
    .OnCompletion(ReturnTo.RallyPoint);
```

Likewise:

```csharp
MilitaryTasks.Scout()
    .Visit(BaseLocations.Natural)
    .Report(BaseIntel.Presence | BaseIntel.Defenses | BaseIntel.Resources);
```

The API should support filters, count ranges, capability predicates, roles, locations, task dependencies, priority, expiry, completion evidence and fallback behavior. Fluent syntax is only an authoring convenience: the canonical representation remains explicit, versionable, inspectable and testable. No fluent method may bypass action validation or fabricate game facts.

## 6. Scaling force over time belongs to strategy

The drop example starts with one transport and eight Marines. A strategy may later request two dropships and their corresponding Marine groups, then four, as economy, production capacity, map knowledge, opponent defenses and strategic opportunity permit.

That scaling factor is strategy-owned configuration or a runtime strategic decision—not a hidden constant inside the Strategic Planner. The Strategic Planner answers: "Can I assemble and reserve this requested force, and which eligible units best satisfy it?" Strategy answers: "How many operations should I attempt, when, and at what risk?"

Scale only when the force can be produced, supported, controlled and recovered. Account for transport capacity, loading/unloading, route safety, anti-air threats, target value, replacement cost, concurrent operations and the effect of detaching units from defense or the main army. A strategy can specify one, two, four or another progression; the runtime must not assume doubling is always correct.

## 7. Force groups and formation ownership

A task's assigned units form a persistent force/group context. The group context owns its member tags, role assignments, objective, cohesion policy, rally point and lifecycle. A formation FSM operates on that group context; a geometry/planning service calculates feasible positions; individual unit FSMs execute their assigned movement/combat behavior.

Membership changes are explicit events: assigned, detached, destroyed, transported, unloaded, returned or reassigned. Do not let individual unit FSMs independently rewrite group membership. Formation calculations must account for terrain, pathability, unit capabilities, threats and movement constraints.

A drop group can therefore contain transport and passenger roles under one task while retaining individual unit execution FSMs. Loading and unloading are verified state transitions, not assumptions based on a submitted command.

## 8. Strategic Planner responsibilities and boundaries

The Strategic Planner should:

1. Register eligible entities as they are created or first observed, preserving identity across fog-of-war disappearance and reappearance.
2. Maintain separate worker and military availability/reservation views over the shared entity registry.
3. Receive typed jobs from strategy, scouting, economy, defense and engagement services.
4. Evaluate hard eligibility, prerequisites, conflicts, priorities and task deadlines.
5. Select and reserve complete compatible groups where composition matters.
6. Issue assignments to task/group FSMs, not raw SC2 commands.
7. Reconcile assignment progress and release units only when completion, cancellation, failure or recovery is confirmed.
8. Requeue released units and select their next eligible job without losing them between tasks.
9. Explain assignment decisions, unavailable candidates, conflicts and rejected matches in diagnostics.
10. Run at a defined point in the frame after job producers have updated their intents, so newly created work can be considered consistently.

It must not own the build order, decide strategic expansion count, invent military objectives, calculate all spatial geometry, or bypass the action pipeline. Those responsibilities stay with strategy, spatial/formation services, task FSMs and validated unit execution.

## 9. Diagnostics and proof gates

Record job creation, eligibility, assignment attempts, candidate rejection reasons, group reservations, unit state changes, travel start/arrival, observations, objective progress, task completion/failure and release/reassignment. Include game-loop timestamps and stable job/site/unit identifiers.

Measure:
- time from job creation to assignment;
- time from assignment to execution;
- unit idle time between jobs;
- scouting revisit lateness and information age;
- force-assembly time and composition shortfall;
- task success, losses, recovery time and objective value;
- conflicts prevented by reservations;
- economic and military opportunity cost of each commitment.

Implement in vertical slices with tests for identity/reappearance, site revisit scheduling, job lifecycle, reservation atomicity, eligibility filters, composition completeness, cancellation/release, group membership and diagnostics. Then validate in deterministic observation/replay fixtures and repeatable local matches. Passing tests do not establish live-match tactical success.

## 10. Suggested implementation sequence

1. Preserve and extend the existing persistent place model with separate visit/observation timestamps and base-site FSM identities.
2. Introduce a typed job contract and common lifecycle semantics, without replacing working opening controllers prematurely.
3. Integrate worker registration, construction completion and return-to-pool with the existing worker planner.
4. Add Strategic Planner assignment reconciliation and diagnostics for worker jobs.
5. Add base scouting jobs for natural, third and fourth sites, initially using existing scout-capable units.
6. Add a military availability/reservation pool and one simple force task (scout or defend).
7. Add filterable composition requirements and atomic group reservation.
8. Add formation/group contexts and the first transport/drop task only after transport protocol behavior is verified.
9. Add strategy-configurable operation scaling and prove one, two and larger concurrent groups through replay and local matches.

The first implementation should preserve current Terran opening behavior and worker throughput. This architecture is a staged extension, not permission to destabilize the proven path with a speculative wholesale rewrite.


## 11. Persistent enemy-unit ledger and fog-of-war memory

Every enemy unit that has been observed becomes a persistent match entity keyed by its SC2 unit tag. Loss of vision changes what is known about that entity; it does not delete the entity or prove that it died.

The enemy-unit record must separate:

- **Identity:** match/player identity, stable unit tag, last observed unit type and any observed morph lineage.
- **Last confirmed facts:** position, health/shields, energy when available, orders, buffs/upgrades when observable, cargo/transport state, and authoritative game-loop timestamp.
- **Visibility:** currently visible, hidden by fog, or otherwise unobservable. Visibility is not a lifecycle state.
- **Lifecycle evidence:** alive/unknown, confirmed destroyed, or match-ended. Only authoritative destruction evidence retires the unit.
- **Belief state:** last-known location, candidate regions/routes, confidence, supporting observations, contradictory evidence and the next reevaluation time.
- **Threat model:** weapon/range/siege state, detection capability, target preferences and strategic importance, derived from game data and observations rather than invented constants.
- **Task relationship:** current watch, search, containment, escort or engagement assignment and its owner.

A stale unit FSM remains alive as a memory-bearing entity. Its execution mode changes from direct observation to memory/reconciliation. It must not continue issuing commands that require current visibility or a current target position.

### Search and fallback behavior

When a high-value enemy unit disappears:

1. Preserve its last confirmed position and game loop.
2. Create or renew a scouting advisor posting for the last-known position and relevant nearby routes/terrain.
3. Give the search task an evidence contract: reacquire this tag/type, inspect the suspected firing area, or establish that the area is currently clear.
4. If the search fails, expand the candidate region according to reachable routes, elapsed game loops and known movement capability; do not teleport the remembered unit to a new coordinate.
5. If evidence remains absent beyond the configured uncertainty horizon, include the opponent start location as a fallback hypothesis where tactically plausible. Retain the original last-known position and alternative hypotheses.
6. Keep the unit in threat calculations until destruction is established or the match ends.

For example, a Siege Tank last seen covering a ramp remains a known threat after it leaves vision. The army must respect its last confirmed siege envelope and avoid blindly marching into it. The scouting advisor searches that location and nearby plausible firing positions. If the Tank is not reacquired, the operational picture may assign probability to a defensive position near the opponent's start, but must not represent that estimate as a newly observed Tank.

A location being visited is not proof that every approach or firing position was cleared. Search completion therefore records coverage, visibility and observation quality, not just arrival at a coordinate.

## 12. Opponent technology and economy as a constraint graph

The Intelligence advisor maintains an evidence graph of observed enemy units, structures, upgrades, production capabilities, resource constraints and inferred build history. It is a constraint-solving model, not a scripted build-order classifier.

Keep three categories distinct:

- **Observed facts:** a Banshee was seen at a specific game loop; a Starport or Tech Lab was directly observed; a structure's progress or an upgrade was visible.
- **Required implications:** a unit type requires particular technology or production capabilities according to the match's authoritative game data.
- **Hypotheses:** the number and timing of production structures, prior spending, hidden expansions, likely follow-up units and the opponent's intended strategy.

A Banshee observation can strongly establish that the opponent has access to the required Starport technology path, even when the production structures themselves are hidden. It should update the inferred Barracks/Factory/Starport prerequisite graph and trigger targeted scouting for the missing structures, add-ons and follow-up technology. The model must not claim that every prerequisite structure is still standing or currently at its original location unless observed.

### Backward production-time reasoning

Use authoritative game data for:

- structure build durations and prerequisites;
- unit production durations and producer/add-on requirements;
- research and morph durations;
- resource costs, gas costs, supply and producer occupancy;
- observed construction/research progress and game-loop timestamps.

For each observed unit, derive a feasible production-time interval. Combine those intervals with prerequisite completion windows, available producer capacity, resource income/spending bounds and other sightings. Eliminate histories that cannot fit the evidence.

For example, three Banshees do not by themselves prove two Starports. The solver must test whether one Starport could have produced the observed count within the feasible time window, accounting for production duration, add-ons, queues, interruptions and any relevant ability or upgrade. Only if the one-producer history is infeasible should the model infer a higher minimum production capacity. Preserve multiple feasible histories when evidence cannot distinguish them.

Resource reconstruction is bounded inference, not exact omniscience. Account for known starting resources, likely worker count and income rates, observed expansions/refineries, known unit and structure costs, upgrades, travel/production windows and uncertainty about hidden spending. Output ranges and confidence, not fabricated exact bank balances.

Each inference records its supporting observations, game-data rules, feasible time interval, alternatives, confidence and invalidation conditions. New evidence can revise or retract an inference without deleting the underlying observations.

## 13. Coordinated raids, target packages and extraction

A raid is a parent military task with a deliberately limited objective package, not a collection of units independently attacking whatever they encounter.

A candidate operation may combine:

- a feint or pressure task against an expansion to draw defenders away;
- a transport group with a complete, atomically reserved passenger manifest;
- a prioritized target list such as Starport, Engineering Bay, Armory or Ghost Academy;
- a Ghost task with explicit cloak, detection assessment, nuke availability, deployment and extraction phases;
- a Marine screen responsible for clearing or suppressing detectors and anti-air threats;
- a transport recovery route, rally point and abort conditions.

The strategy caster evaluates the expected value of crippling production/technology against the cost of the force, likely response, detection coverage, anti-air, travel time, target durability, repair/rebuild cost and the main army's exposure. A feint is authorized only when the force assigned to the feint can survive or disengage and the actual raid has a viable route and extraction plan.

The Ghost is not assumed safe merely because it can cloak. Before deployment, assess known and inferred detectors, detection coverage, static defenses, enemy response time and escape routes. If detectors are present, the operation may stage a Marine screen to clear or suppress them before the Ghost enters. If detector coverage cannot be cleared or avoided, the Ghost task must wait, choose another route or abort. The Ghost's nuke must be confirmed available from observed ability/energy/game state; the target area and friendly-force separation must pass validation. The Ghost should deploy, execute the bounded objective and reboard/extract only through verified transport and action states.

The transport group must preserve a complete passenger manifest and capacity constraints. Loading, transit, unloading, target engagement, reloading and extraction are separate evidence-backed phases. The Medivacs must not be treated as expendable path points: anti-air threat, damage, healing/support value and survival route are part of the operation's feasibility. Marine passengers are assigned explicit target priorities and do not drift into unrelated engagements.

The initial raid implementation should prove one complete, recoverable operation before scaling to multiple transports or simultaneous raids. Failure to reach a target, loss of detection control, a blocked extraction route, or a main-force emergency triggers the defined abort/recovery behavior rather than an improvised continuation.

## 14. Marine stutter-step and bounded engagement micro

Marine micro is a unit-level execution behavior subordinate to squad intent and target allocation. It must preserve the squad's role, formation, objective and hold/withdraw orders.

For a Marine with an authorized target:

1. Read the weapon cooldown and target state from current observations.
2. When the weapon is ready, issue an attack only if target validity, range, visibility and action legality pass.
3. After the attack is issued/observed, move toward the squad's next safe micro waypoint while the weapon is cooling down.
4. Reassess cooldown, target motion, range, threat and formation constraints every observation cycle.
5. Fire again when ready and legally in range; otherwise continue a bounded reposition or return to the assigned slot.
6. Stop micro and obey higher-priority retreat, transport, formation safety or commander hold orders immediately.

This is controlled stutter-step, not continuous random movement and not an order to attack-move. The movement planner should choose waypoints that kite or close according to the authorized engagement policy, preserve spacing, avoid friendly collisions and respect enemy weapon envelopes. A Marine must not chase a target outside the squad's engagement boundary merely because its weapon cooldown completed.

Target packages and fire-control allocation remain authoritative: Marines focus the assigned high-value structure or target group, avoid splitting damage across incidental enemies, and reallocate only when the target is destroyed, invalid, unreachable, or a higher-priority command supersedes it. The attack/move cadence must be driven by observed weapon cooldown and validated action results, not a guessed fixed frame delay.

## 15. Implementation and proof sequence for opponent intelligence and operations

1. Add a persistent enemy-unit ledger that survives fog-of-war absence and retires only on destruction evidence.
2. Add typed last-known-location records and scouting-advisor search postings with coverage evidence.
3. Add deterministic tests for stale Tank memory, search renewal, alternate-location hypotheses, fallback to opponent-start hypotheses and confirmed destruction.
4. Add a prerequisite/production capability graph backed by SC2 game data.
5. Add production-time interval reasoning and bounded resource reconstruction with contradictory-evidence tests.
6. Add a one-target structure raid task with explicit force reservation, target priority, abort and extraction before introducing Ghost/nuke coordination.
7. Add transport passenger lifecycle and Marine stutter-step under the existing action validator.
8. Add Ghost detection assessment, nuke availability/target validation and verified reboarding.
9. Validate each vertical slice with recorded observations and replay scenarios, then local matches. CI success alone does not establish fog-of-war reasoning or tactical success in live play.

The ledger and inference engine must never convert an inference into an observation, and no strategic score may bypass hard action, visibility, detection, pathing, composition or recovery gates.
