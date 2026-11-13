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
- [x] Verify exact FSM_API signatures and semantics against source and SimpleDemos, including multiple FSMs on one context.
- [ ] Verify FSM_Foundry availability, package/version, and intended execution lifecycle before assigning it ownership.
- [ ] Record baseline build/test status and current branch head.

### 2. Shared entity model and capability composition
- [ ] Define common unit and building context contracts with shared observed facts and stable identity.
- [ ] Ensure every building receives common lifecycle behavior.
- [ ] Attach optional production, research, town-hall, access, repair/integrity, threat-response, and other FSMs by capability.
- [ ] Ensure units receive common unit behavior plus optional movement, combat, worker, construction, repair, scouting, and survival capabilities.
- [x] Add composition tests proving multiple independent FSM_API handles share one context and transition independently (building production/research FSMs).

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
- [x] Run full solution build and test suite; capture CI evidence where available (GitHub Build #1178 passed at commit `12748e444e2191b42a62960ae7373a44b03bfdb7`).
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

## Progress evidence

- Refactor plan committed at `fec14362240c8000232b54142d9a052336eb8381`.
- Building capability slice committed at `12513af5737bcc7357940e397a42caaad99ebb4c`: replaced the monolithic BuildingActivity FSM with independently attached BuildingProduction and BuildingResearch FSM definitions. Every building retains BuildingLifecycle; production/research handles are attached according to building capability.
- Regression assertions committed at `12748e444e2191b42a62960ae7373a44b03bfdb7`: a Barracks context simultaneously owns both handles; research observations transition only the research FSM, and production observations transition only the production FSM.
- GitHub Build #1178 completed successfully for `12748e444e2191b42a62960ae7373a44b03bfdb7`, including restore, Release build, and test suite.
- FSM_API source confirms `CreateInstance(string, IStateContext, string processingGroup = "Update")` creates a distinct handle for the supplied shared context. This is the intended composition primitive.
- Current `TheSingularityWorkshop.FSM_Foundry` repository is only a scaffold (README plus policy assets/workflow; no project or runtime source). Do not add a fictional Foundry dependency. Until Foundry has an executable runtime, the bot's single `OnStep` processing-group loop remains the actual scheduler owner and calls `FSM_API.Interaction.Update(group)` once per declared group.
- Remaining audit: imperative opening/expansion/Orbital controllers, production plan runtime, advisor/strategy scheduling, and spatial/navigation behavior still need migration classification and sequencing.

- Building threat proximity slice committed at `11cd2d51d91de08da0452a3084bb0d709de654b9`: every building now owns a `BuildingThreatProximity` FSM with independent `Clear`, `EnemyWithin10`, and `EnemyWithin5` states, driven by nearest visible ground-enemy distance.
- Threat-band regression test committed at `0926ace1422da6ae7595d8f4891e46ddffdf91fb`; GitHub Build #1181 and Sith Policy #1397 both passed. Build #1180 also passed for the production/research split.
- The shared bot frame loop advances the declared shared groups once. Opening production and opening-builder movement use dedicated per-controller processing groups outside that shared schedule; their explicit local ticks are not duplicate ticks of the shared groups. Consolidating those private groups remains a deliberate scheduler migration, not an immediate bug fix.

- Research capability mapping refined at `563bd1f1caecb92f4067f140cfe1574646a577b8` and tested at `ff88607ab5ca80d1ee8b3541539d5f4e19831776`: research FSMs attach to actual research structures (Engineering Bay, Armory, Ghost Academy, Fusion Core, and TechLab add-ons such as BarracksTechLab), while Barracks/Factory/Starport receive production FSMs. The first intermediate test run failed because the old test incorrectly modeled a Barracks itself as the research host; the test was corrected to model a TechLab add-on and the subsequent Build #1184 passed.
- Threat visibility correction committed at `4a6ab3581800cf5c1272cb8f82f7de72dc4d79fb`: a building transitions to `Unobserved` when a previously visible threat leaves vision. It does not infer safety from absence of an observation. Depot access remains raised while threat status is Unobserved and lowers only after visible evidence supports Clear (unless passage is explicitly requested).
- Fog-of-war regression test committed at `599858303f930083c1d162068f1ea49cd595f40d`; GitHub Build #1186 completed successfully, including Release build and full test suite.


## Advisor hierarchy extension (2026-11-12)

- Added `schemas/singularitybot-manifest-v1.schema.json` at commit `44e5f971f325bf3cb43860c00857d6eb3317077c`. It defines a versioned, closed-shape manifest with recursively composable compiled advisor definitions, explicit process groups, typed selection boundaries, and settings payloads.
- Added `docs/advisor-composition.md` at commit `74a4dcf46a75f348ecd87b2a51b9a3832811843d`. It specifies Military, Economic, Research, Base Layout, and Intelligence advisors; hierarchical process-group ownership; intent-only output; economic mining controls; and safe DualRun comparison semantics.
- Audit found `EconomyAdvisor` and `IntelligenceAdvisor` already exist as policy classes implementing `IStrategicAdvisor`, but they are not yet first-class hierarchical FSM_API owners. Existing settings include a single mineral saturation value and MULE/scan policy; per-resource mineral/gas staffing, explicit GameDefault/Custom/DualRun authority modes, and threshold-driven staffing policies remain to implement.
- Existing `UnitDefinitions` demonstrates the right compiled composition pattern: definitions map reusable FSM names to processing groups; each entity gets independent FSM_API handles on its own persistent context.
- The project targets .NET 8 and references FSM_API 1.0.13. No manifest schema directory was previously evidenced at the expected path; the new schema is now explicitly added.
- The current `SingularityBot.csproj` has no embedded manifest resource declaration yet. Do not claim the manifest is ladder-ready until resource loading, validation, deterministic override rules, and ladder input constraints are implemented and tested.
- Important scheduling constraint: an advisor-owned child process group must not also be stepped by the root frame scheduler. The forthcoming scheduler must validate acyclic ownership and exactly-once group advancement per frame. Do not implement recursive stepping until the FSM_API callback/reentrancy semantics are verified.

- Added `config/singularitybot.manifest.json` at commit `7785cb6c729114b935b41fd9c8d67b4219e92b7c` with initial Military, Economy, Research, Base Layout, and Intelligence advisor selections. The default economic policy declares two workers per mineral node, three per gas refinery, Custom mining authority, and MULE/scan policy; these are manifest targets, not yet all active runtime controls.
- Updated `src/SingularityBot/SingularityBot.csproj` at commit `b292325404f457803562225bd0757b79a6ac3988` to embed the default manifest and v1 schema as named assembly resources. This is packaging groundwork only: there is not yet a loader/validator or proof of the ladder's external override channel.
- Current implementation is still in migration: the existing OnStep loop calls `FSM_API.Interaction.Update` for the root declared groups and separately invokes `AdvisorWorkScheduler.Advance` after the Strategy group. The existing advisors are policy objects, not yet independent FSM_API-owned contexts/groups. Next code slice must replace this mixed phase-weight scheduler with FSM_API advisor contexts and a single-owner hierarchical group scheduler without double-stepping existing groups.

- Added `BotManifest` at commit `82c2e5cf43c183bb9bb2c5ac975e42981c992936` and wired startup validation in `SingularityBot` at `c05ff3c8929d32ccba2bf99e736215e6aad80f67`. The loader reads the named embedded resource, rejects unmapped JSON properties, validates schema version/race/profile/deterministic scheduling, restricts advisor IDs to the compiled catalogue, and rejects duplicate advisor IDs/process-group ownership and excessive nesting.
- Added `BotManifestTests` at commit `5a42f528ac0babd58aac513a663067c9d39a6668` covering embedded-resource loading, five default advisor selections, duplicate process-group rejection, unknown compiled definition rejection, and deterministic-scheduling enforcement. CI/build result is pending verification; do not mark this code slice complete until a successful build/test run is observed.

- Added manifest validation tests in `tests/SingularityBot.Tests/BotManifestTests.cs` at `5a42f528ac0babd58aac513a663067c9d39a6668`. They cover embedded resource loading, default advisor selection, duplicate group ownership, unknown definitions, and deterministic scheduling.
- Nullable analysis was enabled for the new loader in commit `6896d084b8f5a9eac2b3a23c5a763d4f4ce1b00f`.
- Verification caveat: GitHub combined-status queries for the manifest/schema/loader/test commits returned no status entries at the time of this update. No successful CI result has been observed for this slice yet; tests remain pending and this work item is not marked complete.


## Entity-owned spatial facts and complete Terran coverage (2026-11-13)

### Spatial update contract

- Entity contexts are the owners of their spatial facts. The frame pipeline first reconciles observations and updates entity-owned spatial facts; advisors must not independently recalculate those facts.
- Each positioned entity caches its current map point, sector, quadrant, half-map/side classification, and squared distance to relevant immutable centers. Map geometry and center coordinates are match-immutable.
- A mobile unit refreshes position-dependent facts when a new visible position is observed. A remembered enemy retains its last-known position and cached classification, explicitly marked remembered rather than current.
- A non-flying structure computes spatial facts when first observed/created and only recomputes when its observed position changes. A flying structure recomputes position-dependent facts on every fresh observation while airborne; on landing it settles into the static path.
- SCV Worker capability owns its resource-distance fact for its current assigned mineral node/refinery. Assignment changes invalidate the old fact. Economic Advisor consumes worker facts and resource-node staffing counts; it does not calculate every worker-to-resource distance itself.
- Enemy units are included in the same entity spatial phase. Their tactical distance fact is distance-squared from their located sector center, not an advisor-side repeated calculation.
- All distance thresholds use squared distances. Avoid square roots unless a display/output genuinely requires linear distance. Cache invariant center coordinates and precomputed transforms; preserve necessary arithmetic and prove equivalence with tests.
- Frame barrier: all friendly, neutral/resource, and enemy entity facts are updated before the first advisor executes. Advisor priority affects decision order only, never which snapshot a decision sees.
- Spatial snapshots carry game-loop/version provenance so tests can prove no advisor reads mixed-frame facts. Fog-of-war retains remembered data and never silently converts unknown into clear/safe.

### Complete Terran entity coverage

- Build a canonical inventory from the active SC2 game-data/unit-type catalogue, not a hand-maintained partial list. Include every Terran unit and structure type exposed by that catalogue, including flying/landed forms, lowered/raised forms, add-ons, morphs, temporary/summoned entities, and other variants exposed by the selected game mode.
- Every catalogued Terran type must resolve to an explicit compiled entity definition. Definitions may initially contain scaffold capabilities with explicit NotImplemented/no-op behavior, but may not be absent or silently fall through to an unknown generic type.
- Keep type identity separate from capability composition: a type definition declares race/category/forms/capabilities; shared compiled capability definitions are reused across types, while each observed entity owns independent FSM_API handles.
- Every definition declares which capabilities are implemented, scaffolded, or intentionally unsupported, with tests preventing omissions when the game-data catalogue changes.
- The first coverage gate is exhaustive enumeration and resolution, not complete strategic intelligence. A scaffold must not emit commands or pretend to know unfinished behavior.

### Next implementation order

1. Add the canonical Terran type inventory and exhaustive coverage tests against the SC2 game-data catalogue.
2. Add cached spatial facts to persistent unit/building contexts and make the entity-update phase populate them before advisor execution.
3. Move SCV resource-distance updates into Worker capability facts; update on assignment/resource-position changes and at the configured worker cadence.
4. Update enemy sector-center distance facts in the same pre-advisor phase.
5. Replace full-contact spatial reclassification with incremental entity-owned updates while retaining a deterministic read-only sector index for advisor queries.
6. Convert advisor scheduling to consume one completed spatial snapshot and test stable priority ordering plus exactly-once group advancement.

### Current audit findings

- UnitBehaviorContext already stores assignment/resource context, sector identity, and sector-center coordinates, but does not yet own cached distances/quadrant/half-map facts.
- UnitBehaviorRuntime.Reconcile updates persistent unit observations and preserves contexts through fog-of-war; ApplyWorkerAssignments binds workers to persistent mineral-node contexts.
- SpatialMap.MapPoint already exposes DistanceSquaredTo; however, SpatialIntelligence.RebuildIndexes currently recalculates sector, quadrant, and side classification for every known contact on each update. This is the behavior to replace, not a completed optimization.
- UnitDefinitions and UnitExpertiseProfiles currently expose only a small initial mixed-race sample (SCV, Probe, Drone, Zealot, Marine, Marauder, Zergling). They are not an exhaustive Terran inventory. Do not claim Terran coverage until the catalogue-driven gate passes.
- BuildingBehaviorContext currently receives nearest visible ground-enemy distance-squared from its runtime, so building threat already avoids square roots. It does not yet own cached sector/quadrant/half-map distances.