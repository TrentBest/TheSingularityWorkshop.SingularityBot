# Architecture

## Purpose

SingularityBot is a StarCraft II player. Its primary product is a reliable decision-and-action runtime that can complete a real opening. Authoring, visualization, replay analysis and learning support that runtime; they are not alternate execution paths.

## Civilization command model

Civilization identity is a strategic/domain layer, not a translation table for game mechanics. The shared strategic vocabulary describes responsibilities such as Economy, Military, Intelligence, Technology, Expansion, Defense and Logistics. Each civilization organizes those responsibilities through its own campaign-inspired authority model:

| Civilization | Supreme authority | Subordinate authority |
|---|---|---|
| Terran | Admiral | Commanders |
| Zerg | Overmind | Cerebrates |
| Protoss | Conclave | Judicators |

These names are represented by `CivilizationLeadershipCatalog` and `CivilizationLeadership`. They carry identity and organizational vocabulary only. They do not contain unit names, abilities, costs, prerequisites, production rules or action code.

The intended composition is:

```text
Shared strategic intent and domains
    |
Civilization leadership model
    +-- Terran: Admiral -> Commanders
    +-- Zerg: Overmind -> Cerebrates
    +-- Protoss: Conclave -> Judicators
    |
Race-specific Commander/advisor implementations
    |
Existing shared runtime, intent arbitration and validated action pipeline
```

The local and ladder runner now requests `Race.Random` from SC2. This is a race-selection setting, not evidence that all three execution adapters are complete. The currently compiled strategy and macro/action implementation remain Terran-specific. Until race detection, dispatch and non-Terran adapters are implemented and tested, a Random-selected Zerg or Protoss match is not considered supported or match-verified. Do not infer race from the configured selection; use authoritative match/player information and observed facts before dispatching. Unsupported race execution must fail closed rather than issuing Terran actions.

## Four-layer executive control

Every active observation is processed through one ordered control cycle, enforced by `ExecutiveController`:

1. **Executive Reaction — Chief:** consumes the normalized match state and updates the executive picture of the match. The existing `GameStateContext` and spatial-intelligence update are the first implementation of this responsibility.
2. **Subcontrollers — Commanders:** apply configured domain policy and publish strategic jobs/intents. The current Terran economy, intelligence, scouting and compiled-strategy policies are the baseline behaviors to be moved behind race-specific Commander contracts; they are not yet fully extracted.
3. **Executive Review — Chief:** reviews proposed strategic jobs, arbitrates/reserves work through the strategic planner, and decides which proposals may proceed. The existing planner is the first review implementation.
4. **Unit AI:** updates per-unit FSM behavior after executive review. The existing `UnitBehaviorRuntime` and FSM_API processing groups are the current implementation of this layer.

The enforced order is:

```text
Executive Reaction (Chief)
        |
Subcontrollers (Terran Commanders / Zerg Cerebrates / Protoss Judicators)
        |
Executive Review (Chief)
        |
Unit AI (individual unit behavior)
        |
Existing intent validation and SC2 action pipeline
```

The first refactor deliberately captures current Terran behavior rather than redesigning its tactics. The existing advisors and controllers are the behavioral baseline. The next extraction moves their policy behind Commander-owned contracts while preserving their observed decisions and action pipeline. The Chief owns situation assessment, desired direction, and review; Commanders own the configurable methods for achieving assigned goals; Unit AI owns local execution choices. None of these layers submits protocol actions directly.

The current integration enforces stage ordering in the live `OnFrame` path. The first Commander extraction is now real: `TerranCommander` owns the existing Economy, Intelligence and Scouting policy specialists, including their configurable settings. The existing strategic planner remains Chief-side review, while opening/build/production controllers and the compiled strategy still need to be moved behind Commander-owned contracts. This is an incremental extraction, not a claim that the mono-controller refactor is complete.

## Captured scenarios and decision rehearsal

Captured observations, screen-derived map annotations, and prestaged scenarios should become versioned decision fixtures. A fixture records the exact observation/state at a game loop, relevant unit statistics and visible context, the selected policy version, the decision alternatives, and the observed result. The editor can present these fixtures as reusable rehearsals: enable a scenario set, choose an accepted/rejected decision, and compare downstream outcomes and metrics.

Screen capture is an annotation and reconstruction aid, not an authority that silently replaces SC2 observations. A screenshot-derived fixture must retain provenance and uncertainty for facts that cannot be recovered exactly. Deterministic normalized-observation replay remains the preferred source when available. Rehearsal runs must not issue live game actions or create a second execution engine. Scenario settings belong to the Commander/Unit AI policy that consumes them, while the Chief may use scenario results as evidence when reviewing policy.

## Composition and execution boundary

