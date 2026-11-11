# Strategic intent and composition goals

## Two authoring models, one planning system

SingularityBot should accept two complementary forms of intent:

1. **Advisor-driven intent:** domain advisors continuously publish what their domain needs. Economy can request worker, gas or infrastructure work; Intelligence can request scouting; Military can request defense or operations; Base Engineering can request supply, production, repair or base-defense work; Science can request research and prerequisites.
2. **Goal-driven intent:** the user declares a desired outcome, such as a standing force of 50 Marines, 25 Marauders and 10 Medivacs. The bot derives the production and technology demand required to sustain that outcome.

Both models publish into the same job market. Advisors own and maintain their postings. A composition goal owns the postings compiled from its declarative target. The Strategic Planner consumes both sets, reconciles them against the world and current progress, resolves dependencies and conflicts, then schedules work. Neither input model directly issues SC2 actions.

This is the same planner with two different intent producers—not two separate execution systems.

## Composition is a standing target, not a one-time build order

A composition target describes the desired steady-state force. Each unit target includes:

- unit kind;
- desired standing count;
- optional expected replacement/loss rate per minute.

The goal also defines the desired initial build-out horizon. The horizon controls how quickly the initial target should be assembled. Replacement rate controls the additional production capacity required to replenish expected losses after the force is established.

Example intent:

| Unit | Standing target | Replacement rate |
|---|---:|---:|
| Marine | 50 | 2 / minute |
| Marauder | 25 | 1 / minute |
| Medivac | 10 | 0.5 / minute |

The goal can be authored independently of the current army, production queue, minerals, gas, supply or tech state. The planner must compare the target with current owned units, units already in production, reserved production capacity, losses and current resources before turning the standing demand into immediate jobs.

The target does not mean "queue 50 Marines right now." It means "maintain this force composition, and plan the work needed to reach and sustain it."

## Deriving production capacity

For a unit type with a known production cycle:

[
	ext{required units/minute} =
rac{	ext{standing target}}{	ext{initial build-out minutes}}
+ 	ext{replacement rate/minute}
]

[
	ext{producer capacity} =
leftlceil
rac{	ext{required units/minute} 	imes 	ext{production seconds}}{60}
ightceil
]

Demand is aggregated by producer structure. Add-on capacity is calculated separately for recipes that require a particular add-on. Prerequisite structures are included in the generated plan.

This is a first-order throughput model. A production solver must later account for reactor/tech-lab slot behavior, shared add-on constraints, construction lead time, upgrade/research contention, resource income, supply, queue utilization, idle time and uncertainty. The formula estimates required capacity; it does not by itself prove that the target is economically sustainable.

## Data-driven technology and production graph

The compiler deliberately requires an explicit `ProductionRecipe` catalog. A recipe declares:

- unit kind;
- producer structure;
- production cycle duration;
- required add-on, if any;
- prerequisite structures.

For example, a Marauder recipe can state that it is produced at a Barracks with a Tech Lab. A Medivac recipe can state that it is produced at a Starport and that its structure prerequisites include a Factory. A Marine recipe can state Barracks production and its prerequisite chain.

The recipe catalog must ultimately be derived from authoritative SC2 game data plus reviewed tech-tree metadata. The current game-data catalog does not yet expose a complete, normalized production/prerequisite graph, so the composition compiler does not invent unit costs, build times, ability IDs or prerequisites. The test fixture uses illustrative cycle durations solely to verify the calculation.

When the catalog says the target includes Marauders, the derived plan can request Tech Lab capacity. When it includes Medivacs, it can request Starport capacity and the necessary structure chain. Multiple unit types can share producer families, so the solver aggregates their throughput before calculating facility count.

## Advisor posting lifecycle

A job posting is a replaceable statement of current intent with:

- stable posting ID;
- source and owner;
- kind and target;
- requested quantity;
- priority;
- creation and update game-loop timestamps;
- optional expiry;
- optional constraints.

The owning advisor may create, update, renew or withdraw its own posting. Updating a posting preserves its original creation timestamp. Another advisor cannot overwrite or withdraw it. Expired postings are excluded from the active planner view. A completed or no-longer-needed job is withdrawn by its owner rather than left as stale demand.

Examples:

- Intelligence publishes `scout:natural`, then raises its priority when information becomes stale, and withdraws it when the information requirement is satisfied.
- Base Engineering publishes `build:second-barracks`, updates its quantity when the production target changes, and withdraws it if the structure is no longer needed.
- Military publishes `defend:natural`, renews it while the threat persists, and withdraws it when the threat condition clears.
- Science publishes `research:stim`, then withdraws or replaces it if the selected tech path changes.

The Strategic Planner reads these postings; it does not edit their owner-controlled intent.

## Planner responsibilities

The planner's intended stages are:

1. Snapshot all active advisor and composition-goal postings.
2. Reconcile standing targets with observed entities, production queues, existing construction, assignments and completed work.
3. Expand prerequisite and production dependencies from the game-data graph.
4. Calculate resource, supply, producer, add-on and worker demand.
5. Rank jobs by priority, urgency, strategic phase, deadlines and opportunity cost.
6. Detect mutually incompatible requests and shared-resource bottlenecks.
7. Reserve eligible actors and production capacity atomically.
8. Emit executable assignments for existing job/unit FSMs.
9. Reconcile progress and report completion, failure, delay or unmet demand to the posting owner.

The planner does not become the source of strategy. Advisors and composition goals remain responsible for what they want; the planner solves how to pursue compatible goals with finite resources.

## Implementation status

Implemented:

- An ownership-enforcing advisor job board with stable IDs, owner checks, update timestamps, expiry filtering and owner withdrawal.
- Composition-goal definitions for standing unit counts, initial build-out horizon and per-minute replacement rates.
- A recipe-driven compiler that emits unit-production, producer-structure, add-on and prerequisite postings into the same board.
- Producer demand aggregation and add-on demand calculation using explicit production-cycle metadata.
- A Strategic Planner view that returns the combined active posting set in priority order.
- Bot-facing APIs to publish/withdraw advisor postings, inspect the current plan and compile a composition goal.
- Tests for posting ownership, replacement, expiry, composition derivation and invalid-goal atomicity.

Not yet implemented:

- Concrete Military, Science, Base Engineering and Intelligence advisors maintaining their own postings.
- Economy Advisor migration from its current reservation policy into the job-posting model.
- Complete authoritative SC2 production, add-on, cost, prerequisite and build-time metadata.
- Reconciliation of desired composition against current army, queued production and units in transit.
- A full constraint solver for resources, supply, production queues, shared add-ons and construction sequencing.
- Atomic reservations, assignment execution, feedback to posting owners and diagnostics for unmet goals.
- User-facing GUI for selecting advisor policies, editing goals and inspecting the derived plan.

The first composition compiler is intentionally an explainable capacity estimator, not yet an autonomous macro solver. It should not be used as a live build-order authority until the game-data graph and planner reconciliation are complete.
