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

The initial trusted runtime catalogue now registers all three playable races. Terran is explicitly marked as the existing legacy gameplay path while its controllers are migrated behind the module boundary. Protoss and Zerg are registered as logging-only scaffolds with distinct module IDs and race-specific capability namespaces (units, structures, production, abilities). Match initialization logs the authoritative actual race, selected module, execution mode, and declared capabilities. Their current frame path logs a single fail-closed diagnostic and emits no gameplay actions; this is intentional scaffolding, not a claim of playable race support. Random/unknown race has no module and also fails closed. Terran-only scouting and rally reset calls are now gated by the actual Terran selection.

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
## AnyApp initialization and local MicroBundle composition

The SC2 Editor Experience is an AnyApp experience, not a separate web application. AnyApp should assemble the experience's MicroBundle locally during initialization from the selected experience manifest, trusted registered components, race module catalogue, command-layer descriptors, and presentation resources. The runtime must not depend on fetching a MicroBundle from a web repository. The bundle is a short-lived, locally composed runtime artifact whose inputs and selected versions are explicit and diagnosable.

Initialization has two distinct stages:

1. **Experience composition:** resolve the local experience definition and its trusted component registrations, validate dependencies and capabilities, then construct the MicroBundle in memory. Missing or incompatible components fail initialization with a useful diagnostic; no remote fallback silently changes the experience.
2. **Match composition:** the race-neutral bot shell is created first. At match start, the authoritative race selects the matching race module, which contributes race-specific capabilities and implementations to the already established runtime boundary. The editor's selected doctrine and presentation profile determine the authored composition and visible controls; the presentation profile never changes behavior.

This keeps the AnyApp experience portable while ensuring the runtime bundle is assembled from the components actually available to that installation. A saved bot definition remains declarative data; it is not itself executable code and does not become a remote dependency manifest.

## FSM_API as the shared lifecycle and interoperability substrate

FSM_API is already a direct dependency and is actively used by `UnitBehaviorRuntime`: persistent entity contexts implement `IStateContext`, own their `FSMHandle` instances, attach capability FSMs, and are advanced through named processing groups. That is the right pattern to extend into the command hierarchy.

Use FSM_API for durable domain contexts and lifecycle-driven behavior where explicit states and transitions add value:
- Squad lifecycle and readiness (forming, assembling, assigned, executing, regrouping, completed/disbanding).
- Command-layer lifecycle and coordination states.
- Phase and operation state machines with evidence-backed transitions.
- Unit and structure capability behavior, continuing the existing context-owned handle model.
- Shared processing cohorts so update frequency and scheduling remain explicit.

Keep observations, immutable definitions, geometry, schemas, and simple calculations as ordinary data/services. Do not turn every property or stateless transformation into an FSM. A context owns its handles and destroys them when its lifecycle ends; temporary fog-of-war or missing observation is not destruction. Command-layer FSMs may express lifecycle and coordination, but unit ownership remains with the single authoritative reservation planner. FSM transitions do not grant authority to issue SC2 actions: accepted intent still passes through reservation, current-observation, and action-legality checks.

This gives the bot a common state/lifecycle vocabulary that can interoperate with the wider Workshop ecosystem without forcing race-specific behavior into the shared shell.
