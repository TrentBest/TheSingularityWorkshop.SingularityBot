# Terran economy and cohesive-force doctrine

## Strategic objective

Build a Terran economy that continuously converts minerals and vespene into supply capacity, production capacity, SCVs and Marines. The first combat objective is not a fixed four-Barracks ceiling: it is the highest sustainable Marine output the observed economy can support while preserving expansion, supply and worker production.

The economy scheduler owns priorities and reservations. Individual construction controllers execute jobs; they must not independently make long-lived strategic decisions about which worker or structure should come next.

## Ephemeral SCV logistics

An SCV is a reusable economic agent, not a permanently assigned "miner", "gas worker" or "builder". Its role is a short-lived task derived from current need and observed state:

1. Determine the next construction, gas, repair, transfer or mining task and its latest useful arrival time.
2. Score eligible live SCVs by current cargo, current order/phase, pathing distance, expected arrival, disruption to mineral/gas throughput, and whether the worker is already committed to a non-interruptible action.
3. Prefer a worker whose completed mineral trip can be converted directly into construction when the opening schedule values the time more than the carried cargo.
4. Reserve the SCV only for the task window. After the structure is observed started/completed or the task is cancelled, release it and re-evaluate its next role against current saturation and demand.
5. Never infer that a Move or Build command succeeded from submission alone. Confirm action result and observed worker/structure state.

A worker may therefore mine minerals, return cargo, mine again, switch to gas, return gas, build a Depot, then return to mineral work. Gas saturation is a live allocation decision: when a Refinery becomes available and the marginal value of vespene exceeds the worker's mineral contribution, dispatch the nearest eligible SCVs; when gas is saturated or the tech queue is empty, release surplus workers back to minerals.

Persistent worker-to-resource assignments remain useful as the default route, but they are not identity-level job contracts. A construction reservation temporarily suspends a route; release must cause a fresh assignment decision rather than blindly restoring a stale patch or gas role.

## Opening construction geometry and cadence

### Supply Depots

Place the first Depot from observed geometry, not a fixed map-center offset. Derive the mineral-line centroid from visible neutral mineral fields associated with the starting Command Center. Generate candidate footprints just behind the mineral line on the town-hall side, with nearby lateral alternatives. Ask SC2 to validate every candidate; never equate a generated coordinate with legal placement.

The initial builder should preferentially be a live SCV already carrying a completed mineral load, unless a specifically preferred recycled builder is part of a deliberate construction chain. This is an intentional conversion of one completed mining trip into an earlier structure. Do not wait for a full unload-and-return cycle when the schedule values the build timing more highly.

After the first Depot, the construction scheduler should continue forecasting supply rather than stopping at a fixed second-Depot milestone. Start another Depot early enough that expected supply demand does not block SCVs or Marines. Continue adding Depots while the projected supply headroom requires them; do not blindly build them when verified Command Center/Orbital supply capacity and current production already provide sufficient headroom.

### Barracks and recycled builders

Dispatch a second SCV for Barracks construction while the first Depot builder is finishing its Depot, when resources and worker throughput permit. On completion, release each builder immediately and choose the next eligible SCV by arrival and economic opportunity cost. A completed structure must not pin its builder to a permanent construction role.

Keep the Marine factory fed. Construction scheduling must not repeatedly consume the same minerals reserved for the next Marine, SCV, Refinery, Depot or expansion. The economy commander must decide which investment wins when not all are affordable; the decision and reason must be recorded.

### Refineries and gas

Establish Refineries early enough to avoid a zero-gas economy becoming a technology bottleneck. Select geysers associated with active bases, validate placement through SC2, and assign the required SCVs using the same ephemeral role scheduler. Re-evaluate gas saturation as soon as a Refinery starts/completes and whenever the technology/production queue changes.

Gas is strategically scarce. The combat and production planners must account for vespene opportunity cost rather than treating one gas as equivalent to one mineral.

### Command Centers, Orbitals and MULEs

Expand the number of Command Centers as the mineral bank and safe expansion sites permit. A new town hall adds production and supply capacity, but its 400-mineral investment, build time, worker transfer and exposure must be weighed against additional Depots and production.

As soon as a Barracks is complete and the Command Center can afford the authoritative Orbital upgrade cost without violating higher-priority SCV/Marine/construction reservations, upgrade the Command Center. Do not wait for an arbitrary later opening milestone. Once the Orbital is observed complete, use available energy for legal MULE casts on visible mineral fields within actual cast range. Verify the action result and observed MULE; energy availability alone is not proof that the cast is legal or productive.