The bot is a self-contained compiled DLL. The Runner selects the statically linked `AdaptiveMarineStrategy`, which implements `ICompiledBotStrategy`. The contract supplies a validated `BotDefinition`, executable initial-goal composition, goal-assignment policy and the Marine advance-to-enemy-start decision; `SingularityBot` consumes those decisions while retaining one shared runtime and action pipeline. `GoalActionPlanner` remains a shared engine service invoked through the strategy contract. `BotDefinitionCatalog` remains compiled typed data, not the strategy class itself. The personality boundary is partially implemented: objective/transition composition and most macro/tactical policy still reside in shared controllers and planners. Runtime JSON files, reflection-based discovery and dynamic assembly loading are not used.

The SC2 Runner is the host/execution driver: it owns the SC2 connection, callbacks, frame lifecycle and protocol action submission. It invokes the bot; it does not supply a separate strategy engine. SingularityBot owns its runtime lifecycle, state, decisions, intent arbitration and action translation. The bot's compiled personality is the replaceable user-specific portion of the DLL.

FSM_COS currently composes a BotDefinition microbundle and the CommandAi vocabulary microbundle at startup, and verifies that they load. This is startup composition, not execution of the bot's strategy; it does not own the match loop or become a second execution model. FSM_API supplies persistent contexts and hierarchical behavior mechanics; the bot decides how those mechanics are used. The existing action pipeline remains the only authority for validating and submitting SC2 commands.

FSM_Foundry is not part of live SC2 execution. Its workflow-executor contract may be useful later for bounded external scenario/replay evaluation, but it must not be inserted between the Runner's frame callbacks and the bot's decisions or unit actions.

## Runtime boundary

```text
SC2 Runner (host)
    |  connection / callbacks / frame lifecycle
    v
SingularityBot.dll
    |  owns match runtime and execution decisions
    +---- compiled BotDefinition data (current implementation)
    |         selected from BotDefinitionCatalog at startup
    |         NOT YET the replaceable compiled strategy implementation
    |
    +---- FSM_COS startup composition
    |         BotDefinition + CommandAi vocabulary microbundles
    |
    +---- FSM_API contexts and hierarchical behavior mechanics
    |
    v
Intent arbitration -> legality validation -> SC2 action adapter
    |
    +---- decision/event trace -> replay and evaluation
    |
    +---- compiled definition + capability metadata -> Forge GUI

FSM_Foundry: not part of the live SC2 execution path.
```

The adapter owns protocol DTOs and protocol-specific details. The rest of the bot consumes stable domain facts and emits domain intents. An intent is a request, not authorization to issue an action.

## Ownership

| Area | Owns | Must not own |
|---|---|---|
| SC2 Runner / adapter | Connection lifecycle, callbacks, frame delivery, protocol conversion, game-data lookup, action submission/results | Strategy decisions |
| Observation model | Stable tags, ownership, visibility, orders, cargo, resources, structures, game-loop time, destruction evidence | Protocol transport or policy |
| Game ontology | Unit/structure/ability identities, race, costs, prerequisites, capabilities | Match-specific mutable state |
| SingularityBot runtime | Match/player/entity contexts, bot lifecycle, shared decisions, intent arbitration and execution cadence; consumes `ICompiledBotStrategy` | SC2 protocol transport details |
| Economy controller | Global worker assignments, reservations, build/production priorities | Per-frame duplicate assignment decisions |
| Unit/route FSMs | Execute assigned work, track local phase, report facts and outcomes | Reassigning other workers or overriding economy constraints |
| Building FSMs | Persistent building identity, construction lifecycle, production/research activity, structure-specific capabilities | Construction-job identity or protocol command submission |
| Action pipeline | Intent arbitration, current-frame validation, translation and submission | Guessing missing abilities or targets |
| Physics | Reusable distance, path and spatial calculations | A bot-specific parallel math library |
| GUI | Edit/version definitions; inspect states, facts, intents, maps and diagnostics | A second runtime or direct game authority |
| Evaluation | Tests, recorded-observation replay, metrics and controlled match experiments | Unverified claims of competitive strength |

## Strategic advisor layer

A match-level Game State FSM classifies Early, Mid and Late game from observed milestones. Economy, Military, Science, Base Engineering and Intelligence advisors own their respective policy domains and receive phase-weighted processing opportunities. Advisors produce assessments and proposed jobs; the Task Master arbitrates assignments and reservations. Advisors do not issue protocol commands or bypass action validation. See [strategic advisors and game-phase scheduling](strategic-advisors-and-game-phases.md) for current phase evidence, compute weights and implementation boundaries.

## One observation cycle

1. Receive the latest SC2 observation and game data.
2. Normalize it into deterministic, protocol-independent facts.
3. Reconcile entity contexts. Fog or temporary absence is not proof of death.
4. Update the match/economy FSM and eligible entity FSMs using current facts.
5. Collect intents without submitting actions from inside behavior callbacks.
6. Resolve conflicts and validate ownership, visibility, current orders, ability availability, target, range, affordability, prerequisites and placement as applicable.
7. Translate accepted intents to protocol actions; reject uncertain or stale commands.
8. Record facts, decisions, rejections, submissions and outcomes against the game-loop identity.

