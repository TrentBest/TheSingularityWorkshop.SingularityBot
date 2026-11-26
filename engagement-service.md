# Engagement service and operational picture

## Purpose

The engagement service maintains the bot's current mental picture of the opponent and coordinates persistent strategic objectives with immediate tactical work. It is not a queue of unrelated attack commands and it is not a build-order script.

The service answers five questions on every observation:

1. **What do we know?** Preserve observed units, structures, locations, orders, construction progress, combat outcomes and confirmed deaths.
2. **What does that imply?** Maintain confidence-rated inferences about enemy production, technology, capabilities, intent and likely timing.
3. **What are we trying to accomplish?** Keep persistent objectives and their dependencies visible.
4. **What can we exploit now?** Add bounded opportunities with explicit expiry conditions.
5. **What changed?** Reconcile completed, invalidated, blocked and newly available work without losing the larger plan.

The service owns the operational picture. Specialized planners own feasibility and execution: economy, scouting, movement, fire allocation, production, drops, construction denial and action validation. A selected engagement is decomposed into typed intents and must still pass the existing action pipeline.

## 1. One coherent picture, multiple objective lifetimes

Do not flatten every goal into one priority-sorted list. Objectives have different lifetimes and relationships.

### Persistent strategic objectives

A persistent objective remains active until it is completed, invalidated by evidence, or deliberately replaced by a recorded strategic decision. Examples:

- destroy the enemy town hall and prevent the opponent from rebuilding a stable economy;
- eliminate or displace the starting worker force and keep enemy workers from safely mining;
- deny production structures and the ability to advance through technology;
- progressively claim, defend and connect map sectors;
- retain enough of our own economy and production to sustain pressure and recover losses.

These goals form a dependency graph. Killing an early scout is useful because it protects our information and reduces an immediate threat, but it does not replace the larger town-hall, worker and map-control objectives.

### Immediate engagements

An immediate engagement is work that can be executed now: kill a visible scout, focus a threatening unit, defend a worker, intercept a builder, clear a route or attack a vulnerable structure.

It is attached to the objective it serves. A visible Zergling therefore adds a Zergling-neutralization task to the existing plan; it does not cause the service to forget the enemy town hall or worker economy.

### Expiring opportunities

A transient opportunity has a trigger, a latest useful time, a feasibility test and a fallback. It may be attempted when advantageous, but it is not a mandatory strategic dependency.

Examples include:

- strike a Hatchery while it is visibly morphing to a Lair, before the upgrade completes;
- use a short-lived transport/drop window to kill a few Drones or interrupt workers;
- attack a builder or unfinished structure before its completion;
- exploit an enemy force that has moved away from a vulnerable area.

If the opportunity expires, record why it was missed and return the committed force to its persistent objective. Do not keep issuing stale orders or sacrifice the main operation to chase an expired window.

## 2. Example: Zergling scout changes the picture

Assume the bot identifies an enemy Zergling.

### Evidence and inference

- **Observed:** enemy Zergling, tag, location, visibility, health and last-seen game loop.
- **Inferred:** a Spawning Pool must have been available to produce Zerglings. This is strong evidence of Zerg tier-one production, not proof of the opponent's entire strategy.
- **Unknown:** exact pool location, current production, upgrades, number of additional Zerglings, and whether the opponent is expanding or committing to aggression.
- **Confidence:** retain the evidence and confidence separately from the inference; update both as new structures and units are observed.

The Zergling becomes an immediate target candidate. Its presence also adds a watch condition to the operational picture: locate or observe the enemy Hatchery/Hatcheries and detect any change in their unit type or build progress that signals a Lair morph.

The Zergling kill is part of the larger operation to destroy the enemy town hall and disrupt the starting workers. It is not a replacement objective.

### Tier-one containment and technology response

The strategy caster can compare the observed Zerg capability with our projected production milestones. Zergling-heavy tier one may become containable once our Factory produces Hellions, subject to actual numbers, terrain, enemy upgrades and reinforcement timing.

After Starport access, Vikings and Banshees create additional air options. Air control is an evaluated capability, not an assumption: Queens, Spore Crawlers, Hydralisks and other anti-air threats must remain in the opponent picture. Banshees should exploit ground targets only while their routes and survival remain viable.

Use ramps, chokes, Hellion mobility and map sectors to limit where tier-one ground forces can safely move. The goal is not simply to win one fight; it is to make the opponent's usable territory progressively smaller while our own controlled and supported area grows.

### Hatchery watch and the expiring strike

When a Hatchery is observed, create a persistent watch record for that structure. Reconcile its identity, visibility, location, health and observed type/build progress every frame it is available.

If it begins morphing to a Lair:

