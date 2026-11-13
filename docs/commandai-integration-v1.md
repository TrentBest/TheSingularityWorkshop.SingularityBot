# CommandAi integration: bot-owned vocabulary v1

## Purpose

CommandAi is the provider-neutral proposal contract. SingularityBot owns the meaning of every action and parameter, validates proposals against the selected BotDefinition and current match state, and remains the only route to SC2 action submission.

The vocabulary in `BotCommandVocabulary` is the first integration step. It deliberately contains domain operations, not SC2 protocol ability IDs.

## Ownership boundary

| Layer | Responsibility |
|---|---|
| CommandAi | Bounded immutable proposal representation and wire encoding |
| SingularityBot vocabulary | Stable action/parameter identities and domain meanings |
| SingularityBot validator/runtime | Required arguments, value semantics, strategy guards, current observations, deduplication, scheduling and outcomes |
| FSM_API | Lifecycle/state-machine execution |
| SC2 adapter | Current-frame legality and protocol action construction |
| Observation reconciliation | Establishes progress, completion or failure |

A well-formed proposal is not authorization and is not evidence that an operation happened.

## Vocabulary v1

Vocabulary identity: `SingularityBot.Commands/1`

| Action ID | Action | Required parameters |
|---:|---|---|
| 1001 | `SetProductionTarget` | Target (2001, identifier), DesiredCount (2002, positive integer text) |
| 1002 | `ConstructBuilding` | Target (2001, building identifier), DesiredCount (2002, positive integer text) |
| 1003 | `AssignScout` | Target (2001, sector identifier) |
| 1004 | `AssignMilitaryOperation` | Operation (2004, authored operation identifier) |
| 1005 | `TransitionStrategy` | Strategy (2005, strategy transition identifier) |

Optional parameter:

| Parameter ID | Name | Type | Constraint |
|---:|---|---|---|
| 2003 | `Priority` | Literal text | Integer from 0 through 100 |

The action and parameter IDs are stable SingularityBot identities. Identifier values refer to bot-owned catalogs or definitions; they are not raw SC2 unit, ability, or order IDs.

## Initial scope and deliberate gaps

This commit establishes the vocabulary only. It does not yet consume `TheSingularityWorkshop.CommandAi` types or execute proposals.

The CommandAi source repository currently exposes protocol `CommandAi/1` and package version `0.1.0-alpha.2`, but the package is not currently verifiable as available from the configured NuGet distribution. SingularityBot must not copy the protocol classes or create a parallel proposal codec to work around that dependency. The thin type adapter should be added when the actual CommandAi assembly can be consumed through an approved dependency path.

The following remain future work:
- Map actual `CommandProposal` / `CommandInstruction` values to the bot-owned vocabulary.
- Reject unknown actions, unknown parameters, missing required parameters, wrong value kinds, and invalid literal values.
- Resolve target identifiers against the active BotDefinition and match catalogs.
- Convert accepted proposals into existing goal/job contracts; do not add a second scheduler.
- Implement proposal idempotency and lifecycle records in SingularityBot.
- Connect the first `SetProductionTarget(Marine, N)` proposal to the existing production runtime.
- Record observed production progress and completion against the originating proposal.

## Safety requirements

- Unknown action or parameter identities fail closed.
- Structural validity does not imply semantic validity.
- Stale or absent observations cannot satisfy hard prerequisites.
- A submitted SC2 action is not a completed objective.
- Duplicate proposals must reconcile to an existing objective rather than create duplicate work.
- CommandAi and model output never submit SC2 protocol actions directly.
- The live frame loop must remain deterministic and must not wait on an LLM.

## Verification

`BotCommandVocabularyTests` checks stable positive action/parameter identities, uniqueness, required parameter typing, and fail-closed lookup of unknown action IDs.
