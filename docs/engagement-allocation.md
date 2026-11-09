# Engagement allocation and opponent-aware plans

## Purpose

This document defines how the bot should represent possible engagements, estimate the force needed to achieve an effect, allocate attackers across targets, and choose among matchup-specific plans. It extends [movement doctrine](movement-doctrine.md): movement decides how forces reach and occupy useful space; engagement allocation decides how their weapons are applied once opportunities arise.

This is a design contract, not a claim that these capabilities are implemented. The first playable Terran economy remains the immediate runtime gate.

## 1. An engagement is a candidate operation

At match start, the bot should have a small set of legal, evaluable engagement candidates—not a single hard-coded attack policy. One candidate may be the simple baseline: assemble available combat power and A-move toward a valid enemy region. It is useful precisely because it is simple, measurable and always available when its preconditions hold.

Other candidates describe different operational commitments, for example:

- **Worker pressure / SCV rush:** commit a calculated number of SCVs to threaten enemy workers and construction while retaining enough economic capacity to sustain the operation.
- **Containment:** deny access to a region, resource line or expansion without necessarily committing to a decisive fight.
- **Siege:** establish a defensible position, protect the siege element, and apply pressure from range.
- **Harassment / raid:** enter a vulnerable area, inflict a bounded economic or strategic cost, then withdraw or reposition.
- **Breakthrough:** concentrate sufficient force to cross a defended boundary and exploit the opening.
- **Counter-composition response:** select targets, positions and production based on observed enemy capabilities and behavior.

A candidate engagement should declare its objective, prerequisites, required capabilities, force and resource commitment, target region, acceptable losses, expected duration, support needs, exit conditions, fallback, and evidence needed to continue. Candidates are evaluated against the current map, own state, opponent model and uncertainty. The bot may retain several candidates, but only validated, authorized intents reach the action pipeline.

An engagement candidate is not a free-form script. It is a parameterized plan whose feasibility and continuation are reassessed as the game changes.

## 2. Firepower is a budget to allocate

The coordinator should estimate the effective damage required to neutralize each relevant target and allocate available attacks accordingly. This is not mysterious: the game data supplies weapon damage, attack period, range, bonuses, armor interactions and unit health/shields. The bot combines those facts with observed target state and uncertainty.

For each attacker-target opportunity, estimate at least:

- expected damage per attack after relevant armor, bonuses and target properties;
- time until the next attack is available;
- travel time and probability of reaching a valid firing position;
- range, line of sight, target visibility and legal targetability;
- target current health/shields when observed, and uncertainty when not;
- damage already committed by friendly attacks that are likely to land;
- target threat, strategic value and likelihood of escaping or becoming unavailable;
- opportunity cost of assigning this attacker here rather than elsewhere.

The coordinator should estimate **time-to-kill**, not only nominal shots-to-kill. A weapon that deals enough damage in one volley may still be a poor assignment if its cooldown, travel time or firing lane makes another target more urgent.

### 2.1 Focus fire without waste

When several attackers can kill a target in the same practical attack window, allocate enough of them to secure that kill, then redirect remaining firepower to the next target. Avoid splitting damage so widely that no target is removed, but also avoid sending every attacker into a target that is already certain to die.

Example: three Roaches face twelve Marines. If eight Marines can reliably kill one Roach in the relevant firing session, the initial allocation can commit eight to Roach A and four to Roach B. Once the first eight have delivered the lethal volley (or the kill is otherwise confirmed), those Marines become available for Roach C while the original four continue pressuring Roach B. The coordinator continuously updates the assignments as attacks launch, land, miss, targets move, and health estimates change.

This is an illustrative allocation, not a hard-coded eight-to-four rule. Actual assignment depends on the weapon and armor data, range, attack readiness, target health, overkill risk, projectile travel, simultaneous attack timing and uncertainty. If the target may survive, reserve or redirect follow-up fire based on the updated estimate.

### 2.2 Track committed damage and uncertainty

Maintain a short-horizon ledger of expected incoming damage per target, attributed to specific friendly attackers and expected impact windows. Do not treat a queued attack as guaranteed damage: attackers can lose range, line of sight, target visibility or the ability to complete the shot.

Use confidence bands or conservative bounds where exact health or attack outcomes are unavailable. If the probability of a kill is not high enough, allocate a follow-up or preserve a ready attacker. If committed damage already exceeds the target's likely remaining health by a safe margin, redirect excess firepower.

