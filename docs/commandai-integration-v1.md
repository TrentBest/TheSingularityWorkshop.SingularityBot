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
| 1001 | `SetProductionTarget` | ProductionTarget (2001, identifier), ProductionDesiredCount (2002, positive integer text) |
| 1002 | `ConstructBuilding` | BuildingTarget (2011, identifier), BuildingDesiredCount (2012, positive integer text) |
| 1003 | `AssignScout` | ScoutSector (2021, sector identifier) |
| 1004 | `AssignMilitaryOperation` | MilitaryOperation (2031, authored operation identifier) |
| 1005 | `TransitionStrategy` | StrategyTransition (2041, strategy transition identifier) |

Optional parameter:

| Parameter ID | Name | Type | Constraint |
|---:|---|---|---|
| 2003 | `ProductionPriority` | Literal text | Integer from 0 through 100 |

The action and parameter IDs are stable SingularityBot identities. Identifier values refer to bot-owned catalogs or definitions; they are not raw SC2 unit, ability, or order IDs. Validation currently confirms that an identifier is structurally valid, not that its target exists in the active game ontology.

## Initial scope and deliberate gaps

SingularityBot now consumes the actual `TheSingularityWorkshop.CommandAi` project from its `development` branch. The project reference is `dependencies/CommandAi/TheSingularityWorkshop.CommandAi.csproj`; GitHub Actions checks out that repository into the matching path before restore/build. Local setup instructions are in the repository README. No NuGet publication is required for active development.

`BotCommandProposalValidator` accepts real `CommandProposal` objects and validates each instruction against the bot-owned vocabulary. It rejects unknown action IDs, undeclared parameters, missing required parameters, incorrect value kinds, invalid desired counts, and priorities outside 0–100. It returns validated, typed command descriptions; it does not authorize execution, resolve ontology IDs, create jobs, or dispatch SC2 actions.

The following remain future work:
- Resolve target identifiers against the active BotDefinition and match catalogs.
- Map the validated production request into the existing production goal/job contracts.
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

`BotCommandVocabularyTests` checks stable positive action/parameter identities, uniqueness, required parameter typing, and fail-closed lookup of unknown action IDs. `BotCommandProposalValidatorTests` exercises the real CommandAi builder/types and covers valid Marine-production-shaped proposals, unknown actions/parameters, missing required values, invalid counts, and invalid priorities.
