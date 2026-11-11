# Architecture

## Purpose

SingularityBot is a StarCraft II player. Its primary product is a reliable decision-and-action runtime that can complete a real opening. Authoring, visualization, replay analysis and learning support that runtime; they are not alternate execution paths.

## Runtime boundary

```text
StarCraft II
    |
    v
SC2 protocol adapter
    |  observations, game data, legal-action results
    v
Normalized game facts + event stream
    |
    v
Game ontology / capabilities
    |
    v
FSM runtime (FSM_API, executed by FSM_Foundry)
    |
    v
Intent collection -> arbitration -> validation
    |
    v
SC2 action translation and submission
    |
    +---- decision/event trace -> replay and evaluation
    |
    +---- versioned definitions/configuration -> GUI authoring
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

## Workshop integration

- **FSM_API** is the reusable heartbeat and context/handle lifecycle.
- **FSM_Foundry** executes the composed FSM workload.
- **Physics** supplies shared spatial mathematics and path reasoning.
- **GrammarAi / ProtocolAi** may provide canonical vocabulary and protocol knowledge; neither replaces the SC2 adapter.
- **CommandAi** may decompose high-level orders into goals; deterministic FSMs and validators remain authoritative.
- **GUI / Renderer / Visualize** provide authoring, inspection and visual presentation over the same runtime contracts.
- **DataWarehouse** stores match traces and durable experience.
- **CiAi / HeadlessAi** build, test, replay and evaluate the bot.

No GUI, neural-network, memory or optional AI package is required to run the headless bot.

## Non-negotiable safety

Missing facts fail closed. Never fabricate an ability ID, target, placement, resource count or legal-action result. Do not issue duplicate conflicting commands in one frame. Every accepted action must be traceable to its source intent, evidence, validation result and game-loop identity.
