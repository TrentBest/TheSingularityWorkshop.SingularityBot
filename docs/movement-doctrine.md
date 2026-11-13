# Movement, force-control and engagement doctrine

## Purpose

This document defines the intended doctrine for how forces occupy, traverse, secure and contest space. It is a design contract for strategy authoring, runtime behavior, visualization and future human-in-the-loop learning. It is not a claim that the complete doctrine engine or its behaviors are implemented.

A formation is not fundamentally a row, column, wedge or circle. Geometry is one possible expression of a larger order: which space a force controls, which routes it protects, what movement is permitted, how its elements support one another, and how it responds to contact.

The system must distinguish strategic intent from tactical execution. Strategy and doctrine define objectives, permissions, roles, constraints and desired effects. Runtime planners resolve feasible positions and actions from current observations, pathing, terrain, unit capabilities, threats and action validation.

## 1. Doctrine is more than playstyle

A playstyle describes broad strategic character: economic, defensive, aggressive, opportunistic, all-in, harassment-oriented, and so on. It does not fully describe how a force operates.

The authoring model should compose several independent dimensions:

- **Strategic objective:** expand, defend, contain, break a position, deny resources, raid, retreat, escort, establish a forward base.
- **Movement doctrine:** advance, hold, screen, patrol, probe, flank, withdraw, leapfrog, reinforce, regroup, or remain concealed.
- **Space-control doctrine:** secure a platform, guard a bridge or ramp, protect a corridor, deny an approach, establish a beachhead, maintain a perimeter.
- **Force organization:** squads, platoons, task groups, reserves, escorts, support elements and independent specialist units.
- **Engagement doctrine:** target selection, focus fire, area damage, cooldown movement, pursuit, disengagement and target handoff.
- **Unit behavior profile:** how a particular unit class should express aggression, caution, mobility, protection, target preference and opportunity-seeking.
- **Presentation intent:** the desired visible character of the action, such as disciplined advance, overwhelming swarm, probing raid, defensive stand or sudden strike.

These dimensions interact, but should not collapse into a single style label or hard-coded formation enum.

## 2. Movement is conditional, not mindless

A movement order must not be interpreted as permission for every unit to take the shortest route to a destination. A movement operation should define:

- objective and destination region (which may be an area, corridor, platform or control boundary rather than one point);
- approach and withdrawal routes where known;
- required reconnaissance and security conditions;
- protected assets and vulnerable force elements;
- permitted movement and engagement behaviors;
- required support, reserve and reinforcement relationships;
- conditions to pause, hold, reform, reroute, abort or continue;
- acceptable risk and the evidence required to accept it.

The commander should reason about the force as a whole. Individual unit controllers execute their assigned role and report facts; they should not independently abandon the plan because a locally attractive target appears.

### 2.1 Advance and establish a beachhead

An advance through a choke or onto an adjacent platform is a staged operation:

