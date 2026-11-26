# Production plans and FSM execution

## The user-authored contract

A production plan is versioned data. It names production goals (target unit or structure, producer, desired count, earliest game loop, priority) and dependency edges between steps. It does not contain SC2 ability IDs, protocol messages, C# callbacks, or executable code. Game facts must be resolved from the active SC2 `ResponseData` catalog before an action can be submitted.

The first contract is deliberately small but composable. A cheese attack can prioritize an early structure and a concentrated unit count. A slow push can stage production through a Barracks, Factory, and Starport while independently accumulating its infantry, siege, and support components. `dependsOn` expresses hard step dependencies; independent steps may progress in parallel. Cycles, duplicate IDs, and unknown dependency IDs are rejected.

## FSM lifecycle

Each production step follows this graph:

```mermaid
stateDiagram-v2
    [*] --> Waiting
    Waiting --> Issuing: resources + producer + prerequisites + time gate
    Issuing --> AwaitingEvidence
    AwaitingEvidence --> Completed: desired count observed
    AwaitingEvidence --> Waiting: another unit may be requested
    Completed --> Waiting: desired count no longer met
```

The `Issuing` entry action emits a semantic `ProductionIntent`. It does not submit an SC2 command. The host action pipeline remains responsible for resolving the target and producer against authoritative game data, checking resources/supply/producer availability, translating to a protocol command, and reporting submission outcomes back to the context.

The context distinguishes currently completed units from in-progress production. This prevents the graph from treating a queued unit as completed or blindly issuing duplicates. A submission latch is held until the host observes the order or rejects the command. Dependency completion is supplied as an observation-derived context fact; the FSM does not infer that a building exists merely because an earlier action was requested.

## User-authored examples

- [Cheese attack example](../strategies/cheese-attack.example.json): Depot → Barracks → Marine commitment.
- [Slow push example](../strategies/slow-push.example.json): staged Barracks/Factory/Starport infrastructure with parallel unit composition.

These examples demonstrate production-plan shape, not a claim that the current runtime can execute every step. The reusable production FSM graph, JSON parser, structural validator, and dependency-cycle checks are implemented. Binding every goal to SC2 game data, evaluating all prerequisites, translating arbitrary structures/units into validated actions, and live match validation remain separate gates.

## Why plans compile to FSM_API

The runtime registers one reusable graph and creates independent FSM_API instances for each configured step. The user's plan determines data and composition; the runtime owns observation, context facts, and handle lifecycle. FSM_API owns the state-machine definition and registered instances. No second state-machine engine or user-supplied executable code is introduced.
