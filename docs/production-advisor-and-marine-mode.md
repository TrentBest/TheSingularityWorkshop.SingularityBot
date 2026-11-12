# Production Advisor and Marine Mode doctrine

## Purpose

Production needs one authoritative decision and command-issuance boundary. Build orders describe what the bot intends to produce; the Production Advisor evaluates that intent against the current observation and is the only component permitted to issue production commands to the StarCraft II API.

This is deliberately narrower than the general action pipeline. Movement, combat, scouting, construction placement and other non-production actions continue through their own validated action paths. Production commands—including SCV and Marine training, structures, add-ons, research and morphs—must converge on the Production Advisor before protocol submission.

The advisor is not a blind queue executor. It continuously reconciles declarative production intent with live game state, game-data capabilities, resource and supply budgets, reservations, producer availability, prerequisites, placement, pending commands and observed progress.

## Ownership and command path

```text
Build-order authoring / strategy profile
                 |
                 v
       Declarative BuildOrder
                 |
                 v
       Transition Evaluation
                 |
                 v
        Production Advisor <---- normalized observations
          |      |               game-data catalog
          |      |               resource reservations
          |      |               producer/add-on state
          |      v
          |   production plan + explainable blockers
          v
     Production command gateway
          |
          v
     SC2 API ActionRawUnitCommand
          |
          v
     Action result + subsequent observation
          |
          v
     Production job / build-order reconciliation
```

Only the Production Advisor's command gateway may create and submit production protocol commands. Existing controllers may remain temporarily as policy or planning components, but they must return typed production requests—not independently construct and submit SC2 production actions. Migration should be incremental and fail closed: no duplicate command path while a controller is being moved.

The advisor owns production arbitration and command lifecycle correlation. It does not own the strategic reason for a build, and it does not declare a command complete merely because an action was submitted. An action result is immediate protocol evidence; observed orders, construction progress, completed units, attached add-ons and completed upgrades are the evidence of actual game progress.

## Fluent build-order authoring

The public surface should be fluent and composable, but the fluent calls compile to a typed, inspectable plan. They never issue a game command themselves.

An illustrative production sequence:

```csharp
var order = BuildOrder
    .ByProductionSequence()
    .Add(ProductionItem.Scv)
    .Add(ProductionItem.SupplyDepot)
    .Add(ProductionItem.Barracks, count: 2)
    .SequenceItem(ProductionItem.Barracks)
        .WithGate(Gates.Completed(ProductionItem.SupplyDepot))
        .Parent()
        .Add(ProductionItem.Refinery)
    .Build();
```

The exact method names are a design target, not an assertion that this API already exists. The final API should make parent/child scope explicit and should avoid ambiguous fluent navigation. A sequence item represents a production objective and quantity; nested items express dependencies or follow-up work. A gate is a typed predicate over observed facts, not a delay or an assumption that an earlier command succeeded.

The canonical plan must retain, at minimum:

- stable item identity and production kind;
- requested count and whether count means total, concurrent or additional;
- parent/dependency relationships;
- prerequisite and completion gates;
- resource and supply requirements;
- producer/add-on capability requirements;
- priority and reservation policy;
- retry, timeout, cancellation and failure behavior;
- evidence required to mark the item started or complete.

The plan must be serializable and inspectable independently of its fluent authoring syntax. This permits future profiles, GUI authoring and alternative configuration formats without creating alternate execution engines.

## Gates, scheduling and reservations

A production item moves through evidence-backed states such as:

`Declared → Blocked → Eligible → Reserved → Submitted → Observed → Completed`

with explicit `Rejected`, `TimedOut`, `Cancelled` and `Failed` paths. A command may be retried only after the previous request has been reconciled and its retry policy permits it.

Before issuing a command, the advisor checks:

1. the requested item is active under the current build-order branch/transition;
2. all prerequisite gates are satisfied by current authoritative observations;
3. the required ability and costs exist in match game data;
4. the resource budget covers the command and all higher-priority reservations;
5. supply capacity is sufficient or a supply action is scheduled with enough lead time;
6. an eligible producer is complete, idle and capable, including required add-on state;
7. no equivalent command is already pending, observed or complete;
8. the target, placement and action semantics are valid for the specific command.

Resource reservations are explicit and explainable. The 150-mineral Orbital Command morph is a first-class reserved strategic commitment in Marine Mode. A worker pull, Marine queue, add-on or expansion must not consume that reserve. The advisor should schedule construction workers using predicted travel and return time: reserve and dispatch a builder only when its estimated arrival aligns with the earliest legal, affordable construction window. Do not send a worker early merely because a future build item exists.

Supply scheduling should be predictive. The advisor estimates queued and in-progress unit supply, production rates and construction lead time, then schedules Depots before the force reaches the cap. It must not overbuild based on a stale forecast; each new action is revalidated against the current observation.

## Transition evaluation

Build orders should be able to declare when strategic reconsideration is useful:

```csharp
BuildOrder.AddTransitionEvaluation(
    when: MatchEvidence.HasReached("1-1-1"),
    evaluate: context => MarineModeTransitions.Assess(context));
```

This is illustrative only. A transition evaluation is a checkpoint, not a hard-coded automatic switch. It examines opponent evidence, our economy, completed technology, army composition, map state, threats and confidence. The result may retain the current plan, select a compatible branch, or request human-configured behavior. Uncertain inferences carry evidence, confidence and expiry.

For example, observing a Roach Warren after an opponent's 1-1-1 can raise the probability of a particular tech response, but the bot must distinguish direct observation (the structure) from inferred intent (possible Banshees). Transition rules must not treat a guess as fact.