1. **Reconnoiter beyond the objective.** Scout far enough beyond the far side of the intended beachhead to detect an immediate ambush or force waiting to engage units as they emerge. Scout both flanks and likely approach routes to detect an attempted envelopment.
2. **Secure the near approach.** Identify threats, pathability, firing positions, retreat routes and the space needed to deploy. Do not send the main body through a corridor whose far side is unknown when reconnaissance is feasible.
3. **Move under protection.** Durable screening units such as Marauders and suitable vehicles should interpose between fragile Marines and likely enemy contact. Support units retain useful range and line of fire without becoming the exposed vanguard.
4. **Take the beachhead.** A suitable forward element establishes control of the next platform or defensible area. The rest of the army does not blindly stream into an unverified space.
5. **Deploy positional support.** Where appropriate, dropships may transport siege-capable vehicles to positions that secure the platform or cover the approach. Deployment must account for unloading exposure, landing access, firing arcs, friendly traffic and retreat or recovery options.
6. **Protect the resupply route.** Establish mutually supporting positions along the corridor. Temporary bunkers can shelter Marines and provide durable local coverage while the line is extended. A bunker chain is not merely a row of structures: each position should cover relevant approaches and support adjacent positions without leaving dangerous gaps.
7. **Provide detection and response.** Detection coverage should make cloaked crossings observable. Detection alone does not neutralize a cloaked intruder: on detection, assign a search, pursuit or containment response using units capable of revealing and engaging it. Preserve the distinction between a suspected contact, a detected cloaked unit and a confirmed target.
8. **Reform on the far side.** The forward element must leave sufficient space and usable routes for following units to cross, deploy and assume roles. Avoid crowding the exit, blocking friendly movement or allowing the first units through to be isolated.
9. **Post guards.** Where feasible, guard the far side of each secured platform and its approaches. If an enemy occupies an adjacent platform, bias the guard position to our side of the connecting bridge or ramp so the enemy cannot cross unobserved or obtain a free engagement.
10. **Confirm before extending.** The next advance begins only when the required security, support, space and force-readiness conditions are met.

A beachhead is a controlled area with defined boundaries, approaches, support and reinforcement requirements—not simply the first unit to reach a waypoint.

### 2.2 Hold a line

Holding is categorically different from advancing. A hold order establishes a movement prohibition:

- Units retain assigned sectors, lanes, positions or coverage responsibilities.
- They may move to close a genuine gap, maintain required spacing, avoid an immediate area threat, respond to a defined attack, or execute another explicit exception.
- They do not chase targets, drift toward attractive fights, or follow retreating enemies beyond the line merely because an attack opportunity exists.
- A unit that leaves its post must trigger coverage reassessment or handoff.
- The line's commander tracks gaps, overlap, exposed flanks, support coverage and reserve availability.

A hold is not “move to these coordinates and then attack anything nearby.” It is an enduring constraint on movement and engagement. Exceptions should be named, bounded and observable.

### 2.3 Guard, screen, patrol and reserve

These are distinct responsibilities:

- **Guard:** watch a defined approach, crossing, platform edge, resource line or asset. A guard's primary duty is observation and denial, not pursuit.
- **Screen:** operate forward or laterally to detect, delay, channel or disrupt enemy movement while preserving the main force.
- **Patrol:** traverse a defined area or route to refresh information and respond to incursions. Patrol routes need coverage, timing and handoff semantics.
- **Reserve:** remain available to reinforce a threatened sector or exploit an opening. A reserve is not idle waste; its availability and commitment conditions are part of the plan.
- **Escort:** protect a moving asset or force element, adapting position to threats and route changes.
- **Search:** investigate a known or suspected contact using a bounded area and explicit completion/abandonment conditions.

A role includes its area, responsibilities, movement permissions, engagement permissions, reporting obligations and conditions for reassignment.

## 3. Force organization and movement relationships

Squads and platoons are useful command abstractions, not necessarily rigid geometric units. A force may contain several task groups with different movement policies at the same time:

- a ground element holding a corridor;
- a screen watching a flank;
- a reserve behind the line;
- a dropship group transporting siege units;
- an air escort protecting that transport;
- a specialist team searching for a detected cloaked unit.

The command structure should support assignment, delegation, support relationships, temporary attachment/detachment, regrouping and replacement. A unit may change task group as the mission evolves, but the change must be explicit and leave its previous responsibility covered.

A force's organization should express relationships such as:

- protects / is protected by;
- screens / is screened by;
- supports / is supported by;
- follows / leads;
- reinforces / is reinforced by;
- guards / is guarded by;
- coordinates fire with;
- may advance only when;
- must remain within support of.

This is more expressive than assigning every unit to a fixed slot in a shape.

## 4. Engagement doctrine: how a force fights

Movement doctrine determines where and when a force operates. Engagement doctrine determines how it applies force once contact occurs. It must account for weapon mechanics, target vulnerability, area damage, cooldowns, friendly roles and the commander's objective.

