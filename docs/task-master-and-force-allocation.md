# Strategic Planner, scouting and military force allocation

## Purpose

The Strategic Planner is the central consumer and planner for the bot's available work and resources. It does not own strategic intent or job lifecycle. Each advisor owns its postings and may create, update, renew or withdraw them as its assessment changes. The Strategic Planner reads the combined live job market, resolves dependencies and conflicts, sequences work, and eventually reserves eligible units/resources. Task and unit FSMs execute assignments through the validated action pipeline.

This document records the intended architecture and future direction. It is not a claim that military task allocation, base-site FSMs, force composition matching or the fluent authoring API are implemented.

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