## Marine Mode: production doctrine

Marine Mode is a deliberately Marine-heavy Terran profile. Its initial production target is four Barracks, with one Tech Lab and three Reactors, followed by an Engineering Bay and additional Supply Depots as forecast demand requires. The starting Command Center's 150-mineral Orbital upgrade remains reserved and takes precedence at its defined gate. The second Command Center is a later economic commitment, not permission to starve the Orbital morph.

The advisor schedules the add-on distribution and verifies each attachment before assigning production roles. Reactors provide additional Marine throughput only when the producer and add-on are observed in the correct state. The Tech Lab is a technology capability and future composition option; it does not silently change the Marine-first policy. The Engineering Bay opens infantry upgrades when their costs, prerequisites and strategic timing are supported.

The production objective is not simply "queue Marines whenever possible." It balances SCVs, supply, Orbital investment, add-ons, engineering, expansion and Marine throughput under a single budget and command authority.

## Marine Mode: military doctrine

Production and military control are separate domains. The Production Advisor creates and sustains the force; the Military Advisor and formation controllers decide where it moves and what it protects. Military orders are group-level objectives translated into coordinated unit commands.

### Two-squad organization

The initial field force should organize into two squads rather than dispatching one Marine to every base. A squad has a stable identity, membership, role, formation, leader/anchor, objective, route, security responsibilities and fallback plan. Membership changes are deliberate and recorded; dead, detached or unavailable Marines do not cause the remaining squad to dissolve into independent patrols.

Each squad can be subdivided into a lead element and a body. In open terrain the squad advances in a 4-by-4 grid where force size permits. Spacing and orientation adapt to terrain while preserving cohesion and fields of fire.

### Marching and constrained terrain

When a full formation cannot pass through a narrow route, the formation controller enters a column/half-step maneuver:

1. The lead half advances through the constrained segment at a controlled pace.
2. The rear half holds its staging line and does not crowd the chokepoint.
3. Once the lead half has cleared the segment and a safe assembly area is confirmed, the rear half advances.
4. The two elements reform into their normal grid and resume the march.

The movement planner must use pathability, terrain width, threat exposure and available assembly space. It must not assume that a visual gap is traversable or that all units can occupy the same waypoint.

### Chokepoint and bridge security

Before the main body crosses a bridge or chokepoint, the force performs a security halt. Scouts or designated security elements move to positions on both sides/approaches and inspect for threats. The main body remains on the protected side until the crossing is assessed as safe.

Once the crossing begins, the security elements hold their assigned positions and watch their respective approaches. If an enemy force arrives from either side, they delay and contain it rather than chasing it or abandoning the crossing. The main body withdraws along the preplanned safe route. The security elements disengage only under the defined withdrawal signal or when their survival threshold is reached.

This is a deliberate sacrifice of local freedom of movement for force protection and orderly withdrawal—not a command to fight to the death. Their task is to buy time and preserve the main force.

### Discipline, facing and drills

A squad's stop/hold intent is authoritative. A fleeting enemy entering vision must not cause Marines to break formation and chase it. Engagement requires an explicit military task, target/engagement envelope and rules of engagement. A local tactical opportunity may be reported to the commander, but it cannot silently replace the squad's current assignment.

Facing drills should orient the formation toward a specified threat axis, route, objective or commander signal. Drill behavior can include:

- halt and dress ranks;
- face left/right/about;
- reform the grid after a turn;
- advance, wheel and fall in;
- deploy a screen and return to formation;
- rotate lead/security elements;
- conduct controlled approach and withdrawal.

When no urgent task exists, squads may execute bounded patrol and drill routes that expand vision, exercise formation transitions and look for trouble. These are still operational tasks: avoid enemy-controlled regions without a risk authorization, preserve a return route, report contacts, and return to readiness when a higher-priority threat or objective appears. Siege Tanks in siege mode are the explicit exception to mobile vision patrols: they hold their assigned fire positions unless a higher-level emergency order authorizes redeployment.

### Withdrawal doctrine

If an engagement is assessed as unwinnable, the commander initiates an orderly withdrawal before the force is destroyed:

1. Break contact toward a safe route and rally point nearer the map center, preserving cohesion and covering arcs.
2. Reassess while withdrawing; do not reverse direction because one enemy unit briefly leaves vision.
3. If the centerward route is unsafe or the force remains threatened, fall back toward the nearest viable friendly Bunker or Planetary Fortress.
4. Regroup, recover, reinforce and reassess before re-engaging.

Threat estimates must include visible enemy strength, range, weapon capability, health, terrain, reinforcement access and confidence in remembered contacts. The retreat state has hysteresis so small observation changes do not cause repeated advance/retreat oscillation.

## Observability and acceptance

Every production decision should explain: active build-order item, gate results, resource bank, reservations, supply forecast, candidate producers, selected producer, ability/cost, pending command identity, action result and observed completion evidence.

Every military group decision should explain: squad identity/membership, formation and facing, current objective, route segment, security positions, observed contacts, engagement authorization, withdrawal stage and regroup condition.

Implementation is complete only when:
- production commands have one issuance path and duplicate issuers are removed;
- build-order DSL compiles to a stable typed plan;
- gates, reservations, retries and transitions have unit tests;
- production timing is validated in recorded observations and live matches;
- squad formations and chokepoint maneuvers have deterministic geometry/decision tests;
- hold discipline, security response and staged withdrawal are tested against representative observations;
- live replays demonstrate the intended visible behavior.

Passing CI is not live-match evidence. Do not claim that the described Marine Mode doctrine is implemented until these acceptance gates have been met.