### 4.1 Area damage and opportunity selection

A splash-damage unit such as a Firebat should evaluate the value of an attack by the damage it can distribute across the affected enemy group, not become emotionally or mechanically attached to one target. It should prefer a valid shot that meaningfully damages several enemies over one that damages only a single enemy, while accounting for range, weapon timing, friendly-fire rules, target durability and the likelihood that the enemies will remain in the area.

Target selection is therefore sometimes an area-of-effect placement problem, not a single-target ranking problem.

### 4.2 Layered force behavior

A mixed force should behave as coordinated layers rather than every unit independently selecting the nearest enemy.

For example:

- durable front-line units absorb, screen or control the initial engagement;
- Marines behind them exploit safe firing opportunities and punish enemies that attempt to engage or bypass the screen;
- high-damage or precision units finish dangerous targets when the remaining health makes that efficient;
- support and reserve elements preserve the force's ability to continue fighting.

The exact role depends on unit capabilities and current conditions. “Finish the last hitpoints” is a target-allocation objective, not a license to waste a high-value shot when another threat is more urgent. The coordinator should consider overkill, time-to-kill, threat, target escape and opportunity cost.

### 4.3 Aggression is a behavior profile, not a universal attack command

Aggression should describe how a unit seeks, enters, sustains and exits engagements. Different units should express it differently.

- **Zerglings — relentless, mobile pressure:** use speed and numbers to attack, pass through, and make room for the next attackers. A swarm can use coordinated alternating circulation—clockwise and counter-clockwise flows or equivalent local traffic patterns—so units do not all collide at one point or queue behind one another. The intent is repeated attack opportunities and continuous pressure, not a literal fixed circular path in every terrain layout.
- **Obstacle interaction:** if Zerglings pass a destructible rock or other attackable neutral obstacle that blocks or constrains the route, the swarm may take useful attacks while moving past, provided doing so does not undermine the primary objective or expose the force. Neutral objects should be evaluated by whether damaging them improves access, creates a route, or is worthwhile opportunistically.
- **Firebats — area pressure:** seek positions and attack windows that distribute splash damage efficiently; avoid wasting attacks on isolated low-value targets when a better cluster is available.
- **Marines — disciplined ranged pressure:** exploit firing lanes, maintain support and spacing, use cooldown-aware stutter-step behavior, and avoid being pulled out from behind their screen without authorization.
- **Marauders and vehicles — durable control:** help establish the front, screen fragile forces, control approaches and create safe operating space for damage-dealing elements.
- **Ghosts — covert strategic aggression:** avoid unnecessary direct exposure while infiltrating, gathering information and creating high-consequence opportunities such as a nuclear strike. Once committed to a strike, the operation still needs an escape, distraction or survival plan; stealth is a means, not invulnerability.
- **Battlecruisers — coordinated air power:** operate as squadrons with escort, focus, screening, rotation, reinforcement and withdrawal behavior. Their movement should express air-space control and concentration of force, not a pile of independent attack-move orders.

These are examples of authoring intent. The runtime must derive feasible behavior from actual unit data, available abilities, terrain, pathing, weapon mechanics and observed threats.

## 5. Human control, learned concepts and AI execution

The long-term training surface should allow a person to play through the bot's SC2 client using familiar hotkeys and left/right mouse interaction. The human should be able to select, group, move, attack, patrol and issue other supported commands in the ordinary game idiom.

The system should not merely copy every mouse gesture into a permanent scripted sequence. It should infer the concept being demonstrated, preserve uncertainty, and expose what it believes it learned.

For example, when a player repeatedly stutter-steps a group, the system should recognize a possible higher-level behavior:

- units fire when their weapons are ready;
- during cooldown, they reposition;
- movement preserves firing opportunities while reducing incoming damage or improving distance to support;
- the group is controlled as a coordinated whole rather than as unrelated unit commands.