Keep SCV production active while the economy is growing. The long-term allocation may shift toward more SCVs on production/gas and MULEs doing more of the mineral harvesting, but this is a measured transition, not an assumed replacement for workers. A flying Orbital is a valuable mobile asset: lift only when the base can safely lose its grounded production/defense role, use a safe route, land and verify before casting or resuming production. MULEs may also be used to repair a damaged retreating Orbital where the game rules and available energy permit; validate repair target/range/ability rather than assuming a MULE can be retasked.

## Unified resource and supply scheduler

At every observation, compute a bounded plan from current facts:

- current minerals and vespene;
- mineral/gas income rates measured from delivered cargo, not theoretical saturation alone;
- current and projected supply used/cap, including units in production;
- SCV production and idle Command Centers;
- completed/in-progress Depots, Barracks, Refineries and town halls;
- production queue uptime and expected completion times;
- available construction crews and their arrival estimates;
- expansion sites and enemy exposure;
- Marine output target and current force composition.

The scheduler should maintain explicit reservations for critical near-term actions, but release reservations when their prerequisites, affordability, or expected value cease to hold. It should choose among the next SCV, Depot, Refinery, Barracks, Command Center, Orbital upgrade and Marine by marginal expected value and deadline risk. Avoid hard-coded serial gates when independent construction can safely overlap.

The target is sustained Marine throughput, not merely maximum Barracks count. Additional production is useful only when mineral/gas income, supply, worker production and build capacity can keep it active.

## Cohesive Marine movement and engagement

### Formation is a movement controller

A formation is not a collection of unrelated Move commands. It is a shared movement objective with a stable anchor, orientation, member slots and arrival tolerances.

- Assign each Marine a deterministic slot relative to the formation anchor.
- Move the anchor along a shared route; recompute member destinations from the anchor and current orientation as it advances.
- Issue individual slot corrections at a bounded cadence, not continuously on every frame.
- Track each Marine's observed position and slot error. A command being accepted is not arrival.
- If a Marine is delayed, blocked, damaged or temporarily absent, the formation slows, reforms or explicitly leaves that member behind according to policy; it must not silently stretch into a long single-file string.
- New Marines join at a safe rendezvous and are assigned stable slots without reshuffling the existing squad unnecessarily.

### One movement owner

The formation controller owns all non-engagement movement while a squad is assembling, travelling or regrouping. The tactical combat translator must not simultaneously issue independent approach, orbit or retreat Move orders to those same Marines.

Combat may temporarily take control only through an explicit engagement transition. After the engagement, Marines return to a regroup/formation state and re-establish slot cohesion before resuming the route. Focus-fire attack commands may coexist with formation only when they do not invalidate the squad's movement/retreat policy.

### Engage by expected exchange, not proximity

Do not interpret seeing an enemy as permission to advance. Evaluate the expected exchange for the whole local force:

- friendly effective DPS after target armor and weapon compatibility;
- enemy effective DPS after friendly armor and targetability;
- health/shields, range, cooldown, attack cadence and available reinforcement arrival;
- mineral and vespene replacement value of every unit;
- position, retreat path, objective value and enemy reinforcement risk.

Weight vespene losses more heavily than mineral losses. The initial policy should expose the vespene multiplier as a named, testable strategy parameter (the proposed experiment is 10x), not bury it in a magic constant. Compare expected value lost on both sides and require a positive safety margin before committing to an engagement. If the force is not favorable, hold vision, maintain safe spacing, retreat cohesively or wait for reinforcements. A scout at the bottom of the enemy ramp is an information asset, not the first member of an automatic attack wave.

## Evidence and acceptance gates

The next implementation increments should prove, in order:

1. First Depot candidates are derived from the observed mineral line and SC2 accepts a site behind it.
2. The selected builder is the intended cargo-bearing SCV; it builds and is released/reassigned after completion.
3. A second construction worker can be dispatched early without starving SCV or Marine production.
4. A Refinery starts, reaches completion, and SCVs are assigned/released dynamically between minerals and gas.
5. Orbital upgrade begins as soon as the Barracks and resource gates permit; MULE casts are legal and observed.
6. Supply planning avoids blocks while accounting for added town-hall supply; Depot cadence responds to projected demand rather than a fixed count.
7. Marine production remains active across all affordable completed Barracks.
8. A travelling formation preserves bounded slot error, joins reinforcements, and does not get overridden by individual combat movement.
9. Engagement decisions use effective DPS, armor and resource replacement value, including the configurable vespene-loss multiplier.
10. Repeat local matches and compare mineral/gas delivery, supply blocks, SCV/Marine uptime, structure timings, force cohesion, damage dealt/received and resource-weighted exchange.

Tests and CI validate code paths. Only local SC2 observations/replays can establish that workers arrive, structures complete, MULEs spawn, formations remain cohesive and the strategy actually wins.
