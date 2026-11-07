# Roadmap

This roadmap is ordered by the shortest path to a bot that plays a real game. Do not expand into generalized strategy tooling, neural policy, ladder operations or additional races until the Terran opening works and is measurable.

## 1. Stabilize the vertical slice

- [ ] Integrate observation normalization and reconciliation into the sole `development` branch.
- [ ] Build and run all tests in CI; fix failures before adding more behavior.
- [ ] Verify persistent-context behavior across repeated observations, fog, death and unit morphs.
- [ ] Verify gather and return-cargo actions against the SC2 protocol and a local match.
- [ ] Record every rejected action with a reason.

## 2. Make workers productive

- [ ] Normalize mineral fields, town halls, worker orders, cargo, positions and game-loop facts.
- [ ] Assign each starting SCV to a persistent worker-to-mineral route.
- [ ] Cache route positions/geometry and update only when relevant facts invalidate the cache.
- [ ] Use game-loop deltas for cheap reevaluation deadlines; confirm transitions from observations.
- [ ] Select the closest eligible SCV for the first Depot without needlessly destroying a productive mining route.
- [ ] Measure delivered minerals, worker idle/travel/gather/return time and assignment churn.

## 3. Complete the Terran opening

- [ ] Validate and place the first Supply Depot.
- [ ] Build the first Barracks and enforce supply/prerequisite/resource gates.
- [ ] Establish four Barracks.
- [ ] Train pure Marines with no gas dependency.
- [ ] Preserve mining and production continuity.
- [ ] Prove the opening in repeatable local matches from the same starting conditions.

## 4. Make it explainable and repeatable

- [ ] Save normalized observation and decision traces keyed by game loop.
- [ ] Replay recorded traces deterministically.
- [ ] Report invalid actions, idle time, delivered resources, structure timing, production uptime and Marine output.
- [ ] Add regression fixtures for each discovered failure.
- [ ] Expose the same runtime state and versioned definitions to GUI without duplicating execution logic.

## 5. Expand only from evidence

After the Terran opening is reliable:
- generalize reusable economy capabilities to Protoss and Zerg;
- add scouting, combat and tactical behavior in measurable slices;
- evaluate neural policies as optional advisors, never as unchecked action authorities;
- investigate ladder, self-play and other game adapters only when the local runtime is dependable.

## Definition of done

A feature is done only when its contract, tests, runtime behavior and evidence agree. A successful compile alone is not proof of in-game behavior. No competitive-strength claim without repeatable match evidence.

## Branch policy

The repository has exactly two long-lived branches:
- `development`: all active integration and testing.
- `master`: stable release-aligned state.

Do not create feature branches for routine work. Integrate directly into `development` in small commits. Keep `master` aligned only at an explicitly authorized release point. NuGet publishing and external deployment require separate explicit authorization.
