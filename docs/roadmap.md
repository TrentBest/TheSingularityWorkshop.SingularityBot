# Roadmap

The shortest path to a bot that actually plays a local StarCraft II match is the priority. Do not let editor polish, generalized strategy tooling, neural policy, ladder operations or additional races displace the first playable Terran economy.

## 1. Observation-to-action foundation

- [x] Integrate deterministic observation normalization into `development`.
- [x] Reconcile persistent own-unit FSM contexts; distinguish fog/absence from authoritative death.
- [x] Replace contexts when a unit morph changes its supported behavior profile.
- [x] Buffer FSM intents per observation and add initial gather/return-cargo translation.
- [x] Add regression tests for normalization, context lifecycle, morphs and initial worker action translation.
- [x] Wire the test project into the GitHub Actions build workflow.
- [ ] Obtain a current green CI run for the integrated `development` head; fix every failure.
- [ ] Validate gather/return ability semantics and action results in a local SC2 match/replay.
- [ ] Add per-unit available-ability checks and explicit rejection/result diagnostics.

## 2. Make workers productive

- [ ] Normalize the economy facts needed by the scheduler: minerals/gas, supply, worker orders, cargo, mineral fields, town halls and construction state.
- [ ] Assign each starting SCV to a persistent worker-to-mineral route.
- [ ] Cache outbound/return geometry and invalidate only when route inputs change.
- [ ] Use game-loop deltas for cheap phase reevaluation; confirm every transition from observations.
- [ ] Coordinate node reservations and saturation centrally; prevent duplicate/conflicting assignments.
- [ ] Select the closest eligible non-mining SCV for the first Depot builder.
- [ ] Measure delivered minerals, worker idle/travel/queue/gather/return time and assignment churn.

## 3. Complete the first playable Terran opening

- [ ] Queue Command Center SCV production and assign starting workers in the intended order.
- [ ] Validate placement, prerequisites, affordability and builder selection for the first Supply Depot.
- [ ] Replace the single-Depot builder with a persistent construction sequence: 80-mineral return-event trigger, raised Depot, then Barracks using the same SCV.
- [ ] Detect the actual ramp and evaluate legal Depot/Barracks placement pairs as a wall; validate ground-unit passage in a live match.
- [ ] Keep Command Center SCV production running while construction is scheduled against a resource budget.
- [ ] Establish four Barracks as the first production milestone, with supply-depot forecasting and no avoidable supply blocks.
- [ ] Train pure Marines from every completed Barracks while protecting SCV production and expansion reserves.
- [ ] Generalize the production scheduler beyond four Barracks toward a configurable eight-Barracks target; reference add-on layout is three Tech Labs and five Reactors.
- [ ] Model add-on construction/attachment, Reactor dual-queue throughput, Tech Lab unit eligibility, production reservations and technology prerequisites from authoritative game data.
- [ ] Preserve Marine dominance while introducing economically justified Marauder, Reaper and later Ghost support.
- [ ] Add Starport / Medivac production, healing groups, confirmed passenger loading, threat-aware drops, unload, extraction and return.
- [ ] Compare four-Barracks and eight-Barracks profiles with repeatable local-match metrics; do not treat the eight-Barracks target as a mandatory blind build order.
- [ ] Prove the opening repeatedly in local matches from controlled starting conditions and retain game-loop telemetry.
- [ ] Prove first-Marine scouting to a map-derived safe observation point at the bottom of the enemy ramp, maintaining vision of the ramp entrance and enemy Town Hall without ascending the ramp.

## 4. Expand and sustain the Terran economy