The allocation objective is not simply minimum total damage. It balances target removal, threat reduction, time-to-kill, friendly survival, overkill, wasted cooldowns and the strategic objective of the engagement.

### 2.3 Retarget when the battlefield changes

Assignments are provisional. Recompute when any of these events occur:

- target dies, disappears, becomes untargetable or changes visibility state;
- an attack lands, misses, is interrupted or becomes unlikely to land;
- target health/shields materially change;
- a new higher-priority threat enters the engagement;
- an attacker becomes unable to fire, loses its path or is displaced;
- a target changes position, threat or strategic importance;
- the engagement changes phase or its exit condition is reached.

The game itself may select another target when an existing target becomes invalid or unavailable. The bot must not assume that an old target assignment remains meaningful, nor assume the engine's automatic choice matches the bot's strategic allocation. Observe the resulting order and combat state, then reconcile it with the allocation plan. Where the engine's automatic retargeting is useful, allow it; where a deliberate focus-fire decision is needed, issue a validated explicit target order.

A burrowed Zergling illustrates why this matters. A Zergling that has taken damage and burrows at low health may cease to be a valid visible target for ordinary attackers. If those attackers' current target becomes unavailable, the game may retarget them according to its own rules. The bot should detect the target-state transition, release stale commitments, and allocate fire against currently valid threats. If detection reveals the burrowed unit, detection must be followed by a deliberate search/engagement response where appropriate; awareness alone is not a kill.

## 3. Continuous-flow swarm engagements

A Zergling swarm should not be modeled as a static ball of units all attacking one point. Its advantage is the repeated arrival of fresh attackers while earlier attackers move aside, die, disengage or continue through the line.

The swarm controller should reason about local traffic and attack opportunities:

- maintain a general direction and operational objective;
- identify congested approach lanes and alternate paths around friendly units;
- allow attackers that have completed an attack opportunity to yield space to following units;
- schedule successive attackers into available contact positions;
- preserve paths for units moving through or around the engagement;
- use local clockwise/counter-clockwise or otherwise opposing circulation when terrain and enemy geometry make it beneficial;
- reassess whether to continue through, surround, disengage or reform as the enemy line changes.

The key state is not “which Zergling owns this defender?” but “which attackers can threaten which defenders during the next few attack windows, and how does traffic preserve a continuing flow of pressure?” Individual unit assignments remain useful for immediate execution, but the swarm policy optimizes the stream.

Do not force circulation where a narrow choke makes it harmful. Pathing, collision, enemy splash damage, friendly congestion and the objective determine whether units should flow, fan out, surround or concentrate.

## 4. Worker pressure is a calculated commitment

SCVs are not merely fragile economy units. They can attack, repair, body-block and interfere with construction. A worker-pressure candidate can therefore be a legitimate early engagement when the expected strategic disruption exceeds the economic opportunity cost.

A worker rush should calculate:

- how many SCVs are required to kill or drive off a target worker within a useful window;
- how many are required to threaten a builder before a Barracks or other structure completes;
- whether worker stacking/body-blocking can deny a construction footprint or keep builders away;
- how many SCVs must remain mining or otherwise support the economy;
- whether repair can sustain the committed force and what resources repair consumes;
- the minimum SCV production and supply infrastructure that must remain funded;
- travel time, enemy reinforcement, defensive structures and likely counterplay;
- a clear continuation, disengagement and recovery condition.

The plan may prioritize disrupting construction over worker kills. If enemy workers cannot mine or complete essential production, the bot may accept a bounded amount of damage while maintaining the minimum economy needed to replace workers, preserve supply and repair the committed SCVs. That is a deliberate economic exchange, not an unlimited permission to sacrifice the economy.

Worker stacking, body-blocking, attack focus and repair must be evaluated together. The bot should not send every SCV simply because the workers can fight; it should commit the smallest force that can achieve the desired denial with acceptable risk, while preserving the resources required to sustain or exit the operation.

## 5. Opponent identification and behavioral model

The bot should identify an opponent by observed behavior and evidence, not by assuming that a name or one early unit proves a fixed strategy. The opponent model should distinguish:

- observed facts (units, structures, timings, locations, orders and combat outcomes);
- inferred capabilities (likely production, tech access, mobility, detection, anti-air, siege or pressure);
- inferred intent (rush, expand, turtle, tech, contain, harass, all-in);
- confidence, freshness and alternative explanations.

