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
- [ ] Build the first Barracks and enforce supply/resource gates.
- [ ] Establish four Barracks.
- [ ] Train pure Marines without a gas dependency.
- [ ] Preserve worker mining and production continuity.
- [ ] Prove the opening repeatedly in local matches from controlled starting conditions.

## 4. Make behavior measurable and reproducible

- [ ] Record normalized observations, assignments, intents, arbitration, validation, submissions and outcomes by game loop.
- [ ] Replay the same observation sequence deterministically.
- [ ] Report invalid actions, idle time, delivered resources, structure timing, production uptime and Marine output.
- [ ] Add a regression fixture for every discovered failure.

## 5. Add supporting surfaces after the runtime works

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
