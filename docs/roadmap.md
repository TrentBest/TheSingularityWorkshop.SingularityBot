# Roadmap

## Phase 0 — Repository foundation

- [x] Establish project purpose, architecture and initial strategy contract.
- [x] Establish Pages documentation site.
- [ ] Add permanent branch policy and live Sith badge.
- [ ] Confirm the intended .NET target matrix and CI environment.

## Phase 1 — Recover the game adapter

- [ ] Build a file-level source map from SimonPrins/ExampleBot and FSM_Bot.
- [ ] Identify the minimum protocol/client surface needed for a working match.
- [ ] Preserve applicable licenses and protocol notices.
- [ ] Remove IDE state, generated output and stale project references.
- [ ] Establish a supported, reproducible build.

## Phase 2 — Make one match work

- [ ] Launch locally against Blizzard AI.
- [ ] Normalize observations and game data.
- [ ] Submit a minimal legal action.
- [ ] Add a headless smoke test and structured diagnostics.
- [ ] Verify ladder packaging separately from local execution.

## Phase 3 — FSM runtime

- [ ] Introduce match/player/unit lifecycles through FSM_API.
- [ ] Add stable unit identity and visibility/death reconciliation.
- [ ] Implement command validation and arbitration.
- [ ] Keep SC2 protocol details behind the adapter boundary.

## Phase 4 — Strategy as data

- [ ] Implement schema validation and ontology resolution.
- [ ] Compile a minimal Terran opening into FSM definitions.
- [ ] Add goal/priority/constraint evaluation.
- [ ] Add scouting, composition and tactical triggers incrementally.

## Phase 5 — Editor and learning

- [ ] Rebuild editor against the versioned strategy contract.
- [ ] Add map overlays and build-order visualization.
- [ ] Persist match/replay experience through DataWarehouse.
- [ ] Add replay regression and strategy evaluation through CiAi/HeadlessAi.

## Release gates

A milestone is not complete because the solution compiles alone. Each gate must name its tests and evidence. No NuGet publishing or external ladder deployment occurs without explicit authorization.
