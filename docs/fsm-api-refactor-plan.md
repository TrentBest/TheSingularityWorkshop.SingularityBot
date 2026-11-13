# FSM_API-First Refactor Plan

## Objective

Make SingularityBot a manifest-configured, FSM-composed StarCraft II bot. FSM_API owns behavior state and transitions; FSM_Foundry is the intended execution owner where its runtime contract supports this. SC2-specific code remains responsible for observation normalization, game-data interpretation, intent validation, and protocol action submission.

The ladder artifact must be self-contained: all runtime code, FSM definitions, catalogs, schemas, and built-in policies required for play are compiled/package-contained. A GUI-authored manifest selects and configures those compiled capabilities. The ladder runtime must not require filesystem or web access.

## Architectural invariants

- One persistent context represents one observed entity identity and contains shared facts, observations, and capability data.
- Multiple independent FSM_API handles may attach to the same context. A building can have common lifecycle plus production, research, threat-response, and other capability FSMs simultaneously.
- FSM definitions are registered once; instances are attached/detached as entity capabilities appear/disappear. Handle ownership and destruction are explicit.
- FSM_API remains the state-machine implementation. Do not introduce a parallel FSM abstraction or manually duplicate FSM state in controllers.
- FSMs emit intents/events; a deterministic coordinator arbitrates conflicting commands and resources; SC2 adapters validate and translate accepted intents into protocol actions.
- A state callback may be null when that state genuinely has no work for that callback. Null placeholders for unfinished behavior are not acceptable.
- Manifest input is data, not executable code. It can select compiled definitions and configure supported parameters/policies, but cannot introduce arbitrary runtime code on the ladder.
- Fail closed on invalid manifest versions, unknown FSM IDs, unsupported parameters, invalid transitions, and unsafe action requests.
- Preserve fog-of-war semantics: lack of observation is not evidence of death.
- Do not regress currently tested behavior while migrating ownership.

## Work items

### 1. Source and API audit
- [ ] Inventory current FSM definitions, contexts, handles, processing groups, update calls, and cleanup paths.
- [ ] Inventory imperative controllers and distinguish behavior decisions from SC2 observation/action adapter responsibilities.
- [ ] Verify exact FSM_API signatures and semantics against source and SimpleDemos, including multiple FSMs on one context.
- [ ] Verify FSM_Foundry availability, package/version, and intended execution lifecycle before assigning it ownership.
- [ ] Record baseline build/test status and current branch head.

### 2. Shared entity model and capability composition
- [ ] Define common unit and building context contracts with shared observed facts and stable identity.
- [ ] Ensure every building receives common lifecycle behavior.
- [ ] Attach optional production, research, town-hall, access, repair/integrity, threat-response, and other FSMs by capability.
- [ ] Ensure units receive common unit behavior plus optional movement, combat, worker, construction, repair, scouting, and survival capabilities.
- [ ] Add composition tests proving multiple independent FSM_API handles share one context and transition independently.

### 3. FSM/API-first behavior migration
- [ ] Move imperative behavior decisions into real FSM_API definitions where stateful behavior is appropriate.
- [ ] Keep SC2-specific ability metadata, observation interpretation, pathing queries, command validation, and protocol translation in adapters/services.
- [ ] Replace duplicate controller-owned state with context facts and authoritative FSM_API state.
- [ ] Establish deterministic processing-group scheduling and explicit ordering for shared-context FSMs.
- [ ] Make handle creation, capability changes, morph reconciliation, and destruction safe and idempotent.

### 4. Intent arbitration and spatial behavior
- [ ] Standardize FSM-produced intents and their provenance/priority.
- [ ] Arbitrate unit command conflicts, worker assignments, resources, production queues, and target reservations centrally.
- [ ] Model navigation as FSM behavior where it benefits from stateful route progress and recovery.
- [ ] Model local threat responses as configurable FSM capabilities, including distance bands (for example enemy within 10 and 5 game units).
- [ ] Permit manifest policy to bind a threat state to responses such as raise/lower a Supply Depot, retreat, defend, reroute, or hold.
- [ ] Preserve pathing/placement/visibility validation; a distance threshold alone must not authorize an impossible or unsafe action.

### 5. Manifest and ladder packaging
- [ ] Define a versioned manifest schema for compiled FSM selection, capability bindings, priorities, policy parameters, and supported state-response mappings.
- [ ] Separate built-in defaults from user overrides and define deterministic merge/precedence.
- [ ] Validate manifests completely before activating a match configuration.
- [ ] Embed the schema, built-in definitions, default manifest, and required assets/catalogues in the compiled artifact.
- [ ] Support manifest delivery through the ladder's supported input mechanism; do not assume arbitrary file access.
- [ ] Test that ladder execution succeeds with filesystem and network access unavailable.

### 6. Verification and documentation
- [ ] Add tests for shared-context composition, independent transitions, scheduler determinism, conflict arbitration, handle cleanup, and manifest fail-closed behavior.
- [ ] Retain and extend existing building, unit, production, navigation, and threat-response tests.
- [ ] Run full solution build and test suite; capture CI/replay evidence where available.
- [ ] Update architecture and behavior docs from implemented definitions, not aspirational catalogs.
- [ ] Generate FSM diagrams/catalogues from definitions where practical.

## Acceptance gates

1. No parallel hand-written FSM/state engine remains for behavior already represented by FSM_API.
2. All entity behavior is composed from independently attachable FSM_API instances over shared entity contexts.
3. Every active behavior path has one authoritative state owner and one deterministic execution path.
4. SC2 protocol commands are emitted only after centralized arbitration and validation.
5. A versioned manifest can select/configure compiled capabilities without runtime filesystem or web access.
6. Tests prove composition, lifecycle, scheduling, safety gates, and manifest validation.
7. No NuGet publish, master merge, deployment, or branch deletion occurs as part of this work without separate explicit authorization.

## Execution notes

Work on `development` only. Implement in small, buildable increments. Keep this plan updated as evidence changes; do not mark an item complete based on documentation alone.