For example, repeated early tunneling-Roach pressure may support a behavioral classification such as **hyper-aggressive tunneling-Roach cheese**. The label is useful to a strategy caster only when accompanied by the evidence and confidence that justify it. The model should update if the opponent changes course or the initial inference proves wrong.

The opponent profile is not the strategy itself. It is input to the strategy caster, which compares viable engagement candidates and chooses a response. The caster should explain why a candidate is eligible, what evidence favors it, what risks remain, and what observation would cause it to switch or abort.

Opponent identity should also distinguish the opponent bot/player from our own SingularityBot instance for match analytics and replay interpretation. Do not infer identity from behavioral similarity alone.

## 6. Strategy caster: select, compare, and revise engagements

The strategy caster evaluates candidate engagements using hard eligibility gates followed by desirability scoring.

**Hard gates** include legal actions, unit capabilities, prerequisites, available resources, map/path feasibility, required detection/visibility, minimum force availability, and explicit user or strategy constraints. A candidate that fails a hard gate is not rescued by a high score.

Among eligible candidates, compare expected strategic value over a defined horizon. Factors may include:

- expected enemy economic/production disruption;
- probability and cost of achieving the objective;
- own force losses and replacement time;
- opportunity cost to mining, production and technology;
- map control and positional advantage;
- opponent response capability and uncertainty;
- time-to-impact and whether the opportunity is temporary;
- ability to disengage, recover or transition to another plan.

Use hysteresis and commitment costs so the caster does not oscillate between plans on every new observation. A committed engagement may continue while its premise remains valid, but must be reconsidered when a hard guard fails or the expected value materially changes.

The caster can select a broad plan such as “eight-Gateway all-in,” “worker pressure,” “siege the natural,” or “contain the tunneling-Roach player.” The map/doctrine layer then expands that plan into editable routes, roles, positions and engagement rules. Simple intent and detailed tactical authoring must resolve to the same underlying contracts.

## 7. Required data concepts

The eventual typed model should represent at least:

- **EngagementCandidate:** identity, objective, applicability, prerequisites, commitment, risk, horizon, support, exit/fallback.
- **OpponentAssessment:** opponent identity, observed facts, inferred profile, confidence, freshness and alternative hypotheses.
- **TargetAssessment:** target identity, observed state, estimated remaining durability, threat/value, targetability and uncertainty.
- **AttackOpportunity:** attacker, target or area, weapon readiness, expected damage, travel/firing feasibility and expiry.
- **DamageCommitment:** attacker, target, expected impact window, expected damage and confidence.
- **FireAllocation:** target assignments, required damage, allocated attackers, reserve/overflow policy and reassessment triggers.
- **EngagementPhase:** approach, establish, trade, exploit, withdraw, recover or complete.
- **DecisionRecord:** eligible candidates, rejected candidates with reasons, chosen plan, evidence, score components and next reassessment conditions.

Keep these contracts declarative and versioned. Game facts come from the active game-data catalog; observations come from the match state; planners produce intents; the existing action validation pipeline remains authoritative.

## 8. Tests and implementation order

Keep the first playable Terran economy gate intact. Add engagement capabilities in testable slices:

1. Unit tests for damage estimates using game-data weapon, armor, health and shield values.
2. Deterministic allocation tests for focus fire, overkill prevention, target death and target-state invalidation.
3. Replay tests for the three-Roach/twelve-Marine allocation example, with actual game values supplied by the data catalog rather than hard-coded assumptions.
4. Tests for attacks already committed, projectile/impact timing where available, and uncertainty when health is not observable.
5. Tests for target loss, burrow/visibility transitions, detection and retarget reconciliation.
6. Swarm traffic tests for congestion, yielding, successive attack opportunities and alternate circulation.
7. Worker-pressure simulations measuring builder denial, worker disruption, repair cost, mining loss and recovery.
8. Opponent-classification tests that distinguish observations from inferences and decay stale confidence.
9. Strategy-caster tests proving hard gates cannot be overridden by desirability scores and that hysteresis prevents oscillation.
10. Replay and live-match comparisons against the simple A-move baseline.

A good score is not proof of a good decision. Record the evidence, expected outcome, actual outcome and reason for every major engagement transition. Do not claim a candidate is implemented until the runtime executes it and deterministic tests or match evidence demonstrate the intended effect.
