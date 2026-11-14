# Roadmap

The shortest path to a bot that actually completes a local StarCraft II match remains the priority. Do not let editor polish, generalized strategy tooling, neural policy, ladder operations or additional races displace the first playable Terran economy.

## Evidence status (2026-11-11)

The runtime behavior baseline is `42f943e14fcc2d85fb3b83ec01ae0f5f74729010`; its [Build](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/42977688888) and [Sith Policy](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/42977688947) runs both succeeded, with 166 tests passed, 0 failed and 0 skipped. The subsequent documentation-only revision [`644dc6535936ec5b29b36b1c7bf905e48cb846f9`](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/commit/644dc6535936ec5b29b36b1c7bf905e48cb846f9) also passed its [Build](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/42978689180) and [Sith Policy](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/42978689315) workflows. These are CI facts only. No live SC2 match was run as part of either CI execution.

Evidence levels must remain distinct:

1. **Implemented** — source contains the behavior.
2. **Regression-tested** — automated tests exercise the behavior and its failure boundaries.
3. **CI-verified** — the exact candidate SHA has complete, successful required workflow evidence, observable through [CiAi](ciai-evidence.md).
4. **Match-verified** — a local SC2 match/replay demonstrates observed state transitions, action results, movement and arrival, and the intended outcome.
5. **Release-authorized** — the owner explicitly authorizes promotion/publication/deployment for the exact candidate. CI or model output never supplies this authorization.
## 0. Establish the compiled personality boundary

The compile-time strategy seam now supplies validated `BotDefinition` data, executable initial-goal composition, goal-assignment policy and the Marine advance-to-enemy-start rally decision. `AdaptiveMarineStrategy` is statically linked and selected by the Runner. `SingularityBot` invokes goal assignment and rally decisions through the contract while retaining the shared `GoalActionPlanner`, engine, runtime and action pipeline. Regression tests prove different compiled strategies can select different goal and rally policies without duplicating the engine. Objective/transition composition and most macro/tactical policy remain future work; this is a partial boundary, not completion. SC2 Runner remains the host/frame driver; FSM_COS is startup composition only; FSM_API supplies persistent contexts and hierarchical behavior mechanics. No runtime strategy file or dynamic assembly loading is planned.

- [x] Remove the obsolete runner help claim for loading `--bot-definition=<path>`; the live runner selects its compiled default.
- [x] Correct the architecture index and BotDefinition documentation so current implementation is not described as the completed personality boundary.
- [x] Define `ICompiledBotStrategy` and make the shared bot engine consume it.
- [ ] Move objective selection and strategy-transition evaluation/composition behind the compiled strategy contract, with tests proving distinct policies through the same engine and action pipeline.
- [ ] Connect authoring JSON to a deterministic build/editor generation path that validates and compiles a selected personality before runtime; keep JSON out of the live runtime.
- [x] Add regression coverage proving two compiled strategy implementations select different goal and Marine rally policies through the same engine and retain the same CommandAi action vocabulary.
- [x] Align architecture, index, README and BotDefinition docs with the first implemented contract slice. Keep the refactor open until objectives/transitions and remaining user-specific policy are moved behind the contract.

## 1. Observation-to-action foundation

