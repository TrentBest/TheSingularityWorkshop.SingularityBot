# Strategy and opening configuration

## Purpose

Strategy is versioned data consumed by the bot runtime. It describes the selected opening and its parameters; it is not executable code, a serialized C# object graph, or a second decision engine.

The immediate consumer is the first playable Terran proof: a no-gas, four-Barracks, pure-Marine opening. Do not broaden the schema for hypothetical races, tactics or editor features until a real runtime capability needs the field.

## Current status

The repository contains a JSON Schema and example strategy document, and CI validates those files. The complete strategy compiler/runtime binding is not yet implemented. Schema validation must not be mistaken for proof that every field is consumed by the bot.

Until the runtime consumes a field, treat it as documented configuration intent only. Do not silently imply that an example build order is executable or optimal.

## Document rules

- JSON, UTF-8, with an explicit schema version.
- Stable textual identifiers for review and authoring; runtime code may map them to compact identifiers.
- Game facts such as costs, prerequisites, abilities and build times come from current SC2 game data/ontology, not guessed values embedded in a strategy.
- Time values must identify their units; authoritative execution time is measured in SC2 game loops.
- Unknown or unsupported directives fail validation with a useful path and reason.
- Strategy data never contains raw protocol requests, arbitrary executable code or permission to bypass action validation.

## First configuration boundary

The first opening configuration should express only what the working runtime can validate and execute:

- race and opening identity;
- ordered goals and hard constraints;
- SCV production and mineral assignment intent;
- Supply Depot and Barracks milestones;
- four-Barracks production target;
- pure-Marine production intent and no-gas constraint;
- resource/supply thresholds only when their runtime semantics are defined.

Do not encode assumed optimal timings as facts. The scheduler must respond to current resources, supply, prerequisites, production queues and observations.

## Compilation and validation direction

When the compiler is implemented, it should:

1. Parse and validate the versioned document.
2. Resolve referenced game data against the active SC2 data catalog.
3. Reject unsupported directives and impossible prerequisites.
4. Compile accepted goals/constraints into FSM definitions and runtime configuration.
5. Bind the definitions to the same observation, economy and action-validation services used by the headless runtime.
6. Produce diagnostics explaining accepted, rejected and unresolved configuration.

The compiler does not submit protocol actions. FSM behavior emits intents; the action pipeline remains the only route to SC2 commands.

## Future expansion

Add generalized race, scouting, composition or tactical fields only when they have a concrete implementation, tests and observable runtime effect. GUI should edit and inspect this same contract; it must not invent a parallel schema or execution model.
