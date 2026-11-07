# BotDefinition format

## Purpose

A `BotDefinition` is the complete, versioned, serializable artifact a player authors and saves. It is independent of the GUI and contains no executable snippets. The GUI edits this artifact; it is not the source of truth.

The first proposal is split into two schemas:

- [BotDefinition schema](../schemas/bot-definition.schema.json) owns identity, game/adapter/race, the nested strategy, and unit-profile bindings with explicit overrides.
- [Strategy payload schema](../schemas/strategy-payload.schema.json) owns opening, goals, constraints, tactics, scouting, composition, and evaluation.

Runtime controls, active observations, decisions, and telemetry are session state. They are not persisted in BotDefinition.

## Ownership and defaults

- `metadata.id` and all plan/intent IDs are stable authoring identifiers.
- `game` identifies the game, adapter, and player race.
- `strategy` is declarative strategy intent. It contains no SC2 protocol messages or executable code.
- `unitProfiles` binds a canonical unit ID and role to a stable profile ID and catalog version.
- Profile defaults are resolved from the referenced versioned catalog. The saved document contains only explicit user overrides, not a copied snapshot of defaults.
- The effective resolved profile should be inspectable by tooling, but it is derived data rather than authored configuration.

The Marine fixture demonstrates one explicit boolean override. The profile catalog and runtime binding are not claimed to exist merely because the fixture references their proposed stable identifiers; resolution must fail with a diagnostic if the selected catalog version or profile is unavailable.

## Migration from Strategy schema 0.1

The existing `schemas/strategy.schema.json` and `strategies/terran-opening.example.json` remain valid legacy documents. Do not reinterpret or rewrite them in place.

Migration to BotDefinition 1.0 is explicit:

| Legacy Strategy 0.1 field | BotDefinition 1.0 destination |
|---|---|
| `schemaVersion: "0.1"` | Set envelope `schemaVersion: "1.0"`; set nested strategy `schemaVersion: "1.0"` after validating the payload migration |
| `metadata` | Move unchanged to envelope `metadata` |
| `race` | Move to `game.race`; set `game.id = "starcraft-ii"` and `game.adapter = "sc2"` |
| `opening`, `goals`, `constraints`, `tactics`, `scouting`, `composition`, `evaluation` | Move under envelope `strategy` without changing their meaning |
| No profile bindings | Add explicit `unitProfiles` bindings only when the author chooses or accepts them; do not silently infer user overrides |

Migration must preserve stable IDs and values. It must report unsupported or unresolvable ontology/profile references rather than dropping them. The legacy document remains available as the source for review and rollback.

## Authoring pipeline

1. Accept plain-language intent or direct GUI edits.
2. Convert suggestions into a typed BotDefinition proposal.
3. Validate JSON Schema, supported catalog versions, ontology references, and semantic constraints.
4. Show a structured diff, including default-versus-override distinctions.
5. Require explicit user approval before saving or applying the proposal.
6. Load the saved definition through the runtime's configuration boundary.

The LLM proposes and explains; it is never queried at runtime to decide unit actions.

## Marine authoring preview

Open [the static authoring preview](../tools/authoring-preview/index.html) to inspect the fixture and toggle its one example override. This is a UI/serialization concept preview only: it does not validate against JSON Schema, resolve a real profile catalog, load the runner, or control a live match.

## Contract still required from runtime

A later runtime-owned, read-only snapshot should expose loaded definition and catalog versions, active plan/phase, role assignments, normalized observations with visibility/confidence/last-seen semantics, and—when the action pipeline exists—candidate/accepted/rejected intents with stable reason codes and outcomes. The GUI must not infer decisions from raw SC2 protocol observations.

Runner start/stop/pause/resume/step controls remain out of scope until command/acknowledgement contracts and an end-to-end local match test exist.
