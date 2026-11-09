# Behavior model

## Runtime ownership

The game loop normalizes SC2 observations into persistent caller-owned UnitBehaviorContext objects. Each context implements IStateContext and owns its independent FSMHandle instances. Reusable definitions are registered through FSM_API.Create.CreateFiniteStateMachine; per-entity instances are created through FSM_API.Create.CreateInstance; groups are advanced through FSM_API.Interaction.Update; and handles are explicitly destroyed through FSM_API.Interaction.DestroyInstance.

IsValid is an execution-eligibility gate. Invalid contexts suspend computation while retaining registered handles and state. Temporary fog-of-war absence is represented as unknown visibility, not context invalidation or destruction.

## Capability composition

A UnitDefinition is compiled composition data: a unit kind plus reusable capability definitions and processing groups. UnitBehaviorRuntime remains generic; it does not instantiate unit-specific runtime classes. Each capability is a separate FSM_API definition and each entity receives its own handle for every capability in its composition.

The initial compiled catalog composes mobile movement capabilities (Move Towards, Move Away From, Follow, Patrol, Flank Left, Flank Right) and Attack Target for the first worker and combat-unit examples. SCV additionally composes construction and repair; worker units compose resource work. These are initial composition resources, not a claim that every SC2 unit type has been catalogued.

## Movement semantics

- Move Towards and Move Away From require a target reference.
- Follow tracks a target as an ongoing relationship.
- Patrol follows an authored route/waypoint sequence.
- Flank Left and Flank Right require an enemy/reference target and express a strategic target-relative flank objective. They do not mean local lateral steering or a fixed offset beside the target. The navigation layer must select a valid route to the requested flank, including bridges, ramps, and terrain detours.
- Attack Target is separate from movement. Having movement does not imply an attack order, and attack does not create a second movement system.
- Repair Target is a support capability with a target and safety guard.

This milestone defines and executes capability FSM graphs and emits semantic behavior intents. It does not yet claim that these new movement intents are translated into SC2 raw actions or that pathfinding/navigation has been implemented. The current SC2 translator remains limited to its existing worker gather/return actions.

## FSM_API execution model

FSM_API owns reusable definitions and the lifecycle/stepping of handles; SingularityBot owns domain contexts and observations. A definition describes states, transitions, guards, and state-entry/update/exit callbacks. CreateInstance binds a definition to a particular context. Processing groups provide host-controlled scheduling. The bot advances the declared groups in order each frame. processRate is measured in calls to Interaction.Update(group), not wall-clock time; rate 1 runs on every host update of that group.

## Evidence boundaries

Passing unit tests can demonstrate composition, independent handle ownership, state transitions, and intent emission. It does not prove SC2 accepts an action or that a live match exhibits the desired maneuver. Live navigation and tactical behavior require subsequent adapter/pathfinding integration and game/replay validation.
