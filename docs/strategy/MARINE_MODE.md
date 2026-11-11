# MARINE MODE

**Status:** Strategy doctrine / implementation target  
**Execution model:** Deterministic, observation-driven policy. No LLM is required for the default mode.

Marine Mode optimizes for sustained map pressure and the fastest reliable delivery of a growing Marine force to useful positions. A lone Marine reaching the enemy start is not an objective. Against an opponent who may field Marines first, it is an avoidable donation. The objective is continuous production, survivable control, distributed information, and repeatable damage against exposed assets.

## Strategic invariants

1. **Keep the economy alive.** Every Command Center/Orbital Command should train an SCV whenever minerals, supply, and production availability permit. Construction temporarily reserves a worker; it must not permanently remove that worker from the economy.
2. **Keep every starting SCV mining until a build is affordable.** Do not reserve or stage a worker before the mineral threshold. If the selected SCV is actively mining, let it finish that harvest; once it carries the load, dispatch it directly to construction rather than sending it back to the Command Center first. After construction, release it to mining unless another affordable build is ready.
3. **Return builders to mining.** Release a construction reservation as soon as the assigned structure is observed complete, then restore the worker to an appropriate mineral route.
4. **Never trade Marines needlessly.** Do not issue blind attack-moves through unknown territory or chase a visible contact across the map. The first Marine is a dedicated early-warning scout: move it to the farthest safe, non-enemy sector adjacent to our starting sector and watch for cannon rushes, proxy structures, worker rushes, and other cheese. Keep this scout separate from the first four-Marine pressure squad. Only nearby contacts inside a bounded local-defense envelope may trigger pursuit. Use visible targets, weapon cooldown, weapon ranges, health, and friendly support to decide whether to fire, kite, advance, or retreat.
5. **Preserve a recoverable force.** Dispersion is useful only while squads can withdraw and reinforce one another. Enemy contact should cause nearby elements to converge toward a survivable critical mass.
6. **Fail closed on missing information.** Unknown enemy capability, stale vision, invalid pathing, or uncertain command acceptance must not be treated as proof of safety.

## Phase plan

### 1. Establish continuous production

- Keep SCV production running.
- Complete the first Depot without stranding its builder.
- Construct the first Barracks, then dispatch Barracks two as soon as its 150-mineral cost is available while Barracks one is still building. Do not wait for the first Marine to enter production.
- Choose the unreserved SCV with the shortest trip to the Barracks placement cluster; return the first Barracks builder to mineral mining as soon as its structure completes.
- Start gas collection as soon as the production plan can sustain the worker and Refinery investment. Add Barracks as mineral income and supply permit.
- Measure worker idle time, builder idle time, production downtime, supply-block duration, and time to first Marine.

### 2. Establish Marine control

A Marine is controlled from current observations, not a one-time move order.

- Assign the first produced Marine to the farthest safe, non-enemy sector adjacent to the friendly start sector. Hold there as an early-warning observer and report visible enemy structures/units through the normal observation pipeline.
- Keep the scout out of the first four-Marine pressure formation. It is watching our approaches for cannon rushes, proxies, worker rushes, and other disruptive openings.
- The next four Marines rendezvous as the first pressure squad. Their movement toward the enemy is governed by the force's current threat assessment; do not send a lone Marine up an unknown enemy ramp.
- Fire when the weapon is ready and the target is in range.
- During weapon cooldown, stutter-step to a better position rather than standing idle. Equal-strength local engagements are not automatic retreat conditions; retreat only when incoming local threat materially exceeds the force's effective damage potential or the Marine is otherwise unsafe.
- Estimate the threat envelope of visible enemy units and defensive structures from authoritative game data.
- Do not step into a known outranging weapon envelope merely to acquire a target.
- Prefer exposed, undefended structures and isolated units when they can be attacked without losing the Marine.
- Retreat toward friendly reinforcements when threatened; regroup before resuming pressure.
- Re-evaluate every frame and cancel stale movement when threat, target, or support conditions change.
- Treat destructible neutral blockers as obstacles to clear, not combat targets to camp: fire when ready, then advance during cooldown. Take another shot only if the Marine is still blocked and the weapon is ready; stop targeting the blocker once the formation has moved beyond it.
- Move each formation member through short local waypoints (about four world units at a time) toward its assigned slot. Do not issue a single distant move that lets pathfinding route the entire squad around the Marine ahead.

### 3. Scale the Marine economy