1. record the observed morph and derive the remaining completion window from authoritative game data and observed progress;
2. create an expiring opportunity whose latest useful time precedes completion by the travel, setup and attack margin required by the chosen operation;
3. evaluate direct pressure, a quick drop, worker harassment or another feasible strike against current force, distance, anti-air, detection, transport readiness and expected damage;
4. attempt the strike only if its hard gates pass and it does not invalidate a more valuable persistent commitment;
5. if it cannot arrive in time, expires, or becomes too costly, abandon the transient attempt and continue containment and the main town-hall/worker operation.

A drop that kills several Drones is a useful result, not a mandatory success condition. The persistent objective remains to destroy the town hall and its workers, deny the opponent a stable economy and force any recovery to begin elsewhere. Preventing the Lair from completing is especially valuable because it delays access to the next tier and may force the opponent to rebuild the technology path. That consequence must be inferred from the actual state, not presumed after one attack.

## 3. Strategy caster and engagement service responsibilities

The engagement service maintains facts, inferences, objective relationships and opportunity lifetimes. It should not directly issue SC2 actions.

The strategy caster evaluates candidate operations using:

- hard eligibility gates for legal actions, prerequisites, capabilities, pathing, vision, detection, resources and required support;
- expected objective value and probability of success;
- time-to-impact relative to enemy production and technology windows;
- force commitment, friendly losses, recovery time and opportunity cost;
- opponent response capability and uncertainty;
- map control, reinforcement paths and the effect on the opponent's remaining territory;
- persistence, expiry and the cost of switching away from a current commitment.

A high desirability score cannot override a failed hard gate. Hysteresis prevents the caster from changing the main operation every time a new unit is seen. A transient opportunity can be accepted without replacing the parent objective.

Every decision should explain: evidence, inference, confidence, candidate operations, rejected candidates and reasons, selected work, expiry/reevaluation condition, and the objective to resume if the opportunity fails.

## 4. Required state model

The typed model should include:

- **OpponentIdentity:** match/player identity supplied by the session, kept distinct from behavioral classification.
- **ObservedFact:** source unit/structure tag, owner, type, position, visibility, health/build progress, orders, game loop and provenance.
- **OpponentInference:** claim, supporting facts, confidence, freshness, alternatives and invalidation conditions.
- **OperationalPicture:** the current coherent view of opponent forces, structures, economy, technology, map presence and uncertainty.
- **StrategicObjective:** durable goal, parent/dependencies, completion evidence, constraints and recovery behavior.
- **EngagementTask:** immediate target or area, assigned force, desired effect, tactical phase and reassessment triggers.
- **ExpiringOpportunity:** trigger, target, latest useful game loop, travel/setup margin, hard gates, expected effect and expiry fallback.
- **WatchCondition:** entity or region to observe, state transition of interest, evidence required and response candidates.
- **DecisionRecord:** evidence snapshot, eligible/rejected candidates, chosen operation, confidence, expected result and observed outcome.

Keep observations separate from inferences. A remembered Hatchery is not currently visible; a predicted Lair is not an observed Lair; a queued drop is not damage already delivered.

## 5. First-frame economy gate

The operational picture is only useful if the bot starts with a functioning economy. The first observation must be reconciled before opening decisions are emitted.

The first-frame acceptance gate is:

- identify the Command Center, all visible starting SCVs and all visible mineral fields from the same normalized observation;
- preserve legitimate existing worker routes while balancing the initial workers across available mineral patches, using the configured saturation limit;
- produce a deterministic assignment for every eligible starting SCV, not just the first worker or the subset with no orders;
- issue the first worker gather commands in that same frame, alongside the independent Command Center rally/production command when those actions are available;
- never let an optional rally or the first SCV production request suppress initial mineral routing;
- verify the emitted actions use visible, owned workers and visible neutral mineral targets, and never exceed patch saturation.

The current baseline uses two workers per mineral patch by default. Twelve starting workers should therefore spread across at least six available patches when that many patches are visible and eligible; if more are available, the scheduler may spread further before assigning second workers. The exact distribution is deterministic and derived from the observed map and routes, not a hard-coded mineral-tag pattern.

## 6. Implementation sequence and proof

1. Preserve the first-frame worker spread and prove all eligible starting workers receive deterministic assignments and gather intents in the initial observation.
2. Add the opponent evidence ledger and stable identity distinction.
3. Add objective persistence and parent/dependency relationships.
4. Add watch conditions for production and technology structures, including Hatchery-to-Lair state changes.
5. Add opportunity expiry and time-to-impact calculations using game data and observed build progress.
6. Connect candidate engagements to movement, fire allocation, production and action validation.
7. Add recorded-observation replay scenarios for scout detection, tier-one containment, a Lair morph, a successful/failed drop window, expiry fallback and progressive sector control.
8. Compare outcomes against the simple attack-move baseline and retain decision traces.

The first-frame spread is a runtime requirement; the opponent-aware operational picture and Zerg scenario remain design requirements until their services, tests and live-match evidence exist. Do not describe documentation or a passing unit test as proof of a complete playable strategy.