- [x] Integrate deterministic observation normalization into `development`.
- [x] Reconcile persistent own-unit FSM contexts; distinguish fog/absence from authoritative death.
- [x] Replace contexts when a unit morph changes its supported behavior profile.
- [x] Buffer FSM intents per observation and add initial gather/return-cargo translation.
- [x] Add regression tests for normalization, context lifecycle, morphs and initial worker action translation.
- [x] Wire the test project into the GitHub Actions build workflow.
- [x] Obtain a green CI run for the integrated `development` head (latest recorded run: [Build #696](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/42977688888)).
- [ ] Validate gather/return ability semantics and action results in a local SC2 match/replay.
- [ ] Extend per-unit available-ability checks and verify all rejection/result paths in a local match.

## 2. Make workers productive

- [x] Normalize the economy facts currently consumed by worker assignment and opening construction: resources, supply, worker orders/cargo, mineral fields, town halls and construction state.
- [x] Assign starting SCVs to persistent worker-to-mineral routes.
- [ ] Cache outbound/return geometry and invalidate only when route inputs change.
- [ ] Use game-loop deltas for cheap phase reevaluation; confirm every transition from observations.
- [x] Coordinate node reservations and saturation centrally; prevent duplicate/conflicting assignments.
- [x] Select and reserve an eligible SCV for the first Depot builder, including the returning-worker opening gate.
- [x] Capture per-worker observation frames and structured worker assignment/release events, including assignment generations, to support repeatable economy analysis.
- [ ] Derive delivered-resource, idle/travel/queue/gather/return durations and assignment-churn metrics from controlled local matches; keep inferred worker income distinct from authoritative player-bank changes.

## 3. Complete the first playable Terran opening

- [x] Queue Command Center SCV production and assign starting workers in the intended order.
- [x] Validate placement, prerequisites, affordability and builder selection for the first Supply Depot.
- [x] Implement the first Supply Depot-to-Barracks construction sequence and recycle the builder.
- [ ] Detect the actual ramp and evaluate legal Depot/Barracks placement pairs as a wall; validate ground-unit passage in a live match.
- [x] Keep Command Center SCV production active while construction is scheduled against a resource budget.
- [x] Add a follow-up Supply Depot gate after Barracks two and recycle the first-Barracks builder into that Depot and then Barracks three; this is a fixed opening milestone, not yet full supply forecasting.
- [x] Gate Barracks three on starting a second Command Center at a resource-derived friendly expansion; preserve the accepted expansion coordinate for later Orbital routing.
- [ ] Add Refinery and Engineering Bay construction jobs, gas-aware tech scheduling, and validate the second Command Center/Orbital timing in local matches.
- [ ] Establish four Barracks as the first production milestone with predictive supply-depot scheduling and no avoidable supply blocks; validate the timing in local matches.
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

- [x] Capture match-start protocol inputs, terrain calibration, economy/opening CSV traces and compact SC2 event timelines in local development runs.
- [x] Record frame summaries, build-job snapshots, Marine decisions, submitted actions, action results, unit snapshots and economy snapshots in the diagnostic event stream.
- [ ] Add deterministic replay of normalized observations and decisions.
- [ ] Report invalid actions, idle time, delivered resources, structure timing, production uptime and Marine output in repeatable match summaries.
- [ ] Add a regression fixture for every discovered live-match failure.

## 7. Add supporting surfaces after the runtime works

- [ ] Expose the same versioned runtime contracts to GUI for authoring and inspection.
- [ ] Add spatial overlays and route timelines where they help diagnose measured behavior.
- [ ] Use CiAi/HeadlessAi for repeatable build, test and replay evaluation. CiAi currently provides read-only GitHub Actions evidence observation; automated bot-specific dispatch/replay evaluation is not yet integrated.
- [ ] Consider neural policies only as optional advisors against a deterministic baseline.
- [ ] Generalize to Protoss/Zerg and tactical play only after the Terran opening is reliable.

## Definition of done

A feature is done only when its contract, automated tests, runtime behavior and evidence agree. Compilation alone is not proof of in-game behavior. CI wiring is not a green CI result; a unit-test pass is not a live-match result. Do not claim competitive strength without repeatable match evidence.

## Sith branch policy

Exactly two branches are permitted:

- `development` — the only active integration, implementation and test branch.
- `master` — the stable, release-aligned state.

Do not create feature branches or parallel long-lived work branches. Integrate directly into `development` in small, reviewable commits. Keep `master` at the last approved release state; update it only at an explicitly authorized release point. NuGet publishing and external deployment require separate explicit authorization.