The AI can then compare the human's demonstrated intent with its own precise estimates of weapon cooldowns, ranges, movement, pathing, threat and unit-specific opportunities. It may execute the recognized concept with more precise per-unit scheduling while preserving the human's intent.

The learning loop should distinguish:

1. **Raw input:** hotkeys, selections, mouse buttons, cursor positions and game-loop timestamps.
2. **Observed effect:** resulting unit selections, orders, movement, attacks and state changes.
3. **Recognized concept:** stutter-step, focus fire, kite, screen, flank, hold, patrol, regroup, drop, breach or another known behavior.
4. **Confidence and alternatives:** what the system thinks the player intended and what else could explain the input.
5. **Doctrine proposal:** a reusable, parameterized behavior or policy, not an opaque recording.
6. **Human review:** show the inferred concept and its visualization; allow correction before treating it as learned doctrine.
7. **Evaluation:** replay the behavior against recorded observations and compare outcomes with a baseline.

Human demonstrations are evidence about intent, not proof that a tactic is safe, optimal or universally applicable. Learned policies remain bounded by hard action validation and current match facts.

## 6. Map visualization is an authoring and reasoning surface

The map visualization should become a practical doctrine editor, not merely a replay minimap. A user should be able to identify and annotate:

- platforms, ramps, bridges, chokes and traversable regions;
- beachhead boundaries and assembly areas;
- guard posts, patrol routes, screens and observation zones;
- advance corridors, fallback routes and reinforcement lanes;
- bunker chains, turret/detection coverage and supporting firing positions;
- siege/deployment areas and dropship landing/unloading zones;
- squad/platoon/task-group areas and support relationships;
- prohibited movement regions, hold lines and engagement boundaries;
- known threats, suspected contacts, uncertainty and stale intelligence.

The visualization should show the difference between planned, observed and inferred information. It should make coverage gaps, unsupported advances, blocked reinforcement routes and exposed flanks apparent. The authoring surface should permit a simple directive—such as an eight-Gateway all-in—as well as detailed map-specific placement and movement doctrine. The simple directive can expand into editable intent; it must not silently invent tactical certainty.

Map annotations should reference stable map features or regions where possible, not brittle screen pixels. The runtime resolves those annotations against the active map's pathing/buildability and current match state.

## 7. Spectacle and drama are explicit evaluation dimensions

Winning matters, but it is not the only measure of a compelling match. The system should be capable of creating readable, varied, consequential action without confusing spectacle with tactical success.

Evaluate at least three dimensions separately:

- **Strategic effectiveness:** objective progress, economy damage, map control, force preservation, victory probability and resource efficiency.
- **Tactical quality:** positioning, timing, coordination, target selection, risk management, execution and recovery.
- **Spectacle:** visible scale, contrast, surprise, synchronized action, meaningful reversals, high-stakes engagements, escapes, raids and decisive moments.

A BC squadron arriving over a defended position, a Ghost infiltrating for a nuke, a Zergling swarm flowing through a breach, or a disciplined Terran advance across a fortified corridor can each be compelling in different ways. The system should create opportunities for these moments through coherent doctrine and then help spectators understand why they matter.

Spectacle must not become random suicidal behavior or hidden manipulation of match outcomes. Report the tradeoff honestly: a spectacular maneuver may be strategically poor, and a strategically sound maneuver may be visually quiet. Spectator camera direction and highlight selection should consume match events without leaking private information to players or changing the bot's decisions.

## 8. Runtime contract and safety

A doctrine definition should eventually identify:

- stable ID and version;
- objective and applicability conditions;
- participating force roles and unit capability requirements;
- spatial regions, routes and support relationships;
- movement and engagement permissions;
- required evidence, confidence and hard guards;
- pause, hold, reform, abort, fallback and completion conditions;
- resource, time, exposure and reorganization costs;
- diagnostics explaining why an action or transition was selected or rejected.

