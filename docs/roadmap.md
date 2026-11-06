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

## Phase 2 — First local economy

The first playable milestone is a complete opening economy, not ladder play or combat tactics.

- [ ] Normalize observations and game data for workers, mineral fields, town halls, resources, supply, orders and construction.
- [ ] Add deterministic worker-to-mineral assignment with reservations.
- [ ] Measure worker travel, queue/wait, gather, return, unload and idle time.
- [ ] Compare baseline, saturation-aware and arrival-window resource scheduling from equivalent starting conditions.
- [ ] Implement race-specific supply planning: Supply Depot, Pylon and Overlord.
- [ ] Implement race-specific production prerequisites: Barracks, Gateway and Spawning Pool.
- [ ] Train the first tier-one unit: Marine, Zealot and Zergling.
- [ ] Add local-match replay fixtures, metrics and invalid-action diagnostics.
- [ ] Verify the opening remains economically healthy after the first unit.

See [Opening economy and resource logistics](opening-economy-logistics.md) for the experiment contract and metrics.

## Phase 3 — FSM runtime and action safety

- [ ] Introduce match/player/unit lifecycles through FSM_API.
- [ ] Add stable unit identity and visibility/death reconciliation.
- [ ] Implement intent arbitration, worker reservations and command validation.
- [ ] Keep SC2 protocol details behind the adapter boundary.
- [ ] Ensure deterministic decisions from identical normalized observations and strategy versions.

## Phase 4 — Strategy as data

- [ ] Implement schema validation and ontology resolution.
- [ ] Compile the opening economy into versioned FSM definitions.
- [ ] Add goal/priority/constraint evaluation.
- [ ] Add scouting, composition and tactical triggers incrementally.

## Phase 5 — Local play, editor and learning

- [ ] Prove local bot-vs-bot/self-play using the same session and action contracts.
- [ ] Investigate and prove a private local human-vs-bot session; keep it separate from ladder mode.
- [ ] Rebuild editor against the versioned strategy contract.
- [ ] Add map overlays, worker logistics visualization and build-order timeline.
- [ ] Persist match/replay experience through DataWarehouse.
- [ ] Add replay regression and strategy evaluation through CiAi/HeadlessAi.
- [ ] Evaluate a separate FPS adapter only after selecting a game with an official local mod/plugin/bot API.
- [ ] Evaluate NeuralNetworks as an optional policy/evaluation advisor against a deterministic baseline.

## Release gates

A milestone is not complete because the solution compiles alone. Each gate must name its tests and evidence. No NuGet publishing or external ladder deployment occurs without explicit authorization.