- [ ] Derive candidate expansion landing sites from map pathing/buildability grids and mineral/geyser clusters, not fixed offsets from the main Command Center.
- [ ] Score sites for resource value, worker travel, approach connectivity, landing clearance, and enemy exposure; query SC2 for final placement legality.
- [ ] Build Command Centers in the protected main, load a configurable SCV crew, lift, fly, land, unload, and verify the landed structure before transferring workers.
- [ ] Add an Orbital fleet manager with stable role assignments: protected home anchors, expansion anchors, mobile MULE tenders and bounded scouts / reserves.
- [ ] Preserve a configurable minimum of grounded town halls and SCV production at active bases before authorizing takeoff; bound concurrent airborne Orbitals.
- [ ] Route mobile Orbitals to safe operating positions near mineral lines, verify landing, then call MULEs only on visible mineral fields within authoritative cast range; verify result and observed MULE before moving on.
- [ ] Add hysteresis, safe route / landing scoring, energy opportunity-cost policy and fleet telemetry to prevent wasteful lift/land oscillation.
- [ ] Prove emergency recovery: after catastrophic SCV losses, route flying Command Centers / Orbitals to safe friendly landing sites, land and verify them, then restart SCV production before resuming mobile MULE operations.
- [ ] Evaluate a large Orbital fleet (eventually dozens) as a late-game experiment against its mineral / upgrade cost, flight risk, landing space and measured MULE return; do not assume the maximum fleet is always optimal.
- [ ] Keep a minimum home defense and production reserve while assigning the expansion crew; recover safely if a landing query or action is rejected.
- [ ] Add expansion saturation, transfer, and return-trip telemetry; compare candidate-site scores with actual mining throughput.

## 5. Marine force control and combat micro

- [ ] Normalize enemy weapon/range/cooldown, unit movement, health, shields, weapon target filters, and friendly reinforcement facts from game data and observations.
- [ ] Build local threat fields from visible enemies and conservative remembered contacts; distinguish armed threats from harmless blockers and structures.
- [ ] Advance Marines toward a shared attack axis while preserving a reinforcement corridor and avoiding unsupported solo advances.
- [ ] Implement focus fire with overkill control, target priority, range checks, and cooldown-aware attack/move decisions.
- [ ] Implement stutter-step kiting: fire when weapon cooldown permits, move during cooldown, and choose retreat vectors that increase distance from danger while reducing distance to friendly reinforcements.
- [ ] Use hysteresis and health/force-balance thresholds to avoid oscillation; regroup before re-engaging when local friendly strength improves.
- [ ] Evaluate micro against baseline policies using damage dealt/received, Marine survival, retreat path quality, reinforcement convergence, and objective progress.

## 6. Make behavior measurable and reproducible

## 7. Make behavior measurable and reproducible

- [ ] Record normalized observations, assignments, intents, arbitration, validation, submissions and outcomes by game loop.
- [ ] Replay the same observation sequence deterministically.
- [ ] Report invalid actions, idle time, delivered resources, structure timing, production uptime and Marine output.
- [ ] Add a regression fixture for every discovered failure.

## 8. Add supporting surfaces after the runtime works

- [ ] Expose the same versioned runtime contracts to GUI for authoring and inspection.
- [ ] Add spatial overlays and route timelines where they help diagnose measured behavior.
- [ ] Use CiAi/HeadlessAi for repeatable build, test and replay evaluation.
- [ ] Consider neural policies only as optional advisors against a deterministic baseline.
- [ ] Generalize to Protoss/Zerg and tactical play only after the Terran opening is reliable.

## Definition of done

A feature is done only when its contract, automated tests, runtime behavior and evidence agree. Compilation alone is not proof of in-game behavior. CI wiring is not a green CI result; a unit-test pass is not a live-match result. Do not claim competitive strength without repeatable match evidence.

## Sith branch policy

Exactly two branches are permitted:

- `development` — the only active integration, implementation and test branch.
- `master` — the stable, release-aligned state.

Do not create feature branches or parallel long-lived work branches. Integrate directly into `development` in small, reviewable commits. Keep `master` at the last approved release state; update it only at an explicitly authorized release point. NuGet publishing and external deployment require separate explicit authorization.
