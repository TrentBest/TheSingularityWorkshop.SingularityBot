# Architecture

## Purpose

SingularityBot is a StarCraft II player. Its primary product is a reliable decision-and-action runtime that can complete a real opening. Authoring, visualization, replay analysis and learning support that runtime; they are not alternate execution paths.

## Composition and execution boundary

The default definition is created by `BotDefinitionCatalog` as typed C# data compiled into the bot assembly. The runner performs no strategy-file read and no dynamic assembly load.

`BotRuntimeComposer` submits a versioned `RuntimeManifest` to FSM_COS. The compiled bot-definition MicroBundle declares its dependency on the bot-owned CommandAi vocabulary MicroBundle. FSM_COS validates and loads that dependency graph and returns a `RuntimeAssembly`; the host retrieves the typed definition and vocabulary from that assembly.

FSM_COS composes capabilities; it does not run the StarCraft match. The host owns the SC2 connection and frame lifecycle, while FSM_API owns the bot's state/context mechanics and behavior updates. The existing action pipeline remains the only authority for translating and submitting SC2 commands.

Foundry's current contract is a host-neutral workflow executor with its own workflow definition, handler registry, lifecycle, and result model. It is not interchangeable with an FSM_COS `RuntimeAssembly` or the synchronous per-frame game loop. It may later orchestrate bounded scenario/replay evaluation workflows, but it is deliberately not inserted into unit action execution.

## Runtime boundary

```text
Compiled BotDefinition + static capability catalog
    |
    v
FSM_COS composition
    |  validated dependency closure
    v
RuntimeAssembly
    |
    v
SingularityBot host
    |  normalized observations / domain intents
    v
FSM_API-backed behavior and entity lifecycles
    |
    v
Intent arbitration -> legality validation -> SC2 adapter
    |
    +---- decision/event trace -> replay and evaluation
    |
    +---- versioned definition + capability metadata -> Forge GUI

Foundry: bounded host-neutral workflows for scenario/evaluation orchestration;
         not the per-frame StarCraft unit-action executor.
```

The adapter owns protocol DTOs and protocol-specific details. The rest of the bot consumes stable domain facts and emits domain intents. An intent is a request, not authorization to issue an action.

## Ownership

| Area | Owns | Must not own |
|---|---|---|
| SC2 adapter | Connection lifecycle, protocol conversion, game-data lookup, action submission/results | Strategy decisions |
| Observation model | Stable tags, ownership, visibility, orders, cargo, resources, structures, game-loop time, destruction evidence | Protocol transport or policy |
| Game ontology | Unit/structure/ability identities, race, costs, prerequisites, capabilities | Match-specific mutable state |
| FSM runtime | Match/player/entity contexts, lifecycle, transitions and execution cadence | SC2 protocol types |
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

- **FSM_API** is the reusable heartbeat and context/handle lifecycle.
- **FSM_Foundry** is the intended composed-workload executor; the current SingularityBot project does not yet reference it.
- **Physics** is the intended shared owner for spatial mathematics and path reasoning; the current bot still contains its existing spatial implementation.
- **GrammarAi / ProtocolAi** may provide canonical vocabulary and protocol knowledge; neither replaces the SC2 adapter.
- **CommandAi** may decompose high-level orders into goals; deterministic FSMs and validators remain authoritative.
- **GUI / Renderer / Visualize** provide authoring, inspection and visual presentation over the same runtime contracts.
- **DataWarehouse** stores match traces and durable experience.
- **CiAi / HeadlessAi** build, test, replay and evaluate the bot.

No GUI, neural-network, memory or optional AI package is required to run the headless bot.

## Non-negotiable safety

Missing facts fail closed. Never fabricate an ability ID, target, placement, resource count or legal-action result. Do not issue duplicate conflicting commands in one frame. Every accepted action must be traceable to its source intent, evidence, validation result and game-loop identity.