- Continue Marine production across available Barracks and spend resource float on the next affordable, placement-validated infrastructure or production action instead of allowing minerals to accumulate. Do not reserve 400 minerals for a Command Center merely to unlock Barracks three.
- Establish Refinery gas income early enough to support the Tech Lab, Marauders, and Marine upgrades; balance gas workers against the mineral income needed to sustain four Barracks.
- Once four Marines are available, prioritize a Tech Lab on one Barracks, then add Reactors to the remaining production facilities as soon as the gas/mineral budget and add-on clearance allow. The initial production milestone is four Barracks, not an unconditional jump to eight.
- Add a Reaper as a dedicated mobile scout when prerequisites and resource budget permit. Preserve it: avoid unfavorable fights, retreat from threats, and use safe alternate routes or another jump point rather than forcing a bad engagement.
- Introduce a small Marauder contingent from the Tech Lab to absorb pressure and slow armored ground threats while Marines provide sustained DPS.
- Use damage-aware focus fire: estimate allied damage over a short firing window, assign only enough attackers to finish a target, and allocate remaining damage to another viable target. Maintain focus on a chosen target until its predicted remaining health reaches the kill threshold; avoid wasting a full volley as overkill.
- Treat the first few dozen Marines as the initial mass-building phase; do not let early add-on construction interrupt the force before the economy can sustain it.
- Do not make an expansion Command Center a prerequisite for Barracks three or four. Expand toward eight Barracks total as the resource and supply model supports it; town-hall expansion is a separate economic decision and must not hold up production.
- The intended add-on mix is one Tech Lab-capable Barracks and seven Reactor Barracks. The Tech Lab timing is a measured decision based on gas income, production saturation, and the value of adding Marauders; it is not a fixed opening timestamp.
- Add two Engineering Bays around the twenty-Marine scale only when doing so does not collapse Marine production or cause a supply/resource stall. Research weapon and armor upgrades concurrently when gas and income support both.

These counts are strategic targets, not unconditional build commands. The planner must account for current resources, construction commitments, add-on availability, supply, and production utilization.

### 4. Distribute pressure and vision

- Begin with one dedicated early-warning Marine in the farthest safe adjacent sector. The next four Marines form the first pressure squad and establish a safe rally/reinforcement route.
- As the central force grows, release small probing elements—initially pairs—into adjacent sectors around the enemy start.
- Assign sectors so patrols cover likely traversal routes without duplicating the same lane.
- Maintain a central reserve. Do not split below the minimum force needed to survive contact.
- When a probe detects a threat, it should delay, kite, and withdraw while nearby elements converge.
- Under tolerable enemy pressure, widen the search and increase the number of probes. Under concentrated pressure, collapse toward the critical mass and re-form.

The long-term formation model treats Marines as a spatial distribution: spread to maximize coverage and information while contact is absent; contract toward mutual support when contact occurs. This is bounded dispersion, not unlimited separation.

## Marine micro decision order

For each Marine, evaluate in this order:

1. Is the unit alive, visible, and commandable?
2. Is it under immediate threat or below its retreat-health threshold?
3. Is a visible enemy inside its weapon envelope?
4. Can it fire safely now?
5. If the weapon is cooling down, what short movement improves safety or firing position?
6. Is the current target still valid and worth pursuing?
7. Is the assigned squad objective safe to advance toward?
8. Otherwise, regroup or hold the assigned coverage position.

Threat evaluation must consider enemy weapon range, damage potential, target health, friendly support, and retreat path—not distance alone. A structure that cannot attack is a valid pressure target only when no nearby defender makes the engagement unsafe.

## Base layout hypothesis

Use map-relative quadrants and terrain/pathing queries rather than fixed map coordinates:

- **Mineral-line side:** Place the first Supply Depot beyond the mineral patches, on the side away from the Command Center, where it does not obstruct worker traffic or mining paths. Validate each placement with SC2 placement and pathing queries.
- **Ramp/production quadrant:** Barracks and Factories grouped near the ramp, with add-on clearance and defensible worker access.
- **Expansion/air-mobility quadrant:** Pre-position additional Command Centers where practical. Lift, fly, and land at expansions when the route and landing zone are safe; upgrade to Orbital Commands when economically appropriate. Orbitals can provide scans and MULE income, and may serve as mobile vision assets, but airborne town halls are not assumed safe or invulnerable.
- **Technology quadrant:** Engineering Bays and other upgrade/technology structures grouped with room for future additions and safe access.

The layout planner must preserve worker routes, building footprints, add-on sockets, rally paths, and expansion flight/landing clearance. The four-quadrant arrangement is a hypothesis to test across maps, not a rigid geometric rule.

## Measurement and tuning

Every phase should be tuned from match traces and replays. Track at minimum:

- SCV production uptime and worker idle time, including idle workers with no active order
- Mineral/gas float, Refinery uptime, and worker distribution between minerals and gas
- Build-order spend decisions, placement-query success, and time from affordability to command submission
- Barracks add-on state, Reactor/Tech Lab production utilization, Marauder/Reaper production, and upgrade progress
- SCV positional progress toward active Move orders; distinguish a stalled worker from a valid gather/build order
- Builder reservation duration and time from completion to mining resumption
- Supply-block time and Depot lead time
- Barracks utilization and Marine production uptime
- Time to first Marine, 10 Marines, 20 Marines, and 40 Marines
- Marine travel time to enemy-side sectors
- Damage dealt/taken, Marine survival, and structure damage per lost unit
- Stutter-step firing uptime and damage avoided while kiting
- Probe coverage, duplicate sector coverage, and regroup time
- Upgrade timing relative to production downtime and resource float

Do not tune thresholds from a single match. Compare multiple maps, opponents, and starting conditions. A faster arrival that loses the force is not an improvement. A contact seen at distance is information, not permission to advance.

## Implementation boundary

Marine Mode is a named, deterministic strategy profile in the bot. It does not require ProtocolAi, GrammarAi, HeadlessAi, or an LLM in its default execution path. An optional LLM-assisted planning mode can be explored later, but all unit commands remain validated against current observations, legal abilities, pathing, and explicit safety gates.
