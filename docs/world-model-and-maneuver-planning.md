# World model, platform doctrine and contingency planning

## Purpose

The bot should reason about a map as a persistent world of places, terrain surfaces, routes, structures and opportunities. It should reason about its army as forces moving through that world according to unit-type doctrine. It should not repeatedly invent arbitrary unit destinations from raw coordinates.

This model extends the existing `SpatialMap`, `SpatialIntelligence`, `SpatialTerrain` and engagement-service design. It does not replace them or create another strategic layer.

## 1. Terrain topology: from pixels to places

The SC2 pathing, placement and terrain-height images are source measurements, not the final world model. Derive a topology from them:

- **Walkable regions:** connected components of pathable cells, with connectivity rules explicit about diagonal corner cutting.
- **Platforms:** coherent walkable surfaces, associated with elevation bands and bounded by ordered contours.
- **Edges:** actual platform boundary paths, not the rectangular perimeter of a sector. Preserve contour direction, segments, corners, curvature and adjacency.
- **Transitions:** ramps, bridges, narrow passages, drops and other connections between platforms. Record entry/exit regions, width, slope/elevation change, directionality where relevant, and traversal constraints.
- **Routes:** connected sequences of transitions and platform regions, with distance, exposure, capacity and current threat estimates.

A platform contour enables edge-following movement: a Reaper can traverse around a cliff edge, a patrol can trace a boundary, and a force can approach a ramp without treating a rectangular sector edge as terrain. Contours must be derived from pathability and height, validated against map bounds, and retain uncertainty where the source grid cannot establish a true cliff or traversable edge.

## 2. Hierarchical platform and elevation queries

Every platform is indexed into the existing spatial hierarchy: sector, quadrant and map. Queries should support:

- highest platform in a sector, quadrant, neighborhood or candidate route;
- elevation relative to nearby enemy and friendly positions;
- platform area, contour length, usable interior and edge segments;
- number of independent access routes and the narrowest transition;
- distance and travel cost from a force's current position;
- likely approach directions, sight lines and exposure;
- suitability for a particular role and unit type.

"Highest" alone is not "best". A useful position also needs access, firing arcs, vision, retreat routes, reinforcement reach and acceptable exposure. Return the measurements and provenance so doctrine can make the decision.

## 3. Persistent place intelligence and place FSMs

Represent important map locations as durable entities rather than rediscovering them every frame. Place kinds include base/expansion sites, resource clusters, platforms, bridges, ramps, watch areas and structure sites.

A base-site state machine can include:

`Unknown → Unseen/Unexplored → Scouted → Available → Claimed → Establishing → Established → Contested → Depleted → Abandoned`

Transitions are evidence-driven, not just elapsed-time guesses. The same site model applies to friendly, enemy and neutral expansion locations; ownership is a separate changing fact. Track resources remaining, town-hall identity, construction, control, last observation, last confirmed clear, threat and confidence.

Each place maintains a revisit priority based on strategic value, uncertainty, enemy likelihood, last-seen age, nearby events and available scouting capability. This creates a living vision schedule: a valuable enemy expansion not seen recently rises in priority; a recently cleared low-value area decays. Timers schedule reevaluation, but never convert stale memory into a current observation.

Structures are persistent entities too. Keep observed type, owner, tag, position, visibility, build progress, last-seen loop and inferred capabilities separate. An unfinished Engineering Bay is not yet evidence of completed research capability. Its construction creates a timed watch/opportunity:
- estimate completion only from authoritative progress and game-loop data;
- prioritize a revisit or attack before completion when feasible;
- if it completes, observe its new state and watch for research/orders;
- if vision is lost, preserve uncertainty rather than assuming completion or destruction.

A structure sighting can therefore create linked goals: revisit it, confirm completion, inspect research, identify nearby production, or deny the structure before its next capability becomes available.

## 4. Force doctrine: prescribed tracks by unit role

The long-term goal is for a platform to expose role-specific positions, lanes and tracks. A planner selects a doctrine and assigns units to compatible slots; it does not independently solve a fresh free-form positioning problem for every unit.

Examples:
- Reapers: traverse platform contours, use cliff/ledge transitions, probe vision boundaries and retain escape routes.
- Zerglings: maintain a moving cloud, orbit and screen outside enemy vision, dart in to reveal unseen areas, then disengage. They should not become a stationary clump.
- Siege Tanks: occupy measured firing slots with spacing and mutually useful coverage.
- Marines: use formation lanes, bridge observation posts and prepared fallback tracks.
- Scouts: use patrol loops and timed revisit points rather than standing indefinitely at one coordinate.

