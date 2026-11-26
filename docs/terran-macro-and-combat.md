# Terran macro, expansion, and Marine combat doctrine

## Objective

Build a reliable four-Barracks Terran engine, expand without exposing an unfinished Command Center, and turn continuous Marine production into a coordinated force that advances quickly but does not feed itself into enemy fire.

This document defines the intended behavior and evidence gates. A policy is not considered implemented because it is described here; code, tests, and local-match evidence must agree.

## Macro priorities

The scheduler arbitrates scarce minerals, supply, SCVs, and production capacity in this order:

1. Keep at least one Command Center training SCVs whenever minerals, supply, and queue availability permit.
2. Prevent supply blocks by forecasting near-term supply demand from queued SCVs and Marines, then start a Depot early enough to finish before the cap is reached.
3. Complete the first raised Depot and first Barracks without pulling additional workers unnecessarily.
4. Add Barracks until the four-Barracks target is reached, but stagger starts when simultaneous construction would starve SCV or Marine production.
5. Keep all completed Barracks producing Marines whenever the resource/supply budget permits.
6. Accumulate a deliberate expansion reserve and launch an expansion when the main base can afford the Command Center, crew, flight, and landing without collapsing production.

The scheduler must reason over committed costs, not just the current mineral bank. A construction order reserves its cost once submitted; a queued Marine or SCV order also consumes its cost and supply budget. Never issue duplicate commands while waiting for observation confirmation.

## Construction sequence and wall

The first builder is selected from the mineral-return event that crosses the 80-mineral pull threshold. The controller must correlate the unload with the worker observed carrying cargo on the preceding frame. If several unloads occur together and the delivered amount cannot identify the worker, it must not guess.

The same reserved SCV constructs the first raised Supply Depot and then the first Barracks. It remains reserved until the second structure is observed complete, after which its persistent worker assignment is restored.

The Depot/Barracks pair is a wall-placement problem, not two independent nearest-legal-placement decisions:

- Derive ramp orientation and choke geometry from the map's pathing, placement, and terrain data.
- Generate candidate footprints that cover the ground traversal corridor while preserving the intended friendly access.
- Ask SC2 to validate each footprint with the relevant build ability and builder.
- Score pairs for choke coverage, legal placement, construction access, and proximity to the home production area.
- Never issue a Depot-lowering command.
- Validate actual obstruction with enemy ground-unit movement in local matches. Placement-query success alone proves only legality, not wall effectiveness.

## Expansion site selection

A Command Center must be constructed in the protected main before it is moved. The expansion planner evaluates map-derived candidate centers rather than using a fixed offset from the main.

A candidate is viable only if:
- the full Command Center footprint is inside playable bounds and legal according to SC2 placement queries;
- the landing footprint is clear of units, resources, destructibles, and other structures;
- a mineral line and, where available, geysers are within useful worker travel distance;
- a pathing connection exists for workers and ground units after landing;
- the site is not an obviously exposed forward location when a safer equivalent exists.

Rank viable candidates using a transparent score with separate terms for resource yield, worker travel, pathing connectivity, landing clearance, and enemy exposure. Keep the raw terms and score in diagnostics so the policy can be tuned from evidence.

The expansion sequence is transactional:
1. Reserve minerals and a crew; do not interrupt the first four-Rax production plan unless the expansion budget explicitly permits it.
2. Build the Command Center at a legal protected-base location.
3. Wait for completion and confirm the exact structure tag.
4. Load the assigned SCV crew into that Command Center and verify cargo/transport state.
5. Lift and fly along a route that stays inside map bounds and avoids known enemy threat regions.
6. Query landing legality at the selected expansion site immediately before landing.
7. Land, verify the same Command Center is grounded and complete, then unload the crew.
8. Assign the crew to nearby mineral patches and add the new town hall to the economy planner.
9. On rejection, obstruction, or threat escalation, retain the crew and choose a safe hold/return policy; do not silently abandon the airborne Command Center.

A site score is a hypothesis. Mining throughput and survival from repeated local matches are the evidence.

## Marine movement and combat

Marines operate as a distributed force with a shared strategic objective and local individual survival decisions. They should move quickly across the map, but the force must remain mutually supporting.

### Threat model

Build a local threat estimate from visible enemy units and conservative remembered contacts. Use game data for weapon range, damage, cooldown, target filters, movement speed, and attack capability. A unit that cannot damage a Marine should not trigger the same response as a combat unit. Unknown enemy capability is treated conservatively until identified.

Each Marine evaluates:
- enemies that can currently threaten it;
- attack range and weapon cooldown;
- health and expected incoming damage;
- nearby friendly Marines and their health;
- friendly reinforcements moving toward the same front;
- retreat distance to a safer position and to friendly support;
- whether the objective can be reached without crossing a lethal threat region.

### Stutter-step policy

The baseline kiting loop is:

1. Select a valid target using threat, strategic value, distance, and expected overkill.
2. If the weapon is ready and the target is in range, attack.
3. During weapon cooldown, move to a point that increases separation from immediate threats while moving toward friendly support.
4. Do not retreat directly toward an enemy or into an already occupied/blocked tile.
5. Reassess after the next observation; issue no redundant move/attack command if the current order already matches the desired action.
6. Re-engage when the local friendly force balance, health, and range make the trade favorable.

Retreat should be vector-based, not simply "move toward home." The preferred point lies on a path that increases distance from the threatening enemy while decreasing distance to the nearest healthy reinforcement group. If no safe point is known, use a conservative fallback toward the friendly rally axis.

### Force cohesion and reinforcement streaming

The army advances along an objective axis. Rear units continue forward to reinforce, while the front line is allowed to kite backward toward them. This creates a moving engagement envelope rather than a single blob that either charges or retreats as one.

Use a cohesion radius and local force-balance threshold with hysteresis:
- advance when the front has support and no immediate lethal threat;
- kite when a threat is in range and the Marine can trade while moving;
- fall back when expected incoming damage or local enemy strength exceeds the configured margin;
- regroup when isolated or badly damaged;
- resume the advance once reinforcements arrive and the local balance recovers.

Do not let one Marine's retreat pull the entire army backward. Squad-level intent and unit-level survival are separate decisions.

### Targeting and damage efficiency

Focus fire should prefer units that are dangerous to Marines, nearly dead, or tactically important. Estimate remaining health and incoming friendly damage to avoid wasting volleys on targets already certain to die. Structures are secondary unless they block the route or are the assigned objective. Attack-move is a fallback, not a replacement for observed target selection and kiting.

## Evidence and scorecard

Every local match should report:
- SCV production uptime and idle time;
- supply blocks and their duration;
- start/completion loops for Depot, each Barracks, and each Command Center;
- Marine production uptime and output per Barracks;
- expansion candidate features, score, flight time, landing result, and worker transfer time;
- Marine damage dealt/received, deaths, target overkill, time in weapon range, and time spent retreating/regrouping;
- distance between front line and reinforcements;
- objective progress and game outcome.

Compare the policy against a simple attack-move baseline and a no-kiting baseline on the same map/opponent setup. Tune one policy dimension at a time. Never describe the Marines as having perfect micro or highest effective DPS until repeated controlled match data supports the claim.

## Implementation boundary

All commands remain subject to live observation, game-data-derived abilities/costs, placement and available-ability queries where supported, action-result handling, and persistent unit identity. Rejected or ambiguous actions fail closed and are logged. No ladder automation, release, or external deployment is implied by this local-match development plan.
