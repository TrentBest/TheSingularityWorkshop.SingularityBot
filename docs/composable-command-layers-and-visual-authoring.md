# Composable command layers and visual bot authoring

## Purpose

SingularityBot should be a composition platform for bot creators, not a single Terran personality with a collection of tuning switches. The editor should let a developer select a race module, then compose the expert command layers that translate strategic intent into concrete lower-level behavior.

The goal is to make the middle of the bot replaceable and inspectable. The strategic top decides what matters; the unit-level bottom knows how to execute local behavior. The command layers between them coordinate forces, translate intent, and apply doctrine. Those middle layers are where a developer should be able to create a distinctive bot identity without rebuilding the protocol, observation, pathing, legality, or unit-execution foundations.

## Two independent axes: doctrine and presentation

Every selected command-layer implementation has two independent identities:

- **Implementation / doctrine** selects behavior: its decisions, policies, coordination rules, accepted intent contracts, emitted lower-level intents, and capability requirements.
- **Presentation profile** selects how that layer is represented in the editor and diagnostics: names, terminology, icons, colors, descriptions, and grouping.

A developer selecting a Terran tab might choose a conventional command doctrine, black-ops doctrine, expeditionary sci-fi doctrine, or a custom registered implementation. The visual profile can use matching language and art, but it is not the behavior itself. A skin change must not alter behavior; a doctrine change must not silently change the visual profile unless the author explicitly chooses that.

Examples are inspiration and authoring vocabularies, not claims that the runtime contains those doctrines already.

## Command layers are expert translators

A layer is not merely a rank in a military tree. It is an expert with a bounded responsibility:

1. It receives typed intent and evidence from above.
2. It decides what its domain can contribute.
3. It translates that decision into typed intent for the next layer.
4. It reports progress, constraints, and outcomes upward.
5. It does not bypass the shared reservation, legality, and action-validation boundaries.

Each layer should declare:

- Stable slot identity and selected implementation identity.
- What intent contracts it accepts.
- What intent contracts it emits.
- Capabilities it requires and provides.
- Configuration/settings schema and version.
- Lifecycle and state ownership.
- Evidence freshness and confidence requirements.
- Resource, unit, and authority boundaries.
- Failure, timeout, cancellation, and recovery behavior.
- Diagnostics explaining why it acted or declined to act.

This makes a layer composable because its neighbors depend on contracts, not its concrete class or visual theme.

## Initial command hierarchy

| Layer | Responsibility | Example translation |
|---|---|---|
| Strategic | Match-wide priorities, phase plans, desired outcomes | “Deny the opponent's third base” |
| Army | Force readiness, force allocation, defense coverage, operation selection | “Commit a mobile strike group while retaining home defense” |
| Task Force | Multi-region or mixed-capability coordination | “Coordinate air raid with ground diversion” |
| Platoon | Local coordination of multiple squads | “Synchronize two attack axes” |
| Squad | Cohesive tactical assignment and formation membership | “Raid this mineral line and preserve escape capacity” |
| Unit | Individual execution, local target choice, abilities, survival | “Acquire target, use ability when legal, disengage” |

Not every bot needs every layer. The default simple composition can be Strategic -> Army -> Squad -> Unit. Task Force and Platoon are optional coordination capabilities, not mandatory empty ranks. A composition may contain **at most five enabled command layers**. This is a deliberate usability and domain-complexity limit: SC2 has a bounded unit population, and adding another tier should require a real coordination benefit. Disabled optional slots do not count toward the limit. A developer can define domain-specific implementations within the five-layer budget, provided the contract and authority model remain valid.

The hierarchy describes responsibility, not necessarily a literal organizational chart or one-to-one parent-child tree. A squad can be referenced by an operation and a task force without acquiring duplicate ownership. Unit assignment remains singular and authoritative through the shared reservation planner.

## Race modules and race-specific authoring

The shared shell owns race-neutral services: observations, normalized evidence, map geometry, spatial intelligence, phase evaluation, shared planners, diagnostics, and the action legality boundary.

At match start, the selected race module binds race-specific capabilities: unit and structure ontology, production semantics, abilities, construction rules, and race-specific command-layer implementations. The editor can present a race tab as the authoring context for those registered capabilities.

A race module should publish a capability catalogue, not dictate one mandatory doctrine. A Terran developer may compose different command doctrines from the same Terran production and action capabilities. The same principle applies to Protoss and Zerg. Race selection also changes the authoring vocabulary and available structural templates. For example, a Zerg Overmind/Cerebrate-inspired preset can model Cerebrates as the top-level editable authorities and deliberately omit additional command tiers. Another Zerg doctrine may expose more intermediate layers. These are selectable command models, not claims that every lore structure exists as a playable SC2 unit or runtime controller.

Unsupported race, missing module, missing implementation, incompatible contract, or unavailable required capability must fail closed. Never silently substitute Terran behavior.

## The visual editor as a composition workbench

The editor should visualize the command stack as connected layers, not a flat list of opaque strategy names.