A movement doctrine defines desired spacing, speed/urgency, path preference, vision behavior, engagement envelope, disengagement trigger, regroup location and interruption policy. Unit-specific capabilities and actual terrain constraints determine which slots are legal.

For highly mobile units such as Zerglings, target repetition should be controlled by an engagement-cycle state: approach/probe → engage → disengage/move → re-approach. Do not repeatedly issue an attack against the same target while stationary. Re-engagement requires a completed disengagement condition (distance, elapsed movement, loss of contact, or another explicit policy gate), not merely another frame. This is doctrine to implement and validate, not an assumption about current behavior.

## 5. Contingency plans and deliberate force splitting

An operation should include a primary plan and one or more precomputed contingencies. A contingency is a conditional plan, not just a fallback point.

Example: if the enemy attacks from the left:
1. detect the attack direction and threshold force;
2. detach scouts or a screen to preserve observation;
3. split the main force onto two viable bridge/platform routes;
4. choose route assignments that make the opponent divide or expose its formation;
5. move to a preselected rally region;
6. establish a defensive line at a ramp or choke where terrain, firing arcs and unit composition favor us;
7. rejoin only after force integrity, enemy position and route safety gates pass.

Each branch records trigger evidence, required capabilities, route feasibility, force allocation, expected enemy response, rally conditions, expiry and a safe fallback if one route is blocked. The plan must not assume the opponent will split; it should score the value and risk if they do not. Keep enough force cohesion and command bandwidth to avoid turning a deliberate split into an uncontrolled rout.

A maneuver plan can be represented as a directed graph of phases and conditional transitions. Higher-level objectives remain intact while the engagement planner switches among maneuver branches.

## 6. Shared operational picture and event-driven goals

Place intelligence feeds the engagement service. Observations update facts; facts may support inferences; inferences and timers may create watch conditions; confirmed transitions may create or complete objectives.

Examples:
- a base site not seen for a long time becomes a high-priority scouting destination;
- an enemy expansion structure under construction creates a completion watch and a possible denial window;
- a completed Engineering Bay creates a follow-up watch for research or other relevant activity;
- a Zergling sighting updates enemy production inference and adds nearby expansion/technology searches;
- an attack from one side activates a prepared alternate-route contingency.

Every generated goal should identify its parent objective, evidence, confidence, expiry/revisit condition and completion evidence. Avoid duplicate goals by keying watches to stable entity/place identity and state transition.

## 7. Ownership boundaries

- `SpatialTerrain`: raw decoded SC2 terrain layers and world-coordinate sampling.
- Spatial topology builder: connected regions, platform contours and transitions derived from terrain.
- `SpatialMap`: canonical sector/quadrant/map indexing and geometric queries.
- Place intelligence: persistent site/structure state, observation age and revisit priority.
- Physics/path service: route feasibility, path costs, movement and collision constraints.
- Doctrine/formation services: role-specific tracks, slots, spacing and movement policies.
- Engagement service: persistent objectives, event-generated watches, opportunities and contingency selection.
- Unit FSMs and action pipeline: execute selected phases and validate/submit legal SC2 commands.

Do not duplicate sector/quadrant types or maintain competing map hierarchies. Extend the existing canonical model. Keep SC2 protocol types at the adapter boundary.

## 8. Implementation sequence and proof gates

1. Build connected pathing regions from cached grids; test connectivity, diagonal rules, bounds and unavailable layers.
2. Derive platform components, height bands and ordered boundary contours; test contour closure, orientation and map-edge behavior.
3. Derive transitions between platforms, including ramps and bridges; validate candidate transitions against SC2 pathing and observed movement.
4. Add platform measurements and hierarchical highest-platform/access-route queries.
5. Add persistent place identities, lifecycle states, observation age and revisit scheduling.
6. Add structure watch conditions and construction-completion opportunities.
7. Add role-specific tracks/slots, starting with Reaper edge traversal and mobile-unit disengage/re-engage cycles.
8. Add contingency graphs and replay scenarios for split routes, rally, regroup and defensive-line establishment.
9. Prove behavior in recorded observations and SC2 replays; compare losses, vision coverage, route choices and objective completion against a baseline.

The current code caches terrain images and provides geometric/spacing/platform-score utilities. It does not yet implement connected platform extraction, real contours, place FSMs, role-specific tracks, event-generated structure watches or contingency execution. Documentation is an architectural target, not a claim those behaviors are already live.