Hard constraints—legal actions, current ownership, visibility requirements, pathability, unit capabilities and explicit hold prohibitions—must never be traded away by a weighted desirability score. Stale or insufficient evidence fails closed. A failed path, unavailable ability, destroyed support position or newly exposed flank should trigger reassessment rather than an endless stream of invalid commands.

Doctrine is declarative intent. FSMs and planners implement it through the existing observation-to-intent-to-validation pipeline. The doctrine layer must not submit raw SC2 protocol actions or bypass action arbitration.

## Named operation and route authoring contract

The repository now has a validated, declarative military-plan model alongside the existing goal/action posture model. It provides stable identities for:

- **Locations:** named map-space objective anchors, including an optional radius for an objective area.
- **Routes:** reusable ordered waypoint paths with explicit constraints such as avoiding known threats, avoiding contact, and preferring pathable terrain.
- **Operations:** named force tasks with a role, route, objective location, required force count, optional semantic ability requirement, synchronization group, completion condition and operation dependencies.

Operation dependencies are hard plan barriers: the model validates references and rejects dependency cycles, and `MilitaryPlanRuntime` withholds dependent operation intents until prerequisite completion is explicitly observed. The runtime distinguishes an intent, a submitted request, observed execution and observed completion; submission alone never releases a dependent operation. A map editor can author or rename locations and waypoints without encoding screen pixels or protocol-specific ability IDs. A semantic ability such as `TacticalJump` must be resolved against the active SC2 game-data catalog by an execution adapter; it is not itself an executable ability ID.

For example, a Battlecruiser mineral-line pincer can describe a first Battlecruiser taking an evasive, avoid-contact route to the opponent's main mineral line, followed by a second Battlecruiser operation that depends on the first positioning operation and requires the semantic `TacticalJump` capability to reach the expansion mineral line. Both operations can share a synchronization-group name so the commander/editor can present them as one coordinated strike.

This is an authoring and validation contract, not yet a military operation executor. The current model does not calculate a threat-aware path, prove that a route is concealed or safe, select units, invoke Tactical Jump, or verify arrival. Those require observation-backed route planning, game-data ability resolution, force assignment and action-pipeline integration. The contract deliberately keeps those responsibilities separate rather than pretending that a list of waypoints is already a working tactical plan.

## 9. Implementation sequence

Do not attempt to implement the entire doctrine in one step. Keep the first playable Terran economy as the near-term runtime gate. Then add capabilities in observable, testable slices:

1. Define typed doctrine contracts for objective, role, movement permission, area/route, guard condition and exit condition.
2. Add recorded-observation fixtures and visualization for map regions, corridors, guard posts and hold boundaries.
3. Implement a minimal hold/guard behavior with explicit movement prohibition and gap-filling exception.
4. Implement staged advance with reconnaissance gates, beachhead establishment, reform space and guarded release.
5. Add support relationships and screen/reserve handoffs.
6. Add bunker/detection corridor planning and a detected-cloaked-unit search response.
7. Add target allocation for focus fire, overkill control and area-damage opportunity scoring.
8. Add unit-specific profiles and coordinated swarm/air-squadron movement.
9. Add human input capture and concept recognition, with reviewable doctrine proposals.
10. Add spectacle analytics and spectator presentation as consumers of the match event stream.

Each slice needs contract tests, deterministic replay tests and, where possible, live-match evidence. A visualization of a proposed doctrine is not proof that the runtime executes it. A successful command submission is not proof that the intended tactical effect occurred.

## Non-goals

- No assumption that every force must occupy a named geometric shape.
- No universal attack-move behavior hidden beneath every doctrine.
- No requirement that every unit remain in a rigid squad forever.
- No raw mouse-recording system masquerading as learning.
- No use of spectacle as a substitute for tactical coherence or honest evaluation.
- No claim that the doctrine described here is already implemented.