A layer card should show:
- Layer role and selected doctrine.
- Presentation profile, independently editable.
- Inputs accepted and outputs emitted.
- Required and provided capabilities.
- Current configuration and schema version.
- Whether the implementation is available for the selected race.
- Validation state and exact incompatibility explanation.
- Runtime diagnostics: current intent, last decision, evidence used, and why it declined or deferred.

The authoring flow should support:
- Select race module.
- Start from curated, working example compositions (the fastest way to learn is to inspect, clone, compare, and improve another creator's work).
- Add, remove, reorder, enable, or disable optional layer slots.
- Choose from registered implementations for each slot.
- Select presentation independently.
- Inspect and connect intent contracts.
- Configure layer settings with schema-aware controls.
- Generate the visible controls from the selected implementation's settings schema and capabilities; hide or disable controls that the selected doctrine does not support.
- Validate the whole composition before launch.
- Save, clone, export, import, and compare bot definitions.
- Preview the doctrine as a readable chain of responsibility.

A future node/graph view can show non-linear coordination, but the initial authoring model should remain ordered and understandable. Do not build a visual graph editor before the contracts and runtime semantics are stable.

## Trust and extension model

Bot definitions are declarative data. An implementation ID resolves through a trusted compiled registry or a separately designed, explicitly secured extension mechanism. JSON must never name an arbitrary assembly, type, method, script, or dynamic code payload.

Validation occurs at multiple boundaries:
1. Static schema and identifier validation.
2. Implementation registration and race compatibility.
3. Capability requirement resolution.
4. Adjacent intent-contract compatibility.
5. Runtime resource/unit authority and reservation checks.
6. Final SC2 command legality against current authoritative observations.

A layer may propose an intent; it cannot manufacture permission. Lower layers may reject unsupported or unsafe requests and report the reason upward. Strategic urgency does not bypass action validation.

## Existing code and migration path

The repository already has useful foundations:
- `BotDefinition` is the portable authoring artifact.
- `ICompiledBotStrategy` is a compiled personality boundary.
- `BotCommandVocabulary` provides stable semantic action identities.
- `AdvisorJobBoard` and `StrategicPlanner` separate domain intent from shared arbitration.
- `MilitaryPlanDefinition` and `MilitaryPlanRuntime` represent named operations and observed lifecycle.
- `UnitBehaviorRuntime` owns persistent unit behavior contexts.
- `TerranCommander` is an early example of grouping domain specialists under a subordinate command layer.

There is also a major migration constraint: the current `BotDefinitionValidator` only supports Terran, the default compiled strategy is Marine-specific, and `SingularityBot` eagerly owns many Terran controllers. The new composition contract must not be mistaken for completed three-race runtime composition. The race-neutral shell / race-specific module boundary still needs to be implemented.

The first contract implementation lives in `Behavior/CommandLayerComposition.cs`. It defines ordered layer selections, independent doctrine and presentation IDs, accepted/emitted intent contracts, capability declarations, sparse hierarchy support, a five-enabled-layer ceiling, and deterministic validation. `Behavior/CommandLayerRegistry.cs` adds a trusted compiled descriptor catalogue and fail-closed resolution by implementation ID, layer kind, race, and required capabilities. Portable definitions still cannot load or execute arbitrary implementations.

## Implementation sequence

1. **Composition contract** — stable layer slots, implementation IDs, presentation IDs, intent contracts, capability declarations, five-layer ceiling, and validation. (Implemented with tests.)
2. **Registry and descriptors** — trusted compiled implementation catalogue with race compatibility and capability declarations. (Initial resolver and tests implemented; settings-schema metadata and runtime factories remain.)
3. **Runtime adapter boundary** — each layer receives a narrow context and emits typed intents; no layer bypasses shared planners or action validation.
4. **Squad vertical slice** — first real formation layer, stable identity, membership lifecycle, role/objective, and single-owner unit reservations.
5. **Army Commander extraction** — translate accepted military intent into named operations and subordinate assignments.
6. **Terran migration** — preserve current behavior while moving Terran-only controllers behind the Terran module.
7. **Race capability catalogues** — add Protoss and Zerg without polluting the shared shell.
8. **Editor visualization** — render the actual composition model, expose contract/capability validation, and allow doctrine/presentation selection.
9. **Diagnostics and replay** — explain decisions from evidence and contracts; compare doctrines under the same observations.
10. **Measured extensibility** — add optional Task Force/Platoon layers only where tests and gameplay demonstrate coordination value.

## Success criteria

A developer can change an Army or Squad doctrine without rewriting Unit AI, the SC2 protocol, or the action validator. A presentation change does not change decisions. Incompatible compositions are rejected before match startup with actionable diagnostics. Runtime decisions can be traced through accepted intent, translation, reservation, execution, and observed outcome. The same shared shell supports Terran, Protoss, and Zerg, while each race module supplies its own concrete capabilities.

The ambition is a bot workbench other developers can build on—not a hard-coded personality that happens to expose a GUI.