The same normalized observation sequence and versioned policy must produce the same decisions and reason codes.

## Context and lifecycle rules

- Unit tags are stable runtime keys within a match.
- A persistent context is refreshed from each authoritative observation.
- A unit hidden by fog is unknown/not visible, not destroyed.
- Explicit death/destruction or authoritative match end retires a context.
- A unit morph that changes its behavior profile must replace or reconfigure the profile deliberately; a stale worker FSM must not continue controlling a combat unit.
- Time is measured in authoritative game loops. Predicted deadlines schedule reevaluation; they never prove that an event occurred.
- Cached route geometry is keyed by map, worker movement profile, town hall and resource identity, and is invalidated when its assumptions change.

## Building identity and lifecycle

Every observed friendly structure receives a persistent building context keyed by its SC2 unit tag and a match-local display identity such as `SupplyDepot 3`. The identity survives Supply Depot raised/lowered morphs and Command Center to Orbital/Planetary morphs; destroyed tags retire their contexts. Numbering is assigned as structures are first reconciled from observations, with SC2 tags providing deterministic ordering when multiple structures first appear together.

Each building owns independent FSM_API capabilities:

- **Lifecycle:** `UnderConstruction -> Operational`. Entering `Operational` emits a one-time event. When the associated construction job identifies its SCV, the match loop releases that worker back to economy tasking.
- **Activity:** `Idle`, `Producing`, or `Researching`, derived from observed orders and the match's ability catalog. The lifecycle FSM remains independent of activity.
- **Supply Depot access:** `Assess`, `Raised`, and `Lowered`. A visible enemy within 10 game units requests a raised Depot. An explicit passage request takes precedence and requests lowering even while enemies remain nearby. The action adapter resolves Raise/Lower abilities from SC2 game data and emits no command when resolution is ambiguous or unavailable.

A build job continues to represent the attempt and its evidence; it does not become the durable identity of the resulting building.

Town Halls own two independent strategic FSMs:

- **Orbital upgrade:** `WaitingForPrerequisites -> UpgradeReady -> Upgrading -> Orbital`. Readiness requires a landed Command Center, a completed Barracks and Supply Depot, no active SCV training order, and a position outside the configured designated Command Center construction area. The match loop invokes the Orbital action adapter only when this FSM is in `UpgradeReady`; mineral reservation is also derived from that FSM state. The exclusion area is a circle configured by center and radius through the bot API or runner argument `--cc-area=centerX,centerY,radius`. The expansion-site planner filters every candidate inside that circle; the bot does not guess the zone. Until configured, no area is excluded.
- **Expansion production:** `Waiting -> StreamingCommandCenters`. A landed Command Center inside the configured designated construction area enters the streaming state when the first Barracks is operational. That FSM state gates the expansion controller, which cycles through available friendly-side expansion sites and continues requesting Command Centers until no legal site is available. The controller backs off between empty-site polls rather than flooding the log.

The generic activity FSM reports whether each building is `Idle`, `Producing`, or `Researching`. Existing SCV production and MULE call-down controllers remain in place. Orbital Scan is now an explicit request FSM: an Orbital with at least 50 energy emits a Scanner Sweep request, the adapter validates the ability and map target, and the FSM waits for the observed energy decrease before completing or retrying. Scan-requested Orbitals are excluded from MULE energy spending.

Supply Depot passage is an explicit event request through the bot API. Movement/attack planners can raise or clear that request as they determine that a friendly route needs to cross the Depot.

## Workshop integration

- **SC2 Runner** is the live host and frame driver for this bot; no separate Foundry execution layer is needed.
- **FSM_API** supplies reusable context/handle lifecycle and hierarchical behavior mechanics.
- **FSM_COS** currently composes the compiled BotDefinition and CommandAi vocabulary at startup; this is not yet a compiled strategy implementation and does not drive the live match.
- **Compiled personality refactor** is in progress: move user-specific strategy behavior behind a statically compiled strategy contract while retaining the shared bot engine and validated action pipeline.
- **Physics** is the intended shared owner for spatial mathematics and path reasoning; the current bot still contains its existing spatial implementation.
- **GrammarAi / ProtocolAi** may provide canonical vocabulary and protocol knowledge; neither replaces the SC2 adapter.
- **CommandAi** may decompose high-level orders into goals; deterministic FSMs and validators remain authoritative.
- **GUI / Renderer / Visualize** provide authoring, inspection and visual presentation over the same runtime contracts.
- **DataWarehouse** stores match traces and durable experience.
- **CiAi / HeadlessAi** build, test, replay and evaluate the bot.

No GUI, neural-network, memory or optional AI package is required to run the headless bot.

## Non-negotiable safety

Missing facts fail closed. Never fabricate an ability ID, target, placement, resource count or legal-action result. Do not issue duplicate conflicting commands in one frame. Every accepted action must be traceable to its source intent, evidence, validation result and game-loop identity.
